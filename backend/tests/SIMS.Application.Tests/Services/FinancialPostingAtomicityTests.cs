using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities.Accounting;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class FinancialPostingAtomicityTests
{
    [Fact]
    public async Task BindAsync_RollsBackInvoiceWhenLedgerPostingFails()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var db = await CreateSchemaAsync(options);
        SeedLedgerAccounts(db);
        await db.SaveChangesAsync();

        var service = new InvoicingService(
            new TestServiceProvider(db),
            new EmptyFeeCalculationService(),
            new ThrowingLedgerService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.BindAsync(BuildInvoiceRequest(), Guid.NewGuid()));

        await using var verifyDb = new ApplicationDbContext(options);
        Assert.Equal(0, await verifyDb.Set<Invoice>().CountAsync());
        Assert.Equal(0, await verifyDb.Set<Payable>().CountAsync());
        Assert.Equal(0, await verifyDb.Set<LedgerTransaction>().CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RollsBackReceiptWhenLedgerPostingFails()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var db = await CreateSchemaAsync(options);
        SeedLedgerAccounts(db);
        await db.SaveChangesAsync();

        var service = new ReceiptsService(
            new TestServiceProvider(db),
            new ThrowingLedgerService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                new CreateReceiptRequest(
                    ReceivedDate: new DateOnly(2026, 5, 28),
                    Amount: 100m,
                    PayerName: "Acme Logistics",
                    Reference: "WIRE-123"),
                Guid.NewGuid()));

        await using var verifyDb = new ApplicationDbContext(options);
        Assert.Equal(0, await verifyDb.Set<Receipt>().CountAsync());
        Assert.Equal(0, await verifyDb.Set<LedgerTransaction>().CountAsync());
    }

    [Fact]
    public async Task ApplyAsync_RollsBackApplicationLedgerAndStatusesWhenDistributionFails()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var db = await CreateSchemaAsync(options);
        SeedLedgerAccounts(db);

        var receipt = new Receipt
        {
            ReceiptNumber = "RCT-2026-00001",
            ReceivedDate = new DateOnly(2026, 5, 28),
            Amount = 100m,
            PayerName = "Acme Logistics",
            Status = "Open",
            CreatedBy = Guid.NewGuid(),
            LedgerTransactionId = Guid.NewGuid()
        };
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-2026-00001",
            EffectiveDate = new DateOnly(2026, 5, 1),
            InvoiceDate = new DateOnly(2026, 5, 1),
            GrossPremium = 100m,
            TotalAmount = 100m,
            Status = "Posted",
            CreatedBy = Guid.NewGuid(),
            LedgerTransactionId = Guid.NewGuid()
        };
        db.AddRange(receipt, invoice);
        await db.SaveChangesAsync();

        var service = new CashApplicationService(
            new TestServiceProvider(db),
            new PersistingCashApplicationLedgerService(db),
            new ThrowingCashDistributionService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyAsync(
                new ApplyCashRequest(
                    receipt.Id,
                    [
                        new ApplicationLineRequest(
                            invoice.Id,
                            GrossApplied: 100m,
                            CommissionAmount: 0m)
                    ]),
                Guid.NewGuid()));

        await using var verifyDb = new ApplicationDbContext(options);
        var savedReceipt = await verifyDb.Set<Receipt>().SingleAsync();
        Assert.Equal("Open", savedReceipt.Status);
        Assert.Equal(0m, savedReceipt.AppliedAmount);

        var savedInvoice = await verifyDb.Set<Invoice>().SingleAsync();
        Assert.Equal("Posted", savedInvoice.Status);
        Assert.Equal(0m, savedInvoice.ClearedAmount);

        Assert.Equal(0, await verifyDb.Set<CashApplication>().CountAsync());
        Assert.Equal(0, await verifyDb.Set<LedgerTransaction>().CountAsync());
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

    private static async Task<ApplicationDbContext> CreateSchemaAsync(DbContextOptions<ApplicationDbContext> options)
    {
        var db = new ApplicationDbContext(options);
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE ledger_accounts (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                TenantId INTEGER NOT NULL,
                InternalCode TEXT NOT NULL,
                ExternalLabel TEXT NOT NULL,
                AccountType TEXT NOT NULL,
                ParentId INTEGER NULL,
                IsActive INTEGER NOT NULL
            );

            CREATE TABLE invoices (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                TenantId INTEGER NOT NULL,
                InvoiceNumber TEXT NOT NULL,
                PolicyTransactionId TEXT NULL,
                PolicyVersionId TEXT NULL,
                EffectiveDate TEXT NOT NULL,
                InvoiceDate TEXT NOT NULL,
                GrossPremium TEXT NOT NULL,
                CommissionAmount TEXT NOT NULL DEFAULT '0',
                AgentCommissionAmount TEXT NOT NULL DEFAULT '0',
                TotalFees TEXT NOT NULL,
                TotalAmount TEXT NOT NULL,
                LedgerTransactionId TEXT NOT NULL,
                ClearedAmount TEXT NOT NULL DEFAULT '0',
                Status TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE invoice_lines (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                InvoiceId INTEGER NOT NULL,
                FeeRuleVersionId INTEGER NULL,
                FeeCode TEXT NOT NULL,
                FeeDisplayName TEXT NOT NULL,
                FeeCategory TEXT NOT NULL,
                Amount TEXT NOT NULL,
                IsTaxable INTEGER NOT NULL,
                LedgerAccountId INTEGER NOT NULL,
                PayableRouting TEXT NULL,
                PayablePayeeId INTEGER NULL
            );

            CREATE TABLE receipts (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                TenantId INTEGER NOT NULL,
                ReceiptNumber TEXT NOT NULL,
                ReceivedDate TEXT NOT NULL,
                Amount TEXT NOT NULL,
                PayerName TEXT NOT NULL,
                Reference TEXT NULL,
                RemittanceBlobPath TEXT NULL,
                LedgerTransactionId TEXT NOT NULL,
                Status TEXT NOT NULL,
                AppliedAmount TEXT NOT NULL DEFAULT '0',
                CreatedBy TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE cash_applications (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                TenantId INTEGER NOT NULL,
                ReceiptId INTEGER NOT NULL,
                InvoiceId INTEGER NOT NULL,
                GrossApplied TEXT NOT NULL,
                CommissionAmount TEXT NOT NULL,
                NetApplied TEXT NOT NULL,
                LedgerTransactionId TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE ledger_transactions (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                TenantId INTEGER NOT NULL,
                TransactionId TEXT NOT NULL,
                PostedAt TEXT NOT NULL,
                EffectiveDate TEXT NOT NULL,
                AccountId INTEGER NOT NULL,
                Debit TEXT NOT NULL,
                Credit TEXT NOT NULL,
                SourceType TEXT NOT NULL,
                SourceId INTEGER NOT NULL,
                Memo TEXT NULL,
                CreatedBy TEXT NOT NULL,
                RolledUpIn INTEGER NULL,
                PostingStatus TEXT NOT NULL,
                VoidedByTransactionId TEXT NULL,
                ReversesTransactionId TEXT NULL,
                VoidedAt TEXT NULL,
                VoidedBy TEXT NULL,
                VoidReason TEXT NULL
            );

            CREATE TABLE payables (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                TenantId INTEGER NOT NULL,
                InvoiceId INTEGER NOT NULL,
                CarrierId TEXT NULL,
                PayeeId INTEGER NULL,
                PayeeName TEXT NOT NULL,
                GlAccountId INTEGER NOT NULL,
                Amount TEXT NOT NULL,
                PaidAmount TEXT NOT NULL,
                InvoiceDate TEXT NOT NULL,
                DueDate TEXT NOT NULL,
                Status TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
        return db;
    }

    private static CreateInvoiceRequest BuildInvoiceRequest() =>
        new(
            EffectiveDate: new DateOnly(2026, 5, 28),
            GrossPremium: 100m,
            StateCode: "TX",
            IsEndorsement: false,
            IsFilingState: true,
            CarrierId: null,
            CompanyId: null,
            ProducerId: null,
            LineOfBusiness: "InlandMarine",
            City: null,
            LicenseType: "Non-Admitted");

    private static void SeedLedgerAccounts(ApplicationDbContext db)
    {
        db.AddRange(
            new LedgerAccount { Id = 1100, InternalCode = "1100", ExternalLabel = "Trust", AccountType = "Asset", IsActive = true },
            new LedgerAccount { Id = 1200, InternalCode = "1200", ExternalLabel = "Accounts Receivable", AccountType = "Asset", IsActive = true },
            new LedgerAccount { Id = 1250, InternalCode = "1250", ExternalLabel = "Unapplied Cash", AccountType = "Asset", IsActive = true },
            new LedgerAccount { Id = 2100, InternalCode = "2100", ExternalLabel = "Carrier Payable", AccountType = "Liability", IsActive = true },
            new LedgerAccount { Id = 4100, InternalCode = "4100", ExternalLabel = "Commission Revenue", AccountType = "Revenue", IsActive = true },
            new LedgerAccount { Id = 5100, InternalCode = "5100", ExternalLabel = "Commission Expense", AccountType = "Expense", IsActive = true });
    }

    private sealed class TestServiceProvider(DbContext db) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(DbContext) ? db : null;
    }

    private sealed class EmptyFeeCalculationService : IFeeCalculationService
    {
        public Task<FeeCalculationResult> CalculateAsync(PolicyContext ctx, CancellationToken ct = default) =>
            Task.FromResult(new FeeCalculationResult([]));
    }

    private sealed class ThrowingLedgerService : ILedgerService
    {
        public Task<Guid> PostInvoiceAsync(
            Invoice invoice,
            int arAccountId,
            int carrierApAccountId,
            int commissionAccountId,
            int agentCommissionExpenseAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new InvalidOperationException("Ledger posting failed.");

        public Task<Guid> PostReceiptAsync(
            Receipt receipt,
            int trustAccountId,
            int unappliedCashAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new InvalidOperationException("Ledger posting failed.");

        public Task<Guid> PostCashApplicationAsync(
            Receipt receipt,
            Invoice invoice,
            decimal grossApplied,
            decimal commissionAmount,
            int unappliedCashAccountId,
            int commissionExpenseAccountId,
            int arAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Guid> PostDisbursementAsync(
            Disbursement disbursementWithLines,
            int trustAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Guid> PostDistributionSweepAsync(
            CashMovementInstruction instruction,
            int trustAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Guid> ReverseTransactionGroupAsync(
            Guid transactionId,
            string voidReason,
            Guid userId,
            DateOnly effectiveDate,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class PersistingCashApplicationLedgerService(DbContext db) : ILedgerService
    {
        public Task<Guid> PostInvoiceAsync(
            Invoice invoice,
            int arAccountId,
            int carrierApAccountId,
            int commissionAccountId,
            int agentCommissionExpenseAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Guid> PostReceiptAsync(
            Receipt receipt,
            int trustAccountId,
            int unappliedCashAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public async Task<Guid> PostCashApplicationAsync(
            Receipt receipt,
            Invoice invoice,
            decimal grossApplied,
            decimal commissionAmount,
            int unappliedCashAccountId,
            int commissionExpenseAccountId,
            int arAccountId,
            Guid userId,
            CancellationToken ct = default)
        {
            var transactionId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            db.Set<LedgerTransaction>().AddRange(
                new LedgerTransaction
                {
                    TransactionId = transactionId,
                    EffectiveDate = receipt.ReceivedDate,
                    AccountId = unappliedCashAccountId,
                    Debit = grossApplied,
                    Credit = 0m,
                    SourceType = "CashApplication",
                    SourceId = receipt.Id,
                    Memo = "Apply cash",
                    CreatedBy = userId,
                    PostedAt = now
                },
                new LedgerTransaction
                {
                    TransactionId = transactionId,
                    EffectiveDate = receipt.ReceivedDate,
                    AccountId = arAccountId,
                    Debit = 0m,
                    Credit = grossApplied,
                    SourceType = "CashApplication",
                    SourceId = receipt.Id,
                    Memo = "Clear AR",
                    CreatedBy = userId,
                    PostedAt = now
                });

            await db.SaveChangesAsync(ct);
            return transactionId;
        }

        public Task<Guid> PostDisbursementAsync(
            Disbursement disbursementWithLines,
            int trustAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Guid> PostDistributionSweepAsync(
            CashMovementInstruction instruction,
            int trustAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Guid> ReverseTransactionGroupAsync(
            Guid transactionId,
            string voidReason,
            Guid userId,
            DateOnly effectiveDate,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingCashDistributionService : ICashDistributionService
    {
        public Task GenerateInstructionsForApplicationAsync(
            CashApplication application,
            Invoice invoiceWithLines,
            int trustGlAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new InvalidOperationException("Distribution failed.");

        public Task<IReadOnlyList<NettedPayeeDto>> GetPendingAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<BatchDetailDto>> CreateBatchAsync(
            CreateBatchRequest req,
            Guid userId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<BatchSummaryDto>> GetBatchesAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<BatchDetailDto>> GetBatchAsync(long id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<BatchDetailDto>> MarkExecutedAsync(
            long batchId,
            MarkExecutedRequest req,
            Guid userId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<string>> GetBatchPdfDownloadUrlAsync(long batchId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}

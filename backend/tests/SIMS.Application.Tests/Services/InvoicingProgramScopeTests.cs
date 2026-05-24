using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class InvoicingProgramScopeTests
{
    [Fact]
    public async Task BindAsync_UsesProgramScopedFeeRulesWhenProgramIsProvided()
    {
        await using var db = CreateDb();
        SeedLedgerAccounts(db);
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var fee = new FeeDefinition
        {
            Code = "MGA",
            DisplayName = "MGA Fee",
            FeeCategory = "PolicyFee",
            IsTaxable = false,
            CalculationOrder = 100,
            LedgerAccountId = 4200,
        };
        db.AddRange(program, fee);
        await db.SaveChangesAsync();

        db.AddRange(
            BuildFlatFeeRule(fee.Id, null, 25m),
            BuildFlatFeeRule(fee.Id, program.Id, 75m));
        await db.SaveChangesAsync();

        var service = new InvoicingService(
            new TestServiceProvider(db),
            new FeeCalculationService(new TestServiceProvider(db)),
            new RecordingLedgerService());

        var result = await service.BindAsync(BuildInvoiceRequest(program.Id), Guid.NewGuid());

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var line = Assert.Single(result.Value!.Lines);
        Assert.Equal("MGA", line.FeeCode);
        Assert.Equal(75m, line.Amount);
        Assert.Equal(75m, result.Value.TotalFees);
    }

    private static CreateInvoiceRequest BuildInvoiceRequest(Guid? programId) =>
        new(
            EffectiveDate: new DateOnly(2026, 1, 1),
            GrossPremium: 1000m,
            StateCode: "TX",
            IsEndorsement: false,
            IsFilingState: true,
            CarrierId: null,
            CompanyId: null,
            ProducerId: null,
            LineOfBusiness: "InlandMarine",
            City: null,
            LicenseType: "Non-Admitted",
            ProgramConfigurationId: programId);

    private static FeeRuleVersion BuildFlatFeeRule(long feeDefinitionId, Guid? programId, decimal flatAmount) =>
        new()
        {
            FeeDefinitionId = feeDefinitionId,
            ProgramConfigurationId = programId,
            EffectiveDate = new DateOnly(2026, 1, 1),
            CalcType = "Flat",
            FlatAmount = flatAmount,
            SendToAccounting = true,
            ApplyAutomatically = true,
            InstallmentBehavior = "PerInstallment",
            RoundingMode = "NearestCent",
            PayableRouting = "NotPayable",
            CreatedBy = Guid.NewGuid(),
        };

    private static void SeedLedgerAccounts(ApplicationDbContext db)
    {
        db.AddRange(
            new LedgerAccount { Id = 1200, InternalCode = "1200", ExternalLabel = "Accounts Receivable", AccountType = "Asset", IsActive = true },
            new LedgerAccount { Id = 2100, InternalCode = "2100", ExternalLabel = "Carrier Payable", AccountType = "Liability", IsActive = true },
            new LedgerAccount { Id = 4100, InternalCode = "4100", ExternalLabel = "Commission Revenue", AccountType = "Revenue", IsActive = true },
            new LedgerAccount { Id = 4200, InternalCode = "4200", ExternalLabel = "Policy Fee Revenue", AccountType = "Revenue", IsActive = true },
            new LedgerAccount { Id = 5100, InternalCode = "5100", ExternalLabel = "Commission Expense", AccountType = "Expense", IsActive = true });
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TestServiceProvider(DbContext db) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(DbContext) ? db : null;
    }

    private sealed class RecordingLedgerService : ILedgerService
    {
        public Task<Guid> PostInvoiceAsync(
            Invoice invoice,
            int arAccountId,
            int carrierApAccountId,
            int commissionAccountId,
            int agentCommissionExpenseAccountId,
            Guid userId,
            CancellationToken ct = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<Guid> PostReceiptAsync(
            Receipt receipt,
            int trustAccountId,
            int unappliedCashAccountId,
            Guid userId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

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
}

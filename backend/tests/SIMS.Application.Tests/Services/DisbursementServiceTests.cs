using Microsoft.EntityFrameworkCore;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Services;
using SIMS.Domain.Entities.Accounting;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class DisbursementServiceTests
{
    [Fact]
    public async Task CreateDisbursementAsync_RejectsMixedPayeePayables()
    {
        await using var db = CreateDb();
        var carrierA = Guid.NewGuid();
        var carrierB = Guid.NewGuid();
        var payableA = CreatePayable("Carrier A", carrierA);
        var payableB = CreatePayable("Carrier B", carrierB);
        db.AddRange(payableA.Invoice, payableB.Invoice, payableA, payableB);
        await db.SaveChangesAsync();

        var service = new DisbursementService(new TestServiceProvider(db), new NoOpLedgerService());

        var result = await service.CreateDisbursementAsync(
            new CreateDisbursementRequest(
                [
                    new DisbursementLineRequest(payableA.Id, 100m),
                    new DisbursementLineRequest(payableB.Id, 100m)
                ],
                PaymentDate: new DateOnly(2026, 5, 29),
                PaymentMethod: "Check",
                Reference: null,
                Notes: null),
            Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("MIXED_PAYEES", result.ErrorCode);
        Assert.Equal(0, await db.Set<Disbursement>().CountAsync());
        Assert.Equal(0, await db.Set<DisbursementLine>().CountAsync());
    }

    [Fact]
    public async Task CreateDisbursementAsync_AllowsSameCarrierPayables()
    {
        await using var db = CreateDb();
        var carrierId = Guid.NewGuid();
        var payableA = CreatePayable("Carrier A", carrierId);
        var payableB = CreatePayable("Carrier A", carrierId);
        db.AddRange(payableA.Invoice, payableB.Invoice, payableA, payableB);
        await db.SaveChangesAsync();

        var service = new DisbursementService(new TestServiceProvider(db), new NoOpLedgerService());

        var result = await service.CreateDisbursementAsync(
            new CreateDisbursementRequest(
                [
                    new DisbursementLineRequest(payableA.Id, 100m),
                    new DisbursementLineRequest(payableB.Id, 75m)
                ],
                PaymentDate: new DateOnly(2026, 5, 29),
                PaymentMethod: "Check",
                Reference: null,
                Notes: null),
            Guid.NewGuid());

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("Carrier A", result.Value!.PayeeName);
        Assert.Equal(carrierId, result.Value.CarrierId);
        Assert.Equal(175m, result.Value.TotalAmount);
        Assert.Equal(2, result.Value.Lines.Count);
    }

    [Fact]
    public async Task CreateDisbursementAsync_RejectsDifferentEntityPayees()
    {
        await using var db = CreateDb();
        var payableA = CreatePayable("State Tax Authority", carrierId: null, payeeId: 101);
        var payableB = CreatePayable("County Fee Entity", carrierId: null, payeeId: 202);
        db.AddRange(payableA.Invoice, payableB.Invoice, payableA, payableB);
        await db.SaveChangesAsync();

        var service = new DisbursementService(new TestServiceProvider(db), new NoOpLedgerService());

        var result = await service.CreateDisbursementAsync(
            new CreateDisbursementRequest(
                [
                    new DisbursementLineRequest(payableA.Id, 100m),
                    new DisbursementLineRequest(payableB.Id, 50m)
                ],
                PaymentDate: new DateOnly(2026, 5, 29),
                PaymentMethod: "ACH",
                Reference: null,
                Notes: null),
            Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("MIXED_PAYEES", result.ErrorCode);
        Assert.Equal(0, await db.Set<Disbursement>().CountAsync());
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Payable CreatePayable(string payeeName, Guid? carrierId, long? payeeId = null)
    {
        var invoice = new Invoice
        {
            InvoiceNumber = $"INV-{Guid.NewGuid():N}",
            EffectiveDate = new DateOnly(2026, 5, 1),
            InvoiceDate = new DateOnly(2026, 5, 1),
            GrossPremium = 100m,
            TotalAmount = 100m,
            Status = "Posted",
            LedgerTransactionId = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid()
        };

        return new Payable
        {
            Invoice = invoice,
            PayeeName = payeeName,
            CarrierId = carrierId,
            PayeeId = payeeId,
            GlAccountId = 2100,
            Amount = 100m,
            PaidAmount = 0m,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.InvoiceDate.AddDays(30),
            Status = "Open",
            CreatedBy = Guid.NewGuid()
        };
    }

    private sealed class TestServiceProvider(DbContext db) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(DbContext) ? db : null;
    }

    private sealed class NoOpLedgerService : ILedgerService
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

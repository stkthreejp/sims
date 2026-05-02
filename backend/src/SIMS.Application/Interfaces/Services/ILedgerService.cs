using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface ILedgerService
{
    Task<Guid> PostInvoiceAsync(
        Invoice invoice, int arAccountId, int carrierApAccountId,
        Guid userId, CancellationToken ct = default);

    Task<Guid> PostReceiptAsync(
        Receipt receipt, int trustAccountId, int unappliedCashAccountId,
        Guid userId, CancellationToken ct = default);

    Task<Guid> PostCashApplicationAsync(
        Receipt receipt, Invoice invoice,
        decimal grossApplied, decimal commissionAmount,
        int unappliedCashAccountId, int commissionExpenseAccountId, int arAccountId,
        Guid userId, CancellationToken ct = default);
}

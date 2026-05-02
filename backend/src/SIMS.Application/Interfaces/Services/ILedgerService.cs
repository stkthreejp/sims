using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface ILedgerService
{
    Task<Guid> PostInvoiceAsync(
        Invoice invoice, int arAccountId, int carrierApAccountId,
        int commissionAccountId, int agentCommissionExpenseAccountId,
        Guid userId, CancellationToken ct = default);

    Task<Guid> PostReceiptAsync(
        Receipt receipt, int trustAccountId, int unappliedCashAccountId,
        Guid userId, CancellationToken ct = default);

    Task<Guid> PostCashApplicationAsync(
        Receipt receipt, Invoice invoice,
        decimal grossApplied, decimal commissionAmount,
        int unappliedCashAccountId, int commissionExpenseAccountId, int arAccountId,
        Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Posts a single balanced JE for a disbursement (one JE covers all lines):
    ///   Per line: DR line.Payable.GlAccountId (clear the AP)
    ///   Per line: CR trustAccountId (reduce trust cash)
    /// </summary>
    Task<Guid> PostDisbursementAsync(
        Disbursement disbursementWithLines,
        int trustAccountId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Posts the sweep JE for a single distribution instruction:
    ///   DR instruction.DistributionGlAccountId (clear the payable liability)
    ///   CR trustAccountId (reduce trust cash)
    /// </summary>
    Task<Guid> PostDistributionSweepAsync(
        CashMovementInstruction instruction,
        int trustAccountId,
        Guid userId,
        CancellationToken ct = default);
}

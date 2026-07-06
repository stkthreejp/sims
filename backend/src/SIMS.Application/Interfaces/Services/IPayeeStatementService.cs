using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IPayeeStatementService
{
    Task<Result<PayeeStatementDto>> ImportAsync(
        ImportPayeeStatementRequest req, Stream csvStream, string fileName,
        Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<PayeeStatementSummaryDto>> GetAllAsync(CancellationToken ct = default);

    Task<Result<PayeeStatementDto>> GetAsync(long id, CancellationToken ct = default);

    Task<Result<PayeeStatementDto>> SetLineMatchAsync(
        long statementId, long lineId, long? invoiceLineId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<PayeeStatementLineCandidateDto>>> GetLineMatchCandidatesAsync(
        long statementId, long lineId, CancellationToken ct = default);

    Task<IReadOnlyList<LedgerAccountOptionDto>> GetApLedgerAccountsAsync(CancellationToken ct = default);

    Task<Result<PayeeStatementDto>> PostReconciliationAsync(
        long id, Guid userId, CancellationToken ct = default);
}

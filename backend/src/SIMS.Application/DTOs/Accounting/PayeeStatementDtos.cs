namespace SIMS.Application.DTOs.Accounting;

public record PayeeStatementSummaryDto(
    long Id,
    string PayeeName,
    DateOnly StatementDate,
    string? ReferenceNumber,
    decimal StatementTotal,
    int TotalLines,
    int MatchedLines,
    string Status,
    DateTime CreatedAt);

public record PayeeStatementLineDto(
    long Id,
    string PolicyNumber,
    string StateCode,
    decimal Amount,
    string? Description,
    string MatchStatus,
    long? MatchedInvoiceLineId,
    string? MatchedFeeCode,
    string? MatchedFeeDisplayName);

public record PayeeStatementDto(
    long Id,
    string PayeeName,
    DateOnly StatementDate,
    string? ReferenceNumber,
    int ApLedgerAccountId,
    string ApLedgerAccountName,
    decimal StatementTotal,
    string Status,
    IReadOnlyList<PayeeStatementLineDto> Lines,
    DateTime CreatedAt);

public record ImportPayeeStatementRequest(
    string PayeeName,
    DateOnly StatementDate,
    string? ReferenceNumber,
    int ApLedgerAccountId);

public record SetLineMatchRequest(long? InvoiceLineId);

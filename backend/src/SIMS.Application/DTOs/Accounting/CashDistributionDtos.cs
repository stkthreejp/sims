namespace SIMS.Application.DTOs.Accounting;

// ---- Pending queue ----

public record PendingInstructionDto(
    long Id,
    long ReceiptId,
    string ReceiptNumber,
    long CashApplicationId,
    long InvoiceLineId,
    string FeeCode,
    string FeeDisplayName,
    decimal Amount,
    DateTime CreatedAt
);

public record NettedPayeeDto(
    long PayeeId,
    string PayeeName,
    string PayeeType,
    decimal TotalAmount,
    int InstructionCount,
    IReadOnlyList<PendingInstructionDto> Instructions
);

// ---- Batch creation ----

public record CreateBatchRequest(
    IReadOnlyList<long> PayeeIds  // which payees to include (nets all their pending instructions)
);

// ---- Batch detail ----

public record BatchWireDto(
    long PayeeId,
    string PayeeName,
    decimal NetAmount,
    IReadOnlyList<BatchInstructionDto> Instructions
);

public record BatchInstructionDto(
    long Id,
    long ReceiptId,
    string ReceiptNumber,
    string FeeDisplayName,
    decimal Amount,
    string Status,
    Guid? LedgerTransactionId
);

public record BatchSummaryDto(
    long Id,
    string BatchNumber,
    string Status,
    int TotalInstructions,
    int TotalWires,
    decimal TotalAmount,
    string? PdfBlobPath,
    DateTime? ExecutedAt,
    string? BankReference,
    DateTime CreatedAt
);

public record BatchDetailDto(
    long Id,
    string BatchNumber,
    string Status,
    int TotalInstructions,
    int TotalWires,
    decimal TotalAmount,
    string? PdfBlobPath,
    DateTime? ExecutedAt,
    string? BankReference,
    DateTime CreatedAt,
    IReadOnlyList<BatchWireDto> Wires
);

// ---- Mark executed ----

public record MarkExecutedRequest(
    string? BankReference
);

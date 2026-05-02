using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class CashDistributionService : ICashDistributionService
{
    private readonly IServiceProvider _sp;
    private readonly ILedgerService _ledger;
    private readonly IBlobStorageService _blob;
    private readonly IWireSheetPdfService _pdf;

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public CashDistributionService(
        IServiceProvider sp,
        ILedgerService ledger,
        IBlobStorageService blob,
        IWireSheetPdfService pdf)
    {
        _sp = sp;
        _ledger = ledger;
        _blob = blob;
        _pdf = pdf;
    }

    public async Task GenerateInstructionsForApplicationAsync(
        CashApplication application,
        Invoice invoiceWithLines,
        int trustGlAccountId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (invoiceWithLines.TotalAmount <= 0) return;

        var db = Db;
        var ratio = application.GrossApplied / invoiceWithLines.TotalAmount;

        var instructions = invoiceWithLines.Lines
            .Where(l => l.PayableRouting == "Entity" && l.PayablePayeeId.HasValue)
            .Select(l => new CashMovementInstruction
            {
                TenantId = application.TenantId,
                ReceiptId = application.ReceiptId,
                CashApplicationId = application.Id,
                InvoiceLineId = l.Id,
                PayeeId = l.PayablePayeeId!.Value,
                Amount = Math.Round(l.Amount * ratio, 4, MidpointRounding.AwayFromZero),
                SourceGlAccountId = trustGlAccountId,
                DistributionGlAccountId = l.LedgerAccountId,
                Status = "Pending",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            })
            .Where(i => i.Amount > 0)
            .ToList();

        if (instructions.Count == 0) return;

        db.Set<CashMovementInstruction>().AddRange(instructions);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NettedPayeeDto>> GetPendingAsync(CancellationToken ct = default)
    {
        var rows = await Db.Set<CashMovementInstruction>()
            .Include(i => i.Payee)
            .Include(i => i.Receipt)
            .Include(i => i.InvoiceLine)
            .Where(i => i.Status == "Pending" && i.TenantId == 1)
            .OrderBy(i => i.PayeeId).ThenBy(i => i.Id)
            .ToListAsync(ct);

        return rows
            .GroupBy(i => i.PayeeId)
            .Select(g => new NettedPayeeDto(
                g.Key,
                g.First().Payee.Name,
                g.First().Payee.PayeeType,
                g.Sum(i => i.Amount),
                g.Count(),
                g.Select(i => new PendingInstructionDto(
                    i.Id,
                    i.ReceiptId,
                    i.Receipt.ReceiptNumber,
                    i.CashApplicationId,
                    i.InvoiceLineId,
                    i.InvoiceLine.FeeCode,
                    i.InvoiceLine.FeeDisplayName,
                    i.Amount,
                    i.CreatedAt))
                .ToList()
            ))
            .OrderBy(g => g.PayeeName)
            .ToList();
    }

    public async Task<Result<BatchDetailDto>> CreateBatchAsync(
        CreateBatchRequest req, Guid userId, CancellationToken ct = default)
    {
        if (req.PayeeIds.Count == 0)
            return Result<BatchDetailDto>.Failure("NO_PAYEES", "Select at least one payee to batch");

        var db = Db;

        var instructions = await db.Set<CashMovementInstruction>()
            .Include(i => i.Payee)
            .Include(i => i.Receipt)
            .Include(i => i.InvoiceLine)
            .Where(i => i.Status == "Pending"
                     && i.TenantId == 1
                     && req.PayeeIds.Contains(i.PayeeId))
            .ToListAsync(ct);

        if (instructions.Count == 0)
            return Result<BatchDetailDto>.Failure("NO_INSTRUCTIONS",
                "No pending instructions found for the selected payees");

        var totalAmount = instructions.Sum(i => i.Amount);
        var wires = instructions
            .GroupBy(i => i.PayeeId)
            .Select(g => new BatchWireDto(
                g.Key,
                g.First().Payee.Name,
                g.Sum(i => i.Amount),
                g.Select(i => new BatchInstructionDto(
                    i.Id, i.ReceiptId, i.Receipt.ReceiptNumber,
                    i.InvoiceLine.FeeDisplayName, i.Amount, i.Status, i.LedgerTransactionId))
                .ToList()))
            .OrderBy(w => w.PayeeName)
            .ToList();

        // Create batch
        var batch = new CashDistributionBatch
        {
            TenantId = 1,
            Status = "Open",
            TotalInstructions = instructions.Count,
            TotalWires = wires.Count,
            TotalAmount = totalAmount,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<CashDistributionBatch>().Add(batch);
        await db.SaveChangesAsync(ct); // get batch.Id

        batch.BatchNumber = $"BATCH-{DateTime.UtcNow.Year}-{batch.Id:D5}";

        // Link instructions to batch
        foreach (var inst in instructions)
        {
            inst.BatchId = batch.Id;
            inst.Status = "Batched";
        }

        // Generate PDF and upload to blob
        var batchDto = new BatchDetailDto(
            batch.Id, batch.BatchNumber, batch.Status,
            batch.TotalInstructions, batch.TotalWires, batch.TotalAmount,
            null, null, null, batch.CreatedAt, wires);

        try
        {
            var pdfBytes = _pdf.Generate(batchDto, "Specialty Market Managers, LLC");
            using var stream = new MemoryStream(pdfBytes);
            var blobPath = await _blob.UploadAsync(
                stream,
                $"wire-sheet-{batch.BatchNumber}.pdf",
                "application/pdf");
            batch.PdfBlobPath = blobPath;
            batch.Status = "PdfGenerated";
        }
        catch
        {
            // PDF failure is non-fatal — batch is still usable, status stays Open
        }

        await db.SaveChangesAsync(ct);
        return Result<BatchDetailDto>.Success(await LoadBatchDetailAsync(batch.Id, db, ct));
    }

    public async Task<IReadOnlyList<BatchSummaryDto>> GetBatchesAsync(CancellationToken ct = default)
    {
        return await Db.Set<CashDistributionBatch>()
            .Where(b => b.TenantId == 1)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BatchSummaryDto(
                b.Id, b.BatchNumber, b.Status,
                b.TotalInstructions, b.TotalWires, b.TotalAmount,
                b.PdfBlobPath, b.ExecutedAt, b.BankReference, b.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<Result<BatchDetailDto>> GetBatchAsync(long id, CancellationToken ct = default)
    {
        var detail = await LoadBatchDetailAsync(id, Db, ct);
        if (detail == null)
            return Result<BatchDetailDto>.Failure("NOT_FOUND", $"Batch {id} not found");
        return Result<BatchDetailDto>.Success(detail);
    }

    public async Task<Result<BatchDetailDto>> MarkExecutedAsync(
        long batchId, MarkExecutedRequest req, Guid userId, CancellationToken ct = default)
    {
        var db = Db;

        var batch = await db.Set<CashDistributionBatch>()
            .Include(b => b.Instructions)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == 1, ct);

        if (batch == null)
            return Result<BatchDetailDto>.Failure("NOT_FOUND", $"Batch {batchId} not found");

        if (batch.Status == "Executed")
            return Result<BatchDetailDto>.Failure("ALREADY_EXECUTED", "Batch is already marked as executed");

        if (batch.Status == "Voided")
            return Result<BatchDetailDto>.Failure("VOIDED", "Cannot execute a voided batch");

        var trustAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "1100" && a.TenantId == 1, ct);

        if (trustAccount == null)
            return Result<BatchDetailDto>.Failure("MISSING_GL_ACCOUNT", "Trust account (1100) not found");

        // Post sweep JE per instruction — ledger is append-only so each gets its own save
        foreach (var inst in batch.Instructions.Where(i => i.Status == "Batched"))
        {
            var txnId = await _ledger.PostDistributionSweepAsync(inst, trustAccount.Id, userId, ct);
            inst.LedgerTransactionId = txnId;
            inst.Status = "Executed";
        }

        batch.Status = "Executed";
        batch.ExecutedAt = DateTime.UtcNow;
        batch.ExecutedBy = userId;
        batch.BankReference = req.BankReference;

        await db.SaveChangesAsync(ct);
        return Result<BatchDetailDto>.Success(await LoadBatchDetailAsync(batchId, db, ct));
    }

    public async Task<Result<string>> GetBatchPdfDownloadUrlAsync(long batchId, CancellationToken ct = default)
    {
        var batch = await Db.Set<CashDistributionBatch>()
            .FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == 1, ct);

        if (batch == null)
            return Result<string>.Failure("NOT_FOUND", $"Batch {batchId} not found");

        if (string.IsNullOrEmpty(batch.PdfBlobPath))
            return Result<string>.Failure("NO_PDF", "No PDF has been generated for this batch");

        var url = await _blob.GetDownloadUrlAsync(
            batch.PdfBlobPath,
            $"{batch.BatchNumber}.pdf",
            TimeSpan.FromMinutes(10));

        return Result<string>.Success(url);
    }

    private static async Task<BatchDetailDto?> LoadBatchDetailAsync(
        long batchId, DbContext db, CancellationToken ct)
    {
        var batch = await db.Set<CashDistributionBatch>()
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch == null) return null;

        var instructions = await db.Set<CashMovementInstruction>()
            .Include(i => i.Payee)
            .Include(i => i.Receipt)
            .Include(i => i.InvoiceLine)
            .Where(i => i.BatchId == batchId)
            .OrderBy(i => i.PayeeId).ThenBy(i => i.Id)
            .ToListAsync(ct);

        var wires = instructions
            .GroupBy(i => i.PayeeId)
            .Select(g => new BatchWireDto(
                g.Key,
                g.First().Payee.Name,
                g.Sum(i => i.Amount),
                g.Select(i => new BatchInstructionDto(
                    i.Id, i.ReceiptId, i.Receipt.ReceiptNumber,
                    i.InvoiceLine.FeeDisplayName, i.Amount, i.Status, i.LedgerTransactionId))
                .ToList()))
            .OrderBy(w => w.PayeeName)
            .ToList();

        return new BatchDetailDto(
            batch.Id, batch.BatchNumber, batch.Status,
            batch.TotalInstructions, batch.TotalWires, batch.TotalAmount,
            batch.PdfBlobPath, batch.ExecutedAt, batch.BankReference, batch.CreatedAt, wires);
    }
}

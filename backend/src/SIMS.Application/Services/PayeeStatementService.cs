using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using InvoiceLineEntity = SIMS.Domain.Entities.Accounting.InvoiceLine;

namespace SIMS.Application.Services;

public class PayeeStatementService : IPayeeStatementService
{
    private readonly IServiceProvider _sp;
    private readonly IBlobStorageService _blob;
    private readonly ILedgerService _ledger;

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public PayeeStatementService(IServiceProvider sp, IBlobStorageService blob, ILedgerService ledger)
    {
        _sp = sp;
        _blob = blob;
        _ledger = ledger;
    }

    public async Task<Result<PayeeStatementDto>> ImportAsync(
        ImportPayeeStatementRequest req, Stream csvStream, string fileName,
        Guid userId, CancellationToken ct = default)
    {
        var db = Db;

        var apAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.Id == req.ApLedgerAccountId && a.TenantId == 1, ct);
        if (apAccount == null)
            return Result<PayeeStatementDto>.Failure("NOT_FOUND", "AP ledger account not found.");

        // Parse CSV
        var lines = ParseCsv(csvStream);
        if (lines.Count == 0)
            return Result<PayeeStatementDto>.Failure("INVALID_CSV", "CSV contains no data rows.");

        // Upload to blob
        string? blobPath = null;
        try
        {
            csvStream.Position = 0;
            blobPath = await _blob.UploadAsync(csvStream, $"payee-statements/{Guid.NewGuid()}/{fileName}", "text/csv");
        }
        catch { /* blob failure is non-fatal — proceed without stored file */ }

        var statement = new PayeeStatement
        {
            PayeeName = req.PayeeName,
            StatementDate = req.StatementDate,
            ReferenceNumber = req.ReferenceNumber,
            BlobPath = blobPath,
            ApLedgerAccountId = req.ApLedgerAccountId,
            StatementTotal = lines.Sum(l => l.Amount),
            Status = "Imported",
            CreatedBy = userId,
        };
        db.Set<PayeeStatement>().Add(statement);
        await db.SaveChangesAsync(ct);

        // Add lines
        foreach (var parsed in lines)
        {
            var line = new PayeeStatementLine
            {
                PayeeStatementId = statement.Id,
                PolicyNumber = parsed.PolicyNumber,
                StateCode = parsed.StateCode,
                Amount = parsed.Amount,
                Description = parsed.Description,
                MatchStatus = "Unmatched",
            };
            db.Set<PayeeStatementLine>().Add(line);
        }
        await db.SaveChangesAsync(ct);

        // Auto-match all lines
        var statementLines = await db.Set<PayeeStatementLine>()
            .Where(l => l.PayeeStatementId == statement.Id)
            .ToListAsync(ct);

        foreach (var line in statementLines)
            await AutoMatchLineAsync(line, db, ct);

        await db.SaveChangesAsync(ct);

        return Result<PayeeStatementDto>.Success(await BuildDtoAsync(statement.Id, db, ct));
    }

    public async Task<IReadOnlyList<PayeeStatementSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var db = Db;
        return await db.Set<PayeeStatement>()
            .OrderByDescending(s => s.StatementDate)
            .ThenByDescending(s => s.Id)
            .Select(s => new PayeeStatementSummaryDto(
                s.Id, s.PayeeName, s.StatementDate, s.ReferenceNumber,
                s.StatementTotal,
                s.Lines.Count,
                s.Lines.Count(l => l.MatchStatus != "Unmatched"),
                s.Status, s.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<Result<PayeeStatementDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var db = Db;
        var exists = await db.Set<PayeeStatement>().AnyAsync(s => s.Id == id, ct);
        if (!exists)
            return Result<PayeeStatementDto>.Failure("NOT_FOUND", "Statement not found.");
        return Result<PayeeStatementDto>.Success(await BuildDtoAsync(id, db, ct));
    }

    public async Task<Result<PayeeStatementDto>> SetLineMatchAsync(
        long statementId, long lineId, long? invoiceLineId, CancellationToken ct = default)
    {
        var db = Db;
        var line = await db.Set<PayeeStatementLine>()
            .FirstOrDefaultAsync(l => l.Id == lineId && l.PayeeStatementId == statementId, ct);
        if (line == null)
            return Result<PayeeStatementDto>.Failure("NOT_FOUND", "Statement line not found.");

        if (line.ReconciliationTransactionId.HasValue)
            return Result<PayeeStatementDto>.Failure("ALREADY_POSTED", "Line has already been reconciled.");

        if (invoiceLineId.HasValue)
        {
            // Confirm the invoice line isn't matched by another unreconciled statement line
            var taken = await db.Set<PayeeStatementLine>()
                .AnyAsync(l => l.Id != lineId && l.MatchedInvoiceLineId == invoiceLineId
                    && l.ReconciliationTransactionId == null && l.MatchStatus != "Unmatched", ct);
            if (taken)
                return Result<PayeeStatementDto>.Failure("CONFLICT", "Invoice line is already matched by another statement line.");

            line.MatchedInvoiceLineId = invoiceLineId;
            line.MatchStatus = "ManualMatched";
        }
        else
        {
            line.MatchedInvoiceLineId = null;
            line.MatchStatus = "Unmatched";
        }

        await db.SaveChangesAsync(ct);
        return Result<PayeeStatementDto>.Success(await BuildDtoAsync(statementId, db, ct));
    }

    public async Task<Result<IReadOnlyList<PayeeStatementLineCandidateDto>>> GetLineMatchCandidatesAsync(
        long statementId, long lineId, CancellationToken ct = default)
    {
        var db = Db;
        var line = await db.Set<PayeeStatementLine>()
            .FirstOrDefaultAsync(l => l.Id == lineId && l.PayeeStatementId == statementId, ct);
        if (line == null)
            return Result<IReadOnlyList<PayeeStatementLineCandidateDto>>.Failure("NOT_FOUND", "Statement line not found.");

        // Entity-routed invoice fee lines not already claimed by another statement line.
        // Surface those matching the line's policy number OR its amount so the user can
        // resolve the cases auto-match missed (formatting drift or amount differences).
        var candidates = await (
            from il in db.Set<InvoiceLineEntity>()
            join inv in db.Set<Invoice>() on il.InvoiceId equals inv.Id
            join pt in db.Set<PolicyTransaction>() on (Guid?)inv.PolicyTransactionId equals (Guid?)pt.Id
            join pol in db.Set<Policy>() on pt.PolicyId equals pol.Id
            where il.PayableRouting == "Entity"
                && (pol.PolicyNumber == line.PolicyNumber || il.Amount == line.Amount)
                && !db.Set<PayeeStatementLine>().Any(psl =>
                    psl.Id != line.Id
                    && psl.MatchedInvoiceLineId == il.Id
                    && psl.MatchStatus != "Unmatched")
            select new PayeeStatementLineCandidateDto(
                il.Id, inv.InvoiceNumber, pol.PolicyNumber, il.FeeDisplayName, il.Amount,
                pol.PolicyNumber == line.PolicyNumber, il.Amount == line.Amount)
        )
        .OrderByDescending(c => c.PolicyMatches)
        .ThenByDescending(c => c.AmountMatches)
        .Take(100)
        .ToListAsync(ct);

        return Result<IReadOnlyList<PayeeStatementLineCandidateDto>>.Success(candidates);
    }

    public async Task<IReadOnlyList<LedgerAccountOptionDto>> GetApLedgerAccountsAsync(CancellationToken ct = default)
    {
        var db = Db;
        return await db.Set<LedgerAccount>()
            .Where(a => a.TenantId == 1 && a.IsActive)
            .OrderBy(a => a.InternalCode)
            .Select(a => new LedgerAccountOptionDto(a.Id, a.InternalCode, a.ExternalLabel, a.AccountType))
            .ToListAsync(ct);
    }

    public async Task<Result<PayeeStatementDto>> PostReconciliationAsync(
        long id, Guid userId, CancellationToken ct = default)
    {
        var db = Db;
        var statement = await db.Set<PayeeStatement>()
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (statement == null)
            return Result<PayeeStatementDto>.Failure("NOT_FOUND", "Statement not found.");
        if (statement.Status == "Reconciled")
            return Result<PayeeStatementDto>.Failure("ALREADY_RECONCILED", "Statement is already reconciled.");

        var unmatched = statement.Lines.Count(l => l.MatchStatus == "Unmatched");
        if (unmatched > 0)
            return Result<PayeeStatementDto>.Failure("UNMATCHED_LINES",
                $"{unmatched} line(s) are unmatched. Resolve all lines before posting.");

        var matchedLineIds = statement.Lines
            .Where(l => l.MatchedInvoiceLineId.HasValue)
            .Select(l => l.MatchedInvoiceLineId!.Value)
            .ToList();

        var invoiceLines = await db.Set<InvoiceLineEntity>()
            .Where(il => matchedLineIds.Contains(il.Id))
            .ToDictionaryAsync(il => il.Id, ct);

        var invoices = await db.Set<Invoice>()
            .Where(inv => invoiceLines.Values.Select(il => il.InvoiceId).Contains(inv.Id))
            .ToDictionaryAsync(inv => inv.Id, ct);

        foreach (var line in statement.Lines.Where(l => l.MatchedInvoiceLineId.HasValue))
        {
            var invoiceLine = invoiceLines[line.MatchedInvoiceLineId!.Value];
            var invoice = invoices[invoiceLine.InvoiceId];

            var txnId = await PostLineJeAsync(line, invoiceLine, invoice,
                statement.ApLedgerAccountId, userId, db, ct);

            line.ReconciliationTransactionId = txnId;

            // Create a Payable for disbursement
            db.Set<Payable>().Add(new Payable
            {
                InvoiceId = invoice.Id,
                PayeeName = statement.PayeeName,
                GlAccountId = statement.ApLedgerAccountId,
                Amount = line.Amount,
                InvoiceDate = statement.StatementDate,
                DueDate = statement.StatementDate.AddDays(30),
                CreatedBy = userId,
            });
        }

        statement.Status = "Reconciled";
        await db.SaveChangesAsync(ct);

        return Result<PayeeStatementDto>.Success(await BuildDtoAsync(id, db, ct));
    }

    // --- Private helpers ---

    private async Task AutoMatchLineAsync(PayeeStatementLine line, DbContext db, CancellationToken ct)
    {
        var matchedId = await (
            from il in db.Set<InvoiceLineEntity>()
            join inv in db.Set<Invoice>() on il.InvoiceId equals inv.Id
            join pt in db.Set<PolicyTransaction>() on (Guid?)inv.PolicyTransactionId equals (Guid?)pt.Id
            join pol in db.Set<Policy>() on pt.PolicyId equals pol.Id
            where il.Amount == line.Amount
                && il.PayableRouting == "Entity"
                && pol.PolicyNumber == line.PolicyNumber
                && !db.Set<PayeeStatementLine>().Any(psl =>
                    psl.Id != line.Id
                    && psl.MatchedInvoiceLineId == il.Id
                    && psl.MatchStatus != "Unmatched")
            select (long?)il.Id
        ).FirstOrDefaultAsync(ct);

        if (matchedId.HasValue)
        {
            line.MatchedInvoiceLineId = matchedId;
            line.MatchStatus = "AutoMatched";
        }
    }

    private async Task<Guid> PostLineJeAsync(
        PayeeStatementLine line, InvoiceLineEntity invoiceLine, Invoice invoice,
        int apAccountId, Guid userId, DbContext db, CancellationToken ct)
    {
        var txnId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var effectiveDate = DateOnly.FromDateTime(now);

        var rows = new List<LedgerTransaction>
        {
            // DR: tax payable account (clear the liability booked on invoice)
            new() {
                TransactionId = txnId, EffectiveDate = effectiveDate,
                AccountId = invoiceLine.LedgerAccountId,
                Debit = line.Amount, Credit = 0,
                SourceType = "PayeeStatement", SourceId = line.PayeeStatementId,
                Memo = $"Recon {line.PolicyNumber} — {invoiceLine.FeeDisplayName}",
                CreatedBy = userId, PostedAt = now
            },
            // CR: AP for filing service (liability now owed to payee, not state)
            new() {
                TransactionId = txnId, EffectiveDate = effectiveDate,
                AccountId = apAccountId,
                Debit = 0, Credit = line.Amount,
                SourceType = "PayeeStatement", SourceId = line.PayeeStatementId,
                Memo = $"AP — {line.PolicyNumber}",
                CreatedBy = userId, PostedAt = now
            }
        };

        db.Set<LedgerTransaction>().AddRange(rows);
        await db.SaveChangesAsync(ct);
        return txnId;
    }

    private async Task<PayeeStatementDto> BuildDtoAsync(long id, DbContext db, CancellationToken ct)
    {
        var statement = await db.Set<PayeeStatement>()
            .Include(s => s.ApLedgerAccount)
            .Include(s => s.Lines)
                .ThenInclude(l => l.MatchedInvoiceLine)
            .FirstAsync(s => s.Id == id, ct);

        var lines = statement.Lines
            .OrderBy(l => l.Id)
            .Select(l => new PayeeStatementLineDto(
                l.Id, l.PolicyNumber, l.StateCode, l.Amount, l.Description,
                l.MatchStatus, l.MatchedInvoiceLineId,
                l.MatchedInvoiceLine?.FeeCode,
                l.MatchedInvoiceLine?.FeeDisplayName))
            .ToList();

        return new PayeeStatementDto(
            statement.Id, statement.PayeeName, statement.StatementDate,
            statement.ReferenceNumber, statement.ApLedgerAccountId,
            statement.ApLedgerAccount.ExternalLabel, statement.StatementTotal,
            statement.Status, lines, statement.CreatedAt);
    }

    private static List<(string PolicyNumber, string StateCode, decimal Amount, string? Description)> ParseCsv(Stream stream)
    {
        var result = new List<(string, string, decimal, string?)>();
        using var reader = new System.IO.StreamReader(stream, leaveOpen: true);
        bool firstLine = true;
        while (!reader.EndOfStream)
        {
            var raw = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (firstLine) { firstLine = false; continue; }  // skip header

            var cols = raw.Split(',');
            if (cols.Length < 3) continue;

            var policy = cols[0].Trim().Trim('"');
            var state = cols[1].Trim().Trim('"');
            if (!decimal.TryParse(cols[2].Trim().Trim('"'), out var amount)) continue;
            var description = cols.Length > 3 ? cols[3].Trim().Trim('"') : null;

            if (string.IsNullOrEmpty(policy) || string.IsNullOrEmpty(state)) continue;
            result.Add((policy, state, amount, string.IsNullOrEmpty(description) ? null : description));
        }
        return result;
    }
}

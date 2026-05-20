using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class UnderwritingGuidelineControlService : IUnderwritingGuidelineControlService
{
    private readonly DbContext _db;

    public UnderwritingGuidelineControlService(DbContext db) => _db = db;

    public async Task<IReadOnlyList<UnderwritingGuidelineDocumentDto>> GetDocumentsAsync(CancellationToken ct = default)
    {
        var docs = await _db.Set<UnderwritingGuidelineDocument>()
            .Include(d => d.Program)
            .Include(d => d.Carrier)
            .Include(d => d.Controls)
            .OrderBy(d => d.ProgramName)
            .ThenBy(d => d.Carrier == null ? "" : d.Carrier.Name)
            .ThenBy(d => d.LineOfBusiness)
            .ThenBy(d => d.StateCode)
            .ThenByDescending(d => d.Version)
            .ToListAsync(ct);

        return docs.Select(MapDocument).ToList();
    }

    public async Task<Result<UnderwritingGuidelineDocumentDto>> CreateDocumentAsync(CreateUnderwritingGuidelineDocumentRequest request, Guid userId, CancellationToken ct = default)
    {
        ProgramConfiguration? program = null;
        var programName = request.ProgramName;
        var carrierId = request.CarrierId;
        var lineOfBusiness = request.LineOfBusiness;
        var stateCode = request.StateCode;

        if (request.ProgramId.HasValue)
        {
            program = await _db.Set<ProgramConfiguration>()
                .Include(p => p.Carrier)
                .SingleOrDefaultAsync(p => p.Id == request.ProgramId.Value, ct);
            if (program is null)
                return Result<UnderwritingGuidelineDocumentDto>.Failure("PROGRAM_NOT_FOUND", "Program was not found.");
            if (!program.IsActive)
                return Result<UnderwritingGuidelineDocumentDto>.Failure("PROGRAM_INACTIVE", "Program is inactive.");

            programName = program.Name;
            carrierId = program.CarrierId;
            lineOfBusiness = program.LineOfBusiness;
            stateCode = program.StateCode;
        }

        var validation = ValidateScope(programName, stateCode);
        if (validation is not null)
            return Result<UnderwritingGuidelineDocumentDto>.Failure(validation.Value.Code, validation.Value.Message);

        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<UnderwritingGuidelineDocumentDto>.Failure("TITLE_REQUIRED", "Guideline title is required.");

        if (carrierId.HasValue && !await _db.Set<Carrier>().AnyAsync(c => c.Id == carrierId.Value, ct))
            return Result<UnderwritingGuidelineDocumentDto>.Failure("CARRIER_NOT_FOUND", "Company was not found.");

        var normalizedProgram = programName.Trim();
        var normalizedState = NormalizeStateCode(stateCode);
        var nextVersion = await _db.Set<UnderwritingGuidelineDocument>()
            .Where(d => d.ProgramName == normalizedProgram
                && d.CarrierId == carrierId
                && d.LineOfBusiness == lineOfBusiness
                && d.StateCode == normalizedState)
            .Select(d => (int?)d.Version)
            .MaxAsync(ct) ?? 0;

        var doc = new UnderwritingGuidelineDocument
        {
            ProgramId = program?.Id,
            ProgramName = normalizedProgram,
            CarrierId = carrierId,
            LineOfBusiness = lineOfBusiness,
            StateCode = normalizedState,
            Title = request.Title.Trim(),
            SourceFileName = TrimToNull(request.SourceFileName),
            SourceBlobName = TrimToNull(request.SourceBlobName),
            Notes = TrimToNull(request.Notes),
            Version = nextVersion + 1,
            CreatedByUserId = userId
        };

        _db.Set<UnderwritingGuidelineDocument>().Add(doc);
        AddAudit(doc.Id, null, "DocumentCreated", userId, request.Notes, null, Snapshot(doc));
        await _db.SaveChangesAsync(ct);

        doc.Program = program;
        doc.Carrier = carrierId.HasValue
            ? await _db.Set<Carrier>().FindAsync([carrierId.Value], ct)
            : null;

        return Result<UnderwritingGuidelineDocumentDto>.Success(MapDocument(doc));
    }

    public async Task<IReadOnlyList<UnderwritingGuidelineControlDto>> GetControlsAsync(Guid guidelineDocumentId, CancellationToken ct = default)
    {
        var controls = await _db.Set<UnderwritingGuidelineControl>()
            .Include(c => c.Program)
            .Include(c => c.Carrier)
            .Where(c => c.GuidelineDocumentId == guidelineDocumentId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Label)
            .ToListAsync(ct);

        return controls.Select(MapControl).ToList();
    }

    public async Task<Result<IReadOnlyList<UnderwritingGuidelineControlDto>>> AddProposedControlsAsync(Guid guidelineDocumentId, AddProposedUnderwritingControlsRequest request, Guid userId, CancellationToken ct = default)
    {
        var doc = await _db.Set<UnderwritingGuidelineDocument>()
            .Include(d => d.Program)
            .SingleOrDefaultAsync(d => d.Id == guidelineDocumentId, ct);
        if (doc is null)
            return Result<IReadOnlyList<UnderwritingGuidelineControlDto>>.Failure("DOCUMENT_NOT_FOUND", "Guideline document was not found.");

        if (request.Controls.Count == 0)
            return Result<IReadOnlyList<UnderwritingGuidelineControlDto>>.Failure("CONTROLS_REQUIRED", "At least one proposed control is required.");

        var created = new List<UnderwritingGuidelineControl>();
        foreach (var item in request.Controls)
        {
            var validation = ValidateControl(item.RuleKey, item.Label, item.ConditionJson, item.AiConfidence);
            if (validation is not null)
                return Result<IReadOnlyList<UnderwritingGuidelineControlDto>>.Failure(validation.Value.Code, validation.Value.Message);

            var control = new UnderwritingGuidelineControl
            {
                GuidelineDocumentId = doc.Id,
                ProgramId = doc.ProgramId,
                Program = doc.Program,
                ProgramName = doc.ProgramName,
                CarrierId = doc.CarrierId,
                LineOfBusiness = doc.LineOfBusiness,
                StateCode = doc.StateCode,
                ItemType = item.ItemType,
                Stage = item.Stage,
                Severity = item.Severity,
                Status = UnderwritingControlStatus.AiSuggested,
                RuleKey = item.RuleKey.Trim(),
                Label = item.Label.Trim(),
                Description = TrimToNull(item.Description),
                ConditionJson = TrimToNull(item.ConditionJson),
                IsBlocking = item.IsBlocking,
                OverrideAllowed = item.OverrideAllowed,
                OverridePermission = TrimToNull(item.OverridePermission) ?? AppPermissions.UnderwritingClearanceOverride,
                SourceCitation = TrimToNull(item.SourceCitation),
                AiConfidence = item.AiConfidence,
                SortOrder = item.SortOrder
            };
            _db.Set<UnderwritingGuidelineControl>().Add(control);
            created.Add(control);
        }

        await _db.SaveChangesAsync(ct);

        foreach (var control in created)
            AddAudit(doc.Id, control.Id, "ControlSuggested", userId, "AI or integration proposed control for human review.", null, Snapshot(control));

        await _db.SaveChangesAsync(ct);
        return Result<IReadOnlyList<UnderwritingGuidelineControlDto>>.Success(created.Select(MapControl).ToList());
    }

    public async Task<Result<UnderwritingGuidelineControlDto>> UpdateControlAsync(Guid controlId, UpdateUnderwritingGuidelineControlRequest request, Guid userId, CancellationToken ct = default)
    {
        var control = await _db.Set<UnderwritingGuidelineControl>().Include(c => c.Program).Include(c => c.Carrier).SingleOrDefaultAsync(c => c.Id == controlId, ct);
        if (control is null)
            return Result<UnderwritingGuidelineControlDto>.Failure("CONTROL_NOT_FOUND", "Guideline control was not found.");

        if (control.Status is UnderwritingControlStatus.Published or UnderwritingControlStatus.Retired)
            return Result<UnderwritingGuidelineControlDto>.Failure("CONTROL_LOCKED", "Published or retired controls cannot be edited. Create a new guideline version instead.");

        var validation = ValidateControl(request.RuleKey, request.Label, request.ConditionJson, null);
        if (validation is not null)
            return Result<UnderwritingGuidelineControlDto>.Failure(validation.Value.Code, validation.Value.Message);

        var before = Snapshot(control);
        control.ItemType = request.ItemType;
        control.Stage = request.Stage;
        control.Severity = request.Severity;
        control.Status = UnderwritingControlStatus.Draft;
        control.RuleKey = request.RuleKey.Trim();
        control.Label = request.Label.Trim();
        control.Description = TrimToNull(request.Description);
        control.ConditionJson = TrimToNull(request.ConditionJson);
        control.IsBlocking = request.IsBlocking;
        control.OverrideAllowed = request.OverrideAllowed;
        control.OverridePermission = TrimToNull(request.OverridePermission);
        control.SourceCitation = TrimToNull(request.SourceCitation);
        control.SortOrder = request.SortOrder;

        AddAudit(control.GuidelineDocumentId, control.Id, "ControlEdited", userId, request.ChangeNotes, before, Snapshot(control));
        await _db.SaveChangesAsync(ct);
        return Result<UnderwritingGuidelineControlDto>.Success(MapControl(control));
    }

    public Task<Result<UnderwritingGuidelineControlDto>> ApproveControlAsync(Guid controlId, Guid userId, string? notes, CancellationToken ct = default) =>
        SetReviewStatusAsync(controlId, userId, UnderwritingControlStatus.Approved, "ControlApproved", notes, ct);

    public Task<Result<UnderwritingGuidelineControlDto>> RejectControlAsync(Guid controlId, Guid userId, string? notes, CancellationToken ct = default) =>
        SetReviewStatusAsync(controlId, userId, UnderwritingControlStatus.Rejected, "ControlRejected", notes, ct);

    public async Task<Result<UnderwritingGuidelineControlDto>> PublishControlAsync(Guid controlId, Guid userId, string? notes, CancellationToken ct = default)
    {
        var control = await _db.Set<UnderwritingGuidelineControl>().Include(c => c.Program).Include(c => c.Carrier).SingleOrDefaultAsync(c => c.Id == controlId, ct);
        if (control is null)
            return Result<UnderwritingGuidelineControlDto>.Failure("CONTROL_NOT_FOUND", "Guideline control was not found.");

        if (control.Status != UnderwritingControlStatus.Approved)
            return Result<UnderwritingGuidelineControlDto>.Failure("CONTROL_NOT_APPROVED", "Only approved controls can be published.");

        var before = Snapshot(control);
        control.Status = UnderwritingControlStatus.Published;
        control.PublishedByUserId = userId;
        control.PublishedAt = DateTime.UtcNow;
        AddAudit(control.GuidelineDocumentId, control.Id, "ControlPublished", userId, notes, before, control);
        await _db.SaveChangesAsync(ct);

        return Result<UnderwritingGuidelineControlDto>.Success(MapControl(control));
    }

    public async Task<Result<UnderwritingGuidelineControlDto>> RetireControlAsync(Guid controlId, Guid userId, string? reason, CancellationToken ct = default)
    {
        var control = await _db.Set<UnderwritingGuidelineControl>().Include(c => c.Program).Include(c => c.Carrier).SingleOrDefaultAsync(c => c.Id == controlId, ct);
        if (control is null)
            return Result<UnderwritingGuidelineControlDto>.Failure("CONTROL_NOT_FOUND", "Guideline control was not found.");

        if (control.Status != UnderwritingControlStatus.Published)
            return Result<UnderwritingGuidelineControlDto>.Failure("CONTROL_NOT_PUBLISHED", "Only published controls can be retired.");

        var before = Snapshot(control);
        control.Status = UnderwritingControlStatus.Retired;
        control.RetiredByUserId = userId;
        control.RetiredAt = DateTime.UtcNow;
        control.RetirementReason = TrimToNull(reason);
        AddAudit(control.GuidelineDocumentId, control.Id, "ControlRetired", userId, reason, before, control);
        await _db.SaveChangesAsync(ct);

        return Result<UnderwritingGuidelineControlDto>.Success(MapControl(control));
    }

    public async Task<IReadOnlyList<UnderwritingGuidelineAuditLogDto>> GetAuditLogAsync(Guid? guidelineDocumentId = null, Guid? guidelineControlId = null, CancellationToken ct = default)
    {
        var query = _db.Set<UnderwritingGuidelineAuditLog>().AsQueryable();
        if (guidelineDocumentId.HasValue)
            query = query.Where(a => a.GuidelineDocumentId == guidelineDocumentId.Value);
        if (guidelineControlId.HasValue)
            query = query.Where(a => a.GuidelineControlId == guidelineControlId.Value);

        var logs = await query.OrderByDescending(a => a.CreatedAt).Take(200).ToListAsync(ct);
        return logs.Select(MapAudit).ToList();
    }

    private async Task<Result<UnderwritingGuidelineControlDto>> SetReviewStatusAsync(Guid controlId, Guid userId, UnderwritingControlStatus status, string action, string? notes, CancellationToken ct)
    {
        var control = await _db.Set<UnderwritingGuidelineControl>().Include(c => c.Program).Include(c => c.Carrier).SingleOrDefaultAsync(c => c.Id == controlId, ct);
        if (control is null)
            return Result<UnderwritingGuidelineControlDto>.Failure("CONTROL_NOT_FOUND", "Guideline control was not found.");

        if (control.Status is UnderwritingControlStatus.Published or UnderwritingControlStatus.Retired)
            return Result<UnderwritingGuidelineControlDto>.Failure("CONTROL_LOCKED", "Published or retired controls cannot be reviewed.");

        var before = Snapshot(control);
        control.Status = status;
        control.ReviewedByUserId = userId;
        control.ReviewedAt = DateTime.UtcNow;
        control.ReviewNotes = TrimToNull(notes);
        AddAudit(control.GuidelineDocumentId, control.Id, action, userId, notes, before, control);
        await _db.SaveChangesAsync(ct);

        return Result<UnderwritingGuidelineControlDto>.Success(MapControl(control));
    }

    private static (string Code, string Message)? ValidateScope(string programName, string stateCode)
    {
        if (string.IsNullOrWhiteSpace(programName))
            return ("PROGRAM_REQUIRED", "Program is required.");

        var normalizedState = NormalizeStateCode(stateCode);
        if (normalizedState != "ALL" && normalizedState.Length != 2)
            return ("STATE_INVALID", "State must be ALL or a two-letter state code.");

        return null;
    }

    private static (string Code, string Message)? ValidateControl(string ruleKey, string label, string? conditionJson, decimal? aiConfidence)
    {
        if (string.IsNullOrWhiteSpace(ruleKey))
            return ("RULE_KEY_REQUIRED", "Rule key is required.");
        if (string.IsNullOrWhiteSpace(label))
            return ("LABEL_REQUIRED", "Control label is required.");
        if (!string.IsNullOrWhiteSpace(conditionJson))
        {
            try { JsonDocument.Parse(conditionJson); }
            catch (JsonException) { return ("CONDITION_JSON_INVALID", "Condition JSON is not valid."); }
        }
        if (aiConfidence is < 0 or > 1)
            return ("AI_CONFIDENCE_INVALID", "AI confidence must be between 0 and 1.");

        return null;
    }

    private static string NormalizeStateCode(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return "ALL";
        var trimmed = stateCode.Trim().ToUpperInvariant();
        return trimmed is "ALL" or "*" ? "ALL" : trimmed;
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void AddAudit(Guid? documentId, Guid? controlId, string action, Guid userId, string? notes, object? before, object? after)
    {
        _db.Set<UnderwritingGuidelineAuditLog>().Add(new UnderwritingGuidelineAuditLog
        {
            GuidelineDocumentId = documentId,
            GuidelineControlId = controlId,
            Action = action,
            ActorUserId = userId,
            Notes = TrimToNull(notes),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after)
        });
    }

    private static object Snapshot(UnderwritingGuidelineControl c) => new
    {
        c.ProgramId,
        c.ProgramName,
        c.CarrierId,
        c.LineOfBusiness,
        c.StateCode,
        c.ItemType,
        c.Stage,
        c.Severity,
        c.Status,
        c.RuleKey,
        c.Label,
        c.Description,
        c.ConditionJson,
        c.IsBlocking,
        c.OverrideAllowed,
        c.OverridePermission,
        c.SourceCitation,
        c.SortOrder
    };

    private static object Snapshot(UnderwritingGuidelineDocument d) => new
    {
        d.ProgramId,
        d.ProgramName,
        d.CarrierId,
        d.LineOfBusiness,
        d.StateCode,
        d.Title,
        d.SourceFileName,
        d.SourceBlobName,
        d.Notes,
        d.Version
    };

    private static UnderwritingGuidelineDocumentDto MapDocument(UnderwritingGuidelineDocument doc) =>
        new(
            doc.Id,
            doc.ProgramId,
            doc.Program?.Code,
            doc.ProgramName,
            doc.CarrierId,
            doc.Carrier?.Name,
            doc.LineOfBusiness,
            doc.StateCode,
            doc.Title,
            doc.SourceFileName,
            doc.SourceBlobName,
            doc.Notes,
            doc.Version,
            doc.CreatedByUserId,
            doc.CreatedAt,
            doc.Controls.Count);

    private static UnderwritingGuidelineControlDto MapControl(UnderwritingGuidelineControl control) =>
        new(
            control.Id,
            control.GuidelineDocumentId,
            control.ProgramId,
            control.Program?.Code,
            control.ProgramName,
            control.CarrierId,
            control.Carrier?.Name,
            control.LineOfBusiness,
            control.StateCode,
            control.ItemType,
            control.Stage,
            control.Severity,
            control.Status,
            control.RuleKey,
            control.Label,
            control.Description,
            control.ConditionJson,
            control.IsBlocking,
            control.OverrideAllowed,
            control.OverridePermission,
            control.SourceCitation,
            control.AiConfidence,
            control.Version,
            control.SortOrder,
            control.ReviewedByUserId,
            control.ReviewedAt,
            control.ReviewNotes,
            control.PublishedByUserId,
            control.PublishedAt,
            control.RetiredByUserId,
            control.RetiredAt,
            control.RetirementReason);

    private static UnderwritingGuidelineAuditLogDto MapAudit(UnderwritingGuidelineAuditLog log) =>
        new(
            log.Id,
            log.GuidelineDocumentId,
            log.GuidelineControlId,
            log.Action,
            log.ActorUserId,
            log.Notes,
            log.BeforeJson,
            log.AfterJson,
            log.CreatedAt);
}

using System.Text.Json;
using SIMS.Application.Common;
using SIMS.Application.Configuration;
using SIMS.Application.DTOs.DocumentExtraction;
using SIMS.Application.DTOs.InboundEmails;
using SIMS.Application.DTOs.Submissions;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SIMS.Application.Services;

public class InboundEmailService : IInboundEmailService
{
    private readonly Microsoft.EntityFrameworkCore.DbContext _db;
    private readonly IDocumentExtractionService _gemini;
    private readonly ILogger<InboundEmailService> _logger;
    private readonly IntakeSettings _intakeSettings;

    public InboundEmailService(
        Microsoft.EntityFrameworkCore.DbContext db,
        IDocumentExtractionService gemini,
        IOptions<IntakeSettings> intakeSettings,
        ILogger<InboundEmailService> logger)
    {
        _db = db;
        _gemini = gemini;
        _intakeSettings = intakeSettings.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<InboundEmailListItemDto>> GetUnprocessedAsync()
    {
        var emails = await _db.Set<InboundEmail>()
            .Include(e => e.Attachments.Where(a => !a.IsDeleted))
            .Where(e => !e.IsDeleted && !e.IsProcessed)
            .OrderByDescending(e => e.ReceivedAt)
            .ToListAsync();

        return emails.Select(MapToListItemDto);
    }

    public async Task<Result<InboundEmailDto>> GetByIdAsync(Guid id)
    {
        var email = await _db.Set<InboundEmail>()
            .Include(e => e.Attachments.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

        return email == null
            ? Result<InboundEmailDto>.Failure("NOT_FOUND", "Inbound email not found.")
            : Result<InboundEmailDto>.Success(MapToDto(email));
    }

    public async Task<Result<CreateSubmissionFromEmailResponse>> CreateSubmissionFromEmailAsync(
        Guid emailId, Guid currentUserId, Guid? insuredId = null, List<Guid>? attachmentIds = null, string? lineOfBusiness = null)
    {
        var email = await _db.Set<InboundEmail>()
            .Include(e => e.Attachments.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(e => e.Id == emailId && !e.IsDeleted);

        if (email == null)
            return Result<CreateSubmissionFromEmailResponse>.Failure("NOT_FOUND", "Inbound email not found.");

        if (email.IsProcessed && email.LinkedSubmissionId.HasValue)
            return Result<CreateSubmissionFromEmailResponse>.Failure("ALREADY_PROCESSED", "A submission has already been created from this email.");

        // Resolve insured
        Guid resolvedInsuredId;
        if (insuredId.HasValue)
        {
            var exists = await _db.Set<Insured>().AnyAsync(i => i.Id == insuredId.Value && !i.IsDeleted);
            if (!exists)
                return Result<CreateSubmissionFromEmailResponse>.Failure("INSURED_NOT_FOUND", "Selected insured not found.");
            resolvedInsuredId = insuredId.Value;
        }
        else
        {
            var newInsured = BuildInsuredFromSender(email.FromName, email.FromAddress, currentUserId);
            _db.Set<Insured>().Add(newInsured);
            await _db.SaveChangesAsync();
            resolvedInsuredId = newInsured.Id;
        }

        // Filter to selected attachments only (if caller specified a subset)
        var attachmentsToProcess = attachmentIds?.Count > 0
            ? email.Attachments.Where(a => attachmentIds.Contains(a.Id)).ToList()
            : email.Attachments.ToList();

        // Run Gemini extraction — all LOB results merged into one submission
        var extractionStatus = "NotApplicable";
        List<DocumentLobExtraction>? extractions = null;
        try
        {
            extractions = await _gemini.ExtractFromAttachmentsAsync(attachmentsToProcess, lineOfBusiness);
            if (extractions != null)
            {
                if (extractions.Count == 0)
                    extractionStatus = "Failed";
                else if (extractions.Any(e => string.IsNullOrEmpty(e.LineOfBusiness)))
                    extractionStatus = "DetectionFailed";
                else
                    extractionStatus = "Completed";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini extraction failed for email {EmailId}", emailId);
            extractionStatus = "Failed";
        }

        // Determine lines of business: detected LOBs + inference from extracted data
        var linesOfBusiness = new List<string>();
        if (extractions != null)
        {
            linesOfBusiness.AddRange(extractions
                .Where(e => !string.IsNullOrEmpty(e.LineOfBusiness))
                .Select(e => e.LineOfBusiness)
                .Distinct());

            // For any result with no detected LOB, try to infer from the data
            foreach (var e in extractions.Where(e => string.IsNullOrEmpty(e.LineOfBusiness)))
                foreach (var inferred in DocumentExtractionResult.InferLinesOfBusiness(e.Data))
                    if (!linesOfBusiness.Contains(inferred))
                        linesOfBusiness.Add(inferred);
        }

        var year = DateTime.UtcNow.Year;
        var prefix = $"SUB-{year}-";
        var count = await _db.Set<Submission>()
            .IgnoreQueryFilters()
            .CountAsync(s => s.SubmissionNumber.StartsWith(prefix));

        var submission = new Submission
        {
            SubmissionNumber = $"{prefix}{(count + 1):D4}",
            InsuredId = resolvedInsuredId,
            UnderwriterId = currentUserId,
            CreatedById = currentUserId,
            Status = SubmissionStatus.New,
            LinesOfBusiness = linesOfBusiness.Count > 0
                ? JsonSerializer.Serialize(linesOfBusiness)
                : null,
        };
        _db.Set<Submission>().Add(submission);

        // Copy email attachments to the submission
        foreach (var emailAttachment in attachmentsToProcess)
        {
            _db.Set<Attachment>().Add(new Attachment
            {
                SubmissionId = submission.Id,
                EntityType = DocumentEntityType.Submission,
                DocumentType = MapDocumentType(emailAttachment.DocumentType),
                FileName = emailAttachment.FileName,
                BlobPath = emailAttachment.BlobUrl,
                ContentType = emailAttachment.ContentType ?? "application/octet-stream",
                FileSizeBytes = emailAttachment.FileSizeBytes,
                Description = $"Imported from email: {email.Subject}",
                UploadedById = currentUserId,
            });
        }

        email.LinkedSubmissionId = submission.Id;
        email.IsProcessed = true;
        email.ProcessedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Queue automated intake for the new submission (kill-switch: Intake:Enabled).
        if (_intakeSettings.Enabled)
        {
            _db.Set<IntakeJob>().Add(new IntakeJob { SubmissionId = submission.Id, Status = IntakeJobStatus.Queued });
            await _db.SaveChangesAsync();
        }

        // Merge all LOB extractions and apply to the single submission
        if (extractions?.Count > 0)
        {
            try
            {
                var merged = new DocumentExtractionResult();
                foreach (var e in extractions)
                    DocumentExtractionResult.MergeInto(merged, e.Data);
                await ApplyExtractionAsync(submission.Id, resolvedInsuredId, merged, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply extraction to submission {SubmissionId}", submission.Id);
            }
        }

        await _db.Entry(submission).Reference(s => s.Insured).LoadAsync();
        await _db.Entry(submission).Reference(s => s.Underwriter).LoadAsync();

        var dto = new SubmissionDto
        {
            Id = submission.Id,
            SubmissionNumber = submission.SubmissionNumber,
            InsuredId = resolvedInsuredId,
            InsuredName = submission.Insured?.DisplayName ?? "",
            UnderwriterId = currentUserId,
            UnderwriterName = submission.Underwriter?.FullName ?? "",
            Status = submission.Status,
            LinesOfBusiness = linesOfBusiness,
            CreatedAt = submission.CreatedAt,
        };

        return Result<CreateSubmissionFromEmailResponse>.Success(new CreateSubmissionFromEmailResponse
        {
            Submission = dto,
            ExtractionStatus = extractionStatus,
            EmailId = emailId,
        });
    }

    public async Task<Result<string>> ReExtractAsync(Guid emailId, Guid currentUserId, string? lineOfBusiness = null)
    {
        var email = await _db.Set<InboundEmail>()
            .Include(e => e.Attachments.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(e => e.Id == emailId && !e.IsDeleted);

        if (email == null)
            return Result<string>.Failure("NOT_FOUND", "Inbound email not found.");

        if (!email.LinkedSubmissionId.HasValue)
            return Result<string>.Failure("NO_SUBMISSION", "No submission is linked to this email.");

        var submissionId = email.LinkedSubmissionId.Value;

        List<DocumentLobExtraction>? extractions;
        try
        {
            extractions = await _gemini.ExtractFromAttachmentsAsync(email.Attachments, lineOfBusinessHint: lineOfBusiness);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Re-extract failed for email {EmailId}", emailId);
            return Result<string>.Failure("EXTRACTION_FAILED", "AI extraction failed. Please fill in the data manually.");
        }

        if (extractions == null || extractions.Count == 0)
            return Result<string>.Failure("NO_ELIGIBLE_ATTACHMENTS", "No extractable attachments found on this email.");

        var submission = await _db.Set<Submission>().FindAsync(submissionId);
        if (submission == null)
            return Result<string>.Failure("SUBMISSION_NOT_FOUND", "Linked submission not found.");

        // Determine updated lines of business from re-extraction result
        var linesOfBusiness = new List<string>();
        linesOfBusiness.AddRange(extractions
            .Where(e => !string.IsNullOrEmpty(e.LineOfBusiness))
            .Select(e => e.LineOfBusiness)
            .Distinct());
        foreach (var e in extractions.Where(e => string.IsNullOrEmpty(e.LineOfBusiness)))
            foreach (var inferred in DocumentExtractionResult.InferLinesOfBusiness(e.Data))
                if (!linesOfBusiness.Contains(inferred))
                    linesOfBusiness.Add(inferred);

        // If a specific LOB was requested, ensure it's in the list even if detection missed it
        if (!string.IsNullOrEmpty(lineOfBusiness) && !linesOfBusiness.Contains(lineOfBusiness))
            linesOfBusiness.Add(lineOfBusiness);

        if (linesOfBusiness.Count > 0)
        {
            submission.LinesOfBusiness = JsonSerializer.Serialize(linesOfBusiness);
            submission.UpdatedAt = DateTime.UtcNow;
        }

        var extractionStatus = extractions.Any(e => string.IsNullOrEmpty(e.LineOfBusiness))
            ? "DetectionFailed"
            : "Completed";

        // Merge all LOB results into one and re-apply to the linked submission
        var merged = new DocumentExtractionResult();
        foreach (var e in extractions)
            DocumentExtractionResult.MergeInto(merged, e.Data);

        await ApplyExtractionAsync(submissionId, submission.InsuredId, merged, currentUserId, replaceExisting: true);

        return Result<string>.Success(extractionStatus);
    }

    // -------------------------------------------------------------------------
    // Extraction helpers
    // -------------------------------------------------------------------------

    private async Task ApplyExtractionAsync(
        Guid submissionId, Guid insuredId, DocumentExtractionResult data, Guid userId, bool replaceExisting = false)
    {
        if (!string.IsNullOrWhiteSpace(data.DescriptionOfOperations))
        {
            var sub = await _db.Set<Submission>().FindAsync(submissionId);
            if (sub != null && (replaceExisting || string.IsNullOrWhiteSpace(sub.DescriptionOfOperations)))
                sub.DescriptionOfOperations = data.DescriptionOfOperations;
        }

        var insured = await _db.Set<Insured>().FindAsync(insuredId);
        if (insured != null)
        {
            if (!string.IsNullOrWhiteSpace(data.Dba) && (replaceExisting || insured.Dba == null))
                insured.Dba = data.Dba;

            if (!string.IsNullOrWhiteSpace(data.EntityType)
                && (replaceExisting || insured.EntityType == null)
                && Enum.TryParse<BusinessEntityType>(data.EntityType, ignoreCase: true, out var entityType))
                insured.EntityType = entityType;

            if (data.YearsInBusiness.HasValue && (replaceExisting || insured.YearsInBusiness == null))
                insured.YearsInBusiness = data.YearsInBusiness;
        }

        // Drivers
        var hasDrivers = await _db.Set<SubmissionDriver>().AnyAsync(d => d.SubmissionId == submissionId && !d.IsDeleted);
        if (!hasDrivers || replaceExisting)
        {
            if (replaceExisting)
                _db.Set<SubmissionDriver>().RemoveRange(
                    await _db.Set<SubmissionDriver>().Where(d => d.SubmissionId == submissionId).ToListAsync());

            for (var i = 0; i < data.Drivers.Count; i++)
            {
                var d = data.Drivers[i];
                if (string.IsNullOrWhiteSpace(d.Name)) continue;
                _db.Set<SubmissionDriver>().Add(new SubmissionDriver
                {
                    SubmissionId = submissionId,
                    DriverNumber = d.DriverNumber ?? (i + 1),
                    Name = d.Name,
                    DateOfBirth = ParseDate(d.DateOfBirth),
                    LicenseNumber = d.LicenseNumber,
                    LicenseState = d.LicenseState,
                    DateHired = ParseDate(d.DateHired)
                });
            }
        }

        // Vehicles
        var hasVehicles = await _db.Set<SubmissionVehicle>().AnyAsync(v => v.SubmissionId == submissionId && !v.IsDeleted);
        if (!hasVehicles || replaceExisting)
        {
            if (replaceExisting)
                _db.Set<SubmissionVehicle>().RemoveRange(
                    await _db.Set<SubmissionVehicle>().Where(v => v.SubmissionId == submissionId).ToListAsync());

            for (var i = 0; i < data.Vehicles.Count; i++)
            {
                var v = data.Vehicles[i];
                _db.Set<SubmissionVehicle>().Add(new SubmissionVehicle
                {
                    SubmissionId = submissionId,
                    UnitNumber = v.UnitNumber ?? (i + 1),
                    Year = v.Year,
                    Make = v.Make,
                    Model = v.Model,
                    Vin = v.Vin,
                    Gvw = v.Gvw,
                    VehicleClass = Enum.TryParse<VehicleClass>(v.VehicleClass, ignoreCase: true, out var vc) ? vc : VehicleClass.Unknown,
                    GaragingZip = v.GaragingZip,
                    Radius = Enum.TryParse<OperatingRadius>(v.Radius, ignoreCase: true, out var radius) ? radius : null
                });
            }
        }

        // Locations
        var hasLocations = await _db.Set<SubmissionLocation>().AnyAsync(l => l.SubmissionId == submissionId && !l.IsDeleted);
        if (!hasLocations || replaceExisting)
        {
            if (replaceExisting)
                _db.Set<SubmissionLocation>().RemoveRange(
                    await _db.Set<SubmissionLocation>().Where(l => l.SubmissionId == submissionId).ToListAsync());

            for (var i = 0; i < data.Locations.Count; i++)
            {
                var l = data.Locations[i];
                if (string.IsNullOrWhiteSpace(l.Address)) continue;
                _db.Set<SubmissionLocation>().Add(new SubmissionLocation
                {
                    SubmissionId = submissionId,
                    LocationNumber = l.LocationNumber ?? (i + 1),
                    Address = l.Address,
                    ZipCode = l.ZipCode
                });
            }
        }

        // Prior carriers
        var hasCarriers = await _db.Set<SubmissionPriorCarrier>().AnyAsync(p => p.SubmissionId == submissionId && !p.IsDeleted);
        if (!hasCarriers || replaceExisting)
        {
            if (replaceExisting)
                _db.Set<SubmissionPriorCarrier>().RemoveRange(
                    await _db.Set<SubmissionPriorCarrier>().Where(p => p.SubmissionId == submissionId).ToListAsync());

            foreach (var pc in data.PriorCarriers)
            {
                if (string.IsNullOrWhiteSpace(pc.CarrierName)) continue;
                _db.Set<SubmissionPriorCarrier>().Add(new SubmissionPriorCarrier
                {
                    SubmissionId = submissionId,
                    LineOfBusiness = pc.LineOfBusiness,
                    CarrierName = pc.CarrierName,
                    PolicyNumber = pc.PolicyNumber,
                    ExpirationDate = ParseDate(pc.ExpirationDate),
                    Premium = pc.Premium
                });
            }
        }

        // Supplemental (1-to-1)
        if (data.Supplemental != null)
        {
            var existing = await _db.Set<SubmissionSupplemental>()
                .FirstOrDefaultAsync(s => s.SubmissionId == submissionId && !s.IsDeleted);

            if (existing == null)
            {
                _db.Set<SubmissionSupplemental>().Add(new SubmissionSupplemental
                {
                    SubmissionId = submissionId,
                    CommoditiesHauled = Serialize(data.Supplemental.CommoditiesHauled),
                    TerminalLocations = Serialize(data.Supplemental.TerminalLocations),
                    FilingsRequired = Serialize(data.Supplemental.FilingsRequired),
                    SafetyProgramInPlace = data.Supplemental.SafetyProgramInPlace,
                    OwnerOperator = data.Supplemental.OwnerOperator
                });
            }
            else if (replaceExisting)
            {
                existing.CommoditiesHauled = Serialize(data.Supplemental.CommoditiesHauled);
                existing.TerminalLocations = Serialize(data.Supplemental.TerminalLocations);
                existing.FilingsRequired = Serialize(data.Supplemental.FilingsRequired);
                existing.SafetyProgramInPlace = data.Supplemental.SafetyProgramInPlace;
                existing.OwnerOperator = data.Supplemental.OwnerOperator;
            }
        }

        // GL coverages (1-to-1)
        if (data.GLCoverages != null)
        {
            var existing = await _db.Set<SubmissionGLCoverages>()
                .FirstOrDefaultAsync(g => g.SubmissionId == submissionId && !g.IsDeleted);

            if (existing == null)
            {
                _db.Set<SubmissionGLCoverages>().Add(new SubmissionGLCoverages
                {
                    SubmissionId = submissionId,
                    GeneralAggregate = data.GLCoverages.GeneralAggregate,
                    ProductsCompletedOps = data.GLCoverages.ProductsCompletedOps,
                    EachOccurrence = data.GLCoverages.EachOccurrence,
                    PersonalAndAdvInjury = data.GLCoverages.PersonalAndAdvInjury,
                    DamageToRentedPremises = data.GLCoverages.DamageToRentedPremises,
                    MedicalExpense = data.GLCoverages.MedicalExpense,
                    TotalSubcontractorCost = data.GLCoverages.TotalSubcontractorCost
                });
            }
            else if (replaceExisting)
            {
                existing.GeneralAggregate = data.GLCoverages.GeneralAggregate;
                existing.ProductsCompletedOps = data.GLCoverages.ProductsCompletedOps;
                existing.EachOccurrence = data.GLCoverages.EachOccurrence;
                existing.PersonalAndAdvInjury = data.GLCoverages.PersonalAndAdvInjury;
                existing.DamageToRentedPremises = data.GLCoverages.DamageToRentedPremises;
                existing.MedicalExpense = data.GLCoverages.MedicalExpense;
                existing.TotalSubcontractorCost = data.GLCoverages.TotalSubcontractorCost;
            }
        }

        // GL classifications
        var hasGLClass = await _db.Set<SubmissionGLClassification>().AnyAsync(c => c.SubmissionId == submissionId && !c.IsDeleted);
        if (!hasGLClass || replaceExisting)
        {
            if (replaceExisting)
                _db.Set<SubmissionGLClassification>().RemoveRange(
                    await _db.Set<SubmissionGLClassification>().Where(c => c.SubmissionId == submissionId).ToListAsync());

            for (var i = 0; i < data.GLClassifications.Count; i++)
            {
                var gc = data.GLClassifications[i];
                if (string.IsNullOrWhiteSpace(gc.ClassCode)) continue;
                _db.Set<SubmissionGLClassification>().Add(new SubmissionGLClassification
                {
                    SubmissionId = submissionId,
                    LocationNumber = gc.LocationNumber ?? 1,
                    ClassCode = gc.ClassCode,
                    Description = gc.Description ?? string.Empty,
                    PremiumBasis = gc.PremiumBasis,
                    Exposure = gc.Exposure
                });
            }
        }

        // IM coverages (1-to-1)
        if (data.IMCoverages != null)
        {
            var existing = await _db.Set<SubmissionIMCoverages>()
                .FirstOrDefaultAsync(m => m.SubmissionId == submissionId && !m.IsDeleted);

            if (existing == null)
            {
                _db.Set<SubmissionIMCoverages>().Add(new SubmissionIMCoverages
                {
                    SubmissionId = submissionId,
                    ScheduledEquipmentTotalLimit = data.IMCoverages.ScheduledEquipmentTotalLimit,
                    UnscheduledEquipmentLimit = data.IMCoverages.UnscheduledEquipmentLimit,
                    MaximumValueAnyOneItem = data.IMCoverages.MaximumValueAnyOneItem,
                    Deductible = data.IMCoverages.Deductible,
                    CoinsurancePercentage = data.IMCoverages.CoinsurancePercentage
                });
            }
            else if (replaceExisting)
            {
                existing.ScheduledEquipmentTotalLimit = data.IMCoverages.ScheduledEquipmentTotalLimit;
                existing.UnscheduledEquipmentLimit = data.IMCoverages.UnscheduledEquipmentLimit;
                existing.MaximumValueAnyOneItem = data.IMCoverages.MaximumValueAnyOneItem;
                existing.Deductible = data.IMCoverages.Deductible;
                existing.CoinsurancePercentage = data.IMCoverages.CoinsurancePercentage;
            }
        }

        // Equipment
        var hasEquipment = await _db.Set<SubmissionEquipment>().AnyAsync(e => e.SubmissionId == submissionId && !e.IsDeleted);
        if (!hasEquipment || replaceExisting)
        {
            if (replaceExisting)
                _db.Set<SubmissionEquipment>().RemoveRange(
                    await _db.Set<SubmissionEquipment>().Where(e => e.SubmissionId == submissionId).ToListAsync());

            for (var i = 0; i < data.Equipment.Count; i++)
            {
                var eq = data.Equipment[i];
                _db.Set<SubmissionEquipment>().Add(new SubmissionEquipment
                {
                    SubmissionId = submissionId,
                    ItemNumber = eq.ItemNumber ?? (i + 1),
                    Year = eq.Year,
                    Make = eq.Make,
                    Model = eq.Model,
                    Description = eq.Description ?? string.Empty,
                    SerialNumber = eq.SerialNumber,
                    Value = eq.Value
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    private static DateOnly? ParseDate(string? s) =>
        string.IsNullOrWhiteSpace(s) || !DateOnly.TryParse(s, out var d) ? null : d;

    private static string? Serialize(List<string> list) =>
        list.Count == 0 ? null : JsonSerializer.Serialize(list);

    private static DocumentType MapDocumentType(EmailAttachmentDocumentType t) => t switch
    {
        EmailAttachmentDocumentType.Acord125 => DocumentType.Application,
        EmailAttachmentDocumentType.Acord126 => DocumentType.SupplementalApplication,
        EmailAttachmentDocumentType.LossRun => DocumentType.LossRuns,
        EmailAttachmentDocumentType.DecPage => DocumentType.DeclarationsPage,
        EmailAttachmentDocumentType.ScheduleOfValues => DocumentType.StatementOfValues,
        EmailAttachmentDocumentType.SignedApplication => DocumentType.SignedApplication,
        EmailAttachmentDocumentType.Acord127 => DocumentType.Application,
        EmailAttachmentDocumentType.Acord146 => DocumentType.StatementOfValues,
        EmailAttachmentDocumentType.Mvr => DocumentType.Mvr,
        _ => DocumentType.Other,
    };

    private static Insured BuildInsuredFromSender(string? fromName, string fromAddress, Guid createdById)
    {
        var parts = (fromName ?? fromAddress).Trim().Split(' ', 2);
        return new Insured
        {
            InsuredType = InsuredType.Individual,
            FirstName = parts[0],
            LastName = parts.Length > 1 ? parts[1] : string.Empty,
            Email = fromAddress,
            AddressLine1 = "Unknown",
            City = "Unknown",
            State = "XX",
            ZipCode = "00000",
            IsActive = true,
            CreatedById = createdById,
        };
    }

    private static InboundEmailListItemDto MapToListItemDto(InboundEmail e) => new()
    {
        Id = e.Id,
        FromAddress = e.FromAddress,
        FromName = e.FromName,
        Subject = e.Subject,
        ReceivedAt = e.ReceivedAt,
        IsProcessed = e.IsProcessed,
        LinkedSubmissionId = e.LinkedSubmissionId,
        AttachmentCount = e.Attachments?.Count ?? 0,
        CreatedAt = e.CreatedAt,
    };

    private static InboundEmailDto MapToDto(InboundEmail e) => new()
    {
        Id = e.Id,
        FromAddress = e.FromAddress,
        FromName = e.FromName,
        Subject = e.Subject,
        BodyText = e.BodyText,
        ReceivedAt = e.ReceivedAt,
        ProcessedAt = e.ProcessedAt,
        IsProcessed = e.IsProcessed,
        LinkedSubmissionId = e.LinkedSubmissionId,
        CreatedAt = e.CreatedAt,
        Attachments = e.Attachments?.Select(a => new EmailAttachmentDto
        {
            Id = a.Id,
            FileName = a.FileName,
            ContentType = a.ContentType,
            BlobUrl = a.BlobUrl,
            FileSizeBytes = a.FileSizeBytes,
            DocumentType = a.DocumentType,
        }).ToList() ?? [],
    };
}


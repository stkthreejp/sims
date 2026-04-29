using System.Text.Json;
using IMS.Application.Common;
using IMS.Application.DTOs.Gemini;
using IMS.Application.DTOs.InboundEmails;
using IMS.Application.DTOs.Submissions;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IMS.Application.Services;

public class InboundEmailService : IInboundEmailService
{
    private readonly IServiceProvider _sp;
    private readonly IGeminiExtractionService _gemini;
    private readonly ILogger<InboundEmailService> _logger;

    private Microsoft.EntityFrameworkCore.DbContext Db =>
        (Microsoft.EntityFrameworkCore.DbContext)_sp.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public InboundEmailService(
        IServiceProvider sp,
        IGeminiExtractionService gemini,
        ILogger<InboundEmailService> logger)
    {
        _sp = sp;
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<IEnumerable<InboundEmailListItemDto>> GetUnprocessedAsync()
    {
        var emails = await Db.Set<InboundEmail>()
            .Include(e => e.Attachments.Where(a => !a.IsDeleted))
            .Where(e => !e.IsDeleted && !e.IsProcessed)
            .OrderByDescending(e => e.ReceivedAt)
            .ToListAsync();

        return emails.Select(MapToListItemDto);
    }

    public async Task<Result<InboundEmailDto>> GetByIdAsync(Guid id)
    {
        var email = await Db.Set<InboundEmail>()
            .Include(e => e.Attachments.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

        return email == null
            ? Result<InboundEmailDto>.Failure("NOT_FOUND", "Inbound email not found.")
            : Result<InboundEmailDto>.Success(MapToDto(email));
    }

    public async Task<Result<CreateSubmissionFromEmailResponse>> CreateSubmissionFromEmailAsync(
        Guid emailId, Guid currentUserId, Guid? insuredId = null, List<Guid>? attachmentIds = null, string? lineOfBusiness = null)
    {
        var email = await Db.Set<InboundEmail>()
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
            var exists = await Db.Set<Insured>().AnyAsync(i => i.Id == insuredId.Value && !i.IsDeleted);
            if (!exists)
                return Result<CreateSubmissionFromEmailResponse>.Failure("INSURED_NOT_FOUND", "Selected insured not found.");
            resolvedInsuredId = insuredId.Value;
        }
        else
        {
            var newInsured = BuildInsuredFromSender(email.FromName, email.FromAddress, currentUserId);
            Db.Set<Insured>().Add(newInsured);
            await Db.SaveChangesAsync();
            resolvedInsuredId = newInsured.Id;
        }

        // Generate submission number
        var year = DateTime.UtcNow.Year;
        var prefix = $"SUB-{year}-";
        var count = await Db.Set<Submission>()
            .IgnoreQueryFilters()
            .CountAsync(s => s.SubmissionNumber.StartsWith(prefix));

        var submission = new Submission
        {
            SubmissionNumber = $"{prefix}{(count + 1):D4}",
            InsuredId = resolvedInsuredId,
            UnderwriterId = currentUserId,
            CreatedById = currentUserId,
            Status = SubmissionStatus.New,
        };
        Db.Set<Submission>().Add(submission);

        // Filter to selected attachments only (if caller specified a subset)
        var attachmentsToProcess = attachmentIds?.Count > 0
            ? email.Attachments.Where(a => attachmentIds.Contains(a.Id)).ToList()
            : email.Attachments.ToList();

        // Copy email attachments to submission
        foreach (var emailAttachment in attachmentsToProcess)
        {
            Db.Set<Attachment>().Add(new Attachment
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

        await Db.SaveChangesAsync();

        // Run Gemini extraction â€” never fails the submission creation
        var extractionStatus = "NotApplicable";
        try
        {
            var extraction = await _gemini.ExtractFromAttachmentsAsync(attachmentsToProcess, lineOfBusiness);
            if (extraction != null)
            {
                await ApplyExtractionAsync(submission.Id, resolvedInsuredId, extraction, currentUserId);
                extractionStatus = "Completed";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini extraction failed for submission {SubmissionId}", submission.Id);
            extractionStatus = "Failed";
        }

        await Db.Entry(submission).Reference(s => s.Insured).LoadAsync();
        await Db.Entry(submission).Reference(s => s.Underwriter).LoadAsync();

        var dto = new SubmissionDto
        {
            Id = submission.Id,
            SubmissionNumber = submission.SubmissionNumber,
            InsuredId = resolvedInsuredId,
            InsuredName = submission.Insured?.DisplayName ?? "",
            UnderwriterId = currentUserId,
            UnderwriterName = submission.Underwriter?.FullName ?? "",
            Status = submission.Status,
            CreatedAt = submission.CreatedAt,
        };

        return Result<CreateSubmissionFromEmailResponse>.Success(new CreateSubmissionFromEmailResponse
        {
            Submission = dto,
            ExtractionStatus = extractionStatus,
            EmailId = emailId,
        });
    }

    public async Task<Result<string>> ReExtractAsync(Guid emailId, Guid currentUserId)
    {
        var email = await Db.Set<InboundEmail>()
            .Include(e => e.Attachments.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(e => e.Id == emailId && !e.IsDeleted);

        if (email == null)
            return Result<string>.Failure("NOT_FOUND", "Inbound email not found.");

        if (!email.LinkedSubmissionId.HasValue)
            return Result<string>.Failure("NO_SUBMISSION", "No submission is linked to this email.");

        var submissionId = email.LinkedSubmissionId.Value;

        GeminiExtractionResult? extraction;
        try
        {
            extraction = await _gemini.ExtractFromAttachmentsAsync(email.Attachments, lineOfBusinessHint: null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Re-extract failed for email {EmailId}", emailId);
            return Result<string>.Failure("EXTRACTION_FAILED", "AI extraction failed. Please fill in the data manually.");
        }

        if (extraction == null)
            return Result<string>.Failure("NO_ELIGIBLE_ATTACHMENTS", "No extractable attachments found on this email.");

        var submission = await Db.Set<Submission>().FindAsync(submissionId);
        if (submission == null)
            return Result<string>.Failure("SUBMISSION_NOT_FOUND", "Linked submission not found.");

        await ApplyExtractionAsync(submissionId, submission.InsuredId, extraction, currentUserId, replaceExisting: true);

        return Result<string>.Success("Completed");
    }

    // -------------------------------------------------------------------------
    // Extraction helpers
    // -------------------------------------------------------------------------

    private async Task ApplyExtractionAsync(
        Guid submissionId, Guid insuredId, GeminiExtractionResult data, Guid userId, bool replaceExisting = false)
    {
        if (!string.IsNullOrWhiteSpace(data.DescriptionOfOperations))
        {
            var sub = await Db.Set<Submission>().FindAsync(submissionId);
            if (sub != null && (replaceExisting || string.IsNullOrWhiteSpace(sub.DescriptionOfOperations)))
                sub.DescriptionOfOperations = data.DescriptionOfOperations;
        }

        var insured = await Db.Set<Insured>().FindAsync(insuredId);
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
        var hasDrivers = await Db.Set<SubmissionDriver>().AnyAsync(d => d.SubmissionId == submissionId && !d.IsDeleted);
        if (!hasDrivers || replaceExisting)
        {
            if (replaceExisting)
                Db.Set<SubmissionDriver>().RemoveRange(
                    await Db.Set<SubmissionDriver>().Where(d => d.SubmissionId == submissionId).ToListAsync());

            for (var i = 0; i < data.Drivers.Count; i++)
            {
                var d = data.Drivers[i];
                if (string.IsNullOrWhiteSpace(d.Name)) continue;
                Db.Set<SubmissionDriver>().Add(new SubmissionDriver
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
        var hasVehicles = await Db.Set<SubmissionVehicle>().AnyAsync(v => v.SubmissionId == submissionId && !v.IsDeleted);
        if (!hasVehicles || replaceExisting)
        {
            if (replaceExisting)
                Db.Set<SubmissionVehicle>().RemoveRange(
                    await Db.Set<SubmissionVehicle>().Where(v => v.SubmissionId == submissionId).ToListAsync());

            for (var i = 0; i < data.Vehicles.Count; i++)
            {
                var v = data.Vehicles[i];
                Db.Set<SubmissionVehicle>().Add(new SubmissionVehicle
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
        var hasLocations = await Db.Set<SubmissionLocation>().AnyAsync(l => l.SubmissionId == submissionId && !l.IsDeleted);
        if (!hasLocations || replaceExisting)
        {
            if (replaceExisting)
                Db.Set<SubmissionLocation>().RemoveRange(
                    await Db.Set<SubmissionLocation>().Where(l => l.SubmissionId == submissionId).ToListAsync());

            for (var i = 0; i < data.Locations.Count; i++)
            {
                var l = data.Locations[i];
                if (string.IsNullOrWhiteSpace(l.Address)) continue;
                Db.Set<SubmissionLocation>().Add(new SubmissionLocation
                {
                    SubmissionId = submissionId,
                    LocationNumber = l.LocationNumber ?? (i + 1),
                    Address = l.Address,
                    ZipCode = l.ZipCode
                });
            }
        }

        // Prior carriers
        var hasCarriers = await Db.Set<SubmissionPriorCarrier>().AnyAsync(p => p.SubmissionId == submissionId && !p.IsDeleted);
        if (!hasCarriers || replaceExisting)
        {
            if (replaceExisting)
                Db.Set<SubmissionPriorCarrier>().RemoveRange(
                    await Db.Set<SubmissionPriorCarrier>().Where(p => p.SubmissionId == submissionId).ToListAsync());

            foreach (var pc in data.PriorCarriers)
            {
                if (string.IsNullOrWhiteSpace(pc.CarrierName)) continue;
                Db.Set<SubmissionPriorCarrier>().Add(new SubmissionPriorCarrier
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
            var existing = await Db.Set<SubmissionSupplemental>()
                .FirstOrDefaultAsync(s => s.SubmissionId == submissionId && !s.IsDeleted);

            if (existing == null)
            {
                Db.Set<SubmissionSupplemental>().Add(new SubmissionSupplemental
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
            var existing = await Db.Set<SubmissionGLCoverages>()
                .FirstOrDefaultAsync(g => g.SubmissionId == submissionId && !g.IsDeleted);

            if (existing == null)
            {
                Db.Set<SubmissionGLCoverages>().Add(new SubmissionGLCoverages
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
        var hasGLClass = await Db.Set<SubmissionGLClassification>().AnyAsync(c => c.SubmissionId == submissionId && !c.IsDeleted);
        if (!hasGLClass || replaceExisting)
        {
            if (replaceExisting)
                Db.Set<SubmissionGLClassification>().RemoveRange(
                    await Db.Set<SubmissionGLClassification>().Where(c => c.SubmissionId == submissionId).ToListAsync());

            for (var i = 0; i < data.GLClassifications.Count; i++)
            {
                var gc = data.GLClassifications[i];
                if (string.IsNullOrWhiteSpace(gc.ClassCode)) continue;
                Db.Set<SubmissionGLClassification>().Add(new SubmissionGLClassification
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
            var existing = await Db.Set<SubmissionIMCoverages>()
                .FirstOrDefaultAsync(m => m.SubmissionId == submissionId && !m.IsDeleted);

            if (existing == null)
            {
                Db.Set<SubmissionIMCoverages>().Add(new SubmissionIMCoverages
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
        var hasEquipment = await Db.Set<SubmissionEquipment>().AnyAsync(e => e.SubmissionId == submissionId && !e.IsDeleted);
        if (!hasEquipment || replaceExisting)
        {
            if (replaceExisting)
                Db.Set<SubmissionEquipment>().RemoveRange(
                    await Db.Set<SubmissionEquipment>().Where(e => e.SubmissionId == submissionId).ToListAsync());

            for (var i = 0; i < data.Equipment.Count; i++)
            {
                var eq = data.Equipment[i];
                Db.Set<SubmissionEquipment>().Add(new SubmissionEquipment
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

        await Db.SaveChangesAsync();
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


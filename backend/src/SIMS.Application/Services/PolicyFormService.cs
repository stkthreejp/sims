using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SIMS.Application.Common;
using SIMS.Application.DTOs.PolicyForms;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class PolicyFormService : IPolicyFormService
{
    private readonly IServiceProvider _sp;
    private readonly IBlobStorageService _blob;
    private readonly IFileScanService _fileScan;
    private readonly long _maxFileSize;
    private readonly HashSet<string> _allowedExtensions;
    private readonly Dictionary<string, string> _contentTypesByExtension;

    public PolicyFormService(IServiceProvider sp, IBlobStorageService blob, IFileScanService fileScan, IConfiguration config)
    {
        _sp = sp;
        _blob = blob;
        _fileScan = fileScan;
        _maxFileSize = long.TryParse(config["Storage:MaxFileSizeBytes"], out var parsed) ? parsed : 52_428_800L;
        _allowedExtensions = [".pdf", ".doc", ".docx", ".html", ".htm"];
        _contentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".html"] = "text/html",
            [".htm"] = "text/html",
        };
    }

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public async Task<IReadOnlyList<PolicyFormTemplateDto>> GetTemplatesAsync(bool includeInactive = false)
    {
        var q = Db.Set<PolicyFormTemplate>()
            .Include(f => f.FieldMappings)
            .AsQueryable();

        if (!includeInactive)
            q = q.Where(f => f.IsActive);

        var forms = await q
            .OrderBy(f => f.FormNumber)
            .ThenBy(f => f.EditionDate)
            .ToListAsync();

        return forms.Select(MapTemplate).ToList();
    }

    public async Task<Result<PolicyFormTemplateDto>> GetTemplateAsync(Guid id)
    {
        var form = await Db.Set<PolicyFormTemplate>()
            .Include(f => f.FieldMappings)
            .FirstOrDefaultAsync(f => f.Id == id);

        return form == null
            ? Result<PolicyFormTemplateDto>.Failure("NOT_FOUND", "Policy form template not found.")
            : Result<PolicyFormTemplateDto>.Success(MapTemplate(form));
    }

    public async Task<Result<PolicyFormTemplateDto>> CreateTemplateAsync(PolicyFormTemplateUpsertDto dto)
    {
        var validation = ValidateTemplate(dto);
        if (validation != null) return Result<PolicyFormTemplateDto>.Failure("VALIDATION", validation);

        var form = new PolicyFormTemplate();
        ApplyTemplate(form, dto);
        Db.Set<PolicyFormTemplate>().Add(form);
        await Db.SaveChangesAsync();
        return await GetTemplateAsync(form.Id);
    }

    public async Task<Result<PolicyFormTemplateDto>> UpdateTemplateAsync(Guid id, PolicyFormTemplateUpsertDto dto)
    {
        var validation = ValidateTemplate(dto);
        if (validation != null) return Result<PolicyFormTemplateDto>.Failure("VALIDATION", validation);

        var form = await Db.Set<PolicyFormTemplate>().FindAsync(id);
        if (form == null)
            return Result<PolicyFormTemplateDto>.Failure("NOT_FOUND", "Policy form template not found.");

        ApplyTemplate(form, dto);
        await Db.SaveChangesAsync();
        return await GetTemplateAsync(form.Id);
    }

    public async Task<Result<PolicyFormTemplateDto>> UploadTemplateFileAsync(Guid id, IFormFile file)
    {
        var form = await Db.Set<PolicyFormTemplate>().FindAsync(id);
        if (form == null)
            return Result<PolicyFormTemplateDto>.Failure("NOT_FOUND", "Policy form template not found.");

        if (file.Length == 0)
            return Result<PolicyFormTemplateDto>.Failure("EMPTY_FILE", "File is empty.");

        if (file.Length > _maxFileSize)
            return Result<PolicyFormTemplateDto>.Failure("FILE_TOO_LARGE", $"File exceeds the {_maxFileSize / 1024 / 1024}MB limit.");

        var safeFileName = System.Text.RegularExpressions.Regex.Replace(
            Path.GetFileName(file.FileName), @"[^\w.\-() ]", "_");
        if (string.IsNullOrWhiteSpace(safeFileName))
            return Result<PolicyFormTemplateDto>.Failure("UNSUPPORTED_FILE_TYPE", "File name is required.");

        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension) || !_contentTypesByExtension.TryGetValue(extension, out var contentType))
            return Result<PolicyFormTemplateDto>.Failure("UNSUPPORTED_FILE_TYPE", "Only PDF, DOC, DOCX, and HTML policy forms are allowed.");

        var scan = await _fileScan.ScanAsync(file);
        if (!scan.IsAllowed)
            return Result<PolicyFormTemplateDto>.Failure(scan.ErrorCode ?? "FILE_SCAN_FAILED", scan.ErrorMessage ?? "The uploaded file could not be scanned.");

        if (!string.IsNullOrWhiteSpace(form.StoragePath))
            await _blob.DeleteAsync(form.StoragePath);

        string blobPath;
        using (var stream = file.OpenReadStream())
            blobPath = await _blob.UploadAsync(stream, safeFileName, contentType);

        form.FileName = safeFileName;
        form.ContentType = contentType;
        form.StoragePath = blobPath;
        form.IsFillable = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) && form.IsFillable;

        await Db.SaveChangesAsync();
        return await GetTemplateAsync(form.Id);
    }

    public async Task<Result<string>> GetTemplateDownloadUrlAsync(Guid id)
    {
        var form = await Db.Set<PolicyFormTemplate>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (form == null)
            return Result<string>.Failure("NOT_FOUND", "Policy form template not found.");
        if (string.IsNullOrWhiteSpace(form.StoragePath) || string.IsNullOrWhiteSpace(form.FileName))
            return Result<string>.Failure("NO_FILE", "This policy form does not have an uploaded file.");

        return Result<string>.Success(await _blob.GetDownloadUrlAsync(form.StoragePath, form.FileName));
    }

    public async Task<Result> DeleteTemplateAsync(Guid id)
    {
        var form = await Db.Set<PolicyFormTemplate>().FindAsync(id);
        if (form == null) return Result.Failure("NOT_FOUND", "Policy form template not found.");

        var inUse = await Db.Set<PolicyPackageForm>().AnyAsync(p => p.PolicyFormTemplateId == id);
        if (inUse) return Result.Failure("IN_USE", "This form is used in one or more policy packages.");

        if (!string.IsNullOrWhiteSpace(form.StoragePath))
            await _blob.DeleteAsync(form.StoragePath);

        form.IsDeleted = true;
        form.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<PolicyFormFieldMappingDto>>> ReplaceMappingsAsync(Guid templateId, IReadOnlyList<PolicyFormFieldMappingUpsertDto> mappings)
    {
        var form = await Db.Set<PolicyFormTemplate>()
            .Include(f => f.FieldMappings)
            .FirstOrDefaultAsync(f => f.Id == templateId);
        if (form == null)
            return Result<IReadOnlyList<PolicyFormFieldMappingDto>>.Failure("NOT_FOUND", "Policy form template not found.");

        if (mappings.Any(m => string.IsNullOrWhiteSpace(m.PdfFieldName) || string.IsNullOrWhiteSpace(m.DataPath)))
            return Result<IReadOnlyList<PolicyFormFieldMappingDto>>.Failure("VALIDATION", "PDF field and data path are required for each mapping.");

        Db.Set<PolicyFormFieldMapping>().RemoveRange(form.FieldMappings);
        foreach (var mapping in mappings)
        {
            form.FieldMappings.Add(new PolicyFormFieldMapping
            {
                PolicyFormTemplateId = templateId,
                PdfFieldName = mapping.PdfFieldName.Trim(),
                DataPath = mapping.DataPath.Trim(),
                Format = mapping.Format?.Trim(),
            });
        }

        await Db.SaveChangesAsync();
        return Result<IReadOnlyList<PolicyFormFieldMappingDto>>.Success(form.FieldMappings.Select(MapMapping).ToList());
    }

    public Task<IReadOnlyList<DocumentTagDto>> GetDocumentTagsAsync()
        => Task.FromResult<IReadOnlyList<DocumentTagDto>>(DocumentTags);

    public async Task<IReadOnlyList<PolicyPackageConfigurationDto>> GetPackagesAsync(Guid? programConfigurationId = null, Guid? carrierId = null, PolicyLineOfBusiness? lineOfBusiness = null, string? state = null, bool includeInactive = false)
    {
        var q = Db.Set<PolicyPackageConfiguration>()
            .Include(p => p.ProgramConfiguration)
            .Include(p => p.Carrier)
            .Include(p => p.Forms).ThenInclude(f => f.PolicyFormTemplate)
            .AsQueryable();

        if (programConfigurationId.HasValue) q = q.Where(p => p.ProgramConfigurationId == programConfigurationId.Value);
        if (carrierId.HasValue) q = q.Where(p => p.CarrierId == carrierId.Value);
        if (lineOfBusiness.HasValue) q = q.Where(p => p.LineOfBusiness == lineOfBusiness.Value);
        if (!string.IsNullOrWhiteSpace(state)) q = q.Where(p => p.State == state.Trim().ToUpper());
        if (!includeInactive) q = q.Where(p => p.IsActive);

        var packages = await q
            .OrderBy(p => p.ProgramConfiguration == null ? string.Empty : p.ProgramConfiguration.Name)
            .ThenBy(p => p.Carrier.Name)
            .ThenBy(p => p.LineOfBusiness)
            .ThenBy(p => p.State)
            .ToListAsync();

        return packages.Select(MapPackage).ToList();
    }

    public async Task<Result<PolicyPackageConfigurationDto>> GetPackageAsync(Guid id)
    {
        var package = await Db.Set<PolicyPackageConfiguration>()
            .Include(p => p.ProgramConfiguration)
            .Include(p => p.Carrier)
            .Include(p => p.Forms).ThenInclude(f => f.PolicyFormTemplate)
            .FirstOrDefaultAsync(p => p.Id == id);

        return package == null
            ? Result<PolicyPackageConfigurationDto>.Failure("NOT_FOUND", "Policy package configuration not found.")
            : Result<PolicyPackageConfigurationDto>.Success(MapPackage(package));
    }

    public async Task<Result<PolicyPackageConfigurationDto>> CreatePackageAsync(PolicyPackageConfigurationUpsertDto dto)
    {
        var validation = await ValidatePackageAsync(dto);
        if (validation != null) return Result<PolicyPackageConfigurationDto>.Failure("VALIDATION", validation);

        var package = new PolicyPackageConfiguration();
        ApplyPackage(package, dto);
        Db.Set<PolicyPackageConfiguration>().Add(package);
        await Db.SaveChangesAsync();
        return await GetPackageAsync(package.Id);
    }

    public async Task<Result<PolicyPackageConfigurationDto>> UpdatePackageAsync(Guid id, PolicyPackageConfigurationUpsertDto dto)
    {
        var validation = await ValidatePackageAsync(dto);
        if (validation != null) return Result<PolicyPackageConfigurationDto>.Failure("VALIDATION", validation);

        var package = await Db.Set<PolicyPackageConfiguration>().FindAsync(id);
        if (package == null)
            return Result<PolicyPackageConfigurationDto>.Failure("NOT_FOUND", "Policy package configuration not found.");

        ApplyPackage(package, dto);
        await Db.SaveChangesAsync();
        return await GetPackageAsync(package.Id);
    }

    public async Task<Result> DeletePackageAsync(Guid id)
    {
        var package = await Db.Set<PolicyPackageConfiguration>().FindAsync(id);
        if (package == null) return Result.Failure("NOT_FOUND", "Policy package configuration not found.");

        package.IsDeleted = true;
        package.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<PolicyPackageConfigurationDto>> ReplacePackageFormsAsync(Guid packageId, IReadOnlyList<PolicyPackageFormUpsertDto> forms)
    {
        var packageExists = await Db.Set<PolicyPackageConfiguration>().AnyAsync(p => p.Id == packageId);
        if (!packageExists)
            return Result<PolicyPackageConfigurationDto>.Failure("NOT_FOUND", "Policy package configuration not found.");

        if (forms.Any(f => f.PolicyFormTemplateId == Guid.Empty || f.SequenceOrder <= 0))
            return Result<PolicyPackageConfigurationDto>.Failure("VALIDATION", "Each package form needs a template and sequence.");

        var templateIds = forms.Select(f => f.PolicyFormTemplateId).Distinct().ToList();
        var existingTemplateCount = await Db.Set<PolicyFormTemplate>().CountAsync(f => templateIds.Contains(f.Id));
        if (existingTemplateCount != templateIds.Count)
            return Result<PolicyPackageConfigurationDto>.Failure("VALIDATION", "One or more selected forms were not found.");

        foreach (var form in forms.Where(f => f.FormType == PolicyFormType.Conditional && !string.IsNullOrWhiteSpace(f.TriggerConditionJson)))
        {
            if (!TryNormalizeJson(form.TriggerConditionJson, out _))
                return Result<PolicyPackageConfigurationDto>.Failure("VALIDATION", "Conditional form rules must be valid trigger rules.");
        }

        await using var transaction = await Db.Database.BeginTransactionAsync();
        await Db.Set<PolicyPackageForm>()
            .Where(f => f.PolicyPackageConfigurationId == packageId)
            .ExecuteDeleteAsync();

        var packageForms = forms.OrderBy(f => f.SequenceOrder).Select(form =>
        {
            var triggerConditionJson = form.FormType == PolicyFormType.Conditional && TryNormalizeJson(form.TriggerConditionJson, out var normalized)
                ? normalized
                : null;

            return new PolicyPackageForm
            {
                PolicyPackageConfigurationId = packageId,
                PolicyFormTemplateId = form.PolicyFormTemplateId,
                SequenceOrder = form.SequenceOrder,
                FormType = form.FormType,
                TriggerConditionJson = triggerConditionJson,
                Notes = form.Notes?.Trim(),
            };
        }).ToList();

        Db.Set<PolicyPackageForm>().AddRange(packageForms);
        try
        {
            await Db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            return Result<PolicyPackageConfigurationDto>.Failure("SAVE_FAILED", ex.InnerException?.Message ?? ex.Message);
        }

        return await GetPackageAsync(packageId);
    }

    private static bool TryNormalizeJson(string? json, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(json);
            normalized = doc.RootElement.GetRawText();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ValidateTemplate(PolicyFormTemplateUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FormNumber)) return "Form number is required.";
        if (string.IsNullOrWhiteSpace(dto.Name)) return "Form name is required.";
        return null;
    }

    private async Task<string?> ValidatePackageAsync(PolicyPackageConfigurationUpsertDto dto)
    {
        if (dto.CarrierId == Guid.Empty) return "Carrier is required.";
        if (string.IsNullOrWhiteSpace(dto.State) || dto.State.Trim().Length != 2) return "State must be a two-letter code.";
        if (string.IsNullOrWhiteSpace(dto.Name)) return "Package name is required.";
        if (!await Db.Set<Carrier>().AnyAsync(c => c.Id == dto.CarrierId))
            return "Carrier not found.";
        if (dto.ProgramConfigurationId.HasValue && !await Db.Set<ProgramConfiguration>().AnyAsync(p => p.Id == dto.ProgramConfigurationId.Value))
            return "Program not found.";
        return null;
    }

    private static void ApplyTemplate(PolicyFormTemplate form, PolicyFormTemplateUpsertDto dto)
    {
        form.FormNumber = dto.FormNumber.Trim();
        form.Name = dto.Name.Trim();
        form.EditionDate = dto.EditionDate?.Trim();
        form.DocumentType = dto.DocumentType;
        form.FileName = dto.FileName?.Trim();
        form.ContentType = dto.ContentType?.Trim();
        form.StoragePath = dto.StoragePath?.Trim();
        form.IsFillable = dto.IsFillable;
        form.IsActive = dto.IsActive;
        form.Notes = dto.Notes?.Trim();
    }

    private static void ApplyPackage(PolicyPackageConfiguration package, PolicyPackageConfigurationUpsertDto dto)
    {
        package.CarrierId = dto.CarrierId;
        package.ProgramConfigurationId = dto.ProgramConfigurationId;
        package.LineOfBusiness = dto.LineOfBusiness;
        package.State = dto.State.Trim().ToUpper();
        package.Name = dto.Name.Trim();
        package.IsActive = dto.IsActive;
    }

    private static PolicyFormTemplateDto MapTemplate(PolicyFormTemplate f) => new()
    {
        Id = f.Id,
        FormNumber = f.FormNumber,
        Name = f.Name,
        EditionDate = f.EditionDate,
        DocumentType = f.DocumentType,
        FileName = f.FileName,
        ContentType = f.ContentType,
        StoragePath = f.StoragePath,
        IsFillable = f.IsFillable,
        IsActive = f.IsActive,
        Notes = f.Notes,
        FieldMappings = f.FieldMappings.OrderBy(m => m.PdfFieldName).Select(MapMapping).ToList(),
        UpdatedAt = f.UpdatedAt,
    };

    private static PolicyFormFieldMappingDto MapMapping(PolicyFormFieldMapping m) => new()
    {
        Id = m.Id,
        PdfFieldName = m.PdfFieldName,
        DataPath = m.DataPath,
        Format = m.Format,
    };

    private static readonly IReadOnlyList<DocumentTagDto> DocumentTags =
    [
        Tag("Insured.DisplayName", "Named insured", "Insured"),
        Tag("Insured.Name", "Named insured", "Insured"),
        Tag("Insured.CompanyName", "Company name", "Insured"),
        Tag("Insured.Dba", "DBA", "Insured"),
        Tag("Insured.FirstName", "First name", "Insured"),
        Tag("Insured.LastName", "Last name", "Insured"),
        Tag("Insured.AddressLine1", "Address line 1", "Insured"),
        Tag("Insured.AddressLine2", "Address line 2", "Insured"),
        Tag("Insured.City", "City", "Insured"),
        Tag("Insured.State", "State", "Insured"),
        Tag("Insured.ZipCode", "ZIP code", "Insured"),
        Tag("Insured.FullAddress", "Full address", "Insured"),
        Tag("Insured.Email", "Email", "Insured"),
        Tag("Insured.Phone", "Phone", "Insured"),

        Tag("Submission.SubmissionNumber", "Submission number", "Submission"),

        Tag("Quote.QuoteNumber", "Quote number", "Quote"),
        Tag("Quote.PolicyNumber", "Quoted policy number", "Quote"),
        Tag("Quote.EffectiveDate", "Effective date", "Quote", "Date", "MM/dd/yyyy"),
        Tag("Quote.ExpirationDate", "Expiration date", "Quote", "Date", "MM/dd/yyyy"),
        Tag("Quote.PremiumAmount", "Premium", "Quote", "Currency", "currency"),
        Tag("Quote.TaxesAndFees", "Taxes and fees", "Quote", "Currency", "currency"),
        Tag("Quote.TotalPremium", "Total premium", "Quote", "Currency", "currency"),
        Tag("Quote.CoverageDescription", "Coverage description", "Quote"),
        Tag("Quote.Deductible", "Deductible", "Quote", "Currency", "currency"),
        Tag("Quote.Limit", "Limit", "Quote", "Currency", "currency"),
        Tag("Quote.UninsuredMotoristLimit", "Uninsured motorist limit", "Quote", "Currency", "currency"),
        Tag("Quote.MedicalPaymentsLimit", "Medical payments limit", "Quote", "Currency", "currency"),
        Tag("Quote.LineOfBusiness", "Line of business", "Quote"),

        Tag("Policy.PolicyNumber", "Policy number", "Policy"),
        Tag("Policy.EffectiveDate", "Effective date", "Policy", "Date", "MM/dd/yyyy"),
        Tag("Policy.ExpirationDate", "Expiration date", "Policy", "Date", "MM/dd/yyyy"),
        Tag("Policy.BoundDate", "Bound date", "Policy", "Date", "MM/dd/yyyy"),
        Tag("Policy.IssuedDate", "Issued date", "Policy", "Date", "MM/dd/yyyy"),
        Tag("Policy.PremiumAmount", "Premium", "Policy", "Currency", "currency"),
        Tag("Policy.TaxesAndFees", "Taxes and fees", "Policy", "Currency", "currency"),
        Tag("Policy.TotalPremium", "Total premium", "Policy", "Currency", "currency"),
        Tag("Policy.LineOfBusiness", "Line of business", "Policy"),

        Tag("Carrier.Name", "Carrier name", "Carrier"),
        Tag("Carrier.Naic", "NAIC", "Carrier"),

        Tag("ItemNumber", "Item number", "Equipment", "Number", "number", "Equipment"),
        Tag("Description", "Description", "Equipment", repeatBlock: "Equipment"),
        Tag("Year", "Year", "Equipment", "Number", "number", "Equipment"),
        Tag("Make", "Make", "Equipment", repeatBlock: "Equipment"),
        Tag("Model", "Model", "Equipment", repeatBlock: "Equipment"),
        Tag("SerialNumber", "Serial number", "Equipment", repeatBlock: "Equipment"),
        Tag("Value", "Value", "Equipment", "Currency", "currency", "Equipment"),
        Tag("Limit", "Limit", "Equipment", "Currency", "currency", "Equipment"),
        Tag("Deductible", "Deductible", "Equipment", "Currency", "currency", "Equipment"),
        Tag("Location", "Location", "Equipment", repeatBlock: "Equipment"),
        Tag("Territory", "Territory", "Equipment", repeatBlock: "Equipment"),

        Tag("Name", "Name", "Additional Interests", repeatBlock: "AdditionalInterests"),
        Tag("Address", "Address", "Additional Interests", repeatBlock: "AdditionalInterests"),
        Tag("Types", "Interest types", "Additional Interests", repeatBlock: "AdditionalInterests"),
        Tag("LoanNumber", "Loan or contract number", "Additional Interests", repeatBlock: "AdditionalInterests"),

        Tag("FormNumber", "Form number", "Forms", repeatBlock: "PolicyForms"),
        Tag("FormName", "Form name", "Forms", repeatBlock: "PolicyForms"),
        Tag("EditionDate", "Edition date", "Forms", repeatBlock: "PolicyForms"),
        Tag("Status", "Included or excluded", "Forms", repeatBlock: "PolicyForms"),
    ];

    private static DocumentTagDto Tag(
        string tag,
        string label,
        string category,
        string dataType = "Text",
        string? defaultFormat = null,
        string? repeatBlock = null)
        => new()
        {
            Tag = tag,
            Label = label,
            Category = category,
            DataType = dataType,
            DefaultFormat = defaultFormat,
            IsRepeatable = repeatBlock != null,
            RepeatBlock = repeatBlock,
        };

    private static PolicyPackageConfigurationDto MapPackage(PolicyPackageConfiguration p) => new()
    {
        Id = p.Id,
        ProgramConfigurationId = p.ProgramConfigurationId,
        ProgramName = p.ProgramConfiguration?.Name,
        CarrierId = p.CarrierId,
        CarrierName = p.Carrier?.Name ?? string.Empty,
        LineOfBusiness = p.LineOfBusiness,
        State = p.State,
        Name = p.Name,
        IsActive = p.IsActive,
        Forms = p.Forms.OrderBy(f => f.SequenceOrder).Select(MapPackageForm).ToList(),
        UpdatedAt = p.UpdatedAt,
    };

    private static PolicyPackageFormDto MapPackageForm(PolicyPackageForm f) => new()
    {
        Id = f.Id,
        PolicyFormTemplateId = f.PolicyFormTemplateId,
        FormNumber = f.PolicyFormTemplate?.FormNumber ?? string.Empty,
        FormName = f.PolicyFormTemplate?.Name ?? string.Empty,
        EditionDate = f.PolicyFormTemplate?.EditionDate,
        SequenceOrder = f.SequenceOrder,
        FormType = f.FormType,
        TriggerConditionJson = f.TriggerConditionJson,
        Notes = f.Notes,
    };
}

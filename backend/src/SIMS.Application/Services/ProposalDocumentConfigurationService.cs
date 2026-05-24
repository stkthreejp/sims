using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.ProposalDocuments;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class ProposalDocumentConfigurationService : IProposalDocumentConfigurationService
{
    private static readonly Dictionary<PolicyLineOfBusiness, string> LobLabels = new()
    {
        [PolicyLineOfBusiness.GeneralLiability] = "General Liability",
        [PolicyLineOfBusiness.InlandMarine] = "Inland Marine",
        [PolicyLineOfBusiness.AutoLiability] = "Auto Liability",
        [PolicyLineOfBusiness.AutoPhysicalDamage] = "Auto Physical Damage",
    };

    private readonly DbContext _db;

    public ProposalDocumentConfigurationService(DbContext db) => _db = db;

    public async Task<IReadOnlyList<ProposalDocumentConfigurationDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = BaseQuery();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        var rows = await query
            .OrderBy(c => c.Carrier.Name)
            .ThenBy(c => c.ProgramConfiguration == null ? string.Empty : c.ProgramConfiguration.Name)
            .ThenBy(c => c.LineOfBusiness)
            .ThenBy(c => c.State)
            .ThenBy(c => c.Role)
            .ThenBy(c => c.SequenceOrder)
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    public async Task<Result<ProposalDocumentConfigurationDto>> CreateAsync(UpsertProposalDocumentConfigurationRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, ct);
        if (validation is not null)
            return Result<ProposalDocumentConfigurationDto>.Failure(validation.Value.Code, validation.Value.Message);

        var configuration = new ProposalDocumentConfiguration();
        Apply(configuration, request);
        _db.Set<ProposalDocumentConfiguration>().Add(configuration);
        await _db.SaveChangesAsync(ct);

        return Result<ProposalDocumentConfigurationDto>.Success(await LoadDtoAsync(configuration.Id, ct));
    }

    public async Task<Result<ProposalDocumentConfigurationDto>> UpdateAsync(Guid id, UpsertProposalDocumentConfigurationRequest request, CancellationToken ct = default)
    {
        var configuration = await _db.Set<ProposalDocumentConfiguration>()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (configuration is null)
            return Result<ProposalDocumentConfigurationDto>.Failure("NOT_FOUND", "Proposal document configuration not found.");

        var validation = await ValidateAsync(request, ct);
        if (validation is not null)
            return Result<ProposalDocumentConfigurationDto>.Failure(validation.Value.Code, validation.Value.Message);

        Apply(configuration, request);
        configuration.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<ProposalDocumentConfigurationDto>.Success(await LoadDtoAsync(configuration.Id, ct));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var configuration = await _db.Set<ProposalDocumentConfiguration>()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (configuration is null)
            return Result<bool>.Failure("NOT_FOUND", "Proposal document configuration not found.");

        configuration.IsDeleted = true;
        configuration.DeletedAt = DateTime.UtcNow;
        configuration.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<ProposalDocumentSelectionDto>> ResolveForQuoteAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await _db.Set<Quote>()
            .AsNoTracking()
            .Include(q => q.Submission)
                .ThenInclude(s => s.Insured)
            .Include(q => q.Submission)
                .ThenInclude(s => s.Locations)
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct);

        if (quote is null)
            return Result<ProposalDocumentSelectionDto>.Failure("QUOTE_NOT_FOUND", "Quote not found.");

        var state = ResolveState(quote);
        var candidates = await BaseQuery()
            .Where(c => c.IsActive
                && c.CarrierId == quote.CarrierId
                && c.LineOfBusiness == quote.LineOfBusiness
                && (c.ProgramConfigurationId == quote.ProgramId || c.ProgramConfigurationId == null)
                && (c.State == state || c.State == null)
                && (c.EffectiveDate == null || c.EffectiveDate <= quote.EffectiveDate)
                && (c.ExpirationDate == null || c.ExpirationDate > quote.EffectiveDate))
            .ToListAsync(ct);

        var proposal = candidates
            .Where(c => c.Role == ProposalDocumentRole.Proposal)
            .OrderByDescending(c => c.ProgramConfigurationId == quote.ProgramId ? 1 : 0)
            .ThenByDescending(c => c.State == state ? 1 : 0)
            .ThenByDescending(c => c.EffectiveDate ?? DateOnly.MinValue)
            .ThenByDescending(c => c.UpdatedAt)
            .FirstOrDefault();

        if (proposal is null)
            return Result<ProposalDocumentSelectionDto>.Failure("PROPOSAL_NOT_CONFIGURED", "No proposal template is configured for this quote.");

        var notices = candidates
            .Where(c => c.Role == ProposalDocumentRole.StateNotice)
            .GroupBy(c => c.DocumentTemplateId)
            .Select(group => group
                .OrderByDescending(c => c.ProgramConfigurationId == quote.ProgramId ? 1 : 0)
                .ThenByDescending(c => c.State == state ? 1 : 0)
                .ThenByDescending(c => c.EffectiveDate ?? DateOnly.MinValue)
                .ThenByDescending(c => c.UpdatedAt)
                .First())
            .OrderBy(c => c.SequenceOrder)
            .ThenBy(c => c.DocumentTemplate.Name)
            .Select(ToSelectionItem)
            .ToList();

        return Result<ProposalDocumentSelectionDto>.Success(new ProposalDocumentSelectionDto(
            quote.Id,
            state,
            ToSelectionItem(proposal),
            notices));
    }

    private IQueryable<ProposalDocumentConfiguration> BaseQuery()
        => _db.Set<ProposalDocumentConfiguration>()
            .Where(c => !c.IsDeleted)
            .Include(c => c.ProgramConfiguration)
            .Include(c => c.Carrier)
            .Include(c => c.DocumentTemplate);

    private async Task<ProposalDocumentConfigurationDto> LoadDtoAsync(Guid id, CancellationToken ct)
    {
        var configuration = await BaseQuery().SingleAsync(c => c.Id == id, ct);
        return Map(configuration);
    }

    private async Task<(string Code, string Message)?> ValidateAsync(UpsertProposalDocumentConfigurationRequest request, CancellationToken ct)
    {
        if (request.ExpirationDate.HasValue && request.EffectiveDate.HasValue && request.ExpirationDate.Value < request.EffectiveDate.Value)
            return ("INVALID_DATE_RANGE", "Expiration date cannot be before effective date.");

        var carrierExists = await _db.Set<Carrier>().AnyAsync(c => c.Id == request.CarrierId && !c.IsDeleted, ct);
        if (!carrierExists)
            return ("CARRIER_NOT_FOUND", "Carrier not found.");

        var templateExists = await _db.Set<DocumentTemplate>().AnyAsync(t => t.Id == request.DocumentTemplateId && t.IsActive && !t.IsDeleted, ct);
        if (!templateExists)
            return ("TEMPLATE_NOT_FOUND", "Document template not found or inactive.");

        if (request.ProgramConfigurationId.HasValue)
        {
            var programExists = await _db.Set<ProgramConfiguration>()
                .AnyAsync(p => p.Id == request.ProgramConfigurationId.Value && p.IsActive, ct);
            if (!programExists)
                return ("PROGRAM_NOT_FOUND", "Program not found or inactive.");
        }

        return null;
    }

    private static void Apply(ProposalDocumentConfiguration configuration, UpsertProposalDocumentConfigurationRequest request)
    {
        configuration.ProgramConfigurationId = request.ProgramConfigurationId;
        configuration.CarrierId = request.CarrierId;
        configuration.LineOfBusiness = request.LineOfBusiness;
        configuration.State = NormalizeState(request.State);
        configuration.Role = request.Role;
        configuration.DocumentTemplateId = request.DocumentTemplateId;
        configuration.SequenceOrder = request.SequenceOrder <= 0 ? 1 : request.SequenceOrder;
        configuration.IsActive = request.IsActive;
        configuration.EffectiveDate = request.EffectiveDate;
        configuration.ExpirationDate = request.ExpirationDate;
        configuration.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private static ProposalDocumentConfigurationDto Map(ProposalDocumentConfiguration c) => new(
        c.Id,
        c.ProgramConfigurationId,
        c.ProgramConfiguration?.Name,
        c.CarrierId,
        c.Carrier.Name,
        c.LineOfBusiness,
        LobLabels.GetValueOrDefault(c.LineOfBusiness, c.LineOfBusiness.ToString()),
        c.State,
        c.Role,
        c.DocumentTemplateId,
        c.DocumentTemplate.Name,
        c.SequenceOrder,
        c.IsActive,
        c.EffectiveDate,
        c.ExpirationDate,
        c.Notes);

    private static ProposalDocumentSelectionItemDto ToSelectionItem(ProposalDocumentConfiguration c) => new(
        c.Id,
        c.DocumentTemplateId,
        c.DocumentTemplate.Name,
        c.Role,
        c.State,
        c.SequenceOrder);

    private static string? ResolveState(Quote quote)
        => NormalizeState(quote.Submission.Insured.State ?? ExtractState(quote.Submission.Locations.FirstOrDefault()?.Address));

    private static string? NormalizeState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return null;
        var trimmed = state.Trim().ToUpperInvariant();
        return trimmed.Length == 2 ? trimmed : null;
    }

    private static string? ExtractState(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;
        var match = System.Text.RegularExpressions.Regex.Match(address, @"\b[A-Z]{2}\b");
        return match.Success ? match.Value : null;
    }
}

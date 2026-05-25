using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.PolicyNumbers;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class PolicyNumberAdminService : IPolicyNumberAdminService
{
    private readonly DbContext _db;

    public PolicyNumberAdminService(DbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PolicyNumberSequenceDto>> GetSequencesAsync(bool includeInactive)
    {
        var sequences = await _db.Set<PolicyNumberSequence>()
            .Where(s => includeInactive || s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
        return sequences.Select(MapSequence).ToList();
    }

    public async Task<Result<PolicyNumberSequenceDto>> CreateSequenceAsync(PolicyNumberSequenceUpsertDto dto)
    {
        var validation = ValidateSequence(dto);
        if (!validation.IsSuccess) return Result<PolicyNumberSequenceDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        var sequence = new PolicyNumberSequence();
        ApplySequence(sequence, dto);
        _db.Set<PolicyNumberSequence>().Add(sequence);
        await _db.SaveChangesAsync();
        return Result<PolicyNumberSequenceDto>.Success(MapSequence(sequence));
    }

    public async Task<Result<PolicyNumberSequenceDto>> UpdateSequenceAsync(Guid id, PolicyNumberSequenceUpsertDto dto)
    {
        var validation = ValidateSequence(dto);
        if (!validation.IsSuccess) return Result<PolicyNumberSequenceDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        var sequence = await _db.Set<PolicyNumberSequence>().FirstOrDefaultAsync(s => s.Id == id);
        if (sequence == null) return Result<PolicyNumberSequenceDto>.Failure("NOT_FOUND", "Policy number sequence not found.");

        ApplySequence(sequence, dto);
        await _db.SaveChangesAsync();
        return Result<PolicyNumberSequenceDto>.Success(MapSequence(sequence));
    }

    public async Task<Result> DeleteSequenceAsync(Guid id)
    {
        var sequence = await _db.Set<PolicyNumberSequence>().FirstOrDefaultAsync(s => s.Id == id);
        if (sequence == null) return Result.Failure("NOT_FOUND", "Policy number sequence not found.");

        sequence.IsDeleted = true;
        sequence.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<IReadOnlyList<PolicyNumberAssignmentDto>> GetAssignmentsAsync(bool includeInactive)
    {
        var assignments = await _db.Set<PolicyNumberAssignment>()
            .Include(a => a.PolicyNumberSequence)
            .Include(a => a.ProgramConfiguration)
            .Include(a => a.Carrier)
            .Where(a => includeInactive || a.IsActive)
            .OrderBy(a => a.ProgramConfiguration != null ? a.ProgramConfiguration.Name : "")
            .ThenBy(a => a.ProgramConfigurationId == null ? 0 : 1)
            .ThenBy(a => a.Carrier.Name)
            .ThenBy(a => a.LineOfBusiness)
            .ThenBy(a => a.State)
            .ToListAsync();
        return assignments.Select(MapAssignment).ToList();
    }

    public async Task<Result<PolicyNumberAssignmentDto>> CreateAssignmentAsync(PolicyNumberAssignmentUpsertDto dto)
    {
        var validation = await ValidateAssignmentAsync(dto);
        if (!validation.IsSuccess) return Result<PolicyNumberAssignmentDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        var assignment = new PolicyNumberAssignment();
        ApplyAssignment(assignment, dto);
        _db.Set<PolicyNumberAssignment>().Add(assignment);
        await _db.SaveChangesAsync();

        return Result<PolicyNumberAssignmentDto>.Success((await GetAssignmentAsync(assignment.Id))!);
    }

    public async Task<Result<PolicyNumberAssignmentDto>> UpdateAssignmentAsync(Guid id, PolicyNumberAssignmentUpsertDto dto)
    {
        var validation = await ValidateAssignmentAsync(dto);
        if (!validation.IsSuccess) return Result<PolicyNumberAssignmentDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        var assignment = await _db.Set<PolicyNumberAssignment>().FirstOrDefaultAsync(a => a.Id == id);
        if (assignment == null) return Result<PolicyNumberAssignmentDto>.Failure("NOT_FOUND", "Policy number assignment not found.");

        ApplyAssignment(assignment, dto);
        await _db.SaveChangesAsync();

        return Result<PolicyNumberAssignmentDto>.Success((await GetAssignmentAsync(assignment.Id))!);
    }

    public async Task<Result> DeleteAssignmentAsync(Guid id)
    {
        var assignment = await _db.Set<PolicyNumberAssignment>().FirstOrDefaultAsync(a => a.Id == id);
        if (assignment == null) return Result.Failure("NOT_FOUND", "Policy number assignment not found.");

        assignment.IsDeleted = true;
        assignment.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public PolicyNumberPreviewDto Preview(PolicyNumberPreviewRequestDto dto)
    {
        var count = Math.Clamp(dto.Count, 1, 10);
        var numbers = Enumerable.Range(0, count)
            .Select(i => BuildBaseNumber(dto.Format, dto.NextNumber + i, dto.LineOfBusiness, dto.State, dto.CarrierName)
                + BuildTermSuffix(dto.TermSuffixFormat, 1))
            .ToList();

        return new PolicyNumberPreviewDto { Numbers = numbers };
    }

    private async Task<PolicyNumberAssignmentDto?> GetAssignmentAsync(Guid id)
    {
        var assignment = await _db.Set<PolicyNumberAssignment>()
            .Include(a => a.PolicyNumberSequence)
            .Include(a => a.ProgramConfiguration)
            .Include(a => a.Carrier)
            .Where(a => a.Id == id)
            .FirstOrDefaultAsync();
        return assignment == null ? null : MapAssignment(assignment);
    }

    private static Result ValidateSequence(PolicyNumberSequenceUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return Result.Failure("VALIDATION", "Sequence name is required.");
        if (string.IsNullOrWhiteSpace(dto.Format)) return Result.Failure("VALIDATION", "Number format is required.");
        if (!dto.Format.Contains("{SEQ", StringComparison.OrdinalIgnoreCase)) return Result.Failure("VALIDATION", "Number format must include {SEQ} or {SEQ:00000}.");
        if (dto.NextNumber < 1) return Result.Failure("VALIDATION", "Next number must be at least 1.");
        if (string.IsNullOrWhiteSpace(dto.TermSuffixFormat)) return Result.Failure("VALIDATION", "Term suffix format is required.");
        return Result.Success();
    }

    private async Task<Result> ValidateAssignmentAsync(PolicyNumberAssignmentUpsertDto dto)
    {
        if (!await _db.Set<PolicyNumberSequence>().AnyAsync(s => s.Id == dto.PolicyNumberSequenceId))
            return Result.Failure("VALIDATION", "Select a valid sequence.");
        if (dto.ProgramConfigurationId.HasValue && !await _db.Set<ProgramConfiguration>().AnyAsync(p => p.Id == dto.ProgramConfigurationId.Value && p.IsActive))
            return Result.Failure("VALIDATION", "Select a valid active program.");
        if (!await _db.Set<Carrier>().AnyAsync(c => c.Id == dto.CarrierId))
            return Result.Failure("VALIDATION", "Select a valid carrier.");
        if (!string.IsNullOrWhiteSpace(dto.State) && dto.State.Trim().Length != 2)
            return Result.Failure("VALIDATION", "State must be blank or a two-letter code.");
        return Result.Success();
    }

    private static void ApplySequence(PolicyNumberSequence sequence, PolicyNumberSequenceUpsertDto dto)
    {
        sequence.Name = dto.Name.Trim();
        sequence.Format = dto.Format.Trim();
        sequence.NextNumber = dto.NextNumber;
        sequence.ResetAnnually = dto.ResetAnnually;
        sequence.TermSuffixFormat = dto.TermSuffixFormat.Trim();
        sequence.RenewalBehavior = dto.RenewalBehavior;
        sequence.AllowManualOverride = dto.AllowManualOverride;
        sequence.IsActive = dto.IsActive;
        sequence.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
    }

    private static void ApplyAssignment(PolicyNumberAssignment assignment, PolicyNumberAssignmentUpsertDto dto)
    {
        assignment.PolicyNumberSequenceId = dto.PolicyNumberSequenceId;
        assignment.ProgramConfigurationId = dto.ProgramConfigurationId;
        assignment.CarrierId = dto.CarrierId;
        assignment.WritingCompanyId = dto.WritingCompanyId;
        assignment.LineOfBusiness = dto.LineOfBusiness;
        assignment.State = string.IsNullOrWhiteSpace(dto.State) ? null : dto.State.Trim().ToUpperInvariant();
        assignment.Priority = dto.Priority;
        assignment.IsActive = dto.IsActive;
    }

    private static PolicyNumberSequenceDto MapSequence(PolicyNumberSequence sequence) => new()
    {
        Id = sequence.Id,
        Name = sequence.Name,
        Format = sequence.Format,
        NextNumber = sequence.NextNumber,
        ResetAnnually = sequence.ResetAnnually,
        TermSuffixFormat = sequence.TermSuffixFormat,
        RenewalBehavior = sequence.RenewalBehavior,
        AllowManualOverride = sequence.AllowManualOverride,
        IsActive = sequence.IsActive,
        Notes = sequence.Notes,
    };

    private static PolicyNumberAssignmentDto MapAssignment(PolicyNumberAssignment assignment) => new()
    {
        Id = assignment.Id,
        PolicyNumberSequenceId = assignment.PolicyNumberSequenceId,
        SequenceName = assignment.PolicyNumberSequence.Name,
        ProgramConfigurationId = assignment.ProgramConfigurationId,
        ProgramName = assignment.ProgramConfiguration?.Name,
        CarrierId = assignment.CarrierId,
        CarrierName = assignment.Carrier.Name,
        WritingCompanyId = assignment.WritingCompanyId,
        LineOfBusiness = assignment.LineOfBusiness,
        State = assignment.State,
        Priority = assignment.Priority,
        IsActive = assignment.IsActive,
    };

    private static string BuildBaseNumber(string format, long sequenceValue, PolicyLineOfBusiness lob, string? state, string? carrierName)
    {
        var year = DateTime.UtcNow.Year;
        var result = format
            .Replace("{YYYY}", year.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{YY}", (year % 100).ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{LOB}", GetLobCode(lob), StringComparison.OrdinalIgnoreCase)
            .Replace("{STATE}", state?.Trim().ToUpperInvariant() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{CARRIER}", NormalizeToken(carrierName), StringComparison.OrdinalIgnoreCase)
            .Replace("{COMPANY}", string.Empty, StringComparison.OrdinalIgnoreCase);

        result = Regex.Replace(result, @"\{SEQ:(0+)\}", m => sequenceValue.ToString($"D{m.Groups[1].Value.Length}"), RegexOptions.IgnoreCase);
        return Regex.Replace(result, @"\{SEQ\}", sequenceValue.ToString(), RegexOptions.IgnoreCase);
    }

    private static string BuildTermSuffix(string format, int termNumber)
    {
        var result = Regex.Replace(format, @"\{TERM:(0+)\}", m => termNumber.ToString($"D{m.Groups[1].Value.Length}"), RegexOptions.IgnoreCase);
        return Regex.Replace(result, @"\{TERM\}", termNumber.ToString(), RegexOptions.IgnoreCase);
    }

    private static string GetLobCode(PolicyLineOfBusiness lob) => lob switch
    {
        PolicyLineOfBusiness.AutoLiability => "AL",
        PolicyLineOfBusiness.AutoPhysicalDamage => "APD",
        PolicyLineOfBusiness.GeneralLiability => "GL",
        PolicyLineOfBusiness.InlandMarine => "IM",
        _ => lob.ToString().ToUpperInvariant(),
    };

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).Take(12).Select(char.ToUpperInvariant).ToArray());
    }
}

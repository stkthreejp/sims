using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIMS.Application.Common;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class PolicyNumberService : IPolicyNumberService
{
    private readonly DbContext _db;

    public PolicyNumberService(DbContext db)
    {
        _db = db;
    }

    public async Task<Result<PolicyNumberGenerationResult>> GenerateForBindAsync(Quote quote, Guid assignedById, DateOnly? effectiveDate = null)
    {
        if (quote.PolicyNumber != null)
            return Result<PolicyNumberGenerationResult>.Failure("POLICY_NUMBER_EXISTS", "This quote already has a policy number.");

        var policyEffectiveDate = effectiveDate ?? quote.EffectiveDate;
        var state = quote.Submission?.Insured?.State?.Trim().ToUpperInvariant();
        var assignment = await FindAssignmentAsync(quote.ProgramId, quote.CarrierId, quote.LineOfBusiness, state);

        if (assignment == null)
        {
            // Fail closed for program-scoped binds (WS5-R Batch 1, A1.2): no silent legacy
            // POL- number on a program policy — it would never resolve to a real sequence and
            // could never appear on a bordereau. The legacy generator remains only for any
            // (now unreachable) program-less quote.
            if (quote.ProgramId != null)
                return Result<PolicyNumberGenerationResult>.Failure("POLICY_NUMBER_ASSIGNMENT_MISSING",
                    "No policy-number assignment is configured for this program, carrier, line of business, and state. Add an assignment before binding.");
            return await GenerateLegacyNumberAsync();
        }

        var ownsTransaction = _db.Database.CurrentTransaction == null;
        IDbContextTransaction? transaction = null;

        try
        {
            if (ownsTransaction)
                transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var sequence = await _db.Set<PolicyNumberSequence>()
                .FirstOrDefaultAsync(s => s.Id == assignment.PolicyNumberSequenceId && s.IsActive);

            if (sequence == null)
                return Result<PolicyNumberGenerationResult>.Failure("POLICY_NUMBER_SEQUENCE_NOT_FOUND", "The assigned policy number sequence is inactive or missing.");

            ResetAnnualSequenceIfNeeded(sequence, policyEffectiveDate.Year);

            var sequenceValue = sequence.NextNumber;
            var baseNumber = BuildBaseNumber(sequence.Format, sequenceValue, quote, state, policyEffectiveDate);
            var termNumber = 1;
            var fullNumber = baseNumber + BuildTermSuffix(sequence.TermSuffixFormat, termNumber);

            sequence.NextNumber++;

            _db.Set<PolicyNumberSequenceUsage>().Add(new PolicyNumberSequenceUsage
            {
                PolicyNumberSequenceId = sequence.Id,
                PolicyNumberAssignmentId = assignment.Id,
                QuoteId = quote.Id,
                BasePolicyNumber = baseNumber,
                FullPolicyNumber = fullNumber,
                SequenceValue = sequenceValue,
                TermNumber = termNumber,
                AssignedById = assignedById,
                AssignedAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync();
            if (transaction != null)
                await transaction.CommitAsync();

            return Result<PolicyNumberGenerationResult>.Success(new PolicyNumberGenerationResult(
                fullNumber,
                baseNumber,
                termNumber,
                sequence.Id,
                assignment.Id,
                sequenceValue));
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<PolicyNumberAssignment?> FindAssignmentAsync(Guid? programConfigurationId, Guid carrierId, PolicyLineOfBusiness lob, string? state)
    {
        var assignments = await _db.Set<PolicyNumberAssignment>()
            .Include(a => a.PolicyNumberSequence)
            .Where(a =>
                a.IsActive &&
                a.PolicyNumberSequence.IsActive &&
                (a.ProgramConfigurationId == null || (programConfigurationId.HasValue && a.ProgramConfigurationId == programConfigurationId.Value)) &&
                a.CarrierId == carrierId &&
                a.LineOfBusiness == lob &&
                a.WritingCompanyId == null &&
                (a.State == null || a.State == state))
            .OrderByDescending(a => programConfigurationId.HasValue && a.ProgramConfigurationId == programConfigurationId.Value)
            .ThenByDescending(a => a.State != null)
            .ThenBy(a => a.Priority)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync();

        return assignments.FirstOrDefault();
    }

    private async Task<Result<PolicyNumberGenerationResult>> GenerateLegacyNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"POL-{year}-";
        var count = await _db.Set<Policy>()
            .IgnoreQueryFilters()
            .CountAsync(p => p.PolicyNumber.StartsWith(prefix));
        var number = $"{prefix}{count + 1:D5}";

        return Result<PolicyNumberGenerationResult>.Success(new PolicyNumberGenerationResult(
            number,
            number,
            1,
            null,
            null,
            null));
    }

    private static void ResetAnnualSequenceIfNeeded(PolicyNumberSequence sequence, int effectiveYear)
    {
        if (!sequence.ResetAnnually || sequence.LastResetYear == effectiveYear)
            return;

        sequence.NextNumber = 1;
        sequence.LastResetYear = effectiveYear;
    }

    private static string BuildBaseNumber(string format, long sequenceValue, Quote quote, string? state, DateOnly effectiveDate)
    {
        var effectiveYear = effectiveDate.Year;
        var result = format
            .Replace("{YYYY}", effectiveYear.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{YY}", (effectiveYear % 100).ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{LOB}", GetLobCode(quote.LineOfBusiness), StringComparison.OrdinalIgnoreCase)
            .Replace("{STATE}", state ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{CARRIER}", NormalizeToken(quote.Carrier?.Name), StringComparison.OrdinalIgnoreCase)
            .Replace("{COMPANY}", string.Empty, StringComparison.OrdinalIgnoreCase);

        result = Regex.Replace(result, @"\{SEQ:(0+)\}", m => sequenceValue.ToString($"D{m.Groups[1].Value.Length}"), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{SEQ\}", sequenceValue.ToString(), RegexOptions.IgnoreCase);

        return result;
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
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value
            .Where(char.IsLetterOrDigit)
            .Take(12)
            .Select(char.ToUpperInvariant)
            .ToArray();
        return new string(chars);
    }
}

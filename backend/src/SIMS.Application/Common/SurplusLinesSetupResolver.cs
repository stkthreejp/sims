using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Common;

/// <summary>
/// Resolves the most-specific active <see cref="SurplusLinesStateSetup"/> for a policy/quote's
/// filing state and program/carrier/LOB scope as of a date. Mirrors the precedence the bordereaux
/// export uses: a setup matches when each scope dimension equals the target value OR is null
/// (a global fallback), and the most-specific match wins.
/// </summary>
public static class SurplusLinesSetupResolver
{
    public static SurplusLinesStateSetup? Resolve(
        IEnumerable<SurplusLinesStateSetup> setups,
        string state,
        Guid? programId,
        Guid? carrierId,
        PolicyLineOfBusiness? lineOfBusiness,
        DateOnly asOf)
        => setups
            .Where(s => s.IsActive
                && string.Equals(s.StateCode, state, StringComparison.OrdinalIgnoreCase)
                && (s.ProgramConfigurationId == null || s.ProgramConfigurationId == programId)
                && (s.CarrierId == null || s.CarrierId == carrierId)
                && (s.LineOfBusiness == null || s.LineOfBusiness == lineOfBusiness)
                && s.EffectiveDate <= asOf
                && (s.ExpirationDate == null || s.ExpirationDate >= asOf))
            .OrderByDescending(s => s.ProgramConfigurationId != null)
            .ThenByDescending(s => s.CarrierId != null)
            .ThenByDescending(s => s.LineOfBusiness != null)
            .ThenByDescending(s => s.EffectiveDate)
            .FirstOrDefault();
}

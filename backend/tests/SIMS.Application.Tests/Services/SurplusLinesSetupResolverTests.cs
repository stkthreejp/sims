using SIMS.Application.Common;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class SurplusLinesSetupResolverTests
{
    private static readonly Guid Program = Guid.NewGuid();
    private static readonly Guid Carrier = Guid.NewGuid();
    private const PolicyLineOfBusiness Lob = PolicyLineOfBusiness.GeneralLiability;
    private static readonly DateOnly AsOf = new(2026, 6, 1);

    // LicenseNumber is used only as a marker to identify which setup was resolved.
    private static SurplusLinesStateSetup Setup(
        string marker, string state, Guid? program, Guid? carrier, PolicyLineOfBusiness? lob,
        bool active = true, DateOnly? effective = null, DateOnly? expiration = null)
        => new()
        {
            LicenseNumber = marker,
            StateCode = state,
            ProgramConfigurationId = program,
            CarrierId = carrier,
            LineOfBusiness = lob,
            IsActive = active,
            EffectiveDate = effective ?? new DateOnly(2026, 1, 1),
            ExpirationDate = expiration,
        };

    private static string? Resolve(IEnumerable<SurplusLinesStateSetup> setups)
        => SurplusLinesSetupResolver.Resolve(setups, "TX", Program, Carrier, Lob, AsOf)?.LicenseNumber;

    [Fact]
    public void Resolve_PrefersMostSpecificScope()
    {
        var setups = new[]
        {
            Setup("global", "TX", null, null, null),
            Setup("program", "TX", Program, null, null),
            Setup("carrier", "TX", null, Carrier, null),
            Setup("full", "TX", Program, Carrier, Lob),
        };
        Assert.Equal("full", Resolve(setups));
    }

    [Fact]
    public void Resolve_FallsBackToGlobalScope()
        => Assert.Equal("global", Resolve(new[] { Setup("global", "TX", null, null, null) }));

    [Fact]
    public void Resolve_MatchesStateCaseInsensitively()
        => Assert.Equal("tx", Resolve(new[] { Setup("tx", "tx", null, null, null) }));

    [Fact]
    public void Resolve_IgnoresOtherStates()
        => Assert.Null(Resolve(new[] { Setup("ga", "GA", null, null, null) }));

    [Fact]
    public void Resolve_ExcludesInactive()
        => Assert.Null(Resolve(new[] { Setup("inactive", "TX", null, null, null, active: false) }));

    [Fact]
    public void Resolve_ExcludesBeforeEffectiveDate()
        => Assert.Null(Resolve(new[] { Setup("future", "TX", null, null, null, effective: new DateOnly(2026, 12, 1)) }));

    [Fact]
    public void Resolve_ExcludesAfterExpirationDate()
        => Assert.Null(Resolve(new[] { Setup("expired", "TX", null, null, null, expiration: new DateOnly(2026, 3, 1)) }));

    [Fact]
    public void Resolve_ExcludesSetupPinnedToADifferentCarrier()
        => Assert.Null(Resolve(new[] { Setup("otherCarrier", "TX", null, Guid.NewGuid(), null) }));
}

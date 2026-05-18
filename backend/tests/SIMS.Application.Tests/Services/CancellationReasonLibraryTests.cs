using SIMS.Application.Policies;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class CancellationReasonLibraryTests
{
    [Fact]
    public void Reasons_IncludeRequiredInputTokensFromBracketedFields()
    {
        var reason = CancellationReasonLibrary.GetByCode("NP-01");

        Assert.NotNull(reason);
        Assert.Equal("Non-Payment - Standard", reason.Label);
        Assert.Equal(10, reason.DefaultNoticeRequirementDays);
        Assert.Equal(["AMOUNT_DUE"], reason.RequiredInputTokens);
    }

    [Fact]
    public void ResolveReasonLanguage_ReplacesRequiredInputs()
    {
        var reason = CancellationReasonLibrary.GetByCode("UW-02");

        var resolved = reason!.Resolve(new Dictionary<string, string>
        {
            ["DESCRIBE_CONDITIONS"] = "missing fire extinguishers and blocked exits"
        });

        Assert.Contains("missing fire extinguishers and blocked exits", resolved);
        Assert.DoesNotContain("[DESCRIBE_CONDITIONS]", resolved);
    }

    [Fact]
    public void ResolveReasonLanguage_RejectsMissingRequiredInputs()
    {
        var reason = CancellationReasonLibrary.GetByCode("FR-01");

        var ex = Assert.Throws<InvalidOperationException>(() => reason!.Resolve(new Dictionary<string, string>()));

        Assert.Contains("DESCRIBE_MISREPRESENTATION", ex.Message);
    }
}

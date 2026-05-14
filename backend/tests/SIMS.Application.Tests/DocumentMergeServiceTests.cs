using SIMS.Application.Common;
using SIMS.Application.Services;
using Xunit;

namespace SIMS.Application.Tests;

public class DocumentMergeServiceTests
{
    [Fact]
    public void MergeText_FormatsDatesAndCurrency()
    {
        var service = new DocumentMergeService();
        var data = new DocumentMergeData();
        data.Values["Policy.EffectiveDate"] = new DateOnly(2026, 5, 14);
        data.Values["Quote.TotalPremium"] = 12500m;

        var result = service.MergeText("Eff {{Policy.EffectiveDate}} Premium {{Quote.TotalPremium | currency}}", data);

        Assert.Equal("Eff 05/14/2026 Premium $12,500.00", result);
    }

    [Fact]
    public void MergeText_RepeatsScheduleBlocks()
    {
        var service = new DocumentMergeService();
        var data = new DocumentMergeData();
        data.RepeatingValues["Equipment"] = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Description"] = "Excavator", ["Value"] = 100000m },
            new Dictionary<string, object?> { ["Description"] = "Loader", ["Value"] = 75000m },
        };

        var result = service.MergeText("{{#Equipment}}<tr><td>{{Description}}</td><td>{{Value | currency}}</td></tr>{{/Equipment}}", data);

        Assert.Equal("<tr><td>Excavator</td><td>$100,000.00</td></tr><tr><td>Loader</td><td>$75,000.00</td></tr>", result);
    }
}

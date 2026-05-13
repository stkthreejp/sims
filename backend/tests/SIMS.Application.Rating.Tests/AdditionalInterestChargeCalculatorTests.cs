using SIMS.Application.Rating;
using SIMS.Domain.Enums;
using Xunit;

namespace SIMS.Application.Rating.Tests;

public class AdditionalInterestChargeCalculatorTests
{
    [Fact]
    public void PerInterest_MultipliesCountByConfiguredAmount()
    {
        var amount = AdditionalInterestChargeCalculator.Calculate(
            AdditionalInterestChargeMethod.PerInterest,
            interestCount: 3,
            perInterestAmount: 50m,
            blanketAmount: null,
            minimumCharge: null,
            maximumCharge: null);

        Assert.Equal(150m, amount);
    }

    [Fact]
    public void BlanketFlat_AppliesOnce()
    {
        var amount = AdditionalInterestChargeCalculator.Calculate(
            AdditionalInterestChargeMethod.BlanketFlat,
            interestCount: 4,
            perInterestAmount: null,
            blanketAmount: 250m,
            minimumCharge: null,
            maximumCharge: null);

        Assert.Equal(250m, amount);
    }

    [Fact]
    public void NoChargeAndIncluded_IgnoreMinimums()
    {
        var noCharge = AdditionalInterestChargeCalculator.Calculate(
            AdditionalInterestChargeMethod.NoCharge, 2, 50m, 250m, 100m, null);
        var included = AdditionalInterestChargeCalculator.Calculate(
            AdditionalInterestChargeMethod.Included, 2, 50m, 250m, 100m, null);

        Assert.Equal(0m, noCharge);
        Assert.Equal(0m, included);
    }

    [Fact]
    public void PerInterest_RespectsMinimumAndMaximum()
    {
        var minimum = AdditionalInterestChargeCalculator.Calculate(
            AdditionalInterestChargeMethod.PerInterest, 1, 25m, null, 50m, 200m);
        var maximum = AdditionalInterestChargeCalculator.Calculate(
            AdditionalInterestChargeMethod.PerInterest, 10, 50m, null, 50m, 200m);

        Assert.Equal(50m, minimum);
        Assert.Equal(200m, maximum);
    }
}

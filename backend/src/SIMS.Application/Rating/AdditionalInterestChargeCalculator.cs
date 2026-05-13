using SIMS.Domain.Enums;

namespace SIMS.Application.Rating;

public static class AdditionalInterestChargeCalculator
{
    public static decimal Calculate(
        AdditionalInterestChargeMethod method,
        int interestCount,
        decimal? perInterestAmount,
        decimal? blanketAmount,
        decimal? minimumCharge,
        decimal? maximumCharge)
    {
        var amount = method switch
        {
            AdditionalInterestChargeMethod.PerInterest => interestCount * (perInterestAmount ?? 0m),
            AdditionalInterestChargeMethod.BlanketFlat => blanketAmount ?? 0m,
            AdditionalInterestChargeMethod.NoCharge => 0m,
            AdditionalInterestChargeMethod.Included => 0m,
            _ => 0m
        };

        if (method is AdditionalInterestChargeMethod.PerInterest or AdditionalInterestChargeMethod.BlanketFlat)
        {
            if (minimumCharge.HasValue && amount < minimumCharge.Value)
                amount = minimumCharge.Value;
            if (maximumCharge.HasValue && amount > maximumCharge.Value)
                amount = maximumCharge.Value;
        }

        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}

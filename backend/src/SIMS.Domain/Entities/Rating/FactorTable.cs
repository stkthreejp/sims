using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities.Rating;

public class FactorTable : BaseEntity
{
    public Guid RatingPlanVersionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string[] DimensionNames { get; set; } = [];
    public FactorKind ValueSemantics { get; set; }

    public RatingPlanVersion RatingPlanVersion { get; set; } = null!;
    public ICollection<FactorRow> Rows { get; set; } = new List<FactorRow>();
}

namespace SIMS.Domain.Entities.Rating;

public class FactorRow : BaseEntity
{
    public Guid FactorTableId { get; set; }
    public Dictionary<string, string> DimensionValues { get; set; } = new();
    public decimal Factor { get; set; }

    public FactorTable FactorTable { get; set; } = null!;
}

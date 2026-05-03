namespace SIMS.Domain.Entities.Rating;

public class Territory : BaseEntity
{
    public int TerritoryNumber { get; set; }
    public string States { get; set; } = string.Empty;
    public decimal Modifier { get; set; }
}

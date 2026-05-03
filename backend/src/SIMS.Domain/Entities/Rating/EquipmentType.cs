namespace SIMS.Domain.Entities.Rating;

public class EquipmentType : BaseEntity
{
    public int TypeNumber { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<EligibilityRule> EligibilityRules { get; set; } = new List<EligibilityRule>();
}

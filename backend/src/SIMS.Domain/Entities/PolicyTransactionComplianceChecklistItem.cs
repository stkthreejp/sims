namespace SIMS.Domain.Entities;

public class PolicyTransactionComplianceChecklistItem : BaseEntity
{
    public Guid PolicyTransactionComplianceChecklistId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public Guid? LegalRequirementSectionId { get; set; }
    public Guid? CompletedById { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public string? SnapshotJson { get; set; }

    public PolicyTransactionComplianceChecklist Checklist { get; set; } = null!;
    public LegalRequirementSection? LegalRequirementSection { get; set; }
    public User? CompletedBy { get; set; }
}

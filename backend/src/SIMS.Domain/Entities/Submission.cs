using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class Submission : BaseEntity
{
    public string SubmissionNumber { get; set; } = string.Empty;
    public Guid InsuredId { get; set; }
    public Guid? AgentId { get; set; }
    public Guid UnderwriterId { get; set; }
    public Guid? AssistantUWId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.New;
    public string? DescriptionOfOperations { get; set; }
    /// <summary>JSON-serialized array of PolicyLineOfBusiness values detected/set for this submission.</summary>
    public string? LinesOfBusiness { get; set; }
    public int? ProducerId { get; set; }
    public Guid CreatedById { get; set; }

    // Navigation
    public Insured Insured { get; set; } = null!;
    public Agent? Agent { get; set; }
    public User Underwriter { get; set; } = null!;
    public User? AssistantUW { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    public ICollection<SubmissionLocation> Locations { get; set; } = new List<SubmissionLocation>();
    public ICollection<SubmissionDriver> Drivers { get; set; } = new List<SubmissionDriver>();
    public ICollection<SubmissionVehicle> Vehicles { get; set; } = new List<SubmissionVehicle>();
    public ICollection<SubmissionPriorCarrier> PriorCarriers { get; set; } = new List<SubmissionPriorCarrier>();
    public ICollection<SubmissionLossYear> LossYears { get; set; } = new List<SubmissionLossYear>();
    public ICollection<SubmissionGLClassification> GLClassifications { get; set; } = new List<SubmissionGLClassification>();
    public ICollection<SubmissionEquipment> Equipment { get; set; } = new List<SubmissionEquipment>();
    public SubmissionSupplemental? Supplemental { get; set; }
    public SubmissionGLCoverages? GLCoverages { get; set; }
    public SubmissionIMCoverages? IMCoverages { get; set; }
}

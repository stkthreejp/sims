namespace SIMS.Application.DTOs.UWWriteup;

// Stored in PayloadJson — only manual/narrative fields
public class IMWriteupPayload
{
    public bool? NewVentureDocsOk { get; set; }
    public string? ReasonSubmitted { get; set; }

    // Referral triggers (auto-computed but UW can override)
    public bool ReferralLossRatioOver55 { get; set; }
    public bool ReferralPieceOver500k { get; set; }
    public bool ReferralTivOver2mil { get; set; }
    public bool ReferralLossOver400k { get; set; }
    public string? ReferralOtherText { get; set; }

    // Losses (manual)
    public string? LossMitigationActions { get; set; }
    public string? LossesOver25kDescription { get; set; }

    // Equipment checks
    public bool EqValueChecked { get; set; }

    // Operations & metrics
    public bool WaterborneExposure { get; set; }
    public string? LastInspectionDate { get; set; }
    public bool RecommendationsOutstanding { get; set; }
    public string? RecommendationsDetail { get; set; }
    public bool? WebsiteReviewed { get; set; }
    public string? WebsiteIssues { get; set; }

    // Narratives
    public string? NarrativeOperators { get; set; }
    public string? NarrativeEquipment { get; set; }
    public string? NarrativeFireSuppression { get; set; }
    public string? NarrativeOtherConcerns { get; set; }

    // Recommendation
    public string? DecisionRationale { get; set; }
}

public class EquipmentSummaryDto
{
    public decimal TotalTiv { get; set; }
    public decimal LargestUnitTiv { get; set; }
    public int CountCutter { get; set; }
    public int CountSkidder { get; set; }
    public int CountLoader { get; set; }
    public int CountDozer { get; set; }
    public int CountOther { get; set; }
    public int TotalCount { get; set; }
}

public class PriorCarrierSummaryDto
{
    public string CarrierName { get; set; } = string.Empty;
    public string? PolicyNumber { get; set; }
    public string? ExpirationDate { get; set; }
    public decimal? PremiumAmount { get; set; }
}

public class WriteupConditionDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool Required { get; set; }
    public bool Satisfied { get; set; }
    public int SortOrder { get; set; }
}

// Full DTO returned to client
public class UWWriteupDto
{
    // Metadata
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Decision { get; set; }
    public int SchemaVersion { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedByName { get; set; }

    // Prefilled context (computed fresh from DB, not stored)
    public string UWName { get; set; } = string.Empty;
    public string? AssistantUWName { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string InsuredName { get; set; } = string.Empty;
    public string Lob { get; set; } = string.Empty;
    public string PolicyType { get; set; } = "New";
    public string EffectiveDate { get; set; } = string.Empty;
    public string? OperationType { get; set; }
    public bool NewVenture { get; set; }
    public int? YearsInBusiness { get; set; }
    public int? CreditScore { get; set; }
    public string? Website { get; set; }
    public string Address { get; set; } = string.Empty;
    public List<PriorCarrierSummaryDto> PriorCarriers { get; set; } = new();

    // Equipment summary (computed from submission)
    public EquipmentSummaryDto Equipment { get; set; } = new();

    // Auto-computed referral triggers (can be overridden in payload)
    public bool AutoReferralPieceOver500k { get; set; }
    public bool AutoReferralTivOver2mil { get; set; }

    // The stored payload (manual/narrative fields)
    public IMWriteupPayload Payload { get; set; } = new();

    // Conditions
    public List<WriteupConditionDto> Conditions { get; set; } = new();
}

// Save request
public class SaveWriteupDto
{
    public IMWriteupPayload Payload { get; set; } = new();
    public List<SaveConditionDto> Conditions { get; set; } = new();
}

public class SaveConditionDto
{
    public Guid? Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
    public bool Satisfied { get; set; }
    public int SortOrder { get; set; }
}

// Submit request
public class SubmitWriteupDto
{
    public string Decision { get; set; } = string.Empty;
}

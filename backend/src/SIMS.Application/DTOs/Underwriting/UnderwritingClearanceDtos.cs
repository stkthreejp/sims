using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Underwriting;

public class UnderwritingClearanceEvaluationDto
{
    public Guid SubmissionId { get; set; }
    public UnderwritingClearanceStatus OverallStatus { get; set; } = UnderwritingClearanceStatus.Clear;
    public IReadOnlyList<UnderwritingClearanceResultDto> Results { get; set; } = [];
}

public class UnderwritingClearanceResultDto
{
    public UnderwritingClearanceCheckType CheckType { get; set; }
    public UnderwritingClearanceStatus Status { get; set; }
    public Guid? MatchedRecordId { get; set; }
    public string? MatchedRecordLabel { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

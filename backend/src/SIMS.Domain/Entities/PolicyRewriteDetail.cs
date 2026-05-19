namespace SIMS.Domain.Entities;

public class PolicyRewriteDetail : BaseEntity
{
    public Guid PolicyTransactionId { get; set; }
    public Guid SourcePolicyId { get; set; }
    public Guid? SourcePolicyVersionId { get; set; }
    public Guid ReplacementQuoteId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public PolicyTransaction PolicyTransaction { get; set; } = null!;
    public Policy SourcePolicy { get; set; } = null!;
    public PolicyVersion? SourcePolicyVersion { get; set; }
    public Quote ReplacementQuote { get; set; } = null!;
}

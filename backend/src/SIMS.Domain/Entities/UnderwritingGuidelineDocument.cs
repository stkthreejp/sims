using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class UnderwritingGuidelineDocument : BaseEntity
{
    public string ProgramName { get; set; } = string.Empty;
    public Guid? CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string StateCode { get; set; } = "ALL";
    public string Title { get; set; } = string.Empty;
    public string? SourceFileName { get; set; }
    public string? SourceBlobName { get; set; }
    public string? Notes { get; set; }
    public int Version { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }

    public Carrier? Carrier { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<UnderwritingGuidelineControl> Controls { get; set; } = new List<UnderwritingGuidelineControl>();
    public ICollection<UnderwritingGuidelineAuditLog> AuditLogs { get; set; } = new List<UnderwritingGuidelineAuditLog>();
}


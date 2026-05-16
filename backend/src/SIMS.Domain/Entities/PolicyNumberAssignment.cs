using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class PolicyNumberAssignment : BaseEntity
{
    public Guid PolicyNumberSequenceId { get; set; }
    public Guid CarrierId { get; set; }
    public Guid? WritingCompanyId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string? State { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;

    public PolicyNumberSequence PolicyNumberSequence { get; set; } = null!;
    public Carrier Carrier { get; set; } = null!;
}

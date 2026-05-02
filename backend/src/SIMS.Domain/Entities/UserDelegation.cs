namespace SIMS.Domain.Entities;

public class UserDelegation : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid DelegateToUserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
    public User DelegateToUser { get; set; } = null!;
}

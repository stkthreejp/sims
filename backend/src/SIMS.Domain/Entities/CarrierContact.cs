namespace SIMS.Domain.Entities;

public class CarrierContact : BaseEntity
{
    public Guid CarrierId { get; set; }
    public Carrier Carrier { get; set; } = null!;

    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

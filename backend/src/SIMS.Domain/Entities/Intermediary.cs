namespace SIMS.Domain.Entities;

public class Intermediary : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountLast4 { get; set; }
    public string? BankRoutingNumber { get; set; }
    public string? BankSwiftCode { get; set; }
    public string? BankInstructions { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public ICollection<IntermediaryProgramCarrierLobSetup> ProgramCarrierLobSetups { get; set; } = new List<IntermediaryProgramCarrierLobSetup>();
}

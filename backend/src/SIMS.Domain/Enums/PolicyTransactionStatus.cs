namespace SIMS.Domain.Enums;

public enum PolicyTransactionStatus
{
    Pending = 1,  // endorsement quoted but not yet issued
    Issued = 2    // confirmed, invoiced
}

namespace SIMS.Domain.Enums;

// SMM writes four lines of business (no package policies). Each carrier × LOB pair
// has its own rating engine. Values 2 and 4–9 are retained only for historical
// data; new quotes/policies must use one of the active LOBs.
public enum PolicyLineOfBusiness
{
    // Active — written by SMM
    GeneralLiability = 1,
    InlandMarine = 10,
    AutoLiability = 11,
    AutoPhysicalDamage = 12,

    // Deprecated — retained for historical records only
    Property = 2,
    CommercialAuto = 3,
    BusinessOwners = 4,
    WorkersCompensation = 5,
    ProfessionalLiability = 6,
    Umbrella = 7,
    Cyber = 8,
    ExcessLiability = 9,

    Other = 99
}

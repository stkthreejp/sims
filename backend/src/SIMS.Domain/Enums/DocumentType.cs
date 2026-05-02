namespace SIMS.Domain.Enums;

public enum DocumentType
{
    // ── Submission ────────────────────────────────────────────────────────────
    Application,
    SupplementalApplication,
    SignedApplication,
    StatementOfValues,
    LossRuns,
    PriorPolicy,
    FinancialStatements,
    CreditReport,
    CabReport,
    Mvr,
    ProposalQuoteLetter,
    Declination,
    UnderwritingMemo,

    // ── Policy ────────────────────────────────────────────────────────────────
    DeclarationsPage,
    PolicyForm,
    Endorsement,
    Binder,
    CertificateOfInsurance,
    Invoice,
    PremiumFinanceAgreement,
    Audit,
    InspectionSurvey,
    CancellationNonRenewal,

    // ── Carrier ───────────────────────────────────────────────────────────────
    AppointmentLetter,
    AgencyAgreement,
    UnderwritingGuidelines,
    RateFiling,
    ComplianceMarketConduct,

    // ── Agent ─────────────────────────────────────────────────────────────────
    License,
    EosCertificate,
    W9,

    // ── Shared ────────────────────────────────────────────────────────────────
    Correspondence,
    Other,
}

public enum DocumentEntityType
{
    Submission,
    Policy,
    Carrier,
    Agent,
}

namespace SIMS.Domain.Enums;

public enum EmailAttachmentDocumentType
{
    Unknown,
    Acord125,
    Acord126,
    LossRun,
    DecPage,
    ScheduleOfValues,
    SignedApplication,
    Other,
    // Appended for automated intake (Claude vision classifier). Append-only — these are
    // stored as int, so new values must go at the end to keep existing rows stable.
    Acord127,
    Acord146,
    Mvr,
}

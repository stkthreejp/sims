namespace SIMS.Domain.Enums;

public enum TaskAuditAction
{
    Created = 1,
    Assigned = 2,
    Reassigned = 3,
    StatusChanged = 4,
    PriorityChanged = 5,
    DueDateChanged = 6,
    Completed = 7,
    Cancelled = 8,
    Escalated = 9,
    ReminderSent = 10,
    OverdueNotified = 11,
    DigestSent = 12,
    Note = 13
}

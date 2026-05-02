using IMS.Domain.Enums;

namespace IMS.Application.Interfaces.Services;

/// <summary>
/// Fires a named system event and creates TaskInstance records for every
/// root WorkflowStep belonging to the matching WorkflowTemplate(s).
/// Dependent steps (DependsOnStepId != null) are deferred to Phase 4.
/// </summary>
public interface IWorkflowEngineService
{
    /// <param name="eventName">Matches SystemEvent.EventName (unique index).</param>
    /// <param name="entityType">Submission, Policy, etc.</param>
    /// <param name="entityId">PK of the triggering entity.</param>
    /// <param name="context">
    ///   Arbitrary key→value bag passed to formula + assignment resolvers.
    ///   DateTime values are surfaced to DueDateFormulaService as-is;
    ///   Guid values are used by RoleAssignmentExpression resolution.
    /// </param>
    Task FireEventAsync(
        string eventName,
        TaskEntityType entityType,
        Guid entityId,
        Dictionary<string, object> context);
}

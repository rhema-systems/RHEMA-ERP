namespace ErpSystem.Core.Exceptions;

/// <summary>
/// Thrown by <see cref="ErpSystem.Core.Services.HR.Appraisal.GoalWorkflowCommandService"/>
/// when a workflow state transition is rejected by domain rules.
///
/// Callers should map this to an appropriate HTTP response:
///   • UnauthorizedAccess flavour → 403 Forbidden
///   • InvalidTransition / MissingFeedback / GoalLocked flavour → 422 Unprocessable Entity
///
/// Distinguishing access errors from business-rule errors is done by the
/// <see cref="Reason"/> property, so the API layer never needs to parse
/// the message string.
/// </summary>
public sealed class GoalWorkflowException : Exception
{
    /// <summary>
    /// Classifies why the transition was rejected, enabling the API layer
    /// to return the correct HTTP status code without parsing message text.
    /// </summary>
    public GoalWorkflowFailureReason Reason { get; }

    public GoalWorkflowException(GoalWorkflowFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public GoalWorkflowException(GoalWorkflowFailureReason reason, string message, Exception inner)
        : base(message, inner)
    {
        Reason = reason;
    }
}

/// <summary>
/// Identifies the business-rule category that caused a <see cref="GoalWorkflowException"/>.
/// </summary>
public enum GoalWorkflowFailureReason
{
    /// <summary>
    /// The current user is not the direct manager of the goal's employee.
    /// Map to HTTP 403.
    /// </summary>
    UnauthorizedAccess,

    /// <summary>
    /// The goal's current status does not permit the requested transition.
    /// Map to HTTP 422.
    /// </summary>
    InvalidTransition,

    /// <summary>
    /// The goal is locked and no further approval/rejection mutations are permitted.
    /// Map to HTTP 422.
    /// </summary>
    GoalLocked,

    /// <summary>
    /// Rejection was attempted without supplying a non-empty feedback message.
    /// Map to HTTP 422.
    /// </summary>
    MissingFeedback,

    /// <summary>
    /// The goal could not be found.  Treated separately from access-denial so the
    /// API layer can return 404 rather than 403 (avoids conflating the two).
    /// </summary>
    GoalNotFound,
}

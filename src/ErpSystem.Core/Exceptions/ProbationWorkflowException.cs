namespace ErpSystem.Core.Exceptions;

/// <summary>
/// Thrown by the probation &amp; confirmation services when a request is refused for a reason the
/// caller can act on — a record that is not there, a probation that is no longer active, or an
/// employee who already has one running.
/// </summary>
/// <remarks>
/// <para>The API layer maps <see cref="Reason"/> to a status code without parsing text, and
/// surfaces <see cref="Exception.Message"/>, so every message must be safe to display and must say
/// what to change.</para>
///
/// <para>It exists because <c>ProbationService</c> previously raised only <see
/// cref="ArgumentException"/> and <see cref="InvalidOperationException"/>, and
/// <c>GlobalExceptionHandlingMiddleware</c> replaces the detail of <i>both</i> with a fixed string
/// — "Invalid argument provided." and "The operation is not valid for the current state of the
/// object." So every rule in the area fired correctly and then said nothing: a missing probation
/// and a malformed payload were the same 400, and the one carefully worded rule in the service
/// (<c>RecordExtensionAsync</c>'s "New end date (x) must be later than the current end date (y)")
/// was discarded on its way out. Measured 2026-08-18 by <c>probe-reads.mjs</c>; see §3.15 of
/// <c>plans/HR-Area-15b-Probation-Confirmation-Build-Plan.md</c>.</para>
///
/// <para>A rule that fires correctly but cannot explain itself is still a defect — the user is
/// left on a failed save with no idea what to change. Same complaint, and the same remedy, as
/// <see cref="SuccessionValidationException"/> and <see cref="MedicalWorkflowException"/>.</para>
/// </remarks>
public sealed class ProbationWorkflowException : Exception
{
    /// <summary>Classifies the refusal so the API layer can choose a status code without parsing text.</summary>
    public ProbationFailureReason Reason { get; }

    public ProbationWorkflowException(ProbationFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public ProbationWorkflowException(ProbationFailureReason reason, string message, Exception inner)
        : base(message, inner)
    {
        Reason = reason;
    }

    /// <summary>A probation period, review or extension that is not there (or belongs to another tenant).</summary>
    public static ProbationWorkflowException NotFound(string message)
        => new(ProbationFailureReason.NotFound, message);

    /// <summary>The record is not in a state that permits the requested action.</summary>
    public static ProbationWorkflowException InvalidState(string message)
        => new(ProbationFailureReason.InvalidState, message);

    /// <summary>The request collides with a record that already exists.</summary>
    public static ProbationWorkflowException Conflict(string message)
        => new(ProbationFailureReason.Conflict, message);

    /// <summary>The payload itself is wrong — a date that goes backwards, a duration out of range.</summary>
    public static ProbationWorkflowException Invalid(string message)
        => new(ProbationFailureReason.Invalid, message);
}

/// <summary>Categories of probation rule violation, and the status each maps to.</summary>
public enum ProbationFailureReason
{
    /// <summary>The record does not exist for this tenant. Map to HTTP 404.</summary>
    NotFound,

    /// <summary>The record's current status forbids the action. Map to HTTP 409.</summary>
    InvalidState,

    /// <summary>The request duplicates something that already exists. Map to HTTP 409.</summary>
    Conflict,

    /// <summary>The payload is invalid on its own terms. Map to HTTP 400.</summary>
    Invalid
}

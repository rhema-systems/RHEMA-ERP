namespace ErpSystem.Core.Exceptions;

/// <summary>
/// Thrown by the staff awards services when a request is refused for a reason the caller can act
/// on — a record that is not there, a nomination that has left draft, or a duplicate.
/// </summary>
/// <remarks>
/// <para>The API layer maps <see cref="Reason"/> to a status code without parsing text, and
/// surfaces <see cref="Exception.Message"/>, so every message must be safe to display and must say
/// what to change.</para>
///
/// <para><b>Why it exists.</b> <c>AwardsServices</c> raised <see cref="InvalidOperationException"/>
/// at <b>38</b> sites, and <c>GlobalExceptionHandlingMiddleware</c> replaces the detail of that
/// exception with the fixed string "The operation is not valid for the current state of the
/// object." Every one of those refusals therefore fired correctly and then said nothing.</para>
///
/// <para>Worse than the lost text: about eighteen of the thirty-eight are <i>"… not found"</i>, and
/// they were all answering <b>400</b>. A caller could not distinguish a record that had been
/// deleted from a payload that was malformed, so no client could decide whether to re-fetch, show
/// "no longer available", or correct a field. Classifying them here makes a missing award a 404, a
/// duplicate a 409, and a real payload error a 400.</para>
///
/// <para>Same complaint and the same remedy as <see cref="ProbationWorkflowException"/>,
/// <see cref="SuccessionValidationException"/>, <see cref="MedicalWorkflowException"/> and
/// <see cref="JobArchitectureException"/> — this is the fifth HR area to need it.</para>
///
/// <para><b>Deliberately not converted:</b> the fourteen <c>"No tenant is associated with the
/// current user."</c> guards. Those are not domain rules the caller can act on — since area 14
/// slice 1 the tenant comes from the token, so reaching one means the token itself is malformed.
/// They stay as <see cref="InvalidOperationException"/> because a generic 400 is the right answer
/// to a condition that should be unreachable.</para>
/// </remarks>
public sealed class AwardsWorkflowException : Exception
{
    /// <summary>Classifies the refusal so the API layer can choose a status code without parsing text.</summary>
    public AwardsFailureReason Reason { get; }

    public AwardsWorkflowException(AwardsFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public AwardsWorkflowException(AwardsFailureReason reason, string message, Exception inner)
        : base(message, inner)
    {
        Reason = reason;
    }

    /// <summary>An award, nomination, committee or milestone that is not there (or belongs to another tenant).</summary>
    public static AwardsWorkflowException NotFound(string message)
        => new(AwardsFailureReason.NotFound, message);

    /// <summary>The record is not in a state that permits the requested action.</summary>
    public static AwardsWorkflowException InvalidState(string message)
        => new(AwardsFailureReason.InvalidState, message);

    /// <summary>The request collides with a record that already exists.</summary>
    public static AwardsWorkflowException Conflict(string message)
        => new(AwardsFailureReason.Conflict, message);

    /// <summary>The payload is wrong on its own terms — an impossible combination, a value out of range.</summary>
    public static AwardsWorkflowException Invalid(string message)
        => new(AwardsFailureReason.Invalid, message);
}

/// <summary>Categories of awards rule violation, and the status each maps to.</summary>
public enum AwardsFailureReason
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

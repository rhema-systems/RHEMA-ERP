namespace ErpSystem.Core.Exceptions;

/// <summary>
/// Thrown by the staff / company asset services when a request is refused for a reason the caller
/// can act on — a record that is not there, an asset that is already held, a requisition that has
/// already been decided.
/// </summary>
/// <remarks>
/// <para>The API layer maps <see cref="Reason"/> to a status code without parsing text, and
/// surfaces <see cref="Exception.Message"/>, so every message must be safe to display and must say
/// what to change.</para>
///
/// <para><b>Why it exists.</b> <c>AssetsServices</c> refused requests at <b>28</b> sites and not
/// one of them could explain itself. <c>GlobalExceptionHandlingMiddleware</c> replaces the detail
/// of an <see cref="InvalidOperationException"/> with the fixed string "The operation is not valid
/// for the current state of the object.", and the detail of an <see cref="ArgumentException"/> with
/// "Invalid argument provided." Every rule in the area therefore fired correctly and then said
/// nothing — a user met a blocked screen with no way to know what to do next. Recorded as defect
/// D-m while area 16 slice 1 was being written, and fixed here.</para>
///
/// <para>Worse than the lost text: <b>eighteen</b> of the twenty-eight are <i>"… not found"</i>,
/// and every one of them was answering <b>400</b>. A caller could not distinguish an asset that had
/// been disposed of from a payload that was malformed, so no client could decide whether to
/// re-fetch, show "no longer available", or correct a field. Classifying them here makes a missing
/// record a 404, a state clash a 409, and a genuinely bad payload a 400.</para>
///
/// <para>A "not found" here is also the tenant refusal: the asset services treat a record belonging
/// to another tenant as absent, which is deliberate — a 404 tells a cross-tenant caller nothing
/// about whether the id exists.</para>
///
/// <para>Same complaint and the same remedy as <see cref="ProbationWorkflowException"/>,
/// <see cref="SuccessionValidationException"/>, <see cref="MedicalWorkflowException"/>,
/// <see cref="JobArchitectureException"/> and <see cref="AwardsWorkflowException"/> — this is the
/// sixth HR area to need it, which is itself worth noticing.</para>
///
/// <para><b>Deliberately not converted:</b> the ten <c>"No tenant is associated with the current
/// user."</c> guards. Those are not domain rules the caller can act on — the tenant comes from the
/// token, so reaching one means the token itself is malformed. They stay as
/// <see cref="InvalidOperationException"/> because a generic 400 is the right answer to a condition
/// that should be unreachable. The same goes for the <see cref="UnauthorizedAccessException"/>
/// thrown when a token carries no user id.</para>
/// </remarks>
public sealed class AssetsWorkflowException : Exception
{
    /// <summary>Classifies the refusal so the API layer can choose a status code without parsing text.</summary>
    public AssetsFailureReason Reason { get; }

    public AssetsWorkflowException(AssetsFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public AssetsWorkflowException(AssetsFailureReason reason, string message, Exception inner)
        : base(message, inner)
    {
        Reason = reason;
    }

    /// <summary>An asset, assignment, requisition, transfer or lookup that is not there (or belongs to another tenant).</summary>
    public static AssetsWorkflowException NotFound(string message)
        => new(AssetsFailureReason.NotFound, message);

    /// <summary>The record is not in a state that permits the requested action.</summary>
    public static AssetsWorkflowException InvalidState(string message)
        => new(AssetsFailureReason.InvalidState, message);

    /// <summary>The request collides with a record that already exists.</summary>
    public static AssetsWorkflowException Conflict(string message)
        => new(AssetsFailureReason.Conflict, message);

    /// <summary>The payload is wrong on its own terms — an impossible combination, a value out of range.</summary>
    public static AssetsWorkflowException Invalid(string message)
        => new(AssetsFailureReason.Invalid, message);
}

/// <summary>Categories of asset rule violation, and the status each maps to.</summary>
public enum AssetsFailureReason
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

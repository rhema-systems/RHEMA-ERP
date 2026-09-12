namespace ErpSystem.Core.Exceptions;

/// <summary>Why an area-17/18 request was refused, so the API layer need not parse text.</summary>
public enum JobArchitectureFailureReason
{
    /// <summary>The record is not there, or belongs to another tenant.</summary>
    NotFound,

    /// <summary>The record exists but is not in a state that permits the action.</summary>
    InvalidState,

    /// <summary>The request collides with a record that already exists.</summary>
    Conflict,

    /// <summary>The payload itself is wrong.</summary>
    Invalid
}

/// <summary>
/// Thrown by the job-description, job-architecture, competency and manpower-budget services when a
/// request is refused for a reason the caller can act on.
/// </summary>
/// <remarks>
/// <para>Same contract as <see cref="ProbationWorkflowException"/> and for the same measured
/// reason: <c>GlobalExceptionHandlingMiddleware</c> replaces the detail of <b>both</b>
/// <see cref="ArgumentException"/> and <see cref="InvalidOperationException"/> with a fixed string
/// — "Invalid argument provided." and "The operation is not valid for the current state of the
/// object." These three services raised nothing else. Across them that silenced <b>76 throws</b>,
/// including every one of these, which are exactly what a user needs to read:</para>
///
/// <list type="bullet">
///   <item>"Only draft job descriptions can be submitted for review."</item>
///   <item>"Cannot update an approved job description. Create a new version instead."</item>
///   <item>"Only submitted budgets can be approved."</item>
///   <item>"A competency with code 'X' already exists."</item>
///   <item>"An assessment record already exists for this employee–competency pair. Use the update
///         operation to record a re-assessment."</item>
/// </list>
///
/// <para>Every one of those fired correctly and then said nothing, and a "not found" was
/// indistinguishable from a malformed payload — both arrived as a 400 reading "Invalid argument
/// provided." A rule that fires correctly but cannot explain itself is still a defect.</para>
///
/// <para>⚠ <see cref="Exception.Message"/> is surfaced to the caller verbatim, so every message
/// must be safe to display and must say what to change.</para>
/// </remarks>
public sealed class JobArchitectureException : Exception
{
    /// <summary>Classifies the refusal so the API layer can choose a status code without parsing text.</summary>
    public JobArchitectureFailureReason Reason { get; }

    public JobArchitectureException(JobArchitectureFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public JobArchitectureException(JobArchitectureFailureReason reason, string message, Exception inner)
        : base(message, inner)
    {
        Reason = reason;
    }

    /// <summary>A job description, competency, budget or taxonomy row that is not there.</summary>
    public static JobArchitectureException NotFound(string message)
        => new(JobArchitectureFailureReason.NotFound, message);

    /// <summary>The record is not in a state that permits the requested action.</summary>
    public static JobArchitectureException InvalidState(string message)
        => new(JobArchitectureFailureReason.InvalidState, message);

    /// <summary>The request collides with a record that already exists.</summary>
    public static JobArchitectureException Conflict(string message)
        => new(JobArchitectureFailureReason.Conflict, message);

    /// <summary>The payload itself is wrong.</summary>
    public static JobArchitectureException Invalid(string message)
        => new(JobArchitectureFailureReason.Invalid, message);
}

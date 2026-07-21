namespace ErpSystem.Core.Exceptions;

/// <summary>
/// Thrown by the medical services when a claim/pre-authorization workflow action is
/// rejected by domain rules (insurance limits, network coverage, claim state).
///
/// The API layer maps this to an HTTP response via <see cref="Reason"/>:
///   • NotFound             → 404
///   • everything else      → 422 Unprocessable Entity
///
/// The exception message is surfaced to the client, so it must be safe to display.
/// </summary>
public sealed class MedicalWorkflowException : Exception
{
    /// <summary>Classifies the rejection so the API layer can choose a status code without parsing text.</summary>
    public MedicalWorkflowFailureReason Reason { get; }

    public MedicalWorkflowException(MedicalWorkflowFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public MedicalWorkflowException(MedicalWorkflowFailureReason reason, string message, Exception inner)
        : base(message, inner)
    {
        Reason = reason;
    }
}

/// <summary>Categories of medical workflow rule violations.</summary>
public enum MedicalWorkflowFailureReason
{
    /// <summary>A referenced policy/dependent record no longer exists. Map to HTTP 404.</summary>
    NotFound,

    /// <summary>The claim is not in a state that permits the requested action. Map to HTTP 422.</summary>
    InvalidState,

    /// <summary>The approved amount would exceed the available insurance limit. Map to HTTP 422.</summary>
    LimitExceeded,

    /// <summary>The chosen facility is not in the insurer's provider network. Map to HTTP 422.</summary>
    FacilityNotInNetwork,

    /// <summary>The policy is not active and cannot absorb new utilization. Map to HTTP 422.</summary>
    PolicyInactive,

    /// <summary>The dependent on the claim is not actively covered under the policy. Map to HTTP 422.</summary>
    DependentNotCovered,
}

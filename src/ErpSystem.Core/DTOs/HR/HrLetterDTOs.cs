using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// HR LETTER REQUESTS — area 25 slice 12b (decision D7)
// ============================================================================

/// <summary>What the employee is asking for, and why.</summary>
public sealed class CreateHrLetterRequestDto
{
    [Required]
    public HrLetterType LetterType { get; set; }

    /// <summary>
    /// Required. HR is being asked to make a statement to somebody outside the organisation —
    /// what it is for decides whether to issue it at all and how to word it.
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Purpose { get; set; } = string.Empty;

    /// <summary>Null falls back to "To whom it may concern", which is a real, common case.</summary>
    [MaxLength(300)]
    public string? AddressedTo { get; set; }
}

/// <summary>HR's refusal. The comment is required — the employee reads it back.</summary>
public sealed class RejectHrLetterRequestDto
{
    [MaxLength(1000)]
    public string? Comments { get; set; }
}

public sealed class HrLetterRequestDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public HrLetterType LetterType { get; set; }
    public string LetterTypeName => LetterType.ToString();

    public string Purpose { get; set; } = string.Empty;
    public string? AddressedTo { get; set; }

    public HrLetterRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime RequestedAt { get; set; }

    public Guid? IssuedById { get; set; }
    public string? IssuedByName { get; set; }
    public DateTime? IssuedAt { get; set; }
    public string? LetterNumber { get; set; }

    public string? DecisionComments { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>True once there is something to hand over, by either route.</summary>
    public bool HasDocument { get; set; }

    /// <summary>
    /// How it was fulfilled: <c>Generated</c> (a frozen document rendered from the template) or
    /// <c>Uploaded</c> (a signed scan). Null while it is still pending. The screens use this to
    /// decide between "View letter" and "Download file".
    /// </summary>
    public string? Fulfilment { get; set; }

    /// <summary>Set only for the uploaded route.</summary>
    public string? FileName { get; set; }
}

/// <summary>The rendered letter — the frozen document for an issued request, or a preview.</summary>
public sealed class HrLetterDocumentDto
{
    public Guid RequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string? LetterNumber { get; set; }
    public HrLetterType LetterType { get; set; }
    public string LetterTypeName => LetterType.ToString();
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The letter itself, as HTML. For an issued request this is the copy FROZEN at issue, not a
    /// re-render — a letter given to a bank must still say what it said on the day.
    /// </summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>False for a preview, which is rendered live and has not been issued.</summary>
    public bool IsIssued { get; set; }

    public DateTime? IssuedAt { get; set; }
    public string? IssuedByName { get; set; }
}

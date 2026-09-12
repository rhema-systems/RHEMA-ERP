using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

#region Oath of Secrecy (FR-HR-030)

public class EmployeeOathOfSecrecyDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public OathAdministrationMethod Method { get; set; }
    public string MethodName => Method.ToString();

    /// <summary>The exact wording sworn, as it stood at the time.</summary>
    public string OathText { get; set; } = string.Empty;

    public DateOnly SwornOn { get; set; }
    public DateTime RecordedAt { get; set; }

    public Guid? WitnessedById { get; set; }
    public string? WitnessedByName { get; set; }

    public Guid RecordedById { get; set; }
    public string RecordedByName { get; set; } = string.Empty;

    /// <summary>
    /// Whether a tamper-detection signature exists — true only for an oath affirmed in the system.
    /// </summary>
    /// <remarks>The hash itself is never returned; publishing it would defeat its purpose.</remarks>
    public bool HasSignature { get; set; }

    /// <summary>The scan, once one has been uploaded through the controlled gate.</summary>
    public Guid? FileUploadRecordId { get; set; }
    public Guid? DocumentRecordId { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public bool HasScan => FileUploadRecordId.HasValue;

    public string? Notes { get; set; }
}

/// <summary>
/// The employee's own affirmation.
/// </summary>
/// <remarks>
/// ⚠ There is deliberately no employee id here. The person swearing is the person on the token, and
/// the date is stamped by the server: you cannot swear on someone else's behalf, and you cannot
/// back-date your own oath.
/// </remarks>
public class AffirmOathOfSecrecyDto
{
    /// <summary>Optional override of the wording. Omit to use the standard text.</summary>
    [MaxLength(4000)]
    public string? OathText { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>HR recording an oath sworn on paper.</summary>
public class RecordAdministeredOathDto
{
    // ⚠ [Required] on a non-nullable Guid does NOT reject an omitted value — it binds to
    // Guid.Empty and passes validation. The service therefore checks both explicitly; the
    // attributes stay for documentation and for the generated API schema.
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>Required: "sworn before" is what makes it an oath rather than a note.</summary>
    [Required]
    public Guid WitnessedById { get; set; }

    [Required]
    public DateOnly SwornOn { get; set; }

    [MaxLength(4000)]
    public string? OathText { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

// ⚠ There is deliberately no file field on either write DTO — not a path, and not an upload id
// either. The scan is attached afterwards through POST {id}/scan, which runs the controlled gate
// and registers the document centrally. A caller-supplied id would be a second way in that skipped
// both, and "the id must have come from the gate" is not something a DTO can assert.

/// <summary>An employee who has no oath on record — the operational view FR-HR-030 needs.</summary>
public class OathOutstandingEmployeeDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public DateOnly? DateEmployed { get; set; }

    /// <summary>Still on probation — the cohort onboarding is actually about.</summary>
    public bool IsOnProbation { get; set; }
}

#endregion

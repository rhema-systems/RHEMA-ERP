using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// =============================================================================
// AREA 9b — SEPARATION, CLEARANCE & EXIT (slice 1: the record and the register)
// =============================================================================

/// <summary>One row of the exit register.</summary>
public class EmployeeSeparationListDto
{
    public Guid Id { get; set; }
    public string SeparationNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? PositionTitle { get; set; }
    public string? OrganizationUnitName { get; set; }

    public EmployeeTerminationType SeparationType { get; set; }
    public string SeparationTypeName { get; set; } = string.Empty;

    public SeparationStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public DateOnly InitiatedOn { get; set; }
    public DateOnly? LastWorkingDay { get; set; }
    public DateOnly? EffectiveDate { get; set; }

    public bool IsProcedural { get; set; }
    public bool IsSystemInitiated { get; set; }

    /// <summary>True when this separation came out of a disciplinary case.</summary>
    public bool IsDisciplinary { get; set; }

    /// <summary>
    /// True when the outcome has been applied to the employee's master record. False on a
    /// completed separation is the 29-orphan defect, visible without a join.
    /// </summary>
    public bool EmployeeRecordUpdated { get; set; }
}

/// <summary>A separation in full.</summary>
public class EmployeeSeparationDetailDto : EmployeeSeparationListDto
{
    public TerminationReason? ReasonCategory { get; set; }
    public string? ReasonCategoryName { get; set; }
    public string? ReasonNotes { get; set; }

    public DateOnly? NoticeGivenOn { get; set; }
    public int? NoticeDays { get; set; }

    /// <summary>
    /// Notice the separation type calls for — the value on the record, which was defaulted from
    /// tenant policy when it was raised.
    /// </summary>
    public int NoticeRequiredDays { get; set; }

    /// <summary>
    /// Notice actually served: <c>NoticeGivenOn</c> to <c>LastWorkingDay</c>. Null while either
    /// date is unknown.
    /// </summary>
    public int? NoticeServedDays { get; set; }

    /// <summary>
    /// How much notice was not served — what FR-HR-184's notice pay is computed from in slice 5.
    /// </summary>
    /// <remarks>
    /// Derived on read, never stored. Its three inputs are all persisted and stop being editable
    /// once the separation leaves Draft, so storing the answer as well would only create something
    /// that can drift from them.
    /// </remarks>
    public int? NoticeShortfallDays { get; set; }

    public DateTime? SubmittedOn { get; set; }
    public Guid? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }

    public Guid? InitiatedById { get; set; }
    public string? InitiatedByName { get; set; }

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedOn { get; set; }
    public string? ApprovalNotes { get; set; }
    public Guid? WorkflowInstanceId { get; set; }

    public bool IsEligibleForRehire { get; set; }
    public DateOnly? EligibleForRehireDate { get; set; }
    public string? RehireRestrictions { get; set; }

    public Guid? DisciplinaryActionId { get; set; }
    public DateTime? EmployeeRecordUpdatedOn { get; set; }

    public DateTime? CancelledOn { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Raise a separation. Status is deliberately absent — a separation always starts as a draft, and
/// every move from there is an endpoint with its own rule and its own gate.
/// </summary>
public class CreateEmployeeSeparationDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public EmployeeTerminationType SeparationType { get; set; }

    public TerminationReason? ReasonCategory { get; set; }

    [MaxLength(2000)]
    public string? ReasonNotes { get; set; }

    /// <summary>Defaults to today when omitted. Never in the future.</summary>
    public DateOnly? InitiatedOn { get; set; }

    public DateOnly? NoticeGivenOn { get; set; }

    /// <summary>
    /// Omit to take the tenant default from <c>CompanyHrPolicySettings</c> — 30 days for a
    /// resignation, 30 for a termination. Supply a value only to override it for this separation.
    /// </summary>
    public int? NoticeDays { get; set; }

    public DateOnly? LastWorkingDay { get; set; }

    /// <summary>
    /// The intended last day of employment. Confirmed at approval; for compulsory retirement the
    /// service computes it from the birthday and refuses a contradicting value (FR-HR-093).
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }

    public bool IsEligibleForRehire { get; set; } = true;

    public DateOnly? EligibleForRehireDate { get; set; }

    [MaxLength(1000)]
    public string? RehireRestrictions { get; set; }

    /// <summary>
    /// Set when a disciplinary outcome is raising this separation, linking back to the action that
    /// decided it. Area 9 supplies this; a human raising a separation by hand does not.
    /// </summary>
    public Guid? DisciplinaryActionId { get; set; }
}

/// <summary>
/// Amend a draft separation. Anything already decided — approval, clearance, settlement — is not
/// here: those move through their own endpoints so each keeps its rule.
/// </summary>
public class UpdateEmployeeSeparationDto
{
    public EmployeeTerminationType? SeparationType { get; set; }
    public TerminationReason? ReasonCategory { get; set; }

    [MaxLength(2000)]
    public string? ReasonNotes { get; set; }

    public DateOnly? NoticeGivenOn { get; set; }
    public int? NoticeDays { get; set; }
    public DateOnly? LastWorkingDay { get; set; }
    public DateOnly? EffectiveDate { get; set; }

    public bool? IsEligibleForRehire { get; set; }
    public DateOnly? EligibleForRehireDate { get; set; }

    [MaxLength(1000)]
    public string? RehireRestrictions { get; set; }
}

/// <summary>Withdraw a separation before it completes — a resignation retracted, a retirement deferred.</summary>
public class CancelEmployeeSeparationDto
{
    /// <summary>Why the separation is being withdrawn. Required — the service enforces it.</summary>
    /// <remarks>
    /// ⚠ Deliberately no <c>[Required]</c>. It would fire first, in ModelState, and answer with the
    /// framework's "One or more validation errors occurred." — which tells the person staring at
    /// the screen nothing about what to do. The service refuses a blank reason with a sentence that
    /// does, and that keeps every refusal on this controller one shape: <c>{ "message": "…" }</c>.
    /// </remarks>
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Submit a draft separation into the approval queue. Carries no dates: everything it needs is
/// already on the record, and a payload that could restate them would be a second way to set the
/// facts the settlement is computed from.
/// </summary>
public class SubmitEmployeeSeparationDto
{
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>A file attached to a separation.</summary>
public class EmployeeSeparationDocumentDto
{
    public Guid Id { get; set; }
    public Guid SeparationId { get; set; }

    public SeparationDocumentCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime UploadedOn { get; set; }
    public Guid? UploadedById { get; set; }
    public string? UploadedByName { get; set; }

    /// <summary>True once the file is registered in the central document repository.</summary>
    public bool IsRegisteredInDms { get; set; }

    // Deliberately absent: FilePath. The stored location is not something a client needs, and not
    // something an exit register should hand out — the file is served by the download endpoint,
    // which re-checks entitlement. Size and content type are absent too: they live on the
    // FileUploadRecord the gate created, and duplicating them here would need a migration to say
    // what a join already knows.
}

/// <summary>Filters for the exit register.</summary>
public class EmployeeSeparationQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;

    public Guid? EmployeeId { get; set; }
    public EmployeeTerminationType? SeparationType { get; set; }
    public SeparationStatus? Status { get; set; }

    /// <summary>Restrict to separations effective on or after this date.</summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>Restrict to separations effective on or before this date.</summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>Employee name, employee number or separation number.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// When true, return only separations that have completed without the employee's master record
    /// being updated — the 29-orphan defect as a query.
    /// </summary>
    public bool? OnlyUnappliedToEmployee { get; set; }
}

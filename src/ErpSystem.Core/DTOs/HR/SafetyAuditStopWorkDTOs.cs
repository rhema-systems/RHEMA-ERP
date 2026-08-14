using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE — AUDITS, STOP-WORK AUTHORITY & STATUTORY SUBMISSIONS (slice 15)
// Domains: T. SHE Audit Management (FRD §12 / FR-SHE-229)
//          U. Stop-Work Authority (FR-SHE-200)
//          V. Statutory Incident Submissions (FR-SHE-103)
// ============================================================================

#region SHE Audits

public class SheAuditDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string AuditNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SheAuditType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Standard { get; set; }
    public string? Scope { get; set; }
    public string? Objectives { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid LeadAuditorId { get; set; }
    public string LeadAuditorName { get; set; } = string.Empty;
    public string? ExternalAuditorName { get; set; }
    public string? ExternalAuditorOrganization { get; set; }
    public DateTime PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public SheAuditStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Summary { get; set; }
    public string? ReportDocumentPath { get; set; }
    public DateTime? ReportIssuedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
    public string? ClosureNotes { get; set; }

    public List<SheAuditTeamMemberDto> TeamMembers { get; set; } = new();
    public List<SheAuditFindingDto> Findings { get; set; } = new();
}

public class SheAuditSummaryDto
{
    public Guid Id { get; set; }
    public string AuditNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SheAuditType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Standard { get; set; }
    public string? LocationName { get; set; }
    public string LeadAuditorName { get; set; } = string.Empty;
    public DateTime PlannedStartDate { get; set; }
    public SheAuditStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int FindingCount { get; set; }
    public int OpenFindingCount { get; set; }
}

public class CreateSheAuditDto : CreateDtoBase
{
    /// <summary>Optional — server-assigned AUD-YYYY-NNNN when blank.</summary>
    [MaxLength(30)]
    public string? AuditNumber { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public SheAuditType Type { get; set; }

    [MaxLength(200)]
    public string? Standard { get; set; }

    [MaxLength(1000)]
    public string? Scope { get; set; }

    [MaxLength(1000)]
    public string? Objectives { get; set; }

    public Guid? LocationId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public Guid LeadAuditorId { get; set; }

    [MaxLength(200)]
    public string? ExternalAuditorName { get; set; }

    [MaxLength(200)]
    public string? ExternalAuditorOrganization { get; set; }

    [Required]
    public DateTime PlannedStartDate { get; set; }

    public DateTime? PlannedEndDate { get; set; }
}

/// <summary>Planning fields only — lifecycle moves through the start/issue-report/close/cancel actions.</summary>
public class UpdateSheAuditDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public SheAuditType Type { get; set; }

    [MaxLength(200)]
    public string? Standard { get; set; }

    [MaxLength(1000)]
    public string? Scope { get; set; }

    [MaxLength(1000)]
    public string? Objectives { get; set; }

    public Guid? LocationId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public Guid LeadAuditorId { get; set; }

    [MaxLength(200)]
    public string? ExternalAuditorName { get; set; }

    [MaxLength(200)]
    public string? ExternalAuditorOrganization { get; set; }

    [Required]
    public DateTime PlannedStartDate { get; set; }

    public DateTime? PlannedEndDate { get; set; }
}

public class StartSheAuditDto
{
    [Required]
    public Guid AuditId { get; set; }

    public DateTime ActualStartDate { get; set; } = DateTime.UtcNow;
}

public class IssueSheAuditReportDto
{
    [Required]
    public Guid AuditId { get; set; }

    [Required, MaxLength(4000)]
    public string Summary { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ReportDocumentPath { get; set; }

    public DateTime ReportIssuedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ActualEndDate { get; set; }
}

public class CloseSheAuditDto
{
    [Required]
    public Guid AuditId { get; set; }

    [Required]
    public Guid ClosedById { get; set; }

    public DateTime ClosedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? ClosureNotes { get; set; }
}

public class CancelSheAuditDto
{
    [Required]
    public Guid AuditId { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public class SheAuditTeamMemberDto : BaseDto
{
    public Guid AuditId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class CreateSheAuditTeamMemberDto : CreateDtoBase
{
    [Required]
    public Guid AuditId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;
}

public class SheAuditFindingDto : BaseDto
{
    public Guid AuditId { get; set; }
    public string? AuditNumber { get; set; }
    public int FindingNumber { get; set; }
    public SheAuditFindingClassification Classification { get; set; }
    public string ClassificationName => Classification.ToString();
    public string? ClauseReference { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public SheAuditFindingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ResponsiblePersonId { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public DateTime? DueDate { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public string? VerificationNotes { get; set; }

    public List<SheAuditFindingActionDto> Actions { get; set; } = new();
}

public class CreateSheAuditFindingDto : CreateDtoBase
{
    [Required]
    public Guid AuditId { get; set; }

    [Required]
    public SheAuditFindingClassification Classification { get; set; }

    [MaxLength(100)]
    public string? ClauseReference { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Evidence { get; set; }

    public Guid? ResponsiblePersonId { get; set; }
    public DateTime? DueDate { get; set; }
}

public class UpdateSheAuditFindingDto : UpdateDtoBase
{
    [Required]
    public SheAuditFindingClassification Classification { get; set; }

    [MaxLength(100)]
    public string? ClauseReference { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Evidence { get; set; }

    public Guid? ResponsiblePersonId { get; set; }
    public DateTime? DueDate { get; set; }

    [MaxLength(2000)]
    public string? ResolutionNotes { get; set; }

    public DateTime? ResolvedDate { get; set; }
}

/// <summary>Marks the finding's resolution as verified for effectiveness.</summary>
public class VerifySheAuditFindingDto
{
    [Required]
    public Guid FindingId { get; set; }

    [Required]
    public Guid VerifiedById { get; set; }

    public DateTime VerifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? VerificationNotes { get; set; }
}

public class SheAuditFindingActionDto : BaseDto
{
    public Guid FindingId { get; set; }
    public Guid CorrectiveActionTemplateId { get; set; }
    public string CorrectiveActionTemplateTitle { get; set; } = string.Empty;
    public SheCorrectiveActionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
}

public class CreateSheAuditFindingActionDto : CreateDtoBase
{
    [Required]
    public Guid FindingId { get; set; }

    [Required]
    public Guid CorrectiveActionTemplateId { get; set; }

    public SheCorrectiveActionStatus Status { get; set; } = SheCorrectiveActionStatus.Pending;
    public DateTime? DueDate { get; set; }
    public Guid? AssignedToId { get; set; }
}

public class UpdateSheAuditFindingActionDto : UpdateDtoBase
{
    [Required]
    public SheCorrectiveActionStatus Status { get; set; }

    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }

    public Guid? AssignedToId { get; set; }
}

#endregion

#region Stop-work authority

public class SheStopWorkOrderDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid RaisedById { get; set; }
    public string RaisedByName { get; set; } = string.Empty;
    public DateTime RaisedDate { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? SpecificArea { get; set; }
    public string WorkDescription { get; set; } = string.Empty;
    public string ReasonDescription { get; set; } = string.Empty;
    public string? ImmediateActionsTaken { get; set; }
    public Guid? PermitToWorkId { get; set; }
    public string? PermitNumber { get; set; }
    public Guid? HazardId { get; set; }
    public string? HazardName { get; set; }
    public Guid? IncidentId { get; set; }
    public string? IncidentNumber { get; set; }
    public SheStopWorkStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? RoutedToId { get; set; }
    public string? RoutedToName { get; set; }
    public DateTime? RoutedDate { get; set; }
    public string? ResolutionDescription { get; set; }
    public Guid? ResolvedById { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public Guid? ClearedById { get; set; }
    public string? ClearedByName { get; set; }
    public DateTime? ClearedDate { get; set; }
    public string? ClearanceNotes { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledDate { get; set; }
    public string? CancellationReason { get; set; }
}

/// <summary>
/// Raising is open to every employee (stop-work authority): the server takes the
/// raiser from the token for non-HR callers, ignoring any body-supplied id.
/// </summary>
public class CreateSheStopWorkOrderDto : CreateDtoBase
{
    /// <summary>Ignored for non-HR callers — the token's employee raises the order.</summary>
    public Guid? RaisedById { get; set; }

    public DateTime RaisedDate { get; set; } = DateTime.UtcNow;

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Required, MaxLength(1000)]
    public string WorkDescription { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string ReasonDescription { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ImmediateActionsTaken { get; set; }

    public Guid? PermitToWorkId { get; set; }
    public Guid? HazardId { get; set; }
    public Guid? IncidentId { get; set; }
}

public class RouteSheStopWorkOrderDto
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public Guid RoutedToId { get; set; }

    public DateTime RoutedDate { get; set; } = DateTime.UtcNow;
}

public class ResolveSheStopWorkOrderDto
{
    [Required]
    public Guid OrderId { get; set; }

    [Required, MaxLength(2000)]
    public string ResolutionDescription { get; set; } = string.Empty;

    [Required]
    public Guid ResolvedById { get; set; }

    public DateTime ResolvedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Authorises resumption of the stopped work. Only a resolved order clears.</summary>
public class ClearSheStopWorkOrderDto
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public Guid ClearedById { get; set; }

    public DateTime ClearedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? ClearanceNotes { get; set; }
}

public class CancelSheStopWorkOrderDto
{
    [Required]
    public Guid OrderId { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

#endregion

#region Statutory incident submissions

public class SheStatutoryIncidentSubmissionDto : BaseDto
{
    public Guid IncidentId { get; set; }
    public string? IncidentNumber { get; set; }
    public Guid RegulatoryBodyId { get; set; }
    public string RegulatoryBodyName { get; set; } = string.Empty;
    public SheStatutorySubmissionType Type { get; set; }
    public string TypeName => Type.ToString();
    public SheStatutorySubmissionMethod Method { get; set; }
    public string MethodName => Method.ToString();
    public DateTime SubmissionDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid SubmittedById { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public string? DocumentPath { get; set; }
    public bool AcknowledgementReceived { get; set; }
    public DateTime? AcknowledgementDate { get; set; }
    public string? AcknowledgementReference { get; set; }
    public string? Notes { get; set; }
}

public class CreateSheStatutoryIncidentSubmissionDto : CreateDtoBase
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required]
    public Guid RegulatoryBodyId { get; set; }

    [Required]
    public SheStatutorySubmissionType Type { get; set; }

    [Required]
    public SheStatutorySubmissionMethod Method { get; set; }

    public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [Required]
    public Guid SubmittedById { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>Records the authority's acknowledgement (or corrects submission detail).</summary>
public class UpdateSheStatutoryIncidentSubmissionDto : UpdateDtoBase
{
    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public bool AcknowledgementReceived { get; set; }
    public DateTime? AcknowledgementDate { get; set; }

    [MaxLength(100)]
    public string? AcknowledgementReference { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

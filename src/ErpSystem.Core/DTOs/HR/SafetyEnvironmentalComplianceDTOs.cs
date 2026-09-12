using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SLICE 17 — PART D ENVIRONMENTAL CORE DTOs
// Domains: X. Environmental Permit & Licence Register (FR-ENV-017–019)
//          Y. Environmental Monitoring Schedules (FR-ENV-023–024)
//          Z. Regulatory Updates Register (FR-ENV-030–032 / FR-SHE-182)
//          AA. Sustainability Initiatives (FR-ENV-028–029)
//          AB. Environmental Compliance Reviews & Clearance (FR-ENV-001–016)
//          AC. Monthly Environmental Reports (FR-ENV-033–034)
// ============================================================================

// ============================================================================
// X. ENVIRONMENTAL PERMIT & LICENCE REGISTER
// ============================================================================

#region Environmental Permit

public class SheEnvironmentalPermitDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RegisterNumber { get; set; } = string.Empty;
    public string PermitName { get; set; } = string.Empty;
    public SheEnvironmentalPermitType PermitType { get; set; }
    public string PermitTypeName => PermitType.ToString();
    public string? AuthorityReferenceNumber { get; set; }
    public Guid? IssuingBodyId { get; set; }
    public string? IssuingBodyName { get; set; }
    public Guid ResponsibleOfficerId { get; set; }
    public string ResponsibleOfficerName { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? Description { get; set; }
    public string? Conditions { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int? RenewalPeriodMonths { get; set; }
    public SheEnvironmentalPermitStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? DocumentRecordId { get; set; }
    public string? CurrentVersionLabel { get; set; }
    public DateTime? LastRenewedDate { get; set; }
    public Guid? LastRenewedById { get; set; }
    public string? LastRenewedByName { get; set; }
    public string? Notes { get; set; }

    /// <summary>Permit document versions on the central DMS, newest first.</summary>
    public List<SheControlledDocumentVersionDto> Versions { get; set; } = new();
}

public class SheEnvironmentalPermitSummaryDto
{
    public Guid Id { get; set; }
    public string RegisterNumber { get; set; } = string.Empty;
    public string PermitName { get; set; } = string.Empty;
    public SheEnvironmentalPermitType PermitType { get; set; }
    public string PermitTypeName => PermitType.ToString();
    public string? AuthorityReferenceNumber { get; set; }
    public string? IssuingBodyName { get; set; }
    public string ResponsibleOfficerName { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public SheEnvironmentalPermitStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? CurrentVersionLabel { get; set; }
    /// <summary>Negative when the permit is past expiry — drives the FR-ENV-019 red status.</summary>
    public int DaysToExpiry { get; set; }
}

public class CreateSheEnvironmentalPermitDto : CreateDtoBase
{
    /// <summary>Optional — left blank, the server assigns the next number in sequence.</summary>
    [MaxLength(30)]
    public string? RegisterNumber { get; set; }

    [Required, MaxLength(300)]
    public string PermitName { get; set; } = string.Empty;

    [Required]
    public SheEnvironmentalPermitType PermitType { get; set; }

    [MaxLength(100)]
    public string? AuthorityReferenceNumber { get; set; }

    public Guid? IssuingBodyId { get; set; }

    [Required]
    public Guid ResponsibleOfficerId { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? Conditions { get; set; }

    [Required]
    public DateTime IssueDate { get; set; }

    [Required]
    public DateTime ExpiryDate { get; set; }

    [Range(1, 600)]
    public int? RenewalPeriodMonths { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateSheEnvironmentalPermitDto : UpdateDtoBase
{
    [Required, MaxLength(300)]
    public string PermitName { get; set; } = string.Empty;

    [Required]
    public SheEnvironmentalPermitType PermitType { get; set; }

    [MaxLength(100)]
    public string? AuthorityReferenceNumber { get; set; }

    public Guid? IssuingBodyId { get; set; }

    [Required]
    public Guid ResponsibleOfficerId { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? Conditions { get; set; }

    [Range(1, 600)]
    public int? RenewalPeriodMonths { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>Renews a permit: fresh issue/expiry dates, back to Active.</summary>
public class RenewSheEnvironmentalPermitDto
{
    [Required]
    public DateTime NewIssueDate { get; set; }

    [Required]
    public DateTime NewExpiryDate { get; set; }

    [MaxLength(100)]
    public string? NewAuthorityReferenceNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// Y. ENVIRONMENTAL MONITORING SCHEDULES
// ============================================================================

#region Monitoring Schedule

public class SheEnvironmentalMonitoringScheduleDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;
    public SheEnvironmentalMonitoringType MonitoringType { get; set; }
    public string MonitoringTypeName => MonitoringType.ToString();
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? MonitoringPoint { get; set; }
    public string? Description { get; set; }
    public int FrequencyDays { get; set; }
    public DateTime NextDueDate { get; set; }
    public DateTime? LastPerformedDate { get; set; }
    public Guid? ResponsibleOfficerId { get; set; }
    public string? ResponsibleOfficerName { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Negative when the cycle is overdue.</summary>
    public int DaysToDue { get; set; }
}

public class CreateSheEnvironmentalMonitoringScheduleDto : CreateDtoBase
{
    /// <summary>Optional — left blank, the server assigns the next number in sequence.</summary>
    [MaxLength(30)]
    public string? ScheduleNumber { get; set; }

    [Required]
    public SheEnvironmentalMonitoringType MonitoringType { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? MonitoringPoint { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, Range(1, 3660)]
    public int FrequencyDays { get; set; }

    [Required]
    public DateTime NextDueDate { get; set; }

    public Guid? ResponsibleOfficerId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSheEnvironmentalMonitoringScheduleDto : UpdateDtoBase
{
    [Required]
    public SheEnvironmentalMonitoringType MonitoringType { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? MonitoringPoint { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, Range(1, 3660)]
    public int FrequencyDays { get; set; }

    [Required]
    public DateTime NextDueDate { get; set; }

    public Guid? ResponsibleOfficerId { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Records a completed monitoring cycle: stamps LastPerformedDate, advances
/// NextDueDate by the schedule's interval, and optionally links the monitoring
/// record that evidences the activity (FR-ENV-024).
/// </summary>
public class CompleteSheMonitoringScheduleDto
{
    public DateTime PerformedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Optional — the monitoring record holding the readings/evidence.</summary>
    public Guid? MonitoringRecordId { get; set; }
}

#endregion

// ============================================================================
// Z. REGULATORY UPDATES REGISTER
// ============================================================================

#region Regulatory Update

public class SheRegulatoryUpdateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string UpdateNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? RegulationReference { get; set; }
    public Guid? RegulatoryBodyId { get; set; }
    public string? RegulatoryBodyName { get; set; }
    public string? AuthorityName { get; set; }
    public SheRegulatoryDomain Domain { get; set; }
    public string DomainName => Domain.ToString();
    public string Summary { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? AffectedDepartments { get; set; }
    public DateTime? ComplianceDeadline { get; set; }
    public SheRegulatoryUpdateRiskLevel RiskLevel { get; set; }
    public string RiskLevelName => RiskLevel.ToString();
    public string? RequiredActions { get; set; }
    public SheRegulatoryUpdateStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public SheComplianceStatus ComplianceStatus { get; set; }
    public string ComplianceStatusName => ComplianceStatus.ToString();
    public DateTime? ReviewDate { get; set; }
    public string? OfficerComments { get; set; }
    public Guid? LinkedObligationId { get; set; }
    public string? LinkedObligationCode { get; set; }
    public DateTime? ManagementNotifiedAt { get; set; }
    public Guid? ManagementNotifiedById { get; set; }
    public string? ManagementNotifiedByName { get; set; }
    public Guid RecordedById { get; set; }
    public string RecordedByName { get; set; } = string.Empty;
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
    public string? ClosureNotes { get; set; }
}

public class SheRegulatoryUpdateSummaryDto
{
    public Guid Id { get; set; }
    public string UpdateNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? RegulationReference { get; set; }
    public string? AuthorityName { get; set; }
    public SheRegulatoryDomain Domain { get; set; }
    public string DomainName => Domain.ToString();
    public DateTime IssueDate { get; set; }
    public DateTime? ComplianceDeadline { get; set; }
    public SheRegulatoryUpdateRiskLevel RiskLevel { get; set; }
    public string RiskLevelName => RiskLevel.ToString();
    public SheRegulatoryUpdateStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public SheComplianceStatus ComplianceStatus { get; set; }
    public string ComplianceStatusName => ComplianceStatus.ToString();
    public bool ManagementNotified { get; set; }
}

public class CreateSheRegulatoryUpdateDto : CreateDtoBase
{
    /// <summary>Optional — left blank, the server assigns the next number in sequence.</summary>
    [MaxLength(30)]
    public string? UpdateNumber { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? RegulationReference { get; set; }

    public Guid? RegulatoryBodyId { get; set; }

    [MaxLength(200)]
    public string? AuthorityName { get; set; }

    [Required]
    public SheRegulatoryDomain Domain { get; set; }

    [Required, MaxLength(2000)]
    public string Summary { get; set; } = string.Empty;

    [Required]
    public DateTime IssueDate { get; set; }

    public DateTime? EffectiveDate { get; set; }

    [MaxLength(500)]
    public string? AffectedDepartments { get; set; }

    public DateTime? ComplianceDeadline { get; set; }

    [Required]
    public SheRegulatoryUpdateRiskLevel RiskLevel { get; set; }

    [MaxLength(2000)]
    public string? RequiredActions { get; set; }

    public DateTime? ReviewDate { get; set; }

    [MaxLength(1000)]
    public string? OfficerComments { get; set; }

    public Guid? LinkedObligationId { get; set; }
}

public class UpdateSheRegulatoryUpdateDto : UpdateDtoBase
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? RegulationReference { get; set; }

    public Guid? RegulatoryBodyId { get; set; }

    [MaxLength(200)]
    public string? AuthorityName { get; set; }

    [Required]
    public SheRegulatoryDomain Domain { get; set; }

    [Required, MaxLength(2000)]
    public string Summary { get; set; } = string.Empty;

    [Required]
    public DateTime IssueDate { get; set; }

    public DateTime? EffectiveDate { get; set; }

    [MaxLength(500)]
    public string? AffectedDepartments { get; set; }

    public DateTime? ComplianceDeadline { get; set; }

    [Required]
    public SheRegulatoryUpdateRiskLevel RiskLevel { get; set; }

    [MaxLength(2000)]
    public string? RequiredActions { get; set; }

    [Required]
    public SheRegulatoryUpdateStatus Status { get; set; }

    [Required]
    public SheComplianceStatus ComplianceStatus { get; set; }

    public DateTime? ReviewDate { get; set; }

    [MaxLength(1000)]
    public string? OfficerComments { get; set; }

    public Guid? LinkedObligationId { get; set; }
}

/// <summary>Closes a regulatory update to compliance (FR-ENV acceptance: tracked to closure).</summary>
public class CloseSheRegulatoryUpdateDto
{
    public SheComplianceStatus ComplianceStatus { get; set; } = SheComplianceStatus.Compliant;

    [MaxLength(1000)]
    public string? ClosureNotes { get; set; }
}

#endregion

// ============================================================================
// AA. SUSTAINABILITY INITIATIVES
// ============================================================================

#region Sustainability Initiative

public class SheSustainabilityInitiativeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string InitiativeNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SheSustainabilityCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public string? Description { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SheSustainabilityStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal? TargetValue { get; set; }
    public decimal? ActualValue { get; set; }
    public string? MeasurementUnit { get; set; }
    public decimal? EstimatedCostSavings { get; set; }
    public string? Notes { get; set; }
}

public class CreateSheSustainabilityInitiativeDto : CreateDtoBase
{
    /// <summary>Optional — left blank, the server assigns the next number in sequence.</summary>
    [MaxLength(30)]
    public string? InitiativeNumber { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public SheSustainabilityCategory Category { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? LocationId { get; set; }

    public Guid? OwnerId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public SheSustainabilityStatus Status { get; set; } = SheSustainabilityStatus.Planned;

    [Range(0, double.MaxValue)]
    public decimal? TargetValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualValue { get; set; }

    [MaxLength(50)]
    public string? MeasurementUnit { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCostSavings { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateSheSustainabilityInitiativeDto : UpdateDtoBase
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public SheSustainabilityCategory Category { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? LocationId { get; set; }

    public Guid? OwnerId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Required]
    public SheSustainabilityStatus Status { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? TargetValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualValue { get; set; }

    [MaxLength(50)]
    public string? MeasurementUnit { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCostSavings { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>Per-category rollup for the FR-ENV-029 KPI strip.</summary>
public class SheSustainabilityCategorySummaryDto
{
    public SheSustainabilityCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public int Initiatives { get; set; }
    public int Completed { get; set; }
    public decimal TargetTotal { get; set; }
    public decimal ActualTotal { get; set; }
    public decimal CostSavings { get; set; }
}

/// <summary>The FR-ENV-029 sustainability KPI rollup (optionally scoped to a year).</summary>
public class SheSustainabilityKpiDto
{
    public int? Year { get; set; }
    public int ActiveInitiatives { get; set; }
    public int CompletedInitiatives { get; set; }
    public decimal TotalCostSavings { get; set; }
    public List<SheSustainabilityCategorySummaryDto> Categories { get; set; } = new();
}

#endregion

// ============================================================================
// AB. ENVIRONMENTAL COMPLIANCE REVIEWS & CLEARANCE
// ============================================================================

#region Environmental Review

public class SheEnvironmentalReviewActionDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime ActionDate { get; set; }
}

public class SheEnvironmentalReviewDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ReviewNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public SheEnvironmentalWorkClassification WorkClassification { get; set; }
    public string WorkClassificationName => WorkClassification.ToString();
    public string? ProjectReference { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? ResponsibleManagerId { get; set; }
    public string? ResponsibleManagerName { get; set; }
    public Guid SubmittedById { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ApplicableLaws { get; set; }
    public bool PermitRequired { get; set; }
    public string? ComplianceChecklist { get; set; }

    public bool RequiresRegistration { get; set; }
    public bool RequiresEnvironmentalPermit { get; set; }
    public bool RequiresFullEia { get; set; }
    public bool RequiresRiskAssessment { get; set; }
    public bool RequiresEpaSubmission { get; set; }
    public bool RequiresManagementApproval { get; set; }
    public string? ScreeningNotes { get; set; }
    public DateTime? ScreeningCompletedDate { get; set; }
    public Guid? ScreenedById { get; set; }
    public string? ScreenedByName { get; set; }

    public SheEnvironmentalReviewStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? OfficerComments { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }

    public DateTime? ManagementApprovedDate { get; set; }
    public Guid? ManagementApprovedById { get; set; }
    public string? ManagementApprovedByName { get; set; }

    public DateTime? EpaSubmissionDate { get; set; }
    public string? EpaSubmissionReference { get; set; }

    public DateTime? ClearanceIssuedDate { get; set; }
    public Guid? ClearanceIssuedById { get; set; }
    public string? ClearanceIssuedByName { get; set; }

    public DateTime? CommencementApprovedDate { get; set; }
    public Guid? CommencementApprovedById { get; set; }
    public string? CommencementApprovedByName { get; set; }

    public string? Notes { get; set; }

    /// <summary>The FR-ENV-011 audit trail, newest first.</summary>
    public List<SheEnvironmentalReviewActionDto> Actions { get; set; } = new();
}

public class SheEnvironmentalReviewSummaryDto
{
    public Guid Id { get; set; }
    public string ReviewNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public SheEnvironmentalWorkClassification WorkClassification { get; set; }
    public string WorkClassificationName => WorkClassification.ToString();
    public string? OrganizationUnitName { get; set; }
    public DateTime SubmittedDate { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public SheEnvironmentalReviewStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool PermitRequired { get; set; }
    public bool RequiresManagementApproval { get; set; }
    public bool ClearanceIssued { get; set; }
}

public class CreateSheEnvironmentalReviewDto : CreateDtoBase
{
    /// <summary>Optional — left blank, the server assigns the next number in sequence.</summary>
    [MaxLength(30)]
    public string? ReviewNumber { get; set; }

    [Required, MaxLength(300)]
    public string ProjectName { get; set; } = string.Empty;

    [Required]
    public SheEnvironmentalWorkClassification WorkClassification { get; set; }

    [MaxLength(200)]
    public string? ProjectReference { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public Guid? ResponsibleManagerId { get; set; }

    public DateTime? PlannedStartDate { get; set; }

    [Required, MaxLength(3000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ApplicableLaws { get; set; }

    public bool PermitRequired { get; set; }

    [MaxLength(2000)]
    public string? ComplianceChecklist { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>Pre-decision edits only — refused once the review is decided.</summary>
public class UpdateSheEnvironmentalReviewDto : UpdateDtoBase
{
    [Required, MaxLength(300)]
    public string ProjectName { get; set; } = string.Empty;

    [Required]
    public SheEnvironmentalWorkClassification WorkClassification { get; set; }

    [MaxLength(200)]
    public string? ProjectReference { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public Guid? ResponsibleManagerId { get; set; }

    public DateTime? PlannedStartDate { get; set; }

    [Required, MaxLength(3000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ApplicableLaws { get; set; }

    public bool PermitRequired { get; set; }

    [MaxLength(2000)]
    public string? ComplianceChecklist { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>The FR-ENV-009 screening determination.</summary>
public class SheEnvironmentalScreeningDto
{
    public bool RequiresRegistration { get; set; }
    public bool RequiresEnvironmentalPermit { get; set; }
    public bool RequiresFullEia { get; set; }
    public bool RequiresRiskAssessment { get; set; }
    public bool RequiresEpaSubmission { get; set; }
    public bool RequiresManagementApproval { get; set; }

    [MaxLength(2000)]
    public string? ScreeningNotes { get; set; }
}

public class SheEnvironmentalReviewDecisionDto
{
    [MaxLength(2000)]
    public string? Comments { get; set; }
}

public class RequestSheReviewCorrectionsDto
{
    [Required, MaxLength(2000)]
    public string Comments { get; set; } = string.Empty;
}

public class RecordSheEpaSubmissionDto
{
    public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>The FR-ENV-016 Environmental Clearance Report — assembled, never stored.</summary>
public class SheEnvironmentalClearanceReportDto
{
    public SheEnvironmentalReviewDto Review { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
}

#endregion

// ============================================================================
// AC. MONTHLY ENVIRONMENTAL REPORTS
// ============================================================================

#region Monthly Environmental Report

public class SheMonthlyEnvironmentalReportDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ReportNumber { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }
    public Guid? GeneratedById { get; set; }
    public string? GeneratedByName { get; set; }

    public int ObligationsTotal { get; set; }
    public int ObligationsCompliant { get; set; }
    public decimal? CompliancePercentage { get; set; }

    public int PermitsActive { get; set; }
    public int PermitsExpiringIn90Days { get; set; }
    public int PermitsExpired { get; set; }

    public int ProjectsReviewed { get; set; }
    public int ClearancesIssued { get; set; }

    public decimal WasteGeneratedKg { get; set; }
    public decimal WasteRecycledKg { get; set; }
    public decimal? WasteRecyclingRate { get; set; }

    public int EnvironmentalIncidents { get; set; }
    public int EnvironmentalIncidentsClosed { get; set; }
    public int MonitoringExceedances { get; set; }

    public int AuditFindingsRaised { get; set; }
    public int CorrectiveActionsOpen { get; set; }

    public int NewRegulatoryUpdates { get; set; }
    public int SustainabilityInitiativesActive { get; set; }
    public int SustainabilityInitiativesCompleted { get; set; }
    public decimal SustainabilityCostSavings { get; set; }

    public string? OfficerSummary { get; set; }

    public DateTime? SubmittedToManagementAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
}

public class SheMonthlyEnvironmentalReportSummaryDto
{
    public Guid Id { get; set; }
    public string ReportNumber { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime GeneratedAt { get; set; }
    public decimal? CompliancePercentage { get; set; }
    public int PermitsExpired { get; set; }
    public int EnvironmentalIncidents { get; set; }
    public DateTime? SubmittedToManagementAt { get; set; }
}

public class GenerateSheMonthlyReportDto
{
    [Required, Range(2000, 2200)]
    public int Year { get; set; }

    [Required, Range(1, 12)]
    public int Month { get; set; }
}

/// <summary>Edits the officer narrative — refused once the report is submitted.</summary>
public class UpdateSheMonthlyReportSummaryDto
{
    [MaxLength(3000)]
    public string? OfficerSummary { get; set; }
}

#endregion

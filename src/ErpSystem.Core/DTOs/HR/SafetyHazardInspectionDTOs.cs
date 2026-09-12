using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE — HAZARD REGISTER, RISK ASSESSMENT & INSPECTION DTOs
// Domains: C. Hazard Register & Formal Risk Assessment   D. Safety Inspections & Audits
// ============================================================================

// ============================================================================
// C. HAZARD REGISTER
// ============================================================================

#region Hazard

public class SheHazardDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public SheHazardCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public string Description { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? SpecificArea { get; set; }

    public int InherentLikelihood { get; set; }
    public int InherentSeverity { get; set; }
    public int InherentRiskScore { get; set; }
    public int ResidualLikelihood { get; set; }
    public int ResidualSeverity { get; set; }
    public int ResidualRiskScore { get; set; }
    public SheHazardRiskLevel ResidualRiskLevel { get; set; }
    public string ResidualRiskLevelName => ResidualRiskLevel.ToString();

    public SheHazardStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? ReviewDueDate { get; set; }
    public DateTime? LastReviewedDate { get; set; }
    public Guid? LastReviewedById { get; set; }
    public string? LastReviewedByName { get; set; }
    public Guid? ReportedById { get; set; }
    public string? ReportedByName { get; set; }
    public DateTime? ReportedDate { get; set; }
    public bool IsActive { get; set; }

    public List<SheHazardControlDto> Controls { get; set; } = new();
    public List<SheHazardCorrectiveActionDto> CorrectiveActions { get; set; } = new();
}

public class SheHazardSummaryDto
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public SheHazardCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public string? LocationName { get; set; }
    public int InherentRiskScore { get; set; }
    public int ResidualRiskScore { get; set; }
    public SheHazardRiskLevel ResidualRiskLevel { get; set; }
    public string ResidualRiskLevelName => ResidualRiskLevel.ToString();
    public SheHazardStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? OwnerName { get; set; }
    public string? ReportedByName { get; set; }
    public DateTime? ReviewDueDate { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSheHazardDto : CreateDtoBase
{
    [MaxLength(30)]
    public string? Code { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public SheHazardCategory Category { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Range(1, 5)]
    public int InherentLikelihood { get; set; }

    [Range(1, 5)]
    public int InherentSeverity { get; set; }

    [Range(1, 5)]
    public int ResidualLikelihood { get; set; }

    [Range(1, 5)]
    public int ResidualSeverity { get; set; }

    public Guid? OwnerId { get; set; }
    public DateTime? ReviewDueDate { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Honoured only for a SHE Write holder (the desk recording on behalf); everyone
    /// else is stamped with the token's employee by the controller.</summary>
    public Guid? ReportedById { get; set; }
}

public class UpdateSheHazardDto : UpdateDtoBase
{
    [MaxLength(30)]
    public string? Code { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public SheHazardCategory Category { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Range(1, 5)]
    public int InherentLikelihood { get; set; }

    [Range(1, 5)]
    public int InherentSeverity { get; set; }

    [Range(1, 5)]
    public int ResidualLikelihood { get; set; }

    [Range(1, 5)]
    public int ResidualSeverity { get; set; }

    [Required]
    public SheHazardStatus Status { get; set; }

    public Guid? OwnerId { get; set; }
    public DateTime? ReviewDueDate { get; set; }
    public DateTime? LastReviewedDate { get; set; }
    public Guid? LastReviewedById { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SheHazardControlDto : BaseDto
{
    public Guid HazardId { get; set; }
    public SheHierarchyOfControl ControlLevel { get; set; }
    public string ControlLevelName => ControlLevel.ToString();
    public string ControlDescription { get; set; } = string.Empty;
    public SheControlStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ResponsiblePersonId { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public DateTime? ImplementationDate { get; set; }
    public DateTime? ReviewDate { get; set; }
}

public class CreateSheHazardControlDto : CreateDtoBase
{
    [Required]
    public Guid HazardId { get; set; }

    [Required]
    public SheHierarchyOfControl ControlLevel { get; set; }

    [Required, MaxLength(500)]
    public string ControlDescription { get; set; } = string.Empty;

    [Required]
    public SheControlStatus Status { get; set; }

    public Guid? ResponsiblePersonId { get; set; }
    public DateTime? ImplementationDate { get; set; }
    public DateTime? ReviewDate { get; set; }
}

public class UpdateSheHazardControlDto : UpdateDtoBase
{
    [Required]
    public SheHierarchyOfControl ControlLevel { get; set; }

    [Required, MaxLength(500)]
    public string ControlDescription { get; set; } = string.Empty;

    [Required]
    public SheControlStatus Status { get; set; }

    public Guid? ResponsiblePersonId { get; set; }
    public DateTime? ImplementationDate { get; set; }
    public DateTime? ReviewDate { get; set; }
}

public class SheHazardCorrectiveActionDto : BaseDto
{
    public Guid HazardId { get; set; }
    public Guid CorrectiveActionTemplateId { get; set; }
    public string CorrectiveActionTemplateTitle { get; set; } = string.Empty;
    public int? DeadlineDays { get; set; }
    public bool IsMandatory { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateSheHazardCorrectiveActionDto : CreateDtoBase
{
    [Required]
    public Guid HazardId { get; set; }

    [Required]
    public Guid CorrectiveActionTemplateId { get; set; }

    public int? DeadlineDays { get; set; }
    public bool IsMandatory { get; set; } = true;
    public int DisplayOrder { get; set; }
}

#endregion

#region Risk Assessment

public class SheRiskAssessmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string AssessmentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SheRiskAssessmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Scope { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? SpecificActivity { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public SheRiskAssessmentStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public Guid PreparedById { get; set; }
    public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedDate { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }

    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public int Version { get; set; }
    public string? DocumentPath { get; set; }

    public List<SheRiskAssessmentHazardDto> AssessedHazards { get; set; } = new();
    public List<SheRiskAssessmentAcknowledgementDto> Acknowledgements { get; set; } = new();
}

public class SheRiskAssessmentSummaryDto
{
    public Guid Id { get; set; }
    public string AssessmentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SheRiskAssessmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public SheRiskAssessmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? LocationName { get; set; }
    public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public int Version { get; set; }
    public int HazardCount { get; set; }
}

public class CreateSheRiskAssessmentDto : CreateDtoBase
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public SheRiskAssessmentType Type { get; set; }

    [MaxLength(1000)]
    public string? Scope { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificActivity { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public Guid PreparedById { get; set; }

    public DateTime PreparedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? NextReviewDate { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}

public class UpdateSheRiskAssessmentDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public SheRiskAssessmentType Type { get; set; }

    [MaxLength(1000)]
    public string? Scope { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificActivity { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public SheRiskAssessmentStatus Status { get; set; }

    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? NextReviewDate { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}

/// <summary>Review/approve transition for a risk assessment.</summary>
public class ApproveSheRiskAssessmentDto
{
    [Required]
    public Guid RiskAssessmentId { get; set; }

    [Required]
    public Guid ApprovedById { get; set; }

    public DateTime ApprovedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? NextReviewDate { get; set; }
}

public class SheRiskAssessmentHazardDto : BaseDto
{
    public Guid RiskAssessmentId { get; set; }
    public Guid? HazardId { get; set; }
    public int ItemNumber { get; set; }
    public string HazardDescription { get; set; } = string.Empty;
    public string? PotentialConsequences { get; set; }
    public string? AffectedPersons { get; set; }
    public int InherentLikelihood { get; set; }
    public int InherentSeverity { get; set; }
    public int InherentRiskScore { get; set; }
    public SheRiskLevel InherentRiskLevel { get; set; }
    public string InherentRiskLevelName => InherentRiskLevel.ToString();
    public string? ControlMeasures { get; set; }
    public int ResidualLikelihood { get; set; }
    public int ResidualSeverity { get; set; }
    public int ResidualRiskScore { get; set; }
    public SheRiskLevel ResidualRiskLevel { get; set; }
    public string ResidualRiskLevelName => ResidualRiskLevel.ToString();
    public string? ResponsiblePerson { get; set; }
    public DateTime? TargetDate { get; set; }
}

public class CreateSheRiskAssessmentHazardDto : CreateDtoBase
{
    [Required]
    public Guid RiskAssessmentId { get; set; }

    public Guid? HazardId { get; set; }
    public int ItemNumber { get; set; }

    [Required, MaxLength(300)]
    public string HazardDescription { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? PotentialConsequences { get; set; }

    [MaxLength(300)]
    public string? AffectedPersons { get; set; }

    [Range(1, 5)]
    public int InherentLikelihood { get; set; }

    [Range(1, 5)]
    public int InherentSeverity { get; set; }

    [MaxLength(2000)]
    public string? ControlMeasures { get; set; }

    [Range(1, 5)]
    public int ResidualLikelihood { get; set; }

    [Range(1, 5)]
    public int ResidualSeverity { get; set; }

    [MaxLength(200)]
    public string? ResponsiblePerson { get; set; }

    public DateTime? TargetDate { get; set; }
}

public class UpdateSheRiskAssessmentHazardDto : UpdateDtoBase
{
    public int ItemNumber { get; set; }

    [Required, MaxLength(300)]
    public string HazardDescription { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? PotentialConsequences { get; set; }

    [MaxLength(300)]
    public string? AffectedPersons { get; set; }

    [Range(1, 5)]
    public int InherentLikelihood { get; set; }

    [Range(1, 5)]
    public int InherentSeverity { get; set; }

    [MaxLength(2000)]
    public string? ControlMeasures { get; set; }

    [Range(1, 5)]
    public int ResidualLikelihood { get; set; }

    [Range(1, 5)]
    public int ResidualSeverity { get; set; }

    [MaxLength(200)]
    public string? ResponsiblePerson { get; set; }

    public DateTime? TargetDate { get; set; }
}

public class SheRiskAssessmentAcknowledgementDto : BaseDto
{
    public Guid RiskAssessmentId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime AcknowledgedDate { get; set; }
    public string? SignaturePath { get; set; }
    public string? Comments { get; set; }
}

public class CreateSheRiskAssessmentAcknowledgementDto : CreateDtoBase
{
    [Required]
    public Guid RiskAssessmentId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public DateTime AcknowledgedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? SignaturePath { get; set; }

    [MaxLength(500)]
    public string? Comments { get; set; }
}

/// <summary>Self-service view: an active risk assessment with this employee's acknowledgement status.</summary>
public class MyRiskAcknowledgementDto
{
    public Guid RiskAssessmentId { get; set; }
    public string AssessmentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SheRiskAssessmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? LocationName { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool AcknowledgedByMe { get; set; }
    public DateTime? AcknowledgedDate { get; set; }
}

#endregion

// ============================================================================
// D. SAFETY INSPECTIONS & AUDITS
// ============================================================================

#region Inspection Checklist

public class SheInspectionChecklistDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ChecklistNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SheInspectionType Type { get; set; }
    public string TypeName => Type.ToString();
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public List<SheInspectionChecklistItemDto> Items { get; set; } = new();
}

public class CreateSheInspectionChecklistDto : CreateDtoBase
{
    [Required, MaxLength(30)]
    public string ChecklistNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public SheInspectionType Type { get; set; }

    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class UpdateSheInspectionChecklistDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public SheInspectionType Type { get; set; }

    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class SheInspectionChecklistItemDto : BaseDto
{
    public Guid ChecklistId { get; set; }
    public int ItemOrder { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string? RegulatoryReference { get; set; }
    public SheRiskLevel? AssociatedRiskLevel { get; set; }
    public string? AssociatedRiskLevelName => AssociatedRiskLevel?.ToString();
}

public class CreateSheInspectionChecklistItemDto : CreateDtoBase
{
    [Required]
    public Guid ChecklistId { get; set; }

    public int ItemOrder { get; set; }

    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;

    public bool IsMandatory { get; set; }

    [MaxLength(200)]
    public string? RegulatoryReference { get; set; }

    public SheRiskLevel? AssociatedRiskLevel { get; set; }
}

public class UpdateSheInspectionChecklistItemDto : UpdateDtoBase
{
    public int ItemOrder { get; set; }

    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;

    public bool IsMandatory { get; set; }

    [MaxLength(200)]
    public string? RegulatoryReference { get; set; }

    public SheRiskLevel? AssociatedRiskLevel { get; set; }
}

#endregion

#region Safety Inspection

public class SafetyInspectionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string InspectionNumber { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? SpecificArea { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public SheInspectionType Type { get; set; }
    public string TypeName => Type.ToString();
    public SheInspectionCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public Guid? ChecklistId { get; set; }
    public string? ChecklistName { get; set; }
    public Guid InspectorId { get; set; }
    public string InspectorName { get; set; } = string.Empty;
    public string? ExternalInspectorName { get; set; }
    public string? ExternalInspectorOrganization { get; set; }
    public string? FindingsAndObservations { get; set; }
    public string? RecommendedActions { get; set; }
    public string? PositiveObservations { get; set; }
    public SheInspectionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public SheRiskLevel? OverallRiskRating { get; set; }
    public string? OverallRiskRatingName => OverallRiskRating?.ToString();
    public int? ComplianceScore { get; set; }
    public DateTime? ComplianceDeadline { get; set; }
    public DateTime? NextInspectionDueDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }

    public List<SafetyInspectionItemDto> Items { get; set; } = new();
    public List<SafetyInspectionHazardDto> Hazards { get; set; } = new();
    public List<SafetyInspectionDocumentDto> Documents { get; set; } = new();
}

public class SafetyInspectionSummaryDto
{
    public Guid Id { get; set; }
    public string InspectionNumber { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public SheInspectionType Type { get; set; }
    public string TypeName => Type.ToString();
    public SheInspectionCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public string? LocationName { get; set; }
    public string InspectorName { get; set; } = string.Empty;
    public SheInspectionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public SheRiskLevel? OverallRiskRating { get; set; }
    public int? ComplianceScore { get; set; }
    public DateTime? NextInspectionDueDate { get; set; }
    public int OpenItemCount { get; set; }
}

public class CreateSafetyInspectionDto : CreateDtoBase
{
    [Required]
    public DateTime InspectionDate { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public SheInspectionType Type { get; set; }

    [Required]
    public SheInspectionCategory Category { get; set; }

    public Guid? ChecklistId { get; set; }

    [Required]
    public Guid InspectorId { get; set; }

    [MaxLength(200)]
    public string? ExternalInspectorName { get; set; }

    [MaxLength(200)]
    public string? ExternalInspectorOrganization { get; set; }

    public DateTime? NextInspectionDueDate { get; set; }
}

public class UpdateSafetyInspectionDto : UpdateDtoBase
{
    [Required]
    public DateTime InspectionDate { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public SheInspectionType Type { get; set; }

    [Required]
    public SheInspectionCategory Category { get; set; }

    public Guid? ChecklistId { get; set; }

    [Required]
    public Guid InspectorId { get; set; }

    [MaxLength(200)]
    public string? ExternalInspectorName { get; set; }

    [MaxLength(200)]
    public string? ExternalInspectorOrganization { get; set; }

    [MaxLength(2000)]
    public string? FindingsAndObservations { get; set; }

    [MaxLength(2000)]
    public string? RecommendedActions { get; set; }

    [MaxLength(1000)]
    public string? PositiveObservations { get; set; }

    [Required]
    public SheInspectionStatus Status { get; set; }

    public SheRiskLevel? OverallRiskRating { get; set; }

    [Range(0, 100)]
    public int? ComplianceScore { get; set; }

    public DateTime? ComplianceDeadline { get; set; }
    public DateTime? NextInspectionDueDate { get; set; }
}

/// <summary>Closes an inspection.</summary>
public class CloseSafetyInspectionDto
{
    [Required]
    public Guid InspectionId { get; set; }

    [Required]
    public Guid ClosedById { get; set; }

    public DateTime ClosedDate { get; set; } = DateTime.UtcNow;
}

public class SafetyInspectionItemDto : BaseDto
{
    public Guid InspectionId { get; set; }
    public Guid? ChecklistItemId { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public SheComplianceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? DeficiencyNoted { get; set; }
    public string? ActionRequired { get; set; }
    public SheRiskLevel? RiskLevel { get; set; }
    public string? RiskLevelName => RiskLevel?.ToString();
    public DateTime? TargetDate { get; set; }
    public Guid? ResponsiblePersonId { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? ResolutionNotes { get; set; }
    public Guid? ResolvedById { get; set; }
    public string? ResolvedByName { get; set; }
}

public class CreateSafetyInspectionItemDto : CreateDtoBase
{
    [Required]
    public Guid InspectionId { get; set; }

    public Guid? ChecklistItemId { get; set; }

    [Required, MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;

    [Required]
    public SheComplianceStatus Status { get; set; }

    [MaxLength(1000)]
    public string? DeficiencyNoted { get; set; }

    [MaxLength(500)]
    public string? ActionRequired { get; set; }

    public SheRiskLevel? RiskLevel { get; set; }
    public DateTime? TargetDate { get; set; }
    public Guid? ResponsiblePersonId { get; set; }
}

public class UpdateSafetyInspectionItemDto : UpdateDtoBase
{
    [Required, MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;

    [Required]
    public SheComplianceStatus Status { get; set; }

    [MaxLength(1000)]
    public string? DeficiencyNoted { get; set; }

    [MaxLength(500)]
    public string? ActionRequired { get; set; }

    public SheRiskLevel? RiskLevel { get; set; }
    public DateTime? TargetDate { get; set; }
    public Guid? ResponsiblePersonId { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedDate { get; set; }

    [MaxLength(500)]
    public string? ResolutionNotes { get; set; }

    public Guid? ResolvedById { get; set; }
}

public class SafetyInspectionHazardDto : BaseDto
{
    public Guid InspectionId { get; set; }
    public Guid? HazardId { get; set; }
    public string HazardDescription { get; set; } = string.Empty;
    public SheHazardStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public SheHazardRiskLevel InitialRiskLevel { get; set; }
    public string InitialRiskLevelName => InitialRiskLevel.ToString();
    public SheHazardRiskLevel ResidualRiskLevel { get; set; }
    public string ResidualRiskLevelName => ResidualRiskLevel.ToString();
    public DateTime? ReviewDueDate { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public List<SafetyInspectionHazardActionDto> Actions { get; set; } = new();
}

public class CreateSafetyInspectionHazardDto : CreateDtoBase
{
    [Required]
    public Guid InspectionId { get; set; }

    public Guid? HazardId { get; set; }

    [Required, MaxLength(500)]
    public string HazardDescription { get; set; } = string.Empty;

    [Required]
    public SheHazardStatus Status { get; set; }

    [Required]
    public SheHazardRiskLevel InitialRiskLevel { get; set; }

    [Required]
    public SheHazardRiskLevel ResidualRiskLevel { get; set; }

    public DateTime? ReviewDueDate { get; set; }
    public Guid? OwnerId { get; set; }
}

public class UpdateSafetyInspectionHazardDto : UpdateDtoBase
{
    public Guid? HazardId { get; set; }

    [Required, MaxLength(500)]
    public string HazardDescription { get; set; } = string.Empty;

    [Required]
    public SheHazardStatus Status { get; set; }

    [Required]
    public SheHazardRiskLevel InitialRiskLevel { get; set; }

    [Required]
    public SheHazardRiskLevel ResidualRiskLevel { get; set; }

    public DateTime? ReviewDueDate { get; set; }
    public Guid? OwnerId { get; set; }
}

public class SafetyInspectionHazardActionDto : BaseDto
{
    public Guid InspectionHazardId { get; set; }
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

public class CreateSafetyInspectionHazardActionDto : CreateDtoBase
{
    [Required]
    public Guid InspectionHazardId { get; set; }

    [Required]
    public Guid CorrectiveActionTemplateId { get; set; }

    [Required]
    public SheCorrectiveActionStatus Status { get; set; }

    public DateTime? DueDate { get; set; }
    public Guid? AssignedToId { get; set; }
}

public class UpdateSafetyInspectionHazardActionDto : UpdateDtoBase
{
    [Required]
    public SheCorrectiveActionStatus Status { get; set; }

    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }

    public Guid? AssignedToId { get; set; }
}

public class SafetyInspectionDocumentDto : BaseDto
{
    public Guid InspectionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class CreateSafetyInspectionDocumentDto : CreateDtoBase
{
    [Required]
    public Guid InspectionId { get; set; }

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public Guid UploadedById { get; set; }
}

#endregion

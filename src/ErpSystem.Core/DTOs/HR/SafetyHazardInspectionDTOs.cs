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

// The builder: docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md. A template is header fields +
// sections of items (+ critical Yes/No sections) + scoring mode + outcomes + signatories. Structure is
// writable only in Draft; the service refuses (422) structure writes on Published / Retired rows.

public class SheInspectionChecklistFieldDto : BaseDto
{
    public Guid ChecklistId { get; set; }
    public int DisplayOrder { get; set; }
    public string Label { get; set; } = string.Empty;
    public SheChecklistFieldType FieldType { get; set; }
    public string FieldTypeName => FieldType.ToString();
    public bool IsRequired { get; set; }
    public string? ChoiceOptions { get; set; }
    public string? HelpText { get; set; }
    /// <summary>ChoiceOptions split on '|', trimmed, blanks dropped.</summary>
    public List<string> Choices => SheChecklistChoiceOptions.Split(ChoiceOptions);
}

public static class SheChecklistChoiceOptions
{
    public static List<string> Split(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public class CreateSheInspectionChecklistFieldDto : CreateDtoBase
{
    [Required]
    public Guid ChecklistId { get; set; }
    public int DisplayOrder { get; set; }
    [Required, MaxLength(150)]
    public string Label { get; set; } = string.Empty;
    [Required]
    public SheChecklistFieldType FieldType { get; set; } = SheChecklistFieldType.Text;
    public bool IsRequired { get; set; }
    [MaxLength(1000)]
    public string? ChoiceOptions { get; set; }
    [MaxLength(300)]
    public string? HelpText { get; set; }
}

public class UpdateSheInspectionChecklistFieldDto : UpdateDtoBase
{
    public int DisplayOrder { get; set; }
    [Required, MaxLength(150)]
    public string Label { get; set; } = string.Empty;
    [Required]
    public SheChecklistFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    [MaxLength(1000)]
    public string? ChoiceOptions { get; set; }
    [MaxLength(300)]
    public string? HelpText { get; set; }
}

public class SheInspectionChecklistItemDto : BaseDto
{
    public Guid ChecklistId { get; set; }
    public Guid? SectionId { get; set; }
    public int ItemOrder { get; set; }
    /// <summary>Running number across the standard sections, as printed (1…N). 0 for critical-section items.</summary>
    public int ItemNumber { get; set; }
    public string? Category { get; set; }
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
    /// <summary>Required for templates built with the builder; the legacy flat path leaves it null.</summary>
    public Guid? SectionId { get; set; }
    public int ItemOrder { get; set; }
    [MaxLength(100)]
    public string? Category { get; set; }
    [Required, MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    [MaxLength(200)]
    public string? RegulatoryReference { get; set; }
    public SheRiskLevel? AssociatedRiskLevel { get; set; }
}

public class UpdateSheInspectionChecklistItemDto : UpdateDtoBase
{
    public Guid? SectionId { get; set; }
    public int ItemOrder { get; set; }
    [MaxLength(100)]
    public string? Category { get; set; }
    [Required, MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    [MaxLength(200)]
    public string? RegulatoryReference { get; set; }
    public SheRiskLevel? AssociatedRiskLevel { get; set; }
}

public class SheInspectionChecklistSectionDto : BaseDto
{
    public Guid ChecklistId { get; set; }
    public int DisplayOrder { get; set; }
    public string? Code { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SheChecklistSectionKind Kind { get; set; }
    public string KindName => Kind.ToString();
    public List<SheInspectionChecklistItemDto> Items { get; set; } = new();
}

public class CreateSheInspectionChecklistSectionDto : CreateDtoBase
{
    [Required]
    public Guid ChecklistId { get; set; }
    public int DisplayOrder { get; set; }
    [MaxLength(10)]
    public string? Code { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Description { get; set; }
    public SheChecklistSectionKind Kind { get; set; } = SheChecklistSectionKind.Standard;
}

public class UpdateSheInspectionChecklistSectionDto : UpdateDtoBase
{
    public int DisplayOrder { get; set; }
    [MaxLength(10)]
    public string? Code { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Description { get; set; }
    public SheChecklistSectionKind Kind { get; set; } = SheChecklistSectionKind.Standard;
}

public class SheInspectionChecklistOutcomeDto : BaseDto
{
    public Guid ChecklistId { get; set; }
    public int DisplayOrder { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? MinPercent { get; set; }
    public decimal? MaxPercent { get; set; }
    public int? ReinspectionWithinDays { get; set; }
    public bool IsDisqualifying { get; set; }
}

public class CreateSheInspectionChecklistOutcomeDto : CreateDtoBase
{
    [Required]
    public Guid ChecklistId { get; set; }
    public int DisplayOrder { get; set; }
    [Required, MaxLength(150)]
    public string Label { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Description { get; set; }
    [Range(0, 100)]
    public decimal? MinPercent { get; set; }
    [Range(0, 100)]
    public decimal? MaxPercent { get; set; }
    [Range(0, 3650)]
    public int? ReinspectionWithinDays { get; set; }
    public bool IsDisqualifying { get; set; }
}

public class UpdateSheInspectionChecklistOutcomeDto : UpdateDtoBase
{
    public int DisplayOrder { get; set; }
    [Required, MaxLength(150)]
    public string Label { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Description { get; set; }
    [Range(0, 100)]
    public decimal? MinPercent { get; set; }
    [Range(0, 100)]
    public decimal? MaxPercent { get; set; }
    [Range(0, 3650)]
    public int? ReinspectionWithinDays { get; set; }
    public bool IsDisqualifying { get; set; }
}

public class SheInspectionChecklistSignatoryDto : BaseDto
{
    public Guid ChecklistId { get; set; }
    public int DisplayOrder { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public SheChecklistSignatoryKind Kind { get; set; }
    public string KindName => Kind.ToString();
    public bool IsRequired { get; set; }
}

public class CreateSheInspectionChecklistSignatoryDto : CreateDtoBase
{
    [Required]
    public Guid ChecklistId { get; set; }
    public int DisplayOrder { get; set; }
    [Required, MaxLength(150)]
    public string RoleLabel { get; set; } = string.Empty;
    public SheChecklistSignatoryKind Kind { get; set; } = SheChecklistSignatoryKind.SystemUser;
    public bool IsRequired { get; set; }
}

public class UpdateSheInspectionChecklistSignatoryDto : UpdateDtoBase
{
    public int DisplayOrder { get; set; }
    [Required, MaxLength(150)]
    public string RoleLabel { get; set; } = string.Empty;
    public SheChecklistSignatoryKind Kind { get; set; } = SheChecklistSignatoryKind.SystemUser;
    public bool IsRequired { get; set; }
}

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
    public SheChecklistStatus Status { get; set; }
    public string StatusName => Status.ToString();
    /// <summary>True once published or retired — structure writes are refused.</summary>
    public bool IsStructureLocked => Status != SheChecklistStatus.Draft;
    public SheChecklistScoringMode ScoringMode { get; set; }
    public string ScoringModeName => ScoringMode.ToString();
    public bool AllowPartialCompliance { get; set; }
    public string? PrintTitle { get; set; }
    public string? PrintSubtitle { get; set; }
    public string? Instructions { get; set; }
    public string? CriticalSectionNote { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }
    public string? PublishedByName { get; set; }
    public DateTime? RetiredAt { get; set; }
    public Guid? PreviousVersionId { get; set; }
    /// <summary>Item count across all sections (list reads carry this without the structure).</summary>
    public int ItemCount { get; set; }
    public List<SheInspectionChecklistFieldDto> Fields { get; set; } = new();
    /// <summary>Ordered sections with their items. Legacy section-less items appear under a synthetic "General" section per category.</summary>
    public List<SheInspectionChecklistSectionDto> Sections { get; set; } = new();
    /// <summary>The flat item list in form order — kept for the pre-builder screens and harness.</summary>
    public List<SheInspectionChecklistItemDto> Items { get; set; } = new();
    public List<SheInspectionChecklistOutcomeDto> Outcomes { get; set; } = new();
    public List<SheInspectionChecklistSignatoryDto> Signatories { get; set; } = new();
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
    public SheChecklistScoringMode ScoringMode { get; set; } = SheChecklistScoringMode.CompliancePercentage;
    public bool AllowPartialCompliance { get; set; }
    [MaxLength(200)]
    public string? PrintTitle { get; set; }
    [MaxLength(200)]
    public string? PrintSubtitle { get; set; }
    [MaxLength(4000)]
    public string? Instructions { get; set; }
    [MaxLength(500)]
    public string? CriticalSectionNote { get; set; }
}

/// <summary>
/// On a locked (published / retired) template only Name, Description and IsActive are applied; a
/// change to any structural field (scoring mode, partial-compliance, print texts) is refused.
/// </summary>
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
    public SheChecklistScoringMode ScoringMode { get; set; } = SheChecklistScoringMode.CompliancePercentage;
    public bool AllowPartialCompliance { get; set; }
    [MaxLength(200)]
    public string? PrintTitle { get; set; }
    [MaxLength(200)]
    public string? PrintSubtitle { get; set; }
    [MaxLength(4000)]
    public string? Instructions { get; set; }
    [MaxLength(500)]
    public string? CriticalSectionNote { get; set; }
}

/// <summary>Replace-set ordering: the complete list of child ids in their new order. A missing or foreign id is refused.</summary>
public class SheChecklistReorderDto
{
    [Required]
    public List<Guid> OrderedIds { get; set; } = new();
}

#endregion

#region Checklist run (inspection side)

public class SafetyInspectionFieldValueDto : BaseDto
{
    public Guid InspectionId { get; set; }
    public Guid ChecklistFieldId { get; set; }
    public string Label { get; set; } = string.Empty;
    public SheChecklistFieldType FieldType { get; set; }
    public string FieldTypeName => FieldType.ToString();
    public string? ValueText { get; set; }
    public Guid? ValueReferenceId { get; set; }
    /// <summary>The resolved name for reference fields; ValueText otherwise.</summary>
    public string? ValueDisplay { get; set; }
}

public class SafetyInspectionFieldValueWriteDto
{
    [Required]
    public Guid ChecklistFieldId { get; set; }
    [MaxLength(2000)]
    public string? ValueText { get; set; }
    public Guid? ValueReferenceId { get; set; }
}

/// <summary>One item's answer in the bulk walk. Status is validated against the section kind and the template's partial-compliance setting.</summary>
public class SafetyInspectionResponseDto
{
    [Required]
    public Guid ItemId { get; set; }
    [Required]
    public SheComplianceStatus Status { get; set; }
    [MaxLength(1000)]
    public string? DeficiencyNoted { get; set; }
    [MaxLength(500)]
    public string? ActionRequired { get; set; }
    public SheRiskLevel? RiskLevel { get; set; }
}

public class ApplySafetyInspectionChecklistDto
{
    [Required]
    public Guid ChecklistId { get; set; }
}

public class CompleteSafetyInspectionDto
{
    /// <summary>Required in QualitativeRating mode; in CompliancePercentage mode defaults to the recommendation.</summary>
    public Guid? OutcomeId { get; set; }
    [MaxLength(500)]
    public string? OutcomeOverrideReason { get; set; }
    [MaxLength(2000)]
    public string? SubjectComments { get; set; }
    [MaxLength(2000)]
    public string? FindingsAndObservations { get; set; }
    [MaxLength(2000)]
    public string? RecommendedActions { get; set; }
    public SheRiskLevel? OverallRiskRating { get; set; }
    public DateTime? ComplianceDeadline { get; set; }
    public DateTime? NextInspectionDueDate { get; set; }
}

public class CreateSafetyInspectionSignatureDto
{
    [Required]
    public Guid ChecklistSignatoryId { get; set; }
    /// <summary>External signatories only — ignored for SystemUser rows, which sign as the token's employee.</summary>
    [MaxLength(200)]
    public string? SignedName { get; set; }
    public DateTime? SignedAt { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class SafetyInspectionSignatureDto : BaseDto
{
    public Guid InspectionId { get; set; }
    public Guid ChecklistSignatoryId { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public SheChecklistSignatoryKind Kind { get; set; }
    public string KindName => Kind.ToString();
    public Guid? SignedByEmployeeId { get; set; }
    public string? SignedByName { get; set; }
    public string SignedName { get; set; } = string.Empty;
    public DateTime SignedAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>The live computation over the current answers — what Complete will persist.</summary>
public class SafetyInspectionScoreDto
{
    public SheChecklistScoringMode ScoringMode { get; set; }
    public string ScoringModeName => ScoringMode.ToString();
    public int TotalItems { get; set; }
    public int TotalApplicableItems { get; set; }
    public int TotalCompliantItems { get; set; }
    public int TotalNonCompliantItems { get; set; }
    public int TotalPartiallyCompliantItems { get; set; }
    public int TotalNotApplicableItems { get; set; }
    public int TotalNotAssessedItems { get; set; }
    public int CriticalNonConformityCount { get; set; }
    public decimal? CompliancePercentage { get; set; }
    public bool IsDisqualified { get; set; }
    public Guid? RecommendedOutcomeId { get; set; }
    public string? RecommendedOutcomeLabel { get; set; }
    /// <summary>Every item assessed and every required header field filled.</summary>
    public bool IsReadyToComplete { get; set; }
    public List<string> MissingRequiredFields { get; set; } = new();
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

    // ── Checklist run (docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md) ──
    public string? ChecklistNumber { get; set; }
    public int? ChecklistVersion { get; set; }
    public SheChecklistScoringMode? ScoringMode { get; set; }
    public string? ScoringModeName => ScoringMode?.ToString();
    public int? TotalApplicableItems { get; set; }
    public int? TotalCompliantItems { get; set; }
    public int? TotalNonCompliantItems { get; set; }
    public int? TotalPartiallyCompliantItems { get; set; }
    public int? CriticalNonConformityCount { get; set; }
    public decimal? CompliancePercentage { get; set; }
    public Guid? RecommendedOutcomeId { get; set; }
    public string? RecommendedOutcomeLabel { get; set; }
    public Guid? OutcomeId { get; set; }
    public string? OutcomeLabel { get; set; }
    public bool? OutcomeIsDisqualifying { get; set; }
    public int? OutcomeReinspectionWithinDays { get; set; }
    public string? OutcomeOverrideReason { get; set; }
    public string? SubjectComments { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
    /// <summary>The pinned template with its structure — detail read only; null on free-form inspections.</summary>
    public SheInspectionChecklistDto? Checklist { get; set; }
    public List<SafetyInspectionFieldValueDto> FieldValues { get; set; } = new();
    public List<SafetyInspectionSignatureDto> Signatures { get; set; } = new();
    /// <summary>In form order (DisplayOrder, then creation) — materialised items first, hand-added findings after.</summary>
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
    /// <summary>Form position when materialised from a template; 0 for hand-added findings.</summary>
    public int DisplayOrder { get; set; }
    /// <summary>Printed running number across the standard sections; 0 for critical items and hand-added findings.</summary>
    public int ItemNumber { get; set; }
    public Guid? SectionId { get; set; }
    public string? SectionCode { get; set; }
    public string? SectionTitle { get; set; }
    public SheChecklistSectionKind? SectionKind { get; set; }
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

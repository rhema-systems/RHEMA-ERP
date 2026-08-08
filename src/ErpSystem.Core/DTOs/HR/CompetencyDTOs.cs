using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// COMPETENCY DTOs
// ============================================================================

#region Competency

/// <summary>
/// Full read model for a competency definition.
/// </summary>
public class CompetencyDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CompetencyCategory CompetencyCategory { get; set; }
    public string CompetencyCategoryName => CompetencyCategory.ToString();
    public int ProficiencyScaleMax { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Detailed competency read model including associated skill indicators
/// and summary counts of positions and employees using this competency.
/// </summary>
public class CompetencyDetailDto : CompetencyDto
{
    public List<CompetencySkillIndicatorDto> SkillIndicators { get; set; } = new();
    public int PositionCount { get; set; }
    public int EmployeeCount { get; set; }
}

/// <summary>
/// DTO for creating a new competency definition.
/// </summary>
public class CreateCompetencyDto : CreateDtoBase
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public CompetencyCategory CompetencyCategory { get; set; }

    [Range(1, 10)]
    public int ProficiencyScaleMax { get; set; } = 5;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an existing competency definition.
/// </summary>
public class UpdateCompetencyDto : UpdateDtoBase
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public CompetencyCategory CompetencyCategory { get; set; }

    [Range(1, 10)]
    public int ProficiencyScaleMax { get; set; }

    public bool IsActive { get; set; }
}

#endregion

// ============================================================================
// COMPETENCY SKILL INDICATOR DTOs
// ============================================================================

#region CompetencySkillIndicator

/// <summary>
/// Read model that maps a skill (at a minimum proficiency level) to the
/// competency it helps evidence.
/// </summary>
public class CompetencySkillIndicatorDto
{
    public Guid Id { get; set; }
    public Guid CompetencyId { get; set; }
    public string CompetencyName { get; set; } = string.Empty;
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string? SkillCategory { get; set; }
    public SkillLevel MinimumSkillLevelRequired { get; set; }
    public string MinimumSkillLevelRequiredName => MinimumSkillLevelRequired.ToString();
    public string? Rationale { get; set; }
}

/// <summary>
/// DTO for declaring that a skill (at a minimum level) provides evidence of a competency.
/// </summary>
public class CreateCompetencySkillIndicatorDto : CreateDtoBase
{
    [Required]
    public Guid CompetencyId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    [Required]
    public SkillLevel MinimumSkillLevelRequired { get; set; }

    [MaxLength(1000)]
    public string? Rationale { get; set; }
}

/// <summary>
/// DTO for updating the minimum skill level or rationale on an existing indicator.
/// The CompetencyId/SkillId pairing is immutable — delete and recreate to change it.
/// </summary>
public class UpdateCompetencySkillIndicatorDto : UpdateDtoBase
{
    [Required]
    public SkillLevel MinimumSkillLevelRequired { get; set; }

    [MaxLength(1000)]
    public string? Rationale { get; set; }
}

#endregion

// ============================================================================
// POSITION COMPETENCY DTOs
// ============================================================================

#region PositionCompetency

/// <summary>
/// Read model declaring the required proficiency level for a competency on a position.
/// </summary>
public class PositionCompetencyDto
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public Guid CompetencyId { get; set; }
    public string CompetencyCode { get; set; } = string.Empty;
    public string CompetencyName { get; set; } = string.Empty;
    public CompetencyCategory CompetencyCategory { get; set; }
    public string CompetencyCategoryName => CompetencyCategory.ToString();
    public int ProficiencyScaleMax { get; set; }
    public int RequiredProficiencyLevel { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for assigning a competency requirement to a position.
/// </summary>
public class CreatePositionCompetencyDto : CreateDtoBase
{
    [Required]
    public Guid PositionId { get; set; }

    [Required]
    public Guid CompetencyId { get; set; }

    [Required]
    [Range(1, 10)]
    public int RequiredProficiencyLevel { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for adjusting the required proficiency level or notes on a position competency.
/// The PositionId/CompetencyId pairing is immutable — delete and recreate to change it.
/// </summary>
public class UpdatePositionCompetencyDto : UpdateDtoBase
{
    [Required]
    [Range(1, 10)]
    public int RequiredProficiencyLevel { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Bulk replace: sets the complete list of competency requirements for a position.
/// Any existing entries not present in <see cref="Competencies"/> are removed.
/// </summary>
public class BulkSetPositionCompetenciesDto
{
    [Required]
    public Guid PositionId { get; set; }

    [Required]
    public List<PositionCompetencyInputDto> Competencies { get; set; } = new();
}

/// <summary>
/// Single competency entry used in bulk-set operations for a position.
/// </summary>
public class PositionCompetencyInputDto
{
    [Required]
    public Guid CompetencyId { get; set; }

    [Required]
    [Range(1, 10)]
    public int RequiredProficiencyLevel { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY DTOs
// ============================================================================

#region EmployeeCompetency

/// <summary>
/// Current assessed competency level for an employee.
/// </summary>
public class EmployeeCompetencyDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid CompetencyId { get; set; }
    public string CompetencyCode { get; set; } = string.Empty;
    public string CompetencyName { get; set; } = string.Empty;
    public CompetencyCategory CompetencyCategory { get; set; }
    public string CompetencyCategoryName => CompetencyCategory.ToString();
    public int ProficiencyScaleMax { get; set; }
    public int CurrentProficiencyLevel { get; set; }
    public DateTime AssessmentDate { get; set; }
    public Guid? AssessedById { get; set; }
    public string? AssessedByName { get; set; }
    public string? AssessmentMethod { get; set; }
    public string? EvidenceNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Detailed employee competency DTO that includes the full assessment history.
/// </summary>
public class EmployeeCompetencyDetailDto : EmployeeCompetencyDto
{
    public List<EmployeeCompetencyHistoryDto> History { get; set; } = new();
}

/// <summary>
/// DTO for recording the first (initial) competency assessment for an employee.
/// </summary>
public class CreateEmployeeCompetencyDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid CompetencyId { get; set; }

    [Required]
    [Range(1, 10)]
    public int CurrentProficiencyLevel { get; set; }

    [Required]
    public DateTime AssessmentDate { get; set; }

    public Guid? AssessedById { get; set; }

    [MaxLength(100)]
    public string? AssessmentMethod { get; set; }

    [MaxLength(2000)]
    public string? EvidenceNotes { get; set; }
}

/// <summary>
/// DTO for re-assessing (updating) an employee's competency level.
/// The service layer must snapshot the current values to
/// <see cref="EmployeeCompetencyHistory"/> before applying this update.
/// </summary>
public class UpdateEmployeeCompetencyDto : UpdateDtoBase
{
    [Required]
    [Range(1, 10)]
    public int CurrentProficiencyLevel { get; set; }

    [Required]
    public DateTime AssessmentDate { get; set; }

    public Guid? AssessedById { get; set; }

    [MaxLength(100)]
    public string? AssessmentMethod { get; set; }

    [MaxLength(2000)]
    public string? EvidenceNotes { get; set; }

    /// <summary>
    /// Reason for this re-assessment (e.g. post-training, promotion review).
    /// Written to the history entry created by the service layer.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string ChangeReason { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY HISTORY DTOs
// ============================================================================

#region EmployeeCompetencyHistory

/// <summary>
/// Immutable audit record of a previous competency assessment for an employee.
/// History records are written by the service layer and are never created or
/// modified directly via API callers.
/// </summary>
public class EmployeeCompetencyHistoryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeCompetencyId { get; set; }
    public int ProficiencyLevel { get; set; }
    public DateTime AssessmentDate { get; set; }
    public Guid? AssessedById { get; set; }
    public string? AssessedByName { get; set; }
    public string? AssessmentMethod { get; set; }
    public string? EvidenceNotes { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid? RecordedById { get; set; }
    public string? RecordedByName { get; set; }
}

#endregion

// ============================================================================
// COMPETENCY GAP / ANALYTICS DTOs
// ============================================================================

#region Competency Analytics

/// <summary>
/// Shows the gap (or surplus) between an employee's current proficiency
/// and what a specific position requires for a single competency.
/// A positive <see cref="Gap"/> value indicates a shortfall.
/// </summary>
public class EmployeeCompetencyGapDto
{
    public Guid CompetencyId { get; set; }
    public string CompetencyCode { get; set; } = string.Empty;
    public string CompetencyName { get; set; } = string.Empty;
    public CompetencyCategory CompetencyCategory { get; set; }
    public string CompetencyCategoryName => CompetencyCategory.ToString();
    public int ProficiencyScaleMax { get; set; }
    public int RequiredLevel { get; set; }

    /// <summary>Null when the competency has not yet been assessed for the employee.</summary>
    public int? CurrentLevel { get; set; }

    /// <summary>Positive = shortfall; zero = exact match; negative = surplus; null = not assessed.</summary>
    public int? Gap => CurrentLevel.HasValue ? RequiredLevel - CurrentLevel.Value : null;

    public GapStatus? GapStatus { get; set; }
}

/// <summary>
/// Competency gap summary for an employee measured against a specific position's requirements.
/// </summary>
public class EmployeePositionCompetencyGapSummaryDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public int TotalCompetencies { get; set; }
    public int MetOrExceeded { get; set; }
    public int HasGap { get; set; }
    public int NotAssessed { get; set; }
    public List<EmployeeCompetencyGapDto> CompetencyGaps { get; set; } = new();
}

/// <summary>
/// Summary of all competency assessments for a single employee,
/// grouped across all their assessed competencies.
/// </summary>
public class EmployeeCompetencyProfileDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public List<EmployeeCompetencyDto> Competencies { get; set; } = new();
    public int TotalAssessed { get; set; }
    public DateTime? LastAssessmentDate { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY BATCH ASSESSMENT DTOs
// ============================================================================

#region Batch Assessment

/// <summary>
/// Batch assessment request — one entry per changed competency.
/// Supports both re-assessments (EmployeeCompetencyId provided) and
/// first-time assessments (CompetencyId provided, EmployeeCompetencyId null).
/// </summary>
public class EmployeeCompetencyAssessmentUpdateDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one assessment update is required.")]
    public List<AssessmentUpdateItemDto> Updates { get; set; } = new();
}

/// <summary>Single competency update within a batch assess call.</summary>
public class AssessmentUpdateItemDto
{
    /// <summary>Set when re-assessing an existing record. Null when recording the first assessment.</summary>
    public Guid? EmployeeCompetencyId { get; set; }

    /// <summary>Required when <see cref="EmployeeCompetencyId"/> is null (first assessment).</summary>
    public Guid? CompetencyId { get; set; }

    [Required]
    [Range(1, 10)]
    public int NewLevel { get; set; }

    [Required]
    [MaxLength(100)]
    public string AssessmentMethod { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? EvidenceNotes { get; set; }

    [MaxLength(500)]
    public string? ChangeReason { get; set; }
}

/// <summary>Result of a batch assessment operation.</summary>
public class BatchAssessmentResultDto
{
    public int TotalSubmitted { get; set; }
    public int Succeeded      { get; set; }
    public int Failed         { get; set; }
    public List<BatchAssessmentErrorDto> Errors { get; set; } = new();
}

/// <summary>Per-item error detail within a batch assessment result.</summary>
public class BatchAssessmentErrorDto
{
    public Guid?  EmployeeCompetencyId { get; set; }
    public Guid?  CompetencyId         { get; set; }
    public string ErrorMessage         { get; set; } = string.Empty;
}

#endregion

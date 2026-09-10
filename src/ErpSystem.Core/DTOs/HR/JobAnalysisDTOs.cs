using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

#region Job Description DTOs

/// <summary>
/// DTO for job description read operations
/// </summary>
public class JobDescriptionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string JobDescriptionNumber { get; set; } = string.Empty;
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    
    // Version Control
    public int VersionNumber { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? RevisionReason { get; set; }
    public Guid? SupersededByVersionId { get; set; }
    
    public string JobSummary { get; set; } = string.Empty;
    public JobDescriptionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    
    // Preparation
    public Guid? PreparedById { get; set; }
    public string? PreparedByName { get; set; }
    public DateTime? PreparedDate { get; set; }
    
    // Review
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    
    // Approval
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    
    // Next Review
    public DateTime? NextReviewDate { get; set; }
    public int ReviewCycleMonths { get; set; }

    // Job Evaluation / Valuation
    public decimal? RoleIntrinsicValue { get; set; }
    public RoleCriticalityLevel? RoleCriticality { get; set; }
    public string? RoleCriticalityName => RoleCriticality?.ToString();
    public decimal? IndustryBenchmarkSalary { get; set; }
    public decimal? EstimatedSalaryLow { get; set; }
    public decimal? EstimatedSalaryHigh { get; set; }
    public Guid? SuggestedSalaryGradeId { get; set; }
    public string? SuggestedSalaryGradeName { get; set; }
    public string? ValuationNotes { get; set; }

    // Authority & financial limits
    public DecisionAuthorityLevel? AutonomyLevel { get; set; }
    public string? AutonomyLevelName => AutonomyLevel?.ToString();
    public string? DecisionMakingScope { get; set; }
    public decimal? FinancialAuthorityLimit { get; set; }
    public string? ApprovalAuthorityNotes { get; set; }

    // Classification (Ghana)
    public Guid? StaffLevelId { get; set; }
    public string? StaffLevelName { get; set; }
    public EmploymentType? IntendedEmploymentType { get; set; }
    public string? IntendedEmploymentTypeName => IntendedEmploymentType?.ToString();
    public bool IsBargainingUnitRole { get; set; }
    public Guid? UnionId { get; set; }
    public string? UnionName { get; set; }
    public string? OccupationCode { get; set; }
    public string? EssentialFunctionsSummary { get; set; }

    // Job Architecture
    public Guid? JobFamilyId { get; set; }
    public string? JobFamilyName { get; set; }
    public Guid? JobSubFamilyId { get; set; }
    public string? JobSubFamilyName { get; set; }
    public Guid? JobLevelId { get; set; }
    public string? JobLevelName { get; set; }
}

/// <summary>
/// Summary DTO for job description list views
/// </summary>
public class JobDescriptionSummaryDto
{
    public Guid Id { get; set; }
    public string JobDescriptionNumber { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public DateTime EffectiveDate { get; set; }
    public JobDescriptionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? NextReviewDate { get; set; }
}

/// <summary>
/// Detailed DTO for job description with all child collections
/// </summary>
public class JobDescriptionDetailDto : JobDescriptionDto
{
    public List<JobDutyItemDto> DutyItems { get; set; } = new();
    public List<JobResponsibilityDto> Responsibilities { get; set; } = new();
    public List<JobQualificationDto> Qualifications { get; set; } = new();
    public List<JobCompetencyDto> Competencies { get; set; } = new();
    public List<JobPhysicalDemandDto> PhysicalDemands { get; set; } = new();
    public List<JobWorkingConditionDto> WorkingConditions { get; set; } = new();
    public List<JobPpeRequirementDto> PpeRequirements { get; set; } = new();
    public List<JobEquipmentToolDto> EquipmentTools { get; set; } = new();
    public List<JobReportingRelationshipDto> ReportingRelationships { get; set; } = new();
    public List<JobMedicalRequirementDto> MedicalRequirements { get; set; } = new();
}

#region Job Duty Item DTOs

/// <summary>Itemized, numbered job-description duty statement (Basic Info tab).</summary>
public class JobDutyItemDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public int SequenceNumber { get; set; }
    public string DutyStatement { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class CreateJobDutyItemDto : CreateDtoBase
{
    [Required]
    public Guid JobDescriptionId { get; set; }

    public int SequenceNumber { get; set; }

    [Required]
    [MaxLength(1000)]
    public string DutyStatement { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateJobDutyItemDto : UpdateDtoBase
{
    public int SequenceNumber { get; set; }

    [Required]
    [MaxLength(1000)]
    public string DutyStatement { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

/// <summary>
/// DTO for creating a job description
/// </summary>
[CallerSuppliesIdentifiers]
public class CreateJobDescriptionDto : CreateDtoBase
{
    [Required]
    public Guid PositionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string JobTitle { get; set; } = string.Empty;

    [Required]
    public DateTime EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? RevisionReason { get; set; }

    [Required]
    [MaxLength(2000)]
    public string JobSummary { get; set; } = string.Empty;

    public int ReviewCycleMonths { get; set; } = 24;

    public List<CreateJobResponsibilityDto>? Responsibilities { get; set; }
    public List<CreateJobPhysicalDemandDto>? PhysicalDemands { get; set; }
    public List<CreateJobWorkingConditionDto>? WorkingConditions { get; set; }
    public List<CreateJobEquipmentToolDto>? EquipmentTools { get; set; }
    // ── classification, valuation and authority ──────────────────────────────
    // ⚠ These existed on the entity and on UpdateJobDescriptionDto but not here, so a job
    // description could only be created bare and classified on a second call — a create form had
    // to save twice, and anything that skipped the second save left the taxonomy tables with no
    // consumer at all. Measured 2026-08-19 by run-slice2.mjs.

    /// <summary>Job family this role belongs to. See <c>JobArchitectureController</c>.</summary>
    public Guid? JobFamilyId { get; set; }

    /// <summary>Sub-family within <see cref="JobFamilyId"/>.</summary>
    public Guid? JobSubFamilyId { get; set; }

    /// <summary>Job level (rank) this role sits at.</summary>
    public Guid? JobLevelId { get; set; }

    /// <summary>Staff level (MGT / SNR / JNR) the role is graded against.</summary>
    public Guid? StaffLevelId { get; set; }

    /// <summary>Payroll-owned salary grade suggested by the valuation. Read-only reference.</summary>
    public Guid? SuggestedSalaryGradeId { get; set; }

    /// <summary>Union the role falls under when <see cref="IsBargainingUnitRole"/> is set.</summary>
    public Guid? UnionId { get; set; }

    public bool IsBargainingUnitRole { get; set; }

    public string? OccupationCode { get; set; }

    public string? EssentialFunctionsSummary { get; set; }

    public EmploymentType? IntendedEmploymentType { get; set; }

    public RoleCriticalityLevel? RoleCriticality { get; set; }

    public decimal? RoleIntrinsicValue { get; set; }

    public decimal? IndustryBenchmarkSalary { get; set; }

    public string? ValuationNotes { get; set; }

    public DecisionAuthorityLevel? AutonomyLevel { get; set; }

    public string? DecisionMakingScope { get; set; }

    public decimal? FinancialAuthorityLimit { get; set; }

    public string? ApprovalAuthorityNotes { get; set; }

    public List<CreateJobReportingRelationshipDto>? ReportingRelationships { get; set; }
}

/// <summary>
/// DTO for updating a job description
/// </summary>
public class UpdateJobDescriptionDto : UpdateDtoBase
{
    [Required]
    public Guid PositionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string JobTitle { get; set; } = string.Empty;

    [Required]
    public DateTime EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? RevisionReason { get; set; }

    [Required]
    [MaxLength(2000)]
    public string JobSummary { get; set; } = string.Empty;

    /// <summary>
    /// The status the caller believes the record is in. Optional, and never written.
    /// </summary>
    /// <remarks>
    /// ⚠ It was non-nullable and it WAS written, which made an omitted status corrupting rather
    /// than harmless: <c>Draft = 1</c>, so a body without it bound to <b>0</b> — a value no member
    /// of the enum holds. A record left on 0 is not authorable and not submittable, so it can never
    /// move again; only a duplicate escapes it. Nullable now, so omitting it means "I am not
    /// saying", and <see cref="ErpSystem.Core.Services.HR.JobDescriptionService"/> refuses a value
    /// that contradicts the record rather than acting on it. Status is moved by submitting,
    /// reviewing or approving — never by an edit.
    /// </remarks>
    public JobDescriptionStatus? Status { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public int ReviewCycleMonths { get; set; }

    // Job Evaluation / Valuation
    [Range(0, double.MaxValue)]
    public decimal? RoleIntrinsicValue { get; set; }
    public RoleCriticalityLevel? RoleCriticality { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? IndustryBenchmarkSalary { get; set; }
    public Guid? SuggestedSalaryGradeId { get; set; }
    [MaxLength(2000)]
    public string? ValuationNotes { get; set; }

    // Authority & financial limits
    public DecisionAuthorityLevel? AutonomyLevel { get; set; }
    [MaxLength(2000)]
    public string? DecisionMakingScope { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? FinancialAuthorityLimit { get; set; }
    [MaxLength(2000)]
    public string? ApprovalAuthorityNotes { get; set; }

    // Classification (Ghana)
    public Guid? StaffLevelId { get; set; }
    public EmploymentType? IntendedEmploymentType { get; set; }
    public bool IsBargainingUnitRole { get; set; }
    public Guid? UnionId { get; set; }
    [MaxLength(50)]
    public string? OccupationCode { get; set; }
    [MaxLength(2000)]
    public string? EssentialFunctionsSummary { get; set; }

    // Job Architecture
    public Guid? JobFamilyId { get; set; }
    public Guid? JobSubFamilyId { get; set; }
    public Guid? JobLevelId { get; set; }
}

/// <summary>
/// DTO for submitting job description for review
/// </summary>
public class SubmitJobDescriptionForReviewDto
{
    [Required]
    public Guid JobDescriptionId { get; set; }
}

/// <summary>
/// DTO for reviewing a job description
/// </summary>
public class ReviewJobDescriptionDto
{
    [Required]
    public Guid JobDescriptionId { get; set; }

    [Required]
    public bool IsApproved { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

/// <summary>
/// DTO for approving a job description
/// </summary>
/// <summary>Why a job description was sent back to its author on the workflow route.</summary>
public class RejectJobDescriptionDto
{
    public string? Reason { get; set; }
}

public class ApproveJobDescriptionDto
{
    [Required]
    public Guid JobDescriptionId { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

/// <summary>
/// DTO for creating a new version of job description
/// </summary>
public class CreateJobDescriptionVersionDto
{
    [Required]
    public Guid OriginalJobDescriptionId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string RevisionReason { get; set; } = string.Empty;
}

#endregion

#region Job Responsibility DTOs

/// <summary>
/// DTO for job responsibility read operations
/// </summary>
public class JobResponsibilityDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public string ResponsibilityDescription { get; set; } = string.Empty;
    public ResponsibilityType Type { get; set; }
    public string TypeName => Type.ToString();
    public decimal? PercentageOfTime { get; set; }
    public int? ImportanceWeight { get; set; }
    public List<JobQualificationDto> Qualifications { get; set; } = new();
    public List<JobCompetencyDto> Competencies { get; set; } = new();
    public List<JobResponsibilityKpiDto> Kpis { get; set; } = new();
}

#region Job Responsibility KPI DTOs

public class JobResponsibilityKpiDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobResponsibilityId { get; set; }
    public string KpiStatement { get; set; } = string.Empty;
    public string? TargetOrStandard { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal? Weight { get; set; }
    public int SequenceNumber { get; set; }
}

public class CreateJobResponsibilityKpiDto : CreateDtoBase
{
    [Required]
    public Guid JobResponsibilityId { get; set; }

    [Required]
    [MaxLength(500)]
    public string KpiStatement { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? TargetOrStandard { get; set; }

    [MaxLength(100)]
    public string? UnitOfMeasure { get; set; }

    [Range(0, 100)]
    public decimal? Weight { get; set; }

    public int SequenceNumber { get; set; }
}

public class UpdateJobResponsibilityKpiDto : UpdateDtoBase
{
    [Required]
    [MaxLength(500)]
    public string KpiStatement { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? TargetOrStandard { get; set; }

    [MaxLength(100)]
    public string? UnitOfMeasure { get; set; }

    [Range(0, 100)]
    public decimal? Weight { get; set; }

    public int SequenceNumber { get; set; }
}

#endregion

/// <summary>
/// DTO for creating a job responsibility
/// </summary>
public class CreateJobResponsibilityDto : CreateDtoBase
{
    public Guid JobDescriptionId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string ResponsibilityDescription { get; set; } = string.Empty;

    [Required]
    public ResponsibilityType Type { get; set; }

    [Range(0, 100, ErrorMessage = "Percentage of time must be between 0 and 100")]
    public decimal? PercentageOfTime { get; set; }

    [Range(1, 10, ErrorMessage = "Importance weight must be between 1 and 10")]
    public int? ImportanceWeight { get; set; }

    public List<CreateJobQualificationDto>? Qualifications { get; set; }
    public List<CreateJobCompetencyDto>? Competencies { get; set; }
}

/// <summary>
/// DTO for updating a job responsibility
/// </summary>
public class UpdateJobResponsibilityDto : UpdateDtoBase
{
    [Required]
    [MaxLength(1000)]
    public string ResponsibilityDescription { get; set; } = string.Empty;

    [Required]
    public ResponsibilityType Type { get; set; }

    [Range(0, 100, ErrorMessage = "Percentage of time must be between 0 and 100")]
    public decimal? PercentageOfTime { get; set; }

    [Range(1, 10, ErrorMessage = "Importance weight must be between 1 and 10")]
    public int? ImportanceWeight { get; set; }
}

#endregion

#region Job Qualification DTOs

/// <summary>
/// DTO for job qualification read operations
/// </summary>
public class JobQualificationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid? JobResponsibilityId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public QualificationType Type { get; set; }
    public string TypeName => Type.ToString();
    public Guid? QualificationId { get; set; }
    public string? QualificationName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? JobSpecificRequirements { get; set; }
    public decimal? MonetaryValue { get; set; }
}

/// <summary>
/// DTO for creating a job qualification
/// </summary>
public class CreateJobQualificationDto : CreateDtoBase
{
    public Guid? JobResponsibilityId { get; set; }

    [Required]
    public Guid JobDescriptionId { get; set; }

    [Required]
    public QualificationType Type { get; set; }

    public Guid? QualificationId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    [MaxLength(500)]
    public string? JobSpecificRequirements { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryValue { get; set; }
}

/// <summary>
/// DTO for updating a job qualification
/// </summary>
public class UpdateJobQualificationDto : UpdateDtoBase
{
    /// <summary>
    /// The responsibility this row hangs off, within its own job description. Null detaches it.
    /// </summary>
    /// <remarks>
    /// ⚠ It was missing from this DTO entirely, so an attachment made on create could never be
    /// moved or cleared — and, because the screen knew that, it hid the control on edit rather than
    /// offering a change the API would silently discard. Like every other field here it REPLACES:
    /// a payload that omits it detaches the row.
    /// </remarks>
    public Guid? JobResponsibilityId { get; set; }

    [Required]
    public QualificationType Type { get; set; }

    public Guid? QualificationId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    [MaxLength(500)]
    public string? JobSpecificRequirements { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryValue { get; set; }
}

#endregion

#region Job Competency DTOs

/// <summary>
/// DTO for job competency read operations
/// </summary>
public class JobCompetencyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid? JobResponsibilityId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public Guid? SkillId { get; set; }
    public string? SkillName { get; set; }
    public Guid? CompetencyId { get; set; }
    public string? MasterCompetencyName { get; set; }
    public string CompetencyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CompetencyType Type { get; set; }
    public string TypeName => Type.ToString();
    public ProficiencyLevel RequiredLevel { get; set; }
    public string RequiredLevelName => RequiredLevel.ToString();
    public bool IsCritical { get; set; }
    public decimal? MonetaryValue { get; set; }
}

/// <summary>
/// DTO for creating a job competency
/// </summary>
public class CreateJobCompetencyDto : CreateDtoBase
{
    public Guid? JobResponsibilityId { get; set; }

    [Required]
    public Guid JobDescriptionId { get; set; }

    public Guid? SkillId { get; set; }
    public Guid? CompetencyId { get; set; }

    [Required]
    [MaxLength(200)]
    public string CompetencyName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public CompetencyType Type { get; set; }

    [Required]
    public ProficiencyLevel RequiredLevel { get; set; }

    public bool IsCritical { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryValue { get; set; }
}

/// <summary>
/// DTO for updating a job competency
/// </summary>
public class UpdateJobCompetencyDto : UpdateDtoBase
{
    /// <summary>
    /// The responsibility this row hangs off, within its own job description. Null detaches it.
    /// </summary>
    /// <remarks>
    /// ⚠ It was missing from this DTO entirely, so an attachment made on create could never be
    /// moved or cleared — and, because the screen knew that, it hid the control on edit rather than
    /// offering a change the API would silently discard. Like every other field here it REPLACES:
    /// a payload that omits it detaches the row.
    /// </remarks>
    public Guid? JobResponsibilityId { get; set; }

    public Guid? SkillId { get; set; }
    public Guid? CompetencyId { get; set; }

    [Required]
    [MaxLength(200)]
    public string CompetencyName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public CompetencyType Type { get; set; }

    [Required]
    public ProficiencyLevel RequiredLevel { get; set; }

    public bool IsCritical { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryValue { get; set; }
}

#endregion

#region Manpower Budget DTOs

/// <summary>
/// DTO for manpower budget read operations
/// </summary>
public class ManpowerBudgetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string BudgetNumber { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public ManpowerBudgetStatus Status { get; set; }
    public string StatusName => Status.ToString();
    
    // Planning Period
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    
    // Current State
    public int CurrentHeadcount { get; set; }
    public decimal CurrentSalaryCost { get; set; }
    
    // Planned State
    public int PlannedHeadcount { get; set; }
    public decimal PlannedSalaryCost { get; set; }
    
    // Changes
    public int PlannedNewHires { get; set; }
    public int PlannedTerminations { get; set; }
    public int PlannedPromotions { get; set; }
    public int PlannedTransfers { get; set; }
    
    // Budget Allocation
    public decimal SalaryBudget { get; set; }
    public decimal BenefitsBudget { get; set; }
    public decimal RecruitmentBudget { get; set; }
    public decimal TrainingBudget { get; set; }
    public decimal TotalBudget { get; set; }
    
    // Variance
    public decimal ActualSpent { get; set; }
    public decimal Variance { get; set; }
    public decimal VariancePercentage => TotalBudget > 0 ? (Variance / TotalBudget) * 100 : 0;
    
    public string? BusinessJustification { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }

    /// <summary>Why the budget was sent back, when it was. See the entity for why this exists.</summary>
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Summary DTO for manpower budget list views
/// </summary>
public class ManpowerBudgetSummaryDto
{
    public Guid Id { get; set; }
    public string BudgetNumber { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public string? OrganizationUnitName { get; set; }
    public string? OrganizationLevelName { get; set; }
    public ManpowerBudgetStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int CurrentHeadcount { get; set; }
    public int PlannedHeadcount { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal ActualSpent { get; set; }
}

/// <summary>
/// Detailed DTO for manpower budget with lines
/// </summary>
public class ManpowerBudgetDetailDto : ManpowerBudgetDto
{
    public List<ManpowerBudgetLineDto> BudgetLines { get; set; } = new();
}

/// <summary>
/// DTO for creating a manpower budget
/// </summary>
public class CreateManpowerBudgetDto : CreateDtoBase
{
    [Required]
    [Range(2000, 2100)]
    public int FiscalYear { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public DateTime PeriodStartDate { get; set; }

    [Required]
    public DateTime PeriodEndDate { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int CurrentHeadcount { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal CurrentSalaryCost { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int PlannedHeadcount { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal PlannedSalaryCost { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedNewHires { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedTerminations { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedPromotions { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedTransfers { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal SalaryBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal BenefitsBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal RecruitmentBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TrainingBudget { get; set; }

    [MaxLength(2000)]
    public string? BusinessJustification { get; set; }

    public List<CreateManpowerBudgetLineDto>? BudgetLines { get; set; }
}

/// <summary>
/// DTO for updating a manpower budget
/// </summary>
public class UpdateManpowerBudgetDto : UpdateDtoBase
{
    /// <summary>
    /// The scope — fiscal year, unit, level — editable while the budget is still its author's
    /// (Draft or Rejected). Round 2b, lane R1: the create form collected all three and the edit
    /// could change none of them, so a budget drafted against the wrong unit had to be deleted and
    /// typed again.
    /// </summary>
    /// <remarks>
    /// All three are optional and <b>null means unchanged</b> — the rest of this DTO is a REPLACE,
    /// but callers written before R1 send no scope and must not have their year wiped. When the
    /// unit changes and no level is named, the level follows the unit.
    /// </remarks>
    [Range(2000, 2100)]
    public int? FiscalYear { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public DateTime PeriodStartDate { get; set; }

    [Required]
    public DateTime PeriodEndDate { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int CurrentHeadcount { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal CurrentSalaryCost { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int PlannedHeadcount { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal PlannedSalaryCost { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedNewHires { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedTerminations { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedPromotions { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedTransfers { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal SalaryBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal BenefitsBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal RecruitmentBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TrainingBudget { get; set; }

    // ⚠ `ActualSpent` is no longer accepted here (round 2b, lane R1). It was the one figure the
    // create form never asked for and the correction dialog never sent, so every correction wrote
    // 0 over it; and it is the one figure that can only come from Finance's actuals
    // (HR-FINANCE-INTEGRATION-BACKLOG, area 17/18). Nothing in HR types it now. An old client that
    // still sends it is ignored, not refused.

    /// <summary>
    /// The status the caller believes the budget is in. Optional, and never written.
    /// </summary>
    /// <remarks>
    /// ⚠ The twin of the job description's own status hole, and unlike that one it had a caller.
    /// It was non-nullable and assigned unconditionally, and the "Correct the budget" dialog on the
    /// manpower budget screen does not send it — <c>Draft = 1</c>, so every correction would have
    /// written <b>0</b>, and <c>SubmitForApprovalAsync</c> admits Draft only. A corrected budget
    /// could then never be submitted, which closes FR-HR-135's chain and with it the approved
    /// establishment that depends on it (D-2). No row was ever damaged only because the table is
    /// still empty (ErpSystemDB, 2026-09-06: 0 budgets) — the correction dialog is newer than the
    /// data. Status is moved by submit, approve and reject; never by an edit.
    /// </remarks>
    public ManpowerBudgetStatus? Status { get; set; }

    [MaxLength(2000)]
    public string? BusinessJustification { get; set; }
}

/// <summary>
/// DTO for approving a manpower budget
/// </summary>
/// <summary>A position with no approved job description — the work list behind FR-HR-134.</summary>
public class UncoveredPositionDto
{
    public Guid PositionId { get; set; }

    public string PositionTitle { get; set; } = string.Empty;

    public string? OrganizationUnitName { get; set; }

    /// <summary>Live count of people doing a job nobody has described.</summary>
    public int CurrentlyFilled { get; set; }

    /// <summary>True when a draft or pending job description exists but has not been approved.</summary>
    public bool HasUnapprovedDraft { get; set; }
}

/// <summary>Where the organisation is short against the competencies its positions require.</summary>
/// <remarks>
/// Aggregates the per-employee gap analysis across everyone in a position that requires the
/// competency. This is the training-needs question — FR-HR-004's planning link in its most concrete
/// form — and it is the reason the competency framework is worth maintaining at all.
/// </remarks>
public class OrganisationCompetencyGapDto
{
    public Guid CompetencyId { get; set; }

    public string CompetencyCode { get; set; } = string.Empty;

    public string CompetencyName { get; set; } = string.Empty;

    public string CompetencyCategory { get; set; } = string.Empty;

    /// <summary>Employees in a position that requires this competency.</summary>
    public int EmployeesRequiring { get; set; }

    /// <summary>Of those, how many have been assessed at or above the required level.</summary>
    public int MeetingRequirement { get; set; }

    /// <summary>Assessed below the required level.</summary>
    public int BelowRequirement { get; set; }

    /// <summary>⚠ Never assessed. Distinct from "below" — an unknown is not a shortfall.</summary>
    public int NotAssessed { get; set; }
}

/// <summary>Sets a position's approved establishment without going through a manpower budget.</summary>
public class SetPositionEstablishmentDto
{
    [Range(0, 10000)]
    public int ExpectedHeadcount { get; set; }

    /// <summary>Why this number, for the audit trail a budget approval would otherwise provide.</summary>
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>A position's establishment, and where the number came from.</summary>
public class PositionEstablishmentResultDto
{
    public Guid PositionId { get; set; }

    public string PositionTitle { get; set; } = string.Empty;

    public int ExpectedHeadcount { get; set; }

    /// <summary>Live count of active employees in the post — not a figure anyone typed.</summary>
    public int CurrentlyFilled { get; set; }

    /// <summary>Null when nobody has ever authorised a headcount for this post.</summary>
    public DateTime? EstablishmentApprovedOn { get; set; }

    /// <summary>The budget that authorised it; null when HR set it directly.</summary>
    public Guid? EstablishmentSourceBudgetId { get; set; }

    public string? EstablishmentSourceBudgetNumber { get; set; }

    /// <summary>False when the post is unestablished, in which case no rule constrains it.</summary>
    public bool IsEstablished { get; set; }
}

/// <summary>Why a manpower budget was refused.</summary>
/// <summary>
/// What the system knows about a unit before anyone types a budget for it (round 2b, lane R2):
/// who is on strength, what they cost, who is due to leave in the period, and where each post
/// stands against its establishment. The demo asked "which values on the form can be
/// automatically specified?" — these are the ones, and the form pre-fills from them.
/// </summary>
/// <remarks>
/// <para><b>The unit and everything under it.</b> A budget for a division covers its departments,
/// so every figure here is over the subtree, walked by <c>ParentUnitId</c> (seeded units carry no
/// <c>Path</c>). <see cref="UnitIds"/> says which units that was.</para>
/// <para><b>One serving predicate</b> — <c>HrServingEmployees</c>; see its remarks for the three
/// older answers this deliberately does not reuse.</para>
/// <para><b>Salary cost is an estimate.</b> It is the sum of each serving employee's basic pay as
/// <c>HrBasicPay</c> resolves it without a payroll read (notch, else level mid-point, else the flat
/// figure on the record). <see cref="EmployeesWithoutPay"/> says how many contributed nothing.</para>
/// <para><b>Exits due</b> are retirements falling in the period (the compulsory age, honouring a
/// gender-specific override, or an explicit retirement date), fixed-term contracts scheduled to
/// end in it, and separations already raised and not yet completed. Terminations nobody has
/// raised yet cannot be known; the screen says so. A person appearing in two lists is counted
/// once in <see cref="ExitsDueTotal"/>.</para>
/// </remarks>
public class ManpowerPlanningBaselineDto
{
    public Guid OrganizationUnitId { get; set; }
    public string OrganizationUnitName { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Always true: the figures are over the unit and its descendants.</summary>
    public bool IncludesDescendantUnits => true;
    public List<Guid> UnitIds { get; set; } = new();

    public int CurrentHeadcount { get; set; }
    public decimal CurrentSalaryCost { get; set; }
    public int EmployeesWithoutPay { get; set; }
    public string SalaryCostNote { get; set; } = string.Empty;

    public List<ManpowerPlanningExitDto> RetirementsDue { get; set; } = new();
    public List<ManpowerPlanningExitDto> ContractExpiriesDue { get; set; } = new();
    public List<ManpowerPlanningExitDto> SeparationsInFlight { get; set; } = new();

    /// <summary>Distinct employees across the three lists.</summary>
    public int ExitsDueTotal { get; set; }
    public int SuggestedPlannedTerminations => ExitsDueTotal;

    public List<ManpowerPlanningPositionDto> Positions { get; set; } = new();
}

/// <summary>One person expected to leave in the period, and why.</summary>
public class ManpowerPlanningExitDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public Guid PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    /// <summary><c>Retirement</c>, <c>ContractExpiry</c> or <c>Separation</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Retirement date, contract end date, or the separation's last working day.</summary>
    public DateOnly? Date { get; set; }

    /// <summary>True when the date is already past and the person is still on strength — a backlog, not a projection.</summary>
    public bool IsOverdue { get; set; }

    /// <summary>A separation already raised for this person (so the sweep would skip them).</summary>
    public bool HasSeparation { get; set; }
    public string? SeparationNumber { get; set; }
    public string? SeparationStatus { get; set; }
}

/// <summary>One post in the subtree: where it stands, and what the arithmetic suggests.</summary>
/// <remarks>
/// ⚠ <see cref="Gap"/> is <b>null, not zero, for an unestablished post</b>. Its
/// <c>ExpectedHeadcount</c> is the column default (1 for 132 of 146 live positions) and means
/// nothing; a gap computed from it would be a phantom. Only <c>EstablishmentApprovedOn</c> makes
/// the number real — the same rule every enforcement path uses.
/// </remarks>
public class ManpowerPlanningPositionDto
{
    public Guid PositionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Code { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? SalaryGradeId { get; set; }

    public int Filled { get; set; }
    public int ExpectedHeadcount { get; set; }
    public bool IsEstablished { get; set; }
    public int? Gap { get; set; }

    /// <summary>People in this post who appear in the exits-due lists.</summary>
    public int ExitsDue { get; set; }

    /// <summary>Mean monthly basic pay of the people in the post (the planning estimate), and its sum.</summary>
    public decimal CurrentAverageSalary { get; set; }
    public decimal CurrentSalaryCost { get; set; }

    /// <summary><c>max(0, Gap) + ExitsDue</c>: what it would take to be at establishment at the end of the period.</summary>
    public int SuggestedNewHires { get; set; }
}

/// <summary>"The salary that goes with this position" (round 2b, R3): the grade the post carries, if any.</summary>
public class PositionSalaryReferenceDto
{
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public bool HasGrade => SalaryGradeId != null;
    public Guid? SalaryGradeId { get; set; }
    public string? GradeCode { get; set; }
    public string? GradeName { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    /// <summary>The scale's tiers for this tenant, so a picker knows whether to show the level.</summary>
    public SalaryStructureTiers Tiers { get; set; }
}

/// <summary>
/// "Use the position establishment to initiate the budget" (round 2b, R4a): a Draft budget for a
/// unit and year, with one line per post in the subtree, pre-filled from the planning baseline.
/// </summary>
public class CreateManpowerBudgetFromEstablishmentDto
{
    [Required]
    public Guid OrganizationUnitId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int FiscalYear { get; set; }

    [Required]
    public DateOnly PeriodStart { get; set; }

    [Required]
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Also draft a line for posts nobody has established. Their gap is unknown, so such a line
    /// plans only the exits due; the budget's approval is what establishes them. Default true.
    /// </summary>
    public bool IncludeUnestablished { get; set; } = true;

    [MaxLength(2000)]
    public string? BusinessJustification { get; set; }
}

/// <summary>What "add posts from the establishment" did to a draft budget.</summary>
public class AddLinesFromEstablishmentResultDto
{
    public int Added { get; set; }
    public int AlreadyOnBudget { get; set; }
    public int SkippedUnestablished { get; set; }
    public List<ManpowerBudgetLineDto> Lines { get; set; } = new();
}

public class RejectManpowerBudgetDto
{
    public string? Reason { get; set; }
}

public class ApproveManpowerBudgetDto
{
    [Required]
    public Guid BudgetId { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

#endregion

#region Manpower Budget Line DTOs

/// <summary>
/// DTO for manpower budget line read operations
/// </summary>
public class ManpowerBudgetLineDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ManpowerBudgetId { get; set; }
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public Guid? JobDescriptionId { get; set; }
    public string? JobDescriptionNumber { get; set; }

    // Where the planned salary came from (round 2b, R3)
    public Guid? SalaryGradeId { get; set; }
    public string? SalaryGradeCode { get; set; }
    public string? SalaryGradeName { get; set; }
    public Guid? SalaryLevelId { get; set; }
    public string? SalaryLevelCode { get; set; }
    public Guid? SalaryNotchId { get; set; }
    public int? SalaryNotchNumber { get; set; }
    public PlannedSalarySource PlannedSalarySource { get; set; }
    public string PlannedSalarySourceName => PlannedSalarySource.ToString();

    /// <summary>Posts on live requisitions drawing down from this line, and what is left (round 2b, R5, D-8). Filled by the list read.</summary>
    public int RequisitionedCount { get; set; }
    public int Remaining { get; set; }
    
    // Current
    public int CurrentCount { get; set; }
    public int CurrentFilled { get; set; }
    public int CurrentVacant { get; set; }
    public decimal CurrentAverageSalary { get; set; }
    public decimal CurrentTotalCost { get; set; }
    
    // Planned
    public int PlannedCount { get; set; }
    public int PlannedNewPositions { get; set; }
    public int PlannedEliminations { get; set; }
    public decimal PlannedAverageSalary { get; set; }
    public decimal PlannedTotalCost { get; set; }
    
    // Timeline
    public int? Quarter { get; set; }
    public DateTime? TargetFillDate { get; set; }
    
    // Priority
    public BudgetPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public bool IsCritical { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for creating a manpower budget line
/// </summary>
public class CreateManpowerBudgetLineDto : CreateDtoBase
{
    /// <summary>
    /// Grade → (level) → notch on the salary scale (round 2b, R3; all optional). The service
    /// checks each belongs to the one above and to this tenant. With no
    /// <see cref="PlannedAverageSalary"/> the amount is read from the deepest one named — notch
    /// amount, else level mid-point, else grade minimum — and the source recorded; with one, the
    /// figure is kept as typed and the source is <c>Manual</c>. Naming neither is refused.
    /// <c>PlannedTotalCost</c> is IGNORED since R3: the server computes average × planned count.
    /// </summary>
    public Guid? SalaryGradeId { get; set; }
    public Guid? SalaryLevelId { get; set; }
    public Guid? SalaryNotchId { get; set; }

    [Required]
    public Guid ManpowerBudgetId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    public Guid? JobDescriptionId { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int CurrentCount { get; set; }

    [Range(0, int.MaxValue)]
    public int CurrentFilled { get; set; }

    [Range(0, int.MaxValue)]
    public int CurrentVacant { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CurrentAverageSalary { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CurrentTotalCost { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int PlannedCount { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedNewPositions { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedEliminations { get; set; }

    /// <summary>Null = read it from the scale (grade/level/notch). A value = typed, kept as is, source <c>Manual</c>.</summary>
    [Range(0, double.MaxValue)]
    public decimal? PlannedAverageSalary { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PlannedTotalCost { get; set; }

    [Range(1, 4)]
    public int? Quarter { get; set; }

    public DateTime? TargetFillDate { get; set; }

    public BudgetPriority Priority { get; set; } = BudgetPriority.Medium;
    public bool IsCritical { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating a manpower budget line
/// </summary>
public class UpdateManpowerBudgetLineDto : UpdateDtoBase
{
    /// <summary>As on the create DTO: the place on the scale, all optional, checked for consistency; the total is computed.</summary>
    public Guid? SalaryGradeId { get; set; }
    public Guid? SalaryLevelId { get; set; }
    public Guid? SalaryNotchId { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int CurrentCount { get; set; }

    [Range(0, int.MaxValue)]
    public int CurrentFilled { get; set; }

    [Range(0, int.MaxValue)]
    public int CurrentVacant { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CurrentAverageSalary { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CurrentTotalCost { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int PlannedCount { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedNewPositions { get; set; }

    [Range(0, int.MaxValue)]
    public int PlannedEliminations { get; set; }

    /// <summary>Null = read it from the scale (grade/level/notch). A value = typed, kept as is, source <c>Manual</c>.</summary>
    [Range(0, double.MaxValue)]
    public decimal? PlannedAverageSalary { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PlannedTotalCost { get; set; }

    [Range(1, 4)]
    public int? Quarter { get; set; }

    public DateTime? TargetFillDate { get; set; }

    public BudgetPriority Priority { get; set; }
    public bool IsCritical { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Job Physical Demand DTOs

public class JobPhysicalDemandDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public PhysicalDemandType DemandType { get; set; }
    public string DemandTypeName => DemandType.ToString();
    public string DemandDescription { get; set; } = string.Empty;
    public PhysicalDemandFrequency Frequency { get; set; }
    public string FrequencyName => Frequency.ToString();
    public double? WeightOrForceKg { get; set; }
    public string? DistanceOrDuration { get; set; }
    public bool IsEssential { get; set; }
    public string? NotesOrExamples { get; set; }
    public bool IsPhysicalAttribute { get; set; }
    public string? AttributeRequirement { get; set; }
    public string? Justification { get; set; }
}

public class CreateJobPhysicalDemandDto : CreateDtoBase
{
    public Guid JobDescriptionId { get; set; }

    [Required]
    public PhysicalDemandType DemandType { get; set; }

    [Required]
    public string DemandDescription { get; set; } = string.Empty;

    [Required]
    public PhysicalDemandFrequency Frequency { get; set; }

    [Range(0, double.MaxValue)]
    public double? WeightOrForceKg { get; set; }

    [MaxLength(200)]
    public string? DistanceOrDuration { get; set; }

    public bool IsEssential { get; set; } = true;

    [MaxLength(1000)]
    public string? NotesOrExamples { get; set; }

    public bool IsPhysicalAttribute { get; set; }

    [MaxLength(500)]
    public string? AttributeRequirement { get; set; }

    [MaxLength(1000)]
    public string? Justification { get; set; }
}

public class UpdateJobPhysicalDemandDto : UpdateDtoBase
{
    [Required]
    public PhysicalDemandType DemandType { get; set; }

    [Required]
    public string DemandDescription { get; set; } = string.Empty;

    [Required]
    public PhysicalDemandFrequency Frequency { get; set; }

    [Range(0, double.MaxValue)]
    public double? WeightOrForceKg { get; set; }

    [MaxLength(200)]
    public string? DistanceOrDuration { get; set; }

    public bool IsEssential { get; set; }

    [MaxLength(1000)]
    public string? NotesOrExamples { get; set; }

    public bool IsPhysicalAttribute { get; set; }

    [MaxLength(500)]
    public string? AttributeRequirement { get; set; }

    [MaxLength(1000)]
    public string? Justification { get; set; }
}

#endregion

#region Job Working Condition DTOs

public class JobWorkingConditionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public WorkEnvironmentType EnvironmentType { get; set; }
    public string EnvironmentTypeName => EnvironmentType.ToString();
    public string Description { get; set; } = string.Empty;
    public ExposureLevel ExposureLevel { get; set; }
    public string ExposureLevelName => ExposureLevel.ToString();
    public bool RequiresPPE { get; set; }
    public string? PPERequirements { get; set; }
    public decimal? TravelPercentage { get; set; }
    public string? TravelRequirements { get; set; }
}

public class CreateJobWorkingConditionDto : CreateDtoBase
{
    public Guid JobDescriptionId { get; set; }

    [Required]
    public WorkEnvironmentType EnvironmentType { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ExposureLevel ExposureLevel { get; set; }

    public bool RequiresPPE { get; set; }

    [MaxLength(500)]
    public string? PPERequirements { get; set; }

    [Range(0, 100)]
    public decimal? TravelPercentage { get; set; }

    public string? TravelRequirements { get; set; }
}

public class UpdateJobWorkingConditionDto : UpdateDtoBase
{
    [Required]
    public WorkEnvironmentType EnvironmentType { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ExposureLevel ExposureLevel { get; set; }

    public bool RequiresPPE { get; set; }

    [MaxLength(500)]
    public string? PPERequirements { get; set; }

    [Range(0, 100)]
    public decimal? TravelPercentage { get; set; }

    public string? TravelRequirements { get; set; }
}

#endregion

#region Job Equipment Tool DTOs

public class JobEquipmentToolDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public EquipmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public string DescriptionOrSpecification { get; set; } = string.Empty;
    public ProficiencyLevel RequiredProficiency { get; set; }
    public string RequiredProficiencyName => RequiredProficiency.ToString();
    public bool IsEssential { get; set; }
    public string? TrainingRequired { get; set; }
    public Guid? LinkedQualificationId { get; set; }
    public List<JobEquipmentTrainingDto> TrainingRequirements { get; set; } = new();
}

public class CreateJobEquipmentToolDto : CreateDtoBase
{
    public Guid JobDescriptionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    public EquipmentType Type { get; set; }

    [MaxLength(500)]
    public string DescriptionOrSpecification { get; set; } = string.Empty;

    [Required]
    public ProficiencyLevel RequiredProficiency { get; set; }

    public bool IsEssential { get; set; }

    [MaxLength(1000)]
    public string? TrainingRequired { get; set; }

    public Guid? LinkedQualificationId { get; set; }
}

public class UpdateJobEquipmentToolDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    public EquipmentType Type { get; set; }

    [MaxLength(500)]
    public string DescriptionOrSpecification { get; set; } = string.Empty;

    [Required]
    public ProficiencyLevel RequiredProficiency { get; set; }

    public bool IsEssential { get; set; }

    [MaxLength(1000)]
    public string? TrainingRequired { get; set; }

    public Guid? LinkedQualificationId { get; set; }
}

#endregion

#region Job Reporting Relationship DTOs

public class JobReportingRelationshipDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public ReportingRelationshipType RelationshipType { get; set; }
    public string RelationshipTypeName => RelationshipType.ToString();
    public string TitleOrRole { get; set; } = string.Empty;
    public Guid? EmployeeOrPositionId { get; set; }
    public string? RelatedPositionTitle { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? NumberOfDirectReports { get; set; }
    public bool IsPrimarySupervisor { get; set; }
}

public class CreateJobReportingRelationshipDto : CreateDtoBase
{
    public Guid JobDescriptionId { get; set; }

    [Required]
    public ReportingRelationshipType RelationshipType { get; set; }

    [Required]
    [MaxLength(200)]
    public string TitleOrRole { get; set; } = string.Empty;

    public Guid? EmployeeOrPositionId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public int? NumberOfDirectReports { get; set; }

    public bool IsPrimarySupervisor { get; set; }
}

public class UpdateJobReportingRelationshipDto : UpdateDtoBase
{
    [Required]
    public ReportingRelationshipType RelationshipType { get; set; }

    [Required]
    [MaxLength(200)]
    public string TitleOrRole { get; set; } = string.Empty;

    public Guid? EmployeeOrPositionId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public int? NumberOfDirectReports { get; set; }

    public bool IsPrimarySupervisor { get; set; }
}

#endregion

#region Job PPE Requirement DTOs

public class JobPpeRequirementDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public Guid? PpeTypeId { get; set; }
    public string? PpeTypeName { get; set; }
    public string? CustomPpeName { get; set; }
    /// <summary>Resolved display name (master PPE name or custom name).</summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(PpeTypeName) ? PpeTypeName! : (CustomPpeName ?? string.Empty);
    public bool IsMandatory { get; set; }
    public string? Notes { get; set; }
}

public class CreateJobPpeRequirementDto : CreateDtoBase
{
    [Required]
    public Guid JobDescriptionId { get; set; }

    public Guid? PpeTypeId { get; set; }

    [MaxLength(200)]
    public string? CustomPpeName { get; set; }

    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateJobPpeRequirementDto : UpdateDtoBase
{
    public Guid? PpeTypeId { get; set; }

    [MaxLength(200)]
    public string? CustomPpeName { get; set; }

    public bool IsMandatory { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Job Equipment Training DTOs

public class JobEquipmentTrainingDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobEquipmentToolId { get; set; }
    public Guid? TrainingProgramId { get; set; }
    public string? TrainingProgramName { get; set; }
    public string RequirementText { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
}

public class CreateJobEquipmentTrainingDto : CreateDtoBase
{
    [Required]
    public Guid JobEquipmentToolId { get; set; }

    public Guid? TrainingProgramId { get; set; }

    [Required]
    [MaxLength(500)]
    public string RequirementText { get; set; } = string.Empty;

    public bool IsMandatory { get; set; } = true;
}

public class UpdateJobEquipmentTrainingDto : UpdateDtoBase
{
    public Guid? TrainingProgramId { get; set; }

    [Required]
    [MaxLength(500)]
    public string RequirementText { get; set; } = string.Empty;

    public bool IsMandatory { get; set; }
}

#endregion

#region Job Medical Requirement DTOs

public class JobMedicalRequirementDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public MedicalRequirementCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public string RequirementDescription { get; set; } = string.Empty;
    public string? Rationale { get; set; }
    public string? Contraindications { get; set; }
    public bool IsMandatory { get; set; }
}

public class CreateJobMedicalRequirementDto : CreateDtoBase
{
    [Required]
    public Guid JobDescriptionId { get; set; }

    [Required]
    public MedicalRequirementCategory Category { get; set; }

    [Required]
    [MaxLength(1000)]
    public string RequirementDescription { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Rationale { get; set; }

    [MaxLength(1000)]
    public string? Contraindications { get; set; }

    public bool IsMandatory { get; set; } = true;
}

public class UpdateJobMedicalRequirementDto : UpdateDtoBase
{
    [Required]
    public MedicalRequirementCategory Category { get; set; }

    [Required]
    [MaxLength(1000)]
    public string RequirementDescription { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Rationale { get; set; }

    [MaxLength(1000)]
    public string? Contraindications { get; set; }

    public bool IsMandatory { get; set; }
}

#endregion

#region Job Analytics DTOs

/// <summary>Read-only aggregates for the Job Analysis dashboard.</summary>
public class JobAnalyticsDto
{
    public int TotalJobDescriptions { get; set; }
    public int DraftCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int ApprovedCount { get; set; }
    public int ActiveCount { get; set; }
    public int DueForReviewCount { get; set; }
    public int PositionsCovered { get; set; }

    /// <summary>Every live position in the tenant — the denominator <see cref="PositionsCovered"/> was missing.</summary>
    /// <remarks>
    /// ⚠ "1 position covered" is not a fact anyone can act on without knowing whether that is 1 of 2
    /// or 1 of 146. FR-HR-134 asks for approved job descriptions <i>against positions</i>, so
    /// coverage — and the list of what is NOT covered — is the whole reporting question.
    /// </remarks>
    public int TotalPositions { get; set; }

    /// <summary>Positions with no approved job description at all.</summary>
    public int PositionsUncovered { get; set; }

    /// <summary>Positions whose establishment has been authorised (FR-HR-136).</summary>
    public int PositionsEstablished { get; set; }

    /// <summary>⚠ Established posts holding more people than the establishment allows.</summary>
    /// <remarks>
    /// The one number on this dashboard that is a live problem rather than a progress bar: every
    /// one of these refuses new movements and requisitions until it is resolved.
    /// </remarks>
    public int PositionsOverStrength { get; set; }
    public int ValuedRoleCount { get; set; }
    public decimal? AverageEstimatedSalary { get; set; }
    public int MissionCriticalRoleCount { get; set; }
    public List<NameCountDto> StatusBreakdown { get; set; } = new();
    public List<NameCountDto> TopCompetencies { get; set; } = new();
    public List<NameCountDto> FamilyBreakdown { get; set; } = new();
}

public class NameCountDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

#endregion

#region Job Valuation DTOs

/// <summary>
/// Computed job-evaluation summary: rolls up per-item monetary values + role intrinsic value
/// into an estimated salary range and a suggested salary grade.
/// </summary>
public class JobValuationSummaryDto
{
    public Guid JobDescriptionId { get; set; }
    public string JobTitle { get; set; } = string.Empty;

    public decimal TotalQualificationValue { get; set; }
    public decimal TotalCompetencyValue { get; set; }
    public decimal RoleIntrinsicValue { get; set; }
    public decimal TotalEstimatedValue => TotalQualificationValue + TotalCompetencyValue + RoleIntrinsicValue;

    public RoleCriticalityLevel? RoleCriticality { get; set; }
    public string? RoleCriticalityName => RoleCriticality?.ToString();
    public decimal? IndustryBenchmarkSalary { get; set; }

    public decimal? EstimatedSalaryLow { get; set; }
    public decimal? EstimatedSalaryHigh { get; set; }

    public Guid? SuggestedSalaryGradeId { get; set; }
    public string? SuggestedSalaryGradeName { get; set; }
    public decimal? SuggestedGradeMinSalary { get; set; }
    public decimal? SuggestedGradeMaxSalary { get; set; }

    public string? ValuationNotes { get; set; }

    public List<JobValuationLineDto> QualificationLines { get; set; } = new();
    public List<JobValuationLineDto> CompetencyLines { get; set; } = new();
}

/// <summary>A single valued item (qualification or competency) in the valuation breakdown.</summary>
public class JobValuationLineDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? MonetaryValue { get; set; }
}

#endregion


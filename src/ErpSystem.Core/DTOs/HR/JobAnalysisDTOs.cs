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

    public JobDescriptionStatus Status { get; set; }
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

    [Range(0, double.MaxValue)]
    public decimal ActualSpent { get; set; }

    public ManpowerBudgetStatus Status { get; set; }

    [MaxLength(2000)]
    public string? BusinessJustification { get; set; }
}

/// <summary>
/// DTO for approving a manpower budget
/// </summary>
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

    [Range(0, double.MaxValue)]
    public decimal PlannedAverageSalary { get; set; }

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

    [Range(0, double.MaxValue)]
    public decimal PlannedAverageSalary { get; set; }

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


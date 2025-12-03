using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class JobDescriptionListDto
{
    public Guid Id { get; set; }
    public string JobDescriptionNumber { get; set; }
    public string JobTitle { get; set; }
    public string PositionName { get; set; }
    public string Department { get; set; }
    public int Version { get; set; }
    public DateTime EffectiveDate { get; set; }
    public JobDescriptionStatus Status { get; set; }
    public string StatusName { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Detail DTO
public class JobDescriptionDetailDto
{
    public Guid Id { get; set; }
    public string JobDescriptionNumber { get; set; }

    // Basic Info
    public Guid PositionId { get; set; }
    public string PositionName { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; }
    public Guid? SectionId { get; set; }
    public string SectionName { get; set; }
    public Guid? UnitId { get; set; }
    public string UnitName { get; set; }

    // Job Details
    public string JobTitle { get; set; }
    public string AlternateTitle { get; set; }
    public int Version { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string RevisionReason { get; set; }

    // Reporting Structure
    public Guid? ReportsToPositionId { get; set; }
    public string ReportsToPositionName { get; set; }
    public int? NumberOfDirectReports { get; set; }
    public int? NumberOfIndirectReports { get; set; }

    // Job Summary
    public string JobPurpose { get; set; }
    public string JobSummary { get; set; }

    // Job Details
    public JobLevel? JobLevel { get; set; }
    public string JobLevelName { get; set; }
    public JobGrade? JobGrade { get; set; }
    public string JobGradeName { get; set; }
    public string SalaryGrade { get; set; }
    public FLSAClassification? FlsaClassification { get; set; }
    public string FlsaClassificationName { get; set; }
    // public EmploymentType EmploymentType { get; set; }
    public string EmploymentTypeName { get; set; }

    // Work Environment
    public string WorkLocation { get; set; }
    public string WorkSchedule { get; set; }
    public bool IsRemoteWorkAllowed { get; set; }
    public int? TravelRequiredPercentage { get; set; }
    public string PhysicalDemands { get; set; }
    public string WorkingConditions { get; set; }

    // Authority & Budget
    public string DecisionMakingAuthority { get; set; }
    public string FinancialAuthority { get; set; }
    public decimal? BudgetResponsibility { get; set; }

    // Salary Range
    public decimal? MinSalary { get; set; }
    public decimal? MidSalary { get; set; }
    public decimal? MaxSalary { get; set; }

    // Status
    public JobDescriptionStatus Status { get; set; }
    public string StatusName { get; set; }
    public bool IsCurrent { get; set; }
    public Guid? SupersededByVersionId { get; set; }
    public string SupersededByVersionNumber { get; set; }

    // Workflow
    public Guid? PreparedById { get; set; }
    public string PreparedByName { get; set; }
    public DateTime? PreparedDate { get; set; }
    public Guid? ReviewedById { get; set; }
    public string ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }

    // Collections
    public List<JobResponsibilityDto> Responsibilities { get; set; }
    public List<JobQualificationDto> Qualifications { get; set; }
    public List<JobCompetencyDto> Competencies { get; set; }
    public List<JobRelationshipDto> Relationships { get; set; }
    public List<JobDescriptionAttachmentDto> Attachments { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateJobDescriptionDto
{
    public Guid PositionId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid? UnitId { get; set; }
    public string JobTitle { get; set; }
    public string AlternateTitle { get; set; }
    public DateTime EffectiveDate { get; set; }
    public Guid? ReportsToPositionId { get; set; }
    public int? NumberOfDirectReports { get; set; }
    public int? NumberOfIndirectReports { get; set; }
    public string JobPurpose { get; set; }
    public string JobSummary { get; set; }
    public JobLevel? JobLevel { get; set; }
    public JobGrade? JobGrade { get; set; }
    public string SalaryGrade { get; set; }
    public FLSAClassification? FlsaClassification { get; set; }
    // public EmploymentType EmploymentType { get; set; }
    public string WorkLocation { get; set; }
    public string WorkSchedule { get; set; }
    public bool IsRemoteWorkAllowed { get; set; }
    public int? TravelRequiredPercentage { get; set; }
    public string PhysicalDemands { get; set; }
    public string WorkingConditions { get; set; }
    public string DecisionMakingAuthority { get; set; }
    public string FinancialAuthority { get; set; }
    public decimal? BudgetResponsibility { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MidSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public List<CreateJobResponsibilityDto> Responsibilities { get; set; }
    public List<CreateJobQualificationDto> Qualifications { get; set; }
    public List<CreateJobCompetencyDto> Competencies { get; set; }
    public List<CreateJobRelationshipDto> Relationships { get; set; }
}

// Supporting DTOs
public class JobResponsibilityDto
{
    public Guid Id { get; set; }
    public ResponsibilityType Type { get; set; }
    public string TypeName { get; set; }
    public string ResponsibilityDescription { get; set; }
    public int? PercentageOfTime { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateJobResponsibilityDto
{
    public ResponsibilityType Type { get; set; }
    public string ResponsibilityDescription { get; set; }
    public int? PercentageOfTime { get; set; }
    public int DisplayOrder { get; set; }
}

public class JobQualificationDto
{
    public Guid Id { get; set; }
    public QualificationType QualificationType { get; set; }
    public string QualificationTypeName { get; set; }
    public string Description { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateJobQualificationDto
{
    public QualificationType QualificationType { get; set; }
    public string Description { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
}

public class JobCompetencyDto
{
    public Guid Id { get; set; }
    public CompetencyType CompetencyType { get; set; }
    public string CompetencyTypeName { get; set; }
    public string CompetencyName { get; set; }
    public string Description { get; set; }
    public ProficiencyLevel RequiredLevel { get; set; }
    public string RequiredLevelName { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateJobCompetencyDto
{
    public CompetencyType CompetencyType { get; set; }
    public string CompetencyName { get; set; }
    public string Description { get; set; }
    public ProficiencyLevel RequiredLevel { get; set; }
    public int DisplayOrder { get; set; }
}

public class JobRelationshipDto
{
    public Guid Id { get; set; }
    public RelationshipType RelationshipType { get; set; }
    public string RelationshipTypeName { get; set; }
    public string RelationshipWith { get; set; }
    public string Purpose { get; set; }
    public InteractionFrequency Frequency { get; set; }
    public string FrequencyName { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateJobRelationshipDto
{
    public RelationshipType RelationshipType { get; set; }
    public string RelationshipWith { get; set; }
    public string Purpose { get; set; }
    public InteractionFrequency Frequency { get; set; }
    public int DisplayOrder { get; set; }
}

public class JobDescriptionAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

// Manpower Budget DTOs
public class ManpowerBudgetListDto
{
    public Guid Id { get; set; }
    public string BudgetNumber { get; set; }
    public string FiscalYear { get; set; }
    public string DepartmentName { get; set; }
    public ManpowerBudgetStatus Status { get; set; }
    public string StatusName { get; set; }
    public int TotalCurrentHeadcount { get; set; }
    public int TotalPlannedHeadcount { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal ActualSpent { get; set; }
}

public class ManpowerBudgetDetailDto
{
    public Guid Id { get; set; }
    public string BudgetNumber { get; set; }
    public string FiscalYear { get; set; }

    // Department Info
    public Guid? DepartmentId { get; set; }
    public string DepartmentName { get; set; }
    public Guid? DivisionId { get; set; }
    public string DivisionName { get; set; }

    // Status
    public ManpowerBudgetStatus Status { get; set; }
    public string StatusName { get; set; }

    // Business Justification
    public string BusinessJustification { get; set; }
    public string StrategicAlignment { get; set; }

    // Summary
    public int TotalCurrentHeadcount { get; set; }
    public int TotalPlannedHeadcount { get; set; }
    public int NetChange { get; set; }

    // Budget
    public decimal CurrentSalaryCost { get; set; }
    public decimal PlannedSalaryCost { get; set; }
    public decimal SalaryBudget { get; set; }
    public decimal BenefitsBudget { get; set; }
    public decimal RecruitmentBudget { get; set; }
    public decimal TrainingBudget { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal ActualSpent { get; set; }
    public decimal Variance { get; set; }

    // Approval
    public Guid? CreatedById { get; set; }
    public string CreatedByName { get; set; }
    public Guid? ApprovedById { get; set; }
    public string ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }

    // Collections
    public List<ManpowerBudgetLineDto> BudgetLines { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ManpowerBudgetLineDto
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public string PositionName { get; set; }
    public BudgetPriority Priority { get; set; }
    public string PriorityName { get; set; }
    public int CurrentHeadcount { get; set; }
    public int PlannedHeadcount { get; set; }
    public int NetChange { get; set; }
    public decimal CurrentAverageSalary { get; set; }
    public decimal CurrentTotalCost { get; set; }
    public decimal PlannedAverageSalary { get; set; }
    public decimal PlannedTotalCost { get; set; }
    public string Notes { get; set; }
}
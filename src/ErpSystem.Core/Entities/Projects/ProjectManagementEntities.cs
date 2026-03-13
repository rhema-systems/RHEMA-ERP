using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Projects;

public static class ProjectStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Planned = "Planned";
    public const string InProgress = "InProgress";
    public const string OnHold = "OnHold";
    public const string Completed = "Completed";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
    public const string Archived = "Archived";
}

public static class ProjectWorkItemNodeTypes
{
    public const string Phase = "Phase";
    public const string Workstream = "Workstream";
    public const string Task = "Task";
    public const string Subtask = "Subtask";
    public const string ChecklistItem = "ChecklistItem";
}

public class ProjectType : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public bool RequiresSponsor { get; set; } = true;
    public bool RequiresApproval { get; set; } = true;

    [MaxLength(4000)]
    public string? MandatoryFieldsJson { get; set; }

    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
    public virtual ICollection<ProjectTemplate> Templates { get; set; } = new List<ProjectTemplate>();
}

public class ProjectPriority : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ColorHex { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
}

public class ProjectTemplate : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? ProjectTypeId { get; set; }

    [MaxLength(50)]
    public string VersionLabel { get; set; } = "1.0";

    [MaxLength(4000)]
    public string? TemplateDefinitionJson { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(ProjectTypeId))]
    public virtual ProjectType? ProjectType { get; set; }
}

public class ProjectPortfolio : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Active";

    [MaxLength(200)]
    public string? StrategicObjective { get; set; }

    public Guid? OwnerId { get; set; }
    public Guid? SponsorId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BudgetCap { get; set; }

    public virtual ICollection<ProjectProgram> Programs { get; set; } = new List<ProjectProgram>();
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
}

public class ProjectProgram : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? PortfolioId { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Active";

    public Guid? ProgramManagerId { get; set; }
    public Guid? SponsorId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BudgetCap { get; set; }

    [ForeignKey(nameof(PortfolioId))]
    public virtual ProjectPortfolio? Portfolio { get; set; }

    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
}

public class ProjectManagementSettings : TenantEntity
{
    [MaxLength(100)]
    public string ProjectNumberFormat { get; set; } = "PRJ-{YYYY}-{####}";

    public bool RequireSponsor { get; set; } = true;
    public bool DefaultApprovalRequired { get; set; } = true;
    public Guid? DefaultProjectTypeId { get; set; }
    public Guid? DefaultProjectPriorityId { get; set; }
    public Guid? DefaultTemplateId { get; set; }

    [MaxLength(4000)]
    public string? MandatoryFieldsByTypeJson { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class ProjectCatalogEntry : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string CatalogType { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Project : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ProjectCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Summary { get; set; }

    [MaxLength(4000)]
    public string? BusinessCase { get; set; }

    [MaxLength(4000)]
    public string? Objectives { get; set; }

    [MaxLength(1000)]
    public string? StrategicAlignment { get; set; }

    public Guid? ProjectTypeId { get; set; }
    public Guid? ProjectPriorityId { get; set; }
    public Guid? TemplateId { get; set; }
    public Guid? PortfolioId { get; set; }
    public Guid? ProgramId { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectStatuses.Draft;

    [MaxLength(100)]
    public string Methodology { get; set; } = "Hybrid";

    public Guid? SponsorId { get; set; }
    public Guid? ProjectManagerId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? TenderId { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedBudget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ApprovedBudget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualCost { get; set; }

    [MaxLength(50)]
    public string BudgetStatus { get; set; } = "NotStarted";

    [Column(TypeName = "decimal(5,2)")]
    public decimal ProgressPercent { get; set; }

    public bool ApprovalRequired { get; set; } = true;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [MaxLength(4000)]
    public string? ScopeStatement { get; set; }

    [MaxLength(4000)]
    public string? Assumptions { get; set; }

    [MaxLength(4000)]
    public string? Constraints { get; set; }

    [MaxLength(2000)]
    public string? ExpectedBenefits { get; set; }

    [MaxLength(500)]
    public string? FundingSource { get; set; }

    [MaxLength(2000)]
    public string? StatusRemarks { get; set; }

    public bool ExternalPortalAccessEnabled { get; set; }
    public bool ExternalCollaborationEnabled { get; set; }

    [ForeignKey(nameof(ProjectTypeId))]
    public virtual ProjectType? ProjectType { get; set; }

    [ForeignKey(nameof(ProjectPriorityId))]
    public virtual ProjectPriority? ProjectPriority { get; set; }

    [ForeignKey(nameof(TemplateId))]
    public virtual ProjectTemplate? Template { get; set; }

    [ForeignKey(nameof(PortfolioId))]
    public virtual ProjectPortfolio? Portfolio { get; set; }

    [ForeignKey(nameof(ProgramId))]
    public virtual ProjectProgram? Program { get; set; }

    public virtual ICollection<ProjectInitiationVersion> InitiationVersions { get; set; } = new List<ProjectInitiationVersion>();
    public virtual ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public virtual ICollection<ProjectWorkItem> WorkItems { get; set; } = new List<ProjectWorkItem>();
    public virtual ICollection<ProjectMilestone> Milestones { get; set; } = new List<ProjectMilestone>();
    public virtual ICollection<ProjectResourceAllocation> ResourceAllocations { get; set; } = new List<ProjectResourceAllocation>();
    public virtual ICollection<ProjectRisk> Risks { get; set; } = new List<ProjectRisk>();
    public virtual ICollection<ProjectIssue> Issues { get; set; } = new List<ProjectIssue>();
    public virtual ICollection<ProjectQualityCheckpoint> QualityCheckpoints { get; set; } = new List<ProjectQualityCheckpoint>();
    public virtual ICollection<ProjectNonConformance> NonConformances { get; set; } = new List<ProjectNonConformance>();
    public virtual ICollection<ProjectChangeRequest> ChangeRequests { get; set; } = new List<ProjectChangeRequest>();
    public virtual ICollection<ProjectBillingSchedule> BillingSchedules { get; set; } = new List<ProjectBillingSchedule>();
    public virtual ICollection<ProjectInvoiceRequest> InvoiceRequests { get; set; } = new List<ProjectInvoiceRequest>();
    public virtual ICollection<ProjectDeliverable> Deliverables { get; set; } = new List<ProjectDeliverable>();
    public virtual ICollection<ProjectTaskDependency> TaskDependencies { get; set; } = new List<ProjectTaskDependency>();
    public virtual ICollection<ProjectBaseline> Baselines { get; set; } = new List<ProjectBaseline>();
    public virtual ICollection<ProjectTimesheetEntry> TimesheetEntries { get; set; } = new List<ProjectTimesheetEntry>();
    public virtual ICollection<ProjectExpense> Expenses { get; set; } = new List<ProjectExpense>();
    public virtual ICollection<ProjectRevenueRecognition> RevenueRecognitions { get; set; } = new List<ProjectRevenueRecognition>();
    public virtual ICollection<ProjectBudgetRevision> BudgetRevisions { get; set; } = new List<ProjectBudgetRevision>();
    public virtual ICollection<ProjectForecastVersion> ForecastVersions { get; set; } = new List<ProjectForecastVersion>();
    public virtual ICollection<ProjectAssetLink> AssetLinks { get; set; } = new List<ProjectAssetLink>();
    public virtual ICollection<ProjectExternalAccessPolicy> ExternalAccessPolicies { get; set; } = new List<ProjectExternalAccessPolicy>();
    public virtual ICollection<ProjectDecision> Decisions { get; set; } = new List<ProjectDecision>();
    public virtual ICollection<ProjectMeetingMinute> Meetings { get; set; } = new List<ProjectMeetingMinute>();
    public virtual ICollection<ProjectActionItem> ActionItems { get; set; } = new List<ProjectActionItem>();
    public virtual ICollection<ProjectLessonLearned> LessonsLearned { get; set; } = new List<ProjectLessonLearned>();
    public virtual ICollection<ProjectDocument> Documents { get; set; } = new List<ProjectDocument>();
    public virtual ICollection<ProjectComment> Comments { get; set; } = new List<ProjectComment>();
    public virtual ProjectClosure? Closure { get; set; }
}

public class ProjectInitiationVersion : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public int VersionNumber { get; set; } = 1;

    [Required]
    [MaxLength(12000)]
    public string SnapshotJson { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ChangeType { get; set; } = "Created";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectMember : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "TeamMember";

    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectWorkItem : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? ParentId { get; set; }

    [Required]
    [MaxLength(30)]
    public string NodeType { get; set; } = ProjectWorkItemNodeTypes.Task;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "New";

    [MaxLength(30)]
    public string Priority { get; set; } = "Normal";

    public int SortOrder { get; set; }
    public Guid? AssignedToUserId { get; set; }

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal PercentComplete { get; set; }

    public bool IsRollupEnabled { get; set; } = true;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EffortEstimateHours { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualEffortHours { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ParentId))]
    public virtual ProjectWorkItem? Parent { get; set; }

    public virtual ICollection<ProjectWorkItem> Children { get; set; } = new List<ProjectWorkItem>();
}

public class ProjectMilestone : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? WorkItemId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime TargetDate { get; set; }
    public DateTime? ActualDate { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    public bool RequiresApproval { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectResourceAllocation : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? WorkItemId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string AllocationRole { get; set; } = "TeamMember";

    [Required]
    [MaxLength(20)]
    public string AllocationType { get; set; } = "Hours";

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocationValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PlannedHours { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [MaxLength(20)]
    public string BookingType { get; set; } = "Soft";

    [MaxLength(20)]
    public string Status { get; set; } = "Requested";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public Guid? SourceAllocationId { get; set; }
    public Guid? ReplacementAllocationId { get; set; }

    [MaxLength(1000)]
    public string? SubstitutionReason { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(WorkItemId))]
    public virtual ProjectWorkItem? WorkItem { get; set; }
}

public class ProjectRisk : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public Guid? OwnerId { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Open";

    [MaxLength(30)]
    public string Category { get; set; } = "General";

    public int Probability { get; set; }
    public int Impact { get; set; }
    public int Exposure { get; set; }

    [MaxLength(100)]
    public string ResponseStrategy { get; set; } = "Monitor";

    [MaxLength(2000)]
    public string? MitigationPlan { get; set; }

    public DateTime? DueDate { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectIssue : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public Guid? OwnerId { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Open";

    [MaxLength(30)]
    public string Severity { get; set; } = "Medium";

    public DateTime? TargetResolutionDate { get; set; }

    [MaxLength(1000)]
    public string? RootCause { get; set; }

    [MaxLength(2000)]
    public string? CorrectiveAction { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectQualityCheckpoint : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? WorkItemId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid? QaOwnerId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Open";

    public DateTime? DueDate { get; set; }
    public bool RequiresQaSignOff { get; set; }
    public DateTime? SignedOffAt { get; set; }
    public Guid? SignedOffById { get; set; }

    [MaxLength(1000)]
    public string? SignOffNotes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(WorkItemId))]
    public virtual ProjectWorkItem? WorkItem { get; set; }

    [ForeignKey(nameof(DeliverableId))]
    public virtual ProjectDeliverable? Deliverable { get; set; }
}

public class ProjectNonConformance : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? QualityCheckpointId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid? OwnerId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(30)]
    public string Severity { get; set; } = "Medium";

    [MaxLength(30)]
    public string Status { get; set; } = "Open";

    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(2000)]
    public string? CorrectiveAction { get; set; }

    [MaxLength(2000)]
    public string? PreventiveAction { get; set; }

    [MaxLength(1000)]
    public string? ResolutionNotes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(QualityCheckpointId))]
    public virtual ProjectQualityCheckpoint? QualityCheckpoint { get; set; }

    [ForeignKey(nameof(DeliverableId))]
    public virtual ProjectDeliverable? Deliverable { get; set; }
}

public class ProjectChangeRequest : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string ChangeType { get; set; } = "Scope";

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    [MaxLength(2000)]
    public string? BusinessImpact { get; set; }

    [MaxLength(2000)]
    public string? RiskImpact { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CostImpact { get; set; }

    public int? ScheduleImpactDays { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectBillingSchedule : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? ContractId { get; set; }
    public Guid? ContractMilestoneId { get; set; }
    public Guid? MilestoneId { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string BillingType { get; set; } = "Milestone";

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? BillingPercentage { get; set; }

    public DateTime BillingDate { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool IsBillable { get; set; } = true;

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(MilestoneId))]
    public virtual ProjectMilestone? Milestone { get; set; }
}

public class ProjectInvoiceRequest : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? BillingScheduleId { get; set; }
    public Guid? ContractId { get; set; }

    [Required]
    [MaxLength(50)]
    public string RequestNumber { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal RequestedAmount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }

    [MaxLength(100)]
    public string? ExternalReference { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(BillingScheduleId))]
    public virtual ProjectBillingSchedule? BillingSchedule { get; set; }
}

public class ProjectDocument : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? FileUploadRecordId { get; set; }

    [Required]
    [MaxLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Category { get; set; } = "General";

    [MaxLength(100)]
    public string DocumentType { get; set; } = "Attachment";

    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? PublicUrl { get; set; }

    [MaxLength(100)]
    public string? FileType { get; set; }

    public long? FileSize { get; set; }

    [MaxLength(50)]
    public string VersionLabel { get; set; } = "1.0";

    [MaxLength(30)]
    public string Status { get; set; } = "Active";

    public DateTime? EffectiveDate { get; set; }
    public bool IsExternalVisible { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(FileUploadRecordId))]
    public virtual FileUploadRecord? FileUploadRecord { get; set; }
}

public class ProjectComment : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? WorkItemId { get; set; }

    [MaxLength(50)]
    public string CommentType { get; set; } = "Comment";

    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? MentionedUsersJson { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectDeliverable : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? WorkItemId { get; set; }
    public Guid? MilestoneId { get; set; }
    public Guid? SubmittedDocumentId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    public DateTime? TargetDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? ExternalApprovedAt { get; set; }
    public Guid? ExternalApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public bool ExternalSubmissionAllowed { get; set; }
    public bool ExternalSignOffRequired { get; set; }
    public bool IsExternalVisible { get; set; }

    [MaxLength(1000)]
    public string? AcceptanceNotes { get; set; }

    [MaxLength(1000)]
    public string? ExternalApprovalNotes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(WorkItemId))]
    public virtual ProjectWorkItem? WorkItem { get; set; }

    [ForeignKey(nameof(MilestoneId))]
    public virtual ProjectMilestone? Milestone { get; set; }

    [ForeignKey(nameof(SubmittedDocumentId))]
    public virtual ProjectDocument? SubmittedDocument { get; set; }
}

public class ProjectTaskDependency : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public Guid PredecessorWorkItemId { get; set; }

    [Required]
    public Guid SuccessorWorkItemId { get; set; }

    [MaxLength(10)]
    public string DependencyType { get; set; } = "FS";

    public int LagDays { get; set; }
    public bool IsEnforced { get; set; } = true;

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectInterdependency : TenantEntity
{
    [Required]
    public Guid SourceProjectId { get; set; }

    [Required]
    public Guid TargetProjectId { get; set; }

    [MaxLength(50)]
    public string DependencyType { get; set; } = "Schedule";

    [MaxLength(30)]
    public string Status { get; set; } = "Open";

    [MaxLength(30)]
    public string ImpactLevel { get; set; } = "Medium";

    public Guid? OwnerId { get; set; }
    public DateTime? DueDate { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? MitigationPlan { get; set; }

    [ForeignKey(nameof(SourceProjectId))]
    public virtual Project SourceProject { get; set; } = null!;

    [ForeignKey(nameof(TargetProjectId))]
    public virtual Project TargetProject { get; set; } = null!;
}

public class ProjectBaseline : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    [MaxLength(16000)]
    public string SnapshotJson { get; set; } = string.Empty;

    public bool IsLocked { get; set; } = true;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectTimesheetEntry : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? WorkItemId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public DateTime EntryDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Hours { get; set; }

    public bool IsBillable { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal HourlyRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CostAmount { get; set; }

    [MaxLength(100)]
    public string WorkType { get; set; } = "Standard";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(WorkItemId))]
    public virtual ProjectWorkItem? WorkItem { get; set; }
}

public class ProjectExpense : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? WorkItemId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public DateTime ExpenseDate { get; set; }

    [MaxLength(100)]
    public string Category { get; set; } = "General";

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    public bool IsBillable { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    public Guid? ReceiptDocumentId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(WorkItemId))]
    public virtual ProjectWorkItem? WorkItem { get; set; }

    [ForeignKey(nameof(ReceiptDocumentId))]
    public virtual ProjectDocument? ReceiptDocument { get; set; }
}

public class ProjectRevenueRecognition : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? InvoiceRequestId { get; set; }

    [Required]
    [MaxLength(20)]
    public string RecognitionPeriod { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal RecognizedRevenue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RecognizedCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossMargin { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CashCollected { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(InvoiceRequestId))]
    public virtual ProjectInvoiceRequest? InvoiceRequest { get; set; }
}

public class ProjectBudgetRevision : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public int VersionNumber { get; set; } = 1;

    [Required]
    [MaxLength(120)]
    public string RevisionName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string RevisionType { get; set; } = "Revision";

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedBudget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ApprovedBudget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CommittedCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ForecastCost { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal ThresholdWarningPercent { get; set; } = 75m;

    [Column(TypeName = "decimal(5,2)")]
    public decimal ThresholdCriticalPercent { get; set; } = 90m;

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }

    [MaxLength(2000)]
    public string? ChangeReason { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectForecastVersion : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public int VersionNumber { get; set; } = 1;

    [Required]
    [MaxLength(120)]
    public string VersionName { get; set; } = string.Empty;

    public DateTime AsOfDate { get; set; } = DateTime.UtcNow.Date;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ForecastCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimateAtCompletion { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ForecastRevenue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ForecastMargin { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectAssetLink : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? MaintenanceAssetId { get; set; }
    public Guid? CompanyAssetId { get; set; }
    public Guid? JobCardId { get; set; }

    [MaxLength(30)]
    public string LinkType { get; set; } = "Asset";

    [MaxLength(30)]
    public string Status { get; set; } = "Linked";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectExternalAccessPolicy : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ArtifactType { get; set; } = "Project";

    public Guid? ArtifactId { get; set; }

    [MaxLength(30)]
    public string AccessLevel { get; set; } = "Read";

    public bool CanComment { get; set; }
    public bool CanUpload { get; set; }
    public bool CanApprove { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectDecision : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateTime DecisionDate { get; set; } = DateTime.UtcNow;
    public Guid? ApproverId { get; set; }

    [MaxLength(4000)]
    public string? Rationale { get; set; }

    [MaxLength(4000)]
    public string? AlternativesConsidered { get; set; }

    [MaxLength(2000)]
    public string? ImpactSummary { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    public DateTime? ApprovedAt { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectMeetingMinute : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateTime MeetingDate { get; set; } = DateTime.UtcNow;
    public Guid? FacilitatorId { get; set; }

    [MaxLength(100)]
    public string MeetingType { get; set; } = "Status";

    [MaxLength(4000)]
    public string? Minutes { get; set; }

    [MaxLength(2000)]
    public string? AttendeesJson { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    public virtual ICollection<ProjectActionItem> ActionItems { get; set; } = new List<ProjectActionItem>();
}

public class ProjectActionItem : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public Guid? MeetingMinuteId { get; set; }
    public Guid? WorkItemId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? OwnerId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Open";

    [MaxLength(30)]
    public string Priority { get; set; } = "Normal";

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(MeetingMinuteId))]
    public virtual ProjectMeetingMinute? MeetingMinute { get; set; }

    [ForeignKey(nameof(WorkItemId))]
    public virtual ProjectWorkItem? WorkItem { get; set; }
}

public class ProjectLessonLearned : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Category { get; set; } = "General";

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? Recommendation { get; set; }

    [MaxLength(100)]
    public string? AppliedPhase { get; set; }

    [MaxLength(30)]
    public string Visibility { get; set; } = "Internal";

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectClosure : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FinalBudget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FinalCost { get; set; }

    public bool DeliverablesAccepted { get; set; }
    public bool TasksCompletedOrWaived { get; set; }
    public bool AssetsReconciled { get; set; }
    public bool OpenItemsDisposed { get; set; }

    [MaxLength(4000)]
    public string? ClosureChecklistJson { get; set; }

    [MaxLength(2000)]
    public string? OpenItemsDisposition { get; set; }

    [MaxLength(2000)]
    public string? AssetReconciliationNotes { get; set; }

    [MaxLength(4000)]
    public string? LessonsLearnedSummary { get; set; }

    [MaxLength(4000)]
    public string? PostImplementationReview { get; set; }

    [MaxLength(1000)]
    public string? OverrideReason { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

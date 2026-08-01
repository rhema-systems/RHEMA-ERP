using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;

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

public static class ProjectDeliveryStructures
{
    public const string WholeDevelopment = "WholeDevelopment";
    public const string SingleUnit = "SingleUnit";
    public const string MultiUnit = "MultiUnit";
}

public static class ProjectPhaseStatuses
{
    public const string NotStarted = "NotStarted";
    public const string InProgress = "InProgress";
    public const string Blocked = "Blocked";
    public const string Completed = "Completed";
    public const string Waived = "Waived";
    public const string Cancelled = "Cancelled";
}

public static class ProjectStageGateRequirementTypes
{
    public const string ApprovedApprovals = "ApprovedApprovals";
    public const string Packages = "Packages";
    public const string BoqItems = "BoqItems";
    public const string Documents = "Documents";
    public const string CompletedCommissioningItems = "CompletedCommissioningItems";
    public const string CompletedHandoverItems = "CompletedHandoverItems";
    public const string OpenSnagItems = "OpenSnagItems";
}

public static class ProjectStageGateScopes
{
    public const string Phase = "Phase";
    public const string Project = "Project";
}

public static class ProjectPackageStatuses
{
    public const string Planned = "Planned";
    public const string ProcurementPending = "ProcurementPending";
    public const string Awarded = "Awarded";
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string OnHold = "OnHold";
    public const string Cancelled = "Cancelled";
}

public static class ProjectPackageTypes
{
    public const string WorkPackage = "WorkPackage";
    public const string TradePackage = "TradePackage";
    public const string SupplyPackage = "SupplyPackage";
    public const string ProvisionalSum = "ProvisionalSum";
}

public static class ProjectBoqItemTypes
{
    public const string Item = "Item";
    public const string ProvisionalSum = "ProvisionalSum";
    public const string PrimeCost = "PrimeCost";
    public const string Variation = "Variation";
    public const string Allowance = "Allowance";
}

public static class ProjectApprovalRegisterStatuses
{
    public const string Planned = "Planned";
    public const string InPreparation = "InPreparation";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";
    public const string Waived = "Waived";
}

public static class ProjectApprovalRegisterTypes
{
    public const string PlanningPermission = "PlanningPermission";
    public const string BuildingPermit = "BuildingPermit";
    public const string EnvironmentalPermit = "EnvironmentalPermit";
    public const string FireClearance = "FireClearance";
    public const string UtilityClearance = "UtilityClearance";
    public const string InspectionCertificate = "InspectionCertificate";
    public const string OccupancyCertificate = "OccupancyCertificate";
    public const string Other = "Other";
}

public static class ProjectDrawingStatuses
{
    public const string Draft = "Draft";
    public const string ForReview = "ForReview";
    public const string ApprovedForConstruction = "ApprovedForConstruction";
    public const string ApprovedAsBuilt = "ApprovedAsBuilt";
    public const string Superseded = "Superseded";
    public const string Archived = "Archived";
}

public static class ProjectDrawingDisciplines
{
    public const string Architectural = "Architectural";
    public const string Structural = "Structural";
    public const string Mechanical = "Mechanical";
    public const string Electrical = "Electrical";
    public const string Plumbing = "Plumbing";
    public const string Civil = "Civil";
    public const string FireProtection = "FireProtection";
    public const string Interior = "Interior";
    public const string Other = "Other";
}

public static class ProjectSubmittalStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string ApprovedWithComments = "ApprovedWithComments";
    public const string Rejected = "Rejected";
    public const string ResubmissionRequired = "ResubmissionRequired";
    public const string Closed = "Closed";
}

public static class ProjectSubmittalTypes
{
    public const string Material = "Material";
    public const string ShopDrawing = "ShopDrawing";
    public const string MethodStatement = "MethodStatement";
    public const string Sample = "Sample";
    public const string TechnicalData = "TechnicalData";
    public const string Mockup = "Mockup";
    public const string Other = "Other";
}

public static class ProjectRfiStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Answered = "Answered";
    public const string Closed = "Closed";
    public const string Void = "Void";
}

public static class ProjectRfiPriorities
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";
}

public static class ProjectSiteInstructionStatuses
{
    public const string Draft = "Draft";
    public const string Issued = "Issued";
    public const string Acknowledged = "Acknowledged";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
}

public static class ProjectSiteInstructionTypes
{
    public const string SiteInstruction = "SiteInstruction";
    public const string ArchitectInstruction = "ArchitectInstruction";
    public const string EngineerInstruction = "EngineerInstruction";
    public const string VariationInstruction = "VariationInstruction";
    public const string SafetyInstruction = "SafetyInstruction";
    public const string QualityInstruction = "QualityInstruction";
    public const string Other = "Other";
}

public static class ProjectVariationOrderStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Implemented = "Implemented";
    public const string Closed = "Closed";
}

public static class ProjectVariationOrderTypes
{
    public const string ScopeChange = "ScopeChange";
    public const string QuantityAdjustment = "QuantityAdjustment";
    public const string ProvisionalSum = "ProvisionalSum";
    public const string RateChange = "RateChange";
    public const string Omission = "Omission";
    public const string Other = "Other";
}

public static class ProjectInterimValuationStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Certified = "Certified";
    public const string Paid = "Paid";
    public const string Rejected = "Rejected";
}

public static class ProjectDocumentArtifactTypes
{
    public const string Project = "Project";
    public const string Phase = "Phase";
    public const string Package = "Package";
    public const string WorkItem = "WorkItem";
}

public static class ProjectUnitInventoryStatuses
{
    public const string PendingRelease = "PendingRelease";
    public const string Released = "Released";
    public const string Allocated = "Allocated";
    public const string Invoiced = "Invoiced";
}

public static class ProjectPaymentCertificateStatuses
{
    public const string Draft = "Draft";
    public const string Issued = "Issued";
    public const string Approved = "Approved";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
}

public static class ProjectPaymentCertificateAuditEvents
{
    public const string Resource = "ProjectPaymentCertificate";
    public const string Snapshot = "ProjectPaymentCertificateSnapshot";
}

public static class ProjectExtensionOfTimeStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Implemented = "Implemented";
    public const string Closed = "Closed";
}

public static class ProjectFinalAccountStatuses
{
    public const string Draft = "Draft";
    public const string UnderReview = "UnderReview";
    public const string Agreed = "Agreed";
    public const string Approved = "Approved";
    public const string Closed = "Closed";
}

public static class ProjectUnitStatuses
{
    public const string Planned = "Planned";
    public const string Available = "Available";
    public const string Reserved = "Reserved";
    public const string Sold = "Sold";
    public const string Leased = "Leased";
    public const string HandedOver = "HandedOver";
    public const string Occupied = "Occupied";
    public const string Archived = "Archived";
}

public static class ProjectUnitHandoverStatuses
{
    public const string NotScheduled = "NotScheduled";
    public const string Pending = "Pending";
    public const string Scheduled = "Scheduled";
    public const string Due = "Due";
    public const string HandedOver = "HandedOver";
    public const string Occupied = "Occupied";
}

public static class ProjectUnitCommercialIntents
{
    public const string Sale = "Sale";
    public const string Lease = "Lease";
}

public static class ProjectUnitTypes
{
    public const string WholeBuilding = "WholeBuilding";
    public const string Apartment = "Apartment";
    public const string OfficeSuite = "OfficeSuite";
    public const string RetailShop = "RetailShop";
    public const string Warehouse = "Warehouse";
    public const string Unit = "Unit";
}

public static class ProjectUnitReleaseBatchStatuses
{
    public const string Draft = "Draft";
    public const string Planned = "Planned";
    public const string Released = "Released";
    public const string Closed = "Closed";
}

public static class ProjectUnitHandoverBatchStatuses
{
    public const string Planned = "Planned";
    public const string InPreparation = "InPreparation";
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string Closed = "Closed";
}

public static class ProjectCustomerVariationStatuses
{
    public const string Requested = "Requested";
    public const string UnderReview = "UnderReview";
    public const string Quoted = "Quoted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Billed = "Billed";
    public const string Cancelled = "Cancelled";
}

public static class ProjectCustomerVariationTimings
{
    public const string PreHandover = "PreHandover";
    public const string PostHandover = "PostHandover";
}

public static class ProjectCommissioningItemStatuses
{
    public const string Planned = "Planned";
    public const string InProgress = "InProgress";
    public const string ReadyForInspection = "ReadyForInspection";
    public const string Completed = "Completed";
    public const string Waived = "Waived";
}

public static class ProjectHandoverItemStatuses
{
    public const string Planned = "Planned";
    public const string InPreparation = "InPreparation";
    public const string Ready = "Ready";
    public const string Completed = "Completed";
    public const string Waived = "Waived";
}

public static class ProjectHandoverItemTypes
{
    public const string PracticalCompletion = "PracticalCompletion";
    public const string AsBuiltDrawing = "AsBuiltDrawing";
    public const string OperationManual = "OperationManual";
    public const string KeyHandover = "KeyHandover";
    public const string OccupancyCertificate = "OccupancyCertificate";
    public const string FinalCompletion = "FinalCompletion";
    public const string Other = "Other";
}

public static class ProjectSnagStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string ReadyForVerification = "ReadyForVerification";
    public const string Closed = "Closed";
    public const string Waived = "Waived";
}

public static class ProjectSnagSeverities
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";
}

public static class ProjectDefectLiabilityStatuses
{
    public const string Reported = "Reported";
    public const string UnderReview = "UnderReview";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
    public const string WarrantyExpired = "WarrantyExpired";
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

public class ProjectPhaseTemplate : TenantEntity
{
    public Guid? ProjectTypeId { get; set; }
    public Guid? ParentPhaseTemplateId { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(30)]
    public string DefaultStatus { get; set; } = ProjectPhaseStatuses.NotStarted;

    public int SortOrder { get; set; }
    [Column(TypeName = "decimal(5,2)")]
    public decimal CompletionWeightPercent { get; set; }
    public bool IsOptional { get; set; }
    public bool IsStageGateRequired { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    public string? AppliesToDeliveryStructure { get; set; }

    [MaxLength(100)]
    public string? AppliesToDevelopmentType { get; set; }

    [ForeignKey(nameof(ProjectTypeId))]
    public virtual ProjectType? ProjectType { get; set; }

    [ForeignKey(nameof(ParentPhaseTemplateId))]
    public virtual ProjectPhaseTemplate? ParentPhaseTemplate { get; set; }

    public virtual ICollection<ProjectPhaseTemplate> Children { get; set; } = new List<ProjectPhaseTemplate>();
    public virtual ICollection<ProjectStageGateRule> StageGateRules { get; set; } = new List<ProjectStageGateRule>();
}

public class ProjectStageGateRule : TenantEntity
{
    public Guid ProjectPhaseTemplateId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string RequirementType { get; set; } = ProjectStageGateRequirementTypes.Packages;

    [Required]
    [MaxLength(20)]
    public string Scope { get; set; } = ProjectStageGateScopes.Phase;

    public int? MinimumCount { get; set; }
    public int? MaximumCount { get; set; }
    public bool IsBlocking { get; set; } = true;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(ProjectPhaseTemplateId))]
    public virtual ProjectPhaseTemplate ProjectPhaseTemplate { get; set; } = null!;
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
    public int SlackMonths { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedBudget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ApprovedBudget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualCost { get; set; }

    [MaxLength(10)]
    public string? BaseCurrencyCode { get; set; }

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

    public virtual ProjectDevelopmentProfile? DevelopmentProfile { get; set; }

    public virtual ICollection<ProjectInitiationVersion> InitiationVersions { get; set; } = new List<ProjectInitiationVersion>();
    public virtual ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public virtual ICollection<ProjectPhase> Phases { get; set; } = new List<ProjectPhase>();
    public virtual ICollection<ProjectPackage> Packages { get; set; } = new List<ProjectPackage>();
    public virtual ICollection<ProjectBoqItem> BoqItems { get; set; } = new List<ProjectBoqItem>();
    public virtual ICollection<ProjectApprovalRegisterItem> ApprovalRegisterItems { get; set; } = new List<ProjectApprovalRegisterItem>();
    public virtual ICollection<ProjectDrawing> Drawings { get; set; } = new List<ProjectDrawing>();
    public virtual ICollection<ProjectSubmittal> Submittals { get; set; } = new List<ProjectSubmittal>();
    public virtual ICollection<ProjectRfi> Rfis { get; set; } = new List<ProjectRfi>();
    public virtual ICollection<ProjectSiteInstruction> SiteInstructions { get; set; } = new List<ProjectSiteInstruction>();
    public virtual ICollection<ProjectVariationOrder> VariationOrders { get; set; } = new List<ProjectVariationOrder>();
    public virtual ICollection<ProjectInterimValuation> InterimValuations { get; set; } = new List<ProjectInterimValuation>();
    public virtual ICollection<ProjectPaymentCertificate> PaymentCertificates { get; set; } = new List<ProjectPaymentCertificate>();
    public virtual ICollection<ProjectExtensionOfTime> ExtensionOfTimeRequests { get; set; } = new List<ProjectExtensionOfTime>();
    public virtual ICollection<ProjectBuilding> Buildings { get; set; } = new List<ProjectBuilding>();
    public virtual ICollection<ProjectFloor> Floors { get; set; } = new List<ProjectFloor>();
    public virtual ICollection<ProjectUnitReleaseBatch> UnitReleaseBatches { get; set; } = new List<ProjectUnitReleaseBatch>();
    public virtual ICollection<ProjectUnitHandoverBatch> UnitHandoverBatches { get; set; } = new List<ProjectUnitHandoverBatch>();
    public virtual ICollection<ProjectUnit> Units { get; set; } = new List<ProjectUnit>();
    public virtual ICollection<ProjectCustomerVariation> CustomerVariations { get; set; } = new List<ProjectCustomerVariation>();
    public virtual ICollection<ProjectCommissioningItem> CommissioningItems { get; set; } = new List<ProjectCommissioningItem>();
    public virtual ICollection<ProjectHandoverItem> HandoverItems { get; set; } = new List<ProjectHandoverItem>();
    public virtual ICollection<ProjectSnagItem> SnagItems { get; set; } = new List<ProjectSnagItem>();
    public virtual ICollection<ProjectDefectLiabilityCase> DefectLiabilityCases { get; set; } = new List<ProjectDefectLiabilityCase>();
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
    public virtual ICollection<ProjectMaterialCostEntry> MaterialCostEntries { get; set; } = new List<ProjectMaterialCostEntry>();
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
    public virtual ProjectFinalAccount? FinalAccount { get; set; }
    public virtual ProjectClosure? Closure { get; set; }
}

public class ProjectDevelopmentProfile : TenantEntity
{
    public Guid ProjectId { get; set; }

    [Required]
    [MaxLength(30)]
    public string DeliveryStructure { get; set; } = ProjectDeliveryStructures.WholeDevelopment;

    [MaxLength(100)]
    public string? DevelopmentType { get; set; }

    [MaxLength(200)]
    public string? SiteName { get; set; }

    [MaxLength(2000)]
    public string? SiteAddress { get; set; }

    [MaxLength(200)]
    public string? LandReference { get; set; }

    [MaxLength(100)]
    public string? ProcurementRoute { get; set; }

    [MaxLength(100)]
    public string? ContractStrategy { get; set; }

    [MaxLength(1000)]
    public string? ConsultantTeam { get; set; }

    [MaxLength(200)]
    public string? FundingArrangement { get; set; }

    [MaxLength(200)]
    public string? HandoverStrategy { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public class ProjectPhase : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ParentPhaseId { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectPhaseStatuses.NotStarted;

    public int SortOrder { get; set; }
    public bool IsOptional { get; set; }
    public bool IsStageGateRequired { get; set; }
    public bool IsTemplateSeeded { get; set; }
    [Column(TypeName = "decimal(5,2)")]
    public decimal CompletionWeightPercent { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ParentPhaseId))]
    public virtual ProjectPhase? ParentPhase { get; set; }

    public virtual ICollection<ProjectPhase> Children { get; set; } = new List<ProjectPhase>();
    public virtual ICollection<ProjectPackage> Packages { get; set; } = new List<ProjectPackage>();
    public virtual ICollection<ProjectMilestonePhase> MilestoneSelections { get; set; } = new List<ProjectMilestonePhase>();
    public virtual ICollection<ProjectApprovalRegisterItem> ApprovalRegisterItems { get; set; } = new List<ProjectApprovalRegisterItem>();
}

public class ProjectPackage : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string PackageType { get; set; } = ProjectPackageTypes.WorkPackage;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectPackageStatuses.Planned;

    public int SortOrder { get; set; }
    [Column(TypeName = "decimal(5,2)")]
    public decimal CompletionWeightPercent { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }

    [MaxLength(100)]
    public string? ProcurementRoute { get; set; }

    [MaxLength(100)]
    public string? ContractStrategy { get; set; }

    public Guid? BusinessPartnerId { get; set; }
    public Guid? TenderId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? ProcurementPlanItemId { get; set; }
    public Guid? PurchaseRequisitionId { get; set; }
    public Guid? PurchaseOrderId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BudgetAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CommittedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ForecastAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase? ProjectPhase { get; set; }

    public virtual ICollection<ProjectBoqItem> BoqItems { get; set; } = new List<ProjectBoqItem>();
}

public class ProjectBoqItem : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid ProjectPackageId { get; set; }

    [MaxLength(50)]
    public string? LineNumber { get; set; }

    [MaxLength(50)]
    public string? ItemCode { get; set; }

    [Required]
    [MaxLength(50)]
    public string ItemType { get; set; } = ProjectBoqItemTypes.Item;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? UnitRate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? BudgetQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? BudgetUnitRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BudgetAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CommittedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ForecastAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public Guid? InventoryItemId { get; set; }
    public Guid? TenderItemId { get; set; }
    public Guid? ProcurementPlanItemId { get; set; }
    public Guid? PurchaseRequisitionItemId { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPackageId))]
    public virtual ProjectPackage ProjectPackage { get; set; } = null!;
}

public class ProjectApprovalRegisterItem : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ApprovalType { get; set; } = ProjectApprovalRegisterTypes.Other;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AuthorityName { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectApprovalRegisterStatuses.Planned;

    public bool IsRequired { get; set; } = true;
    public DateTime? SubmittedDate { get; set; }
    public DateTime? TargetDecisionDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? ConditionSummary { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase? ProjectPhase { get; set; }
}

public class ProjectDrawing : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DrawingNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Discipline { get; set; } = ProjectDrawingDisciplines.Other;

    [MaxLength(30)]
    public string? Revision { get; set; }

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = ProjectDrawingStatuses.Draft;

    public DateTime? IssuedDate { get; set; }
    public DateTime? ReviewDueDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public bool IsAsBuilt { get; set; }

    [MaxLength(200)]
    public string? ResponsibleParty { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase? ProjectPhase { get; set; }
}

public class ProjectSubmittal : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SubmittalType { get; set; } = ProjectSubmittalTypes.Other;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = ProjectSubmittalStatuses.Draft;

    public DateTime? SubmittedDate { get; set; }
    public DateTime? ResponseDueDate { get; set; }
    public DateTime? RespondedDate { get; set; }

    [MaxLength(200)]
    public string? SubmittedByName { get; set; }

    [MaxLength(200)]
    public string? ReviewedByName { get; set; }

    [MaxLength(200)]
    public string? ResponsibleParty { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase? ProjectPhase { get; set; }

    [ForeignKey(nameof(ProjectPackageId))]
    public virtual ProjectPackage? ProjectPackage { get; set; }
}

public class ProjectRfi : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Question { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Priority { get; set; } = ProjectRfiPriorities.Medium;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectRfiStatuses.Draft;

    public DateTime RaisedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ResponseDueDate { get; set; }
    public DateTime? RespondedDate { get; set; }

    [MaxLength(200)]
    public string? RaisedByName { get; set; }

    [MaxLength(200)]
    public string? RespondedByName { get; set; }

    [MaxLength(2000)]
    public string? ImpactSummary { get; set; }

    [MaxLength(2000)]
    public string? Response { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase? ProjectPhase { get; set; }

    [ForeignKey(nameof(ProjectPackageId))]
    public virtual ProjectPackage? ProjectPackage { get; set; }
}

public class ProjectSiteInstruction : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }

    [Required]
    [MaxLength(50)]
    public string InstructionType { get; set; } = ProjectSiteInstructionTypes.SiteInstruction;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectSiteInstructionStatuses.Draft;

    public DateTime IssuedDate { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ClosedDate { get; set; }

    [MaxLength(200)]
    public string? IssuedByName { get; set; }

    [MaxLength(200)]
    public string? ResponsibleParty { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedCostImpact { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public int? ScheduleImpactDays { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase? ProjectPhase { get; set; }

    [ForeignKey(nameof(ProjectPackageId))]
    public virtual ProjectPackage? ProjectPackage { get; set; }
}

public class ProjectVariationOrder : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ContractId { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string VariationType { get; set; } = ProjectVariationOrderTypes.Other;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectVariationOrderStatuses.Draft;

    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ImplementedDate { get; set; }

    [MaxLength(200)]
    public string? RequestedByName { get; set; }

    [MaxLength(200)]
    public string? ApprovedByName { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ApprovedAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public int? ScheduleImpactDays { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase? ProjectPhase { get; set; }

    [ForeignKey(nameof(ProjectPackageId))]
    public virtual ProjectPackage? ProjectPackage { get; set; }

    [ForeignKey(nameof(ContractId))]
    public virtual Contract? Contract { get; set; }
}

public class ProjectInterimValuation : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ProjectMilestoneId { get; set; }
    public Guid? ContractId { get; set; }

    [MaxLength(100)]
    public string? ValuationNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectInterimValuationStatuses.Draft;

    public DateTime ValuationDate { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossWorkValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaterialsOnSiteValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal VariationValue { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal RetentionPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RetentionAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PreviousCertifiedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetValuationAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase? ProjectPhase { get; set; }

    [ForeignKey(nameof(ProjectPackageId))]
    public virtual ProjectPackage? ProjectPackage { get; set; }

    [ForeignKey(nameof(ProjectMilestoneId))]
    public virtual ProjectMilestone? ProjectMilestone { get; set; }

    [ForeignKey(nameof(ContractId))]
    public virtual Contract? Contract { get; set; }

    public virtual ICollection<ProjectInterimValuationPackageCompletion> CompletedProjectPackages { get; set; } = new List<ProjectInterimValuationPackageCompletion>();
}

public class ProjectInterimValuationPackageCompletion : TenantEntity
{
    public Guid ProjectInterimValuationId { get; set; }
    public Guid ProjectPackageId { get; set; }

    [ForeignKey(nameof(ProjectInterimValuationId))]
    public virtual ProjectInterimValuation ProjectInterimValuation { get; set; } = null!;

    [ForeignKey(nameof(ProjectPackageId))]
    public virtual ProjectPackage ProjectPackage { get; set; } = null!;
}

public class ProjectPaymentCertificate : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? ProjectInterimValuationId { get; set; }

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectPaymentCertificateStatuses.Draft;

    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime? PaymentDueDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossCertifiedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RetentionHeldAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RetentionReleasedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OtherDeductionsAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetCertifiedAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase? ProjectPhase { get; set; }

    [ForeignKey(nameof(ProjectPackageId))]
    public virtual ProjectPackage? ProjectPackage { get; set; }

    [ForeignKey(nameof(ContractId))]
    public virtual Contract? Contract { get; set; }

    [ForeignKey(nameof(ProjectInterimValuationId))]
    public virtual ProjectInterimValuation? ProjectInterimValuation { get; set; }
}

public class ProjectExtensionOfTime : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ContractId { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Reason { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectExtensionOfTimeStatuses.Draft;

    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
    public DateTime? DecisionDate { get; set; }
    public int? DaysRequested { get; set; }
    public int? DaysApproved { get; set; }
    public DateTime? RevisedCompletionDate { get; set; }

    [MaxLength(200)]
    public string? RequestedByName { get; set; }

    [MaxLength(200)]
    public string? DecidedByName { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase? ProjectPhase { get; set; }

    [ForeignKey(nameof(ProjectPackageId))]
    public virtual ProjectPackage? ProjectPackage { get; set; }

    [ForeignKey(nameof(ContractId))]
    public virtual Contract? Contract { get; set; }
}

public class ProjectFinalAccount : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ContractId { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectFinalAccountStatuses.Draft;

    public DateTime? SettlementDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalContractValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ApprovedVariationAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CertifiedToDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RetentionHeldAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RetentionReleasedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalAccountValue { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ContractId))]
    public virtual Contract? Contract { get; set; }
}

public class ProjectBuilding : TenantEntity
{
    public Guid ProjectId { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    public virtual ICollection<ProjectFloor> Floors { get; set; } = new List<ProjectFloor>();
    public virtual ICollection<ProjectUnit> Units { get; set; } = new List<ProjectUnit>();
    public virtual ICollection<ProjectUnitReleaseBatch> ReleaseBatches { get; set; } = new List<ProjectUnitReleaseBatch>();
    public virtual ICollection<ProjectUnitHandoverBatch> HandoverBatches { get; set; } = new List<ProjectUnitHandoverBatch>();
}

public class ProjectFloor : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectBuildingId { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int? LevelNumber { get; set; }
    public int SortOrder { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectBuildingId))]
    public virtual ProjectBuilding? ProjectBuilding { get; set; }

    public virtual ICollection<ProjectUnit> Units { get; set; } = new List<ProjectUnit>();
    public virtual ICollection<ProjectUnitReleaseBatch> ReleaseBatches { get; set; } = new List<ProjectUnitReleaseBatch>();
    public virtual ICollection<ProjectUnitHandoverBatch> HandoverBatches { get; set; } = new List<ProjectUnitHandoverBatch>();
}

public class ProjectUnitReleaseBatch : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectBuildingId { get; set; }
    public Guid? ProjectFloorId { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectUnitReleaseBatchStatuses.Draft;

    public DateTime? PlannedReleaseDate { get; set; }
    public DateTime? ActualReleaseDate { get; set; }
    public int SortOrder { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectBuildingId))]
    public virtual ProjectBuilding? ProjectBuilding { get; set; }

    [ForeignKey(nameof(ProjectFloorId))]
    public virtual ProjectFloor? ProjectFloor { get; set; }

    public virtual ICollection<ProjectUnit> Units { get; set; } = new List<ProjectUnit>();
}

public class ProjectUnitTypeTemplate : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string DefaultProjectUnitType { get; set; } = ProjectUnitTypes.Unit;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(10)]
    public string? Currency { get; set; }

    public virtual ICollection<ProjectUnitTypeTemplateAmenity> Amenities { get; set; } = new List<ProjectUnitTypeTemplateAmenity>();
    public virtual ICollection<ProjectUnit> Units { get; set; } = new List<ProjectUnit>();
}

public class ProjectUnitTypeTemplateAmenity : TenantEntity
{
    public Guid ProjectUnitTypeTemplateId { get; set; }
    public Guid InventoryItemId { get; set; }

    [MaxLength(100)]
    public string? ItemCode { get; set; }

    [Required]
    [MaxLength(200)]
    public string AmenityName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantity { get; set; } = 1m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey(nameof(ProjectUnitTypeTemplateId))]
    public virtual ProjectUnitTypeTemplate ProjectUnitTypeTemplate { get; set; } = null!;

    [ForeignKey(nameof(InventoryItemId))]
    public virtual InventoryItem InventoryItem { get; set; } = null!;
}

public class ProjectUnitHandoverBatch : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectBuildingId { get; set; }
    public Guid? ProjectFloorId { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectUnitHandoverBatchStatuses.Planned;

    public DateTime? PlannedHandoverDate { get; set; }
    public DateTime? ActualHandoverDate { get; set; }
    public int SortOrder { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectBuildingId))]
    public virtual ProjectBuilding? ProjectBuilding { get; set; }

    [ForeignKey(nameof(ProjectFloorId))]
    public virtual ProjectFloor? ProjectFloor { get; set; }

    public virtual ICollection<ProjectHandoverItem> HandoverItems { get; set; } = new List<ProjectHandoverItem>();
}

public class ProjectUnit : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectBuildingId { get; set; }
    public Guid? ProjectFloorId { get; set; }
    public Guid? ProjectUnitReleaseBatchId { get; set; }
    public Guid? ProjectUnitTypeTemplateId { get; set; }
    public Guid? CustomerBusinessPartnerId { get; set; }
    public Guid? SalesAgreementId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public bool IsReleasedForMarket { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public Guid? ReleasedById { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string UnitType { get; set; } = ProjectUnitTypes.Unit;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectUnitStatuses.Planned;

    [MaxLength(100)]
    public string? BlockName { get; set; }

    [MaxLength(100)]
    public string? FloorLabel { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? AreaSquareMeters { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ValuationRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BasePrice { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public DateTime? HandoverDate { get; set; }
    public int SortOrder { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectBuildingId))]
    public virtual ProjectBuilding? ProjectBuilding { get; set; }

    [ForeignKey(nameof(ProjectFloorId))]
    public virtual ProjectFloor? ProjectFloor { get; set; }

    [ForeignKey(nameof(ProjectUnitReleaseBatchId))]
    public virtual ProjectUnitReleaseBatch? ProjectUnitReleaseBatch { get; set; }

    [ForeignKey(nameof(ProjectUnitTypeTemplateId))]
    public virtual ProjectUnitTypeTemplate? ProjectUnitTypeTemplate { get; set; }

    [ForeignKey(nameof(SalesAgreementId))]
    public virtual SalesAgreement? SalesAgreement { get; set; }

    [ForeignKey(nameof(SalesOrderId))]
    public virtual SalesOrder? SalesOrder { get; set; }

    public virtual ICollection<ProjectCustomerVariation> CustomerVariations { get; set; } = new List<ProjectCustomerVariation>();
    public virtual ICollection<ProjectCommissioningItem> CommissioningItems { get; set; } = new List<ProjectCommissioningItem>();
    public virtual ICollection<ProjectHandoverItem> HandoverItems { get; set; } = new List<ProjectHandoverItem>();
    public virtual ICollection<ProjectSnagItem> SnagItems { get; set; } = new List<ProjectSnagItem>();
    public virtual ICollection<ProjectDefectLiabilityCase> DefectLiabilityCases { get; set; } = new List<ProjectDefectLiabilityCase>();
    public virtual ICollection<ProjectUnitAmenity> Amenities { get; set; } = new List<ProjectUnitAmenity>();
}

public class ProjectUnitAmenity : TenantEntity
{
    public Guid ProjectUnitId { get; set; }
    public Guid? InventoryItemId { get; set; }

    [MaxLength(100)]
    public string? ItemCode { get; set; }

    [Required]
    [MaxLength(200)]
    public string AmenityName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantity { get; set; } = 1m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey(nameof(ProjectUnitId))]
    public virtual ProjectUnit ProjectUnit { get; set; } = null!;

    [ForeignKey(nameof(InventoryItemId))]
    public virtual InventoryItem? InventoryItem { get; set; }
}

public class ProjectCustomerVariation : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }
    public Guid? CustomerBusinessPartnerId { get; set; }
    public Guid? SalesAgreementId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? VariationType { get; set; }

    [Required]
    [MaxLength(30)]
    public string Timing { get; set; } = ProjectCustomerVariationTimings.PreHandover;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectCustomerVariationStatuses.Requested;

    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? QuotedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ApprovedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BilledAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public bool RequiresScheduleAdjustment { get; set; }
    public int? ScheduleImpactDays { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectUnitId))]
    public virtual ProjectUnit? ProjectUnit { get; set; }

    [ForeignKey(nameof(SalesAgreementId))]
    public virtual SalesAgreement? SalesAgreement { get; set; }

    [ForeignKey(nameof(SalesOrderId))]
    public virtual SalesOrder? SalesOrder { get; set; }

    [ForeignKey(nameof(JobCardId))]
    public virtual JobCard? JobCard { get; set; }

    [ForeignKey(nameof(WorkOrderId))]
    public virtual WorkOrder? WorkOrder { get; set; }
}

public class ProjectCommissioningItem : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SystemArea { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectCommissioningItemStatuses.Planned;

    public bool RequiresRegulatoryInspection { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    [MaxLength(100)]
    public string? CertificateReference { get; set; }

    [MaxLength(200)]
    public string? ResponsibleParty { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectUnitId))]
    public virtual ProjectUnit? ProjectUnit { get; set; }
}

public class ProjectHandoverItem : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }
    public Guid? ProjectUnitHandoverBatchId { get; set; }

    [Required]
    [MaxLength(50)]
    public string HandoverType { get; set; } = ProjectHandoverItemTypes.Other;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectHandoverItemStatuses.Planned;

    [MaxLength(200)]
    public string? ResponsibleParty { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public DateTime? TargetDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int SortOrder { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectUnitId))]
    public virtual ProjectUnit? ProjectUnit { get; set; }

    [ForeignKey(nameof(ProjectUnitHandoverBatchId))]
    public virtual ProjectUnitHandoverBatch? ProjectUnitHandoverBatch { get; set; }
}

public class ProjectSnagItem : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = ProjectSnagSeverities.Medium;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectSnagStatuses.Open;

    public DateTime ReportedDate { get; set; } = DateTime.UtcNow;
    public DateTime? TargetClosureDate { get; set; }
    public DateTime? ClosedDate { get; set; }

    [MaxLength(200)]
    public string? RaisedByName { get; set; }

    [MaxLength(200)]
    public string? ResponsibleParty { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectUnitId))]
    public virtual ProjectUnit? ProjectUnit { get; set; }
}

public class ProjectDefectLiabilityCase : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }
    public Guid? CustomerBusinessPartnerId { get; set; }
    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = ProjectDefectLiabilityStatuses.Reported;

    public DateTime ReportedDate { get; set; } = DateTime.UtcNow;
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public bool IsWarrantyRelated { get; set; }

    [MaxLength(100)]
    public string? WarrantyCategory { get; set; }

    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? FirstResponseDate { get; set; }
    public int? ResponseSlaDays { get; set; }
    public int? ResolutionSlaDays { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? RectificationCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ChargeableAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ProjectUnitId))]
    public virtual ProjectUnit? ProjectUnit { get; set; }

    [ForeignKey(nameof(JobCardId))]
    public virtual JobCard? JobCard { get; set; }

    [ForeignKey(nameof(WorkOrderId))]
    public virtual WorkOrder? WorkOrder { get; set; }
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
    public Guid? ProjectPackageId { get; set; }

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

    [ForeignKey(nameof(ProjectPackageId))]
    public virtual ProjectPackage? ProjectPackage { get; set; }

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

    public virtual ICollection<ProjectMilestonePhase> PhaseSelections { get; set; } = new List<ProjectMilestonePhase>();
}

public class ProjectMilestonePhase : TenantEntity
{
    public Guid ProjectMilestoneId { get; set; }
    public Guid ProjectPhaseId { get; set; }

    [ForeignKey(nameof(ProjectMilestoneId))]
    public virtual ProjectMilestone ProjectMilestone { get; set; } = null!;

    [ForeignKey(nameof(ProjectPhaseId))]
    public virtual ProjectPhase ProjectPhase { get; set; } = null!;
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

    [MaxLength(4000)]
    public string? RequiredSkillsJson { get; set; }

    [MaxLength(4000)]
    public string? RequiredCertificationsJson { get; set; }

    [MaxLength(30)]
    public string RoutingPolicy { get; set; } = "Balanced";

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

    [MaxLength(30)]
    public string ArtifactType { get; set; } = ProjectDocumentArtifactTypes.Project;

    public Guid? ArtifactId { get; set; }

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

    public virtual ICollection<ProjectDeliverableExternalReview> ExternalReviews { get; set; } = new List<ProjectDeliverableExternalReview>();
}

public class ProjectDeliverableExternalReview : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public Guid DeliverableId { get; set; }

    public DateTime ReviewDate { get; set; } = DateTime.UtcNow;

    public Guid? ReviewedById { get; set; }

    public Guid? SubmittedDocumentId { get; set; }

    [Required]
    [MaxLength(30)]
    public string Decision { get; set; } = "Submitted";

    [MaxLength(30)]
    public string? StatusSnapshot { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(DeliverableId))]
    public virtual ProjectDeliverable Deliverable { get; set; } = null!;

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

public class ProjectMaterialCostEntry : TenantEntity
{
    [Required]
    public Guid ProjectId { get; set; }

    public DateTime EntryDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(50)]
    public string EntryType { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string PostingState { get; set; } = "Posted";

    public bool AffectsActualCost { get; set; }
    public bool IsReversed { get; set; }

    [MaxLength(50)]
    public string? SourceDocumentType { get; set; }

    public Guid? SourceDocumentId { get; set; }

    [MaxLength(100)]
    public string? SourceDocumentNumber { get; set; }

    [MaxLength(50)]
    public string? SourceTransactionType { get; set; }

    public Guid? SourceTransactionId { get; set; }

    public Guid? InventoryItemId { get; set; }

    [MaxLength(100)]
    public string? InventoryItemCode { get; set; }

    [MaxLength(200)]
    public string? InventoryItemName { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public bool HasMissingSourceLink { get; set; }
    public bool HasReversalGap { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
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

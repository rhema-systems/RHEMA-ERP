using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.DTOs.Projects;

public class ProjectLookupDto
{
    public Guid Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProjectTypeName { get; set; }
    public string? PortfolioName { get; set; }
    public string? ProgramName { get; set; }
}

public class ProjectResourceLookupDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
}

public class ProjectDto
{
    public Guid Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ProjectTypeName { get; set; }
    public string? ProjectPriorityName { get; set; }
    public Guid? PortfolioId { get; set; }
    public string? PortfolioName { get; set; }
    public Guid? ProgramId { get; set; }
    public string? ProgramName { get; set; }
    public Guid? ProjectManagerId { get; set; }
    public string? ProjectManagerDisplayName { get; set; }
    public Guid? SponsorId { get; set; }
    public string? SponsorDisplayName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }
    public decimal? EstimatedBudget { get; set; }
    public decimal? ApprovedBudget { get; set; }
    public decimal? ActualCost { get; set; }
    public decimal ProgressPercent { get; set; }
    public int OpenRiskCount { get; set; }
    public int OpenIssueCount { get; set; }
    public int OverdueMilestoneCount { get; set; }
    public string? CurrentWorkflowStepName { get; set; }
    public bool ExternalPortalAccessEnabled { get; set; }
    public bool ExternalCollaborationEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProjectDetailDto : ProjectDto
{
    public Guid? ProjectTypeId { get; set; }
    public Guid? ProjectPriorityId { get; set; }
    public Guid? TemplateId { get; set; }
    public string? BusinessCase { get; set; }
    public string? Objectives { get; set; }
    public string? StrategicAlignment { get; set; }
    public string Methodology { get; set; } = "Hybrid";
    public Guid? DepartmentId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? TenderId { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public string BudgetStatus { get; set; } = string.Empty;
    public bool ApprovalRequired { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ScopeStatement { get; set; }
    public string? Assumptions { get; set; }
    public string? Constraints { get; set; }
    public string? ExpectedBenefits { get; set; }
    public string? FundingSource { get; set; }
    public string? StatusRemarks { get; set; }
    public new bool ExternalPortalAccessEnabled { get; set; }
    public new bool ExternalCollaborationEnabled { get; set; }
    public Guid? ActiveBaselineId { get; set; }
    public string? ActiveBaselineName { get; set; }
    public DateTime? ActiveBaselineCreatedOn { get; set; }
    public bool HasLockedBaseline { get; set; }
    public ProjectDevelopmentProfileDto? DevelopmentProfile { get; set; }
    public List<ProjectMemberDto> Members { get; set; } = new();
    public List<ProjectPhaseDto> Phases { get; set; } = new();
    public List<ProjectPackageDto> Packages { get; set; } = new();
    public List<ProjectBoqItemDto> BoqItems { get; set; } = new();
    public List<ProjectApprovalRegisterItemDto> ApprovalRegister { get; set; } = new();
    public List<ProjectDrawingDto> Drawings { get; set; } = new();
    public List<ProjectSubmittalDto> Submittals { get; set; } = new();
    public List<ProjectRfiDto> Rfis { get; set; } = new();
    public List<ProjectSiteInstructionDto> SiteInstructions { get; set; } = new();
    public List<ProjectVariationOrderDto> VariationOrders { get; set; } = new();
    public List<ProjectInterimValuationDto> InterimValuations { get; set; } = new();
    public List<ProjectPaymentCertificateDto> PaymentCertificates { get; set; } = new();
    public List<ProjectExtensionOfTimeDto> ExtensionOfTimeRequests { get; set; } = new();
    public ProjectFinalAccountDto? FinalAccount { get; set; }
    public List<ProjectBuildingDto> Buildings { get; set; } = new();
    public List<ProjectFloorDto> Floors { get; set; } = new();
    public List<ProjectUnitReleaseBatchDto> UnitReleaseBatches { get; set; } = new();
    public List<ProjectUnitHandoverBatchDto> UnitHandoverBatches { get; set; } = new();
    public List<ProjectUnitDto> Units { get; set; } = new();
    public List<ProjectCustomerVariationDto> CustomerVariations { get; set; } = new();
    public List<ProjectCommissioningItemDto> CommissioningItems { get; set; } = new();
    public List<ProjectHandoverItemDto> HandoverItems { get; set; } = new();
    public List<ProjectSnagItemDto> SnagItems { get; set; } = new();
    public List<ProjectDefectLiabilityCaseDto> DefectLiabilityCases { get; set; } = new();
    public List<ProjectWorkItemDto> WorkItems { get; set; } = new();
    public List<ProjectMilestoneDto> Milestones { get; set; } = new();
    public List<ProjectResourceAllocationDto> ResourceAllocations { get; set; } = new();
    public List<ProjectRiskDto> Risks { get; set; } = new();
    public List<ProjectIssueDto> Issues { get; set; } = new();
    public List<ProjectQualityCheckpointDto> QualityCheckpoints { get; set; } = new();
    public List<ProjectNonConformanceDto> NonConformances { get; set; } = new();
    public List<ProjectChangeRequestDto> ChangeRequests { get; set; } = new();
    public List<ProjectBillingScheduleDto> BillingSchedules { get; set; } = new();
    public List<ProjectInvoiceRequestDto> InvoiceRequests { get; set; } = new();
    public List<ProjectDeliverableDto> Deliverables { get; set; } = new();
    public List<ProjectTaskDependencyDto> TaskDependencies { get; set; } = new();
    public List<ProjectBaselineDto> Baselines { get; set; } = new();
    public List<ProjectTimesheetEntryDto> TimesheetEntries { get; set; } = new();
    public List<ProjectExpenseDto> Expenses { get; set; } = new();
    public List<ProjectMaterialCostEntryDto> MaterialCostEntries { get; set; } = new();
    public List<ProjectRevenueRecognitionDto> RevenueRecognitions { get; set; } = new();
    public List<ProjectAssetLinkDto> AssetLinks { get; set; } = new();
    public List<ProjectExternalAccessPolicyDto> ExternalAccessPolicies { get; set; } = new();
    public List<ProjectDecisionDto> Decisions { get; set; } = new();
    public List<ProjectMeetingMinuteDto> Meetings { get; set; } = new();
    public List<ProjectActionItemDto> ActionItems { get; set; } = new();
    public List<ProjectLessonLearnedDto> LessonsLearned { get; set; } = new();
    public List<ProjectDocumentDto> Documents { get; set; } = new();
    public List<ProjectCommentDto> Comments { get; set; } = new();
    public List<ProjectInitiationVersionDto> InitiationVersions { get; set; } = new();
    public ProjectClosureDto? Closure { get; set; }
}

public class ProjectDevelopmentProfileDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string DeliveryStructure { get; set; } = string.Empty;
    public string? DevelopmentType { get; set; }
    public string? SiteName { get; set; }
    public string? SiteAddress { get; set; }
    public string? LandReference { get; set; }
    public string? ProcurementRoute { get; set; }
    public string? ContractStrategy { get; set; }
    public string? ConsultantTeam { get; set; }
    public string? FundingArrangement { get; set; }
    public string? HandoverStrategy { get; set; }
    public string? Notes { get; set; }
}

public class UpsertProjectDevelopmentProfileDto
{
    [Required]
    public string DeliveryStructure { get; set; } = "WholeDevelopment";

    public string? DevelopmentType { get; set; }
    public string? SiteName { get; set; }
    public string? SiteAddress { get; set; }
    public string? LandReference { get; set; }
    public string? ProcurementRoute { get; set; }
    public string? ContractStrategy { get; set; }
    public string? ConsultantTeam { get; set; }
    public string? FundingArrangement { get; set; }
    public string? HandoverStrategy { get; set; }
    public string? Notes { get; set; }
}

public class ProjectPhaseDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentPhaseId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsOptional { get; set; }
    public bool IsStageGateRequired { get; set; }
    public bool IsTemplateSeeded { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public List<ProjectPhaseDto> Children { get; set; } = new();
}

public class CreateProjectPhaseDto
{
    public Guid? ParentPhaseId { get; set; }
    public string? Code { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string Status { get; set; } = "NotStarted";
    public int? SortOrder { get; set; }
    public bool IsOptional { get; set; }
    public bool IsStageGateRequired { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
}

public class UpdateProjectPhaseDto : CreateProjectPhaseDto
{
    public bool OverrideStageGate { get; set; }
    public string? OverrideReason { get; set; }
}

public class ProjectPhaseGateRequirementResultDto
{
    public Guid? StageGateRuleId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RequirementType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int? MinimumCount { get; set; }
    public int? MaximumCount { get; set; }
    public int ActualCount { get; set; }
    public bool IsBlocking { get; set; }
    public bool IsSatisfied { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ProjectPhaseGateEvaluationDto
{
    public Guid ProjectPhaseId { get; set; }
    public string? ProjectPhaseCode { get; set; }
    public string ProjectPhaseName { get; set; } = string.Empty;
    public Guid? ProjectPhaseTemplateId { get; set; }
    public string? ProjectPhaseTemplateName { get; set; }
    public bool IsStageGateRequired { get; set; }
    public bool HasConfiguredRules { get; set; }
    public bool IsReady { get; set; }
    public int BlockingFailureCount { get; set; }
    public List<ProjectPhaseGateRequirementResultDto> RequirementResults { get; set; } = new();
}

public class AdvanceProjectPhaseDto
{
    public bool OverrideStageGate { get; set; }
    public string? OverrideReason { get; set; }
    public bool StartNextPhase { get; set; } = true;
}

public class ProjectPhaseProgressionResultDto
{
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool OverrideUsed { get; set; }
    public bool StageGateEvaluated { get; set; }
    public bool StageGatePassed { get; set; }
    public bool NextPhaseStarted { get; set; }
    public int PackageStatusUpdateCount { get; set; }
    public ProjectPhaseDto Phase { get; set; } = new();
    public ProjectPhaseDto? NextPhase { get; set; }
    public ProjectPhaseGateEvaluationDto? GateEvaluation { get; set; }
}

public class ProjectPackageDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PackageType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsPhaseCommerciallyAligned { get; set; }
    public string PhaseCommercialSyncStatus { get; set; } = string.Empty;
    public string? PhaseCommercialSyncMessage { get; set; }
    public string? RecommendedNextStatus { get; set; }
    public string? RecommendedNextAction { get; set; }
    public int SortOrder { get; set; }
    public string? ProcurementRoute { get; set; }
    public string? ContractStrategy { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public Guid? TenderId { get; set; }
    public string? TenderNumber { get; set; }
    public string? TenderTitle { get; set; }
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public string? ContractTitle { get; set; }
    public Guid? ProcurementPlanItemId { get; set; }
    public string? ProcurementPlanItemLabel { get; set; }
    public Guid? PurchaseRequisitionId { get; set; }
    public string? PurchaseRequisitionNumber { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? CommittedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public decimal? ForecastAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<ProjectBoqItemDto> BoqItems { get; set; } = new();
}

public class CreateProjectPackageDto
{
    public Guid? ProjectPhaseId { get; set; }
    public string? Code { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string PackageType { get; set; } = "WorkPackage";
    public string Status { get; set; } = "Planned";
    public int? SortOrder { get; set; }
    public string? ProcurementRoute { get; set; }
    public string? ContractStrategy { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? TenderId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? ProcurementPlanItemId { get; set; }
    public Guid? PurchaseRequisitionId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? CommittedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public decimal? ForecastAmount { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectPackageDto : CreateProjectPackageDto
{
}

public class ProjectBoqItemDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectPackageId { get; set; }
    public string? PackageCode { get; set; }
    public string? PackageName { get; set; }
    public string? LineNumber { get; set; }
    public string? ItemCode { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal? UnitRate { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? CommittedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public decimal? ForecastAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Guid? InventoryItemId { get; set; }
    public Guid? TenderItemId { get; set; }
    public Guid? ProcurementPlanItemId { get; set; }
    public Guid? PurchaseRequisitionItemId { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}

public class CreateProjectBoqItemDto
{
    [Required]
    public Guid ProjectPackageId { get; set; }

    public string? LineNumber { get; set; }
    public string? ItemCode { get; set; }
    public string ItemType { get; set; } = "Item";

    [Required]
    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal? UnitRate { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? CommittedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public decimal? ForecastAmount { get; set; }
    public string? Currency { get; set; }
    public Guid? InventoryItemId { get; set; }
    public Guid? TenderItemId { get; set; }
    public Guid? ProcurementPlanItemId { get; set; }
    public Guid? PurchaseRequisitionItemId { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
    public string? Notes { get; set; }
    public int? SortOrder { get; set; }
}

public class UpdateProjectBoqItemDto : CreateProjectBoqItemDto
{
}

public class ProjectApprovalRegisterItemDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public string ApprovalType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AuthorityName { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? TargetDecisionDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? ConditionSummary { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectApprovalRegisterItemDto
{
    public Guid? ProjectPhaseId { get; set; }
    public string ApprovalType { get; set; } = "Other";

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? AuthorityName { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = "Planned";
    public bool IsRequired { get; set; } = true;
    public DateTime? SubmittedDate { get; set; }
    public DateTime? TargetDecisionDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? ConditionSummary { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectApprovalRegisterItemDto : CreateProjectApprovalRegisterItemDto
{
}

public class ProjectDrawingDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public string DrawingNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? IssuedDate { get; set; }
    public DateTime? ReviewDueDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public bool IsAsBuilt { get; set; }
    public string? ResponsibleParty { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectDrawingDto
{
    public Guid? ProjectPhaseId { get; set; }

    [Required]
    public string DrawingNumber { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Discipline { get; set; }
    public string? Revision { get; set; }
    public string? Status { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? ReviewDueDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public bool IsAsBuilt { get; set; }
    public string? ResponsibleParty { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectDrawingDto : CreateProjectDrawingDto
{
}

public class ProjectSubmittalDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? ProjectPackageName { get; set; }
    public string SubmittalType { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ResponseDueDate { get; set; }
    public DateTime? RespondedDate { get; set; }
    public string? SubmittedByName { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ResponsibleParty { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectSubmittalDto
{
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? SubmittalType { get; set; }
    public string? ReferenceNumber { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Status { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ResponseDueDate { get; set; }
    public DateTime? RespondedDate { get; set; }
    public string? SubmittedByName { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ResponsibleParty { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectSubmittalDto : CreateProjectSubmittalDto
{
}

public class ProjectRfiDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? ProjectPackageName { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RaisedDate { get; set; }
    public DateTime? ResponseDueDate { get; set; }
    public DateTime? RespondedDate { get; set; }
    public string? RaisedByName { get; set; }
    public string? RespondedByName { get; set; }
    public string? ImpactSummary { get; set; }
    public string? Response { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectRfiDto
{
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? ReferenceNumber { get; set; }

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Question { get; set; } = string.Empty;

    public string? Priority { get; set; }
    public string? Status { get; set; }
    public DateTime? RaisedDate { get; set; }
    public DateTime? ResponseDueDate { get; set; }
    public DateTime? RespondedDate { get; set; }
    public string? RaisedByName { get; set; }
    public string? RespondedByName { get; set; }
    public string? ImpactSummary { get; set; }
    public string? Response { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectRfiDto : CreateProjectRfiDto
{
}

public class ProjectSiteInstructionDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? ProjectPackageName { get; set; }
    public string InstructionType { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? IssuedByName { get; set; }
    public string? ResponsibleParty { get; set; }
    public decimal? EstimatedCostImpact { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int? ScheduleImpactDays { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectSiteInstructionDto
{
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? InstructionType { get; set; }
    public string? ReferenceNumber { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? IssuedByName { get; set; }
    public string? ResponsibleParty { get; set; }
    public decimal? EstimatedCostImpact { get; set; }
    public string? Currency { get; set; }
    public int? ScheduleImpactDays { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectSiteInstructionDto : CreateProjectSiteInstructionDto
{
}

public class ProjectVariationOrderDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? ProjectPackageName { get; set; }
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public string? ContractTitle { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string VariationType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ImplementedDate { get; set; }
    public string? RequestedByName { get; set; }
    public string? ApprovedByName { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int? ScheduleImpactDays { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectVariationOrderDto
{
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ContractId { get; set; }
    public string? ReferenceNumber { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? VariationType { get; set; }
    public string? Status { get; set; }
    public DateTime? RequestedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ImplementedDate { get; set; }
    public string? RequestedByName { get; set; }
    public string? ApprovedByName { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? Currency { get; set; }
    public int? ScheduleImpactDays { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectVariationOrderDto : CreateProjectVariationOrderDto
{
}

public class ProjectInterimValuationDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? ProjectPackageName { get; set; }
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public string? ContractTitle { get; set; }
    public string? ValuationNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ValuationDate { get; set; }
    public decimal GrossWorkValue { get; set; }
    public decimal MaterialsOnSiteValue { get; set; }
    public decimal VariationValue { get; set; }
    public decimal RetentionPercentage { get; set; }
    public decimal RetentionAmount { get; set; }
    public decimal PreviousCertifiedAmount { get; set; }
    public decimal NetValuationAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class CreateProjectInterimValuationDto
{
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ContractId { get; set; }
    public string? ValuationNumber { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Status { get; set; }
    public DateTime? ValuationDate { get; set; }
    public decimal GrossWorkValue { get; set; }
    public decimal MaterialsOnSiteValue { get; set; }
    public decimal VariationValue { get; set; }
    public decimal RetentionPercentage { get; set; }
    public decimal RetentionAmount { get; set; }
    public decimal PreviousCertifiedAmount { get; set; }
    public decimal NetValuationAmount { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectInterimValuationDto : CreateProjectInterimValuationDto
{
}

public class ProjectPaymentCertificateDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? ProjectPackageName { get; set; }
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public string? ContractTitle { get; set; }
    public Guid? ProjectInterimValuationId { get; set; }
    public string? InterimValuationNumber { get; set; }
    public string? InterimValuationTitle { get; set; }
    public string? CertificateNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime? PaymentDueDate { get; set; }
    public decimal GrossCertifiedAmount { get; set; }
    public decimal RetentionHeldAmount { get; set; }
    public decimal RetentionReleasedAmount { get; set; }
    public decimal OtherDeductionsAmount { get; set; }
    public decimal NetCertifiedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class CreateProjectPaymentCertificateDto
{
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? ProjectInterimValuationId { get; set; }
    public string? CertificateNumber { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Status { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? PaymentDueDate { get; set; }
    public decimal GrossCertifiedAmount { get; set; }
    public decimal RetentionHeldAmount { get; set; }
    public decimal RetentionReleasedAmount { get; set; }
    public decimal OtherDeductionsAmount { get; set; }
    public decimal NetCertifiedAmount { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectPaymentCertificateDto : CreateProjectPaymentCertificateDto
{
}

public class ProjectExtensionOfTimeDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? ProjectPackageName { get; set; }
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public string? ContractTitle { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public DateTime? DecisionDate { get; set; }
    public int? DaysRequested { get; set; }
    public int? DaysApproved { get; set; }
    public DateTime? RevisedCompletionDate { get; set; }
    public string? RequestedByName { get; set; }
    public string? DecidedByName { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectExtensionOfTimeDto
{
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ContractId { get; set; }
    public string? ReferenceNumber { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Reason { get; set; }
    public string? Status { get; set; }
    public DateTime? RequestedDate { get; set; }
    public DateTime? DecisionDate { get; set; }
    public int? DaysRequested { get; set; }
    public int? DaysApproved { get; set; }
    public DateTime? RevisedCompletionDate { get; set; }
    public string? RequestedByName { get; set; }
    public string? DecidedByName { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectExtensionOfTimeDto : CreateProjectExtensionOfTimeDto
{
}

public class ProjectFinalAccountDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public string? ContractTitle { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SettlementDate { get; set; }
    public decimal OriginalContractValue { get; set; }
    public decimal ApprovedVariationAmount { get; set; }
    public decimal CertifiedToDate { get; set; }
    public decimal RetentionHeldAmount { get; set; }
    public decimal RetentionReleasedAmount { get; set; }
    public decimal FinalAccountValue { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class UpsertProjectFinalAccountDto
{
    public Guid? ContractId { get; set; }
    public string? Status { get; set; }
    public DateTime? SettlementDate { get; set; }
    public decimal OriginalContractValue { get; set; }
    public decimal ApprovedVariationAmount { get; set; }
    public decimal CertifiedToDate { get; set; }
    public decimal RetentionHeldAmount { get; set; }
    public decimal RetentionReleasedAmount { get; set; }
    public decimal FinalAccountValue { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
}

public class ProjectBuildingDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectBuildingDto
{
    public string? Code { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public int? SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectBuildingDto : CreateProjectBuildingDto
{
}

public class ProjectFloorDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectBuildingId { get; set; }
    public string? ProjectBuildingCode { get; set; }
    public string? ProjectBuildingName { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? LevelNumber { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectFloorDto
{
    public Guid? ProjectBuildingId { get; set; }
    public string? Code { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public int? LevelNumber { get; set; }
    public int? SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectFloorDto : CreateProjectFloorDto
{
}

public class ProjectUnitReleaseBatchDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectBuildingId { get; set; }
    public string? ProjectBuildingCode { get; set; }
    public string? ProjectBuildingName { get; set; }
    public Guid? ProjectFloorId { get; set; }
    public string? ProjectFloorCode { get; set; }
    public string? ProjectFloorName { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PlannedReleaseDate { get; set; }
    public DateTime? ActualReleaseDate { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectUnitReleaseBatchDto
{
    public Guid? ProjectBuildingId { get; set; }
    public Guid? ProjectFloorId { get; set; }
    public string? Code { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = ProjectUnitReleaseBatchStatuses.Draft;
    public DateTime? PlannedReleaseDate { get; set; }
    public DateTime? ActualReleaseDate { get; set; }
    public int? SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectUnitReleaseBatchDto : CreateProjectUnitReleaseBatchDto
{
}

public class ProjectUnitHandoverBatchDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectBuildingId { get; set; }
    public string? ProjectBuildingCode { get; set; }
    public string? ProjectBuildingName { get; set; }
    public Guid? ProjectFloorId { get; set; }
    public string? ProjectFloorCode { get; set; }
    public string? ProjectFloorName { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PlannedHandoverDate { get; set; }
    public DateTime? ActualHandoverDate { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectUnitHandoverBatchDto
{
    public Guid? ProjectBuildingId { get; set; }
    public Guid? ProjectFloorId { get; set; }
    public string? Code { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = ProjectUnitHandoverBatchStatuses.Planned;
    public DateTime? PlannedHandoverDate { get; set; }
    public DateTime? ActualHandoverDate { get; set; }
    public int? SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectUnitHandoverBatchDto : CreateProjectUnitHandoverBatchDto
{
}

public class ProjectUnitDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectBuildingId { get; set; }
    public string? ProjectBuildingCode { get; set; }
    public string? ProjectBuildingName { get; set; }
    public Guid? ProjectFloorId { get; set; }
    public string? ProjectFloorCode { get; set; }
    public string? ProjectFloorName { get; set; }
    public Guid? ProjectUnitReleaseBatchId { get; set; }
    public string? ProjectUnitReleaseBatchCode { get; set; }
    public string? ProjectUnitReleaseBatchName { get; set; }
    public string? ProjectUnitReleaseBatchStatus { get; set; }
    public bool IsReleasedForMarket { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? ReleasedByDisplayName { get; set; }
    public Guid? CustomerBusinessPartnerId { get; set; }
    public string? CustomerBusinessPartnerName { get; set; }
    public Guid? SalesAgreementId { get; set; }
    public string? SalesAgreementNumber { get; set; }
    public string? SalesAgreementTitle { get; set; }
    public string? SalesAgreementType { get; set; }
    public string? SalesAgreementStatus { get; set; }
    public Guid? SalesOrderId { get; set; }
    public string? SalesOrderNumber { get; set; }
    public string? SalesOrderStatus { get; set; }
    public string CommercialStatus { get; set; } = string.Empty;
    public string? CommercialIntent { get; set; }
    public string HandoverStatus { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? BlockName { get; set; }
    public string? FloorLabel { get; set; }
    public decimal? AreaSquareMeters { get; set; }
    public decimal? ValuationRate { get; set; }
    public decimal? BasePrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime? HandoverDate { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class ProjectReleasedUnitSalesLookupDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid ProjectUnitId { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string ProjectUnitName { get; set; } = string.Empty;
    public string ProjectUnitType { get; set; } = string.Empty;
    public string ProjectUnitStatus { get; set; } = string.Empty;
    public string CommercialStatus { get; set; } = string.Empty;
    public string? CommercialIntent { get; set; }
    public string HandoverStatus { get; set; } = string.Empty;
    public Guid? CustomerBusinessPartnerId { get; set; }
    public string? CustomerBusinessPartnerName { get; set; }
    public decimal? AreaSquareMeters { get; set; }
    public decimal? BasePrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? PropertyReference { get; set; }
    public string? SuggestedAgreementTitle { get; set; }
    public string? SuggestedAgreementType { get; set; }
    public string? SuggestedLeaseAgreementTitle { get; set; }
    public string? SuggestedLeaseAgreementType { get; set; }
    public string? SuggestedOrderType { get; set; }
    public string? SuggestedPropertyType { get; set; }
    public string? SuggestedPropertyDescription { get; set; }
    public string? SuggestedPropertyLocation { get; set; }
    public string? SuggestedSalesOrderLineDescription { get; set; }
    public bool CanCreateSalesAgreement { get; set; }
    public bool CanCreateLeaseAgreement { get; set; }
    public bool CanCreateSalesOrder { get; set; }
    public string? SalesAgreementNumber { get; set; }
    public string? SalesOrderNumber { get; set; }
}

public class LinkProjectUnitSalesAgreementDto
{
    [Required]
    public Guid SalesAgreementId { get; set; }
}

public class LinkProjectUnitSalesOrderDto
{
    [Required]
    public Guid SalesOrderId { get; set; }
}

public class CreateProjectUnitDto
{
    public Guid? ProjectBuildingId { get; set; }
    public Guid? ProjectFloorId { get; set; }
    public Guid? ProjectUnitReleaseBatchId { get; set; }
    public bool IsReleasedForMarket { get; set; }
    public Guid? CustomerBusinessPartnerId { get; set; }
    public Guid? SalesAgreementId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public string? Code { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string UnitType { get; set; } = "Unit";
    public string Status { get; set; } = "Planned";
    public string? BlockName { get; set; }
    public string? FloorLabel { get; set; }
    public decimal? AreaSquareMeters { get; set; }
    public decimal? ValuationRate { get; set; }
    public decimal? BasePrice { get; set; }
    public string? Currency { get; set; }
    public DateTime? HandoverDate { get; set; }
    public int? SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectUnitDto : CreateProjectUnitDto
{
}

public class ProjectCustomerVariationDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string? ProjectUnitName { get; set; }
    public Guid? CustomerBusinessPartnerId { get; set; }
    public string? CustomerBusinessPartnerName { get; set; }
    public Guid? SalesAgreementId { get; set; }
    public string? SalesAgreementNumber { get; set; }
    public string? SalesAgreementTitle { get; set; }
    public Guid? SalesOrderId { get; set; }
    public string? SalesOrderNumber { get; set; }
    public string? SalesOrderStatus { get; set; }
    public Guid? JobCardId { get; set; }
    public string? JobCardNumber { get; set; }
    public string? JobCardTitle { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string? WorkOrderTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? VariationType { get; set; }
    public string Timing { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public decimal? QuotedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public decimal? BilledAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool RequiresScheduleAdjustment { get; set; }
    public int? ScheduleImpactDays { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectCustomerVariationDto
{
    public Guid? ProjectUnitId { get; set; }
    public Guid? CustomerBusinessPartnerId { get; set; }
    public Guid? SalesAgreementId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? VariationType { get; set; }
    public string Timing { get; set; } = "PreHandover";
    public string Status { get; set; } = "Requested";
    public DateTime? RequestDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public decimal? QuotedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public decimal? BilledAmount { get; set; }
    public string? Currency { get; set; }
    public bool RequiresScheduleAdjustment { get; set; }
    public int? ScheduleImpactDays { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectCustomerVariationDto : CreateProjectCustomerVariationDto
{
}

public class CreateProjectMaintenanceFollowThroughDto
{
    public Guid? MaintenanceAssetId { get; set; }
    public Guid? MaintenanceTypeId { get; set; }
    public Guid? PriorityLevelId { get; set; }
    public Guid? WorkOrderTypeId { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public string? BillingType { get; set; }
}

public class ProjectCommissioningItemDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string? ProjectUnitName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? SystemArea { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool RequiresRegulatoryInspection { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? CertificateReference { get; set; }
    public string? ResponsibleParty { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectCommissioningItemDto
{
    public Guid? ProjectUnitId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? SystemArea { get; set; }
    public string Status { get; set; } = "Planned";
    public bool RequiresRegulatoryInspection { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? CertificateReference { get; set; }
    public string? ResponsibleParty { get; set; }
    public int? SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectCommissioningItemDto : CreateProjectCommissioningItemDto
{
}

public class ProjectHandoverItemDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string? ProjectUnitName { get; set; }
    public Guid? ProjectUnitHandoverBatchId { get; set; }
    public string? ProjectUnitHandoverBatchCode { get; set; }
    public string? ProjectUnitHandoverBatchName { get; set; }
    public string? ProjectUnitHandoverBatchStatus { get; set; }
    public string HandoverType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ResponsibleParty { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? TargetDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectHandoverItemDto
{
    public Guid? ProjectUnitId { get; set; }
    public Guid? ProjectUnitHandoverBatchId { get; set; }
    public string HandoverType { get; set; } = "Other";

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "Planned";
    public string? ResponsibleParty { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? TargetDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? SortOrder { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectHandoverItemDto : CreateProjectHandoverItemDto
{
}

public class ProjectSnagItemDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string? ProjectUnitName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReportedDate { get; set; }
    public DateTime? TargetClosureDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? RaisedByName { get; set; }
    public string? ResponsibleParty { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectSnagItemDto
{
    public Guid? ProjectUnitId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public DateTime? ReportedDate { get; set; }
    public DateTime? TargetClosureDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? RaisedByName { get; set; }
    public string? ResponsibleParty { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectSnagItemDto : CreateProjectSnagItemDto
{
}

public class ProjectDefectLiabilityCaseDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string? ProjectUnitName { get; set; }
    public Guid? CustomerBusinessPartnerId { get; set; }
    public string? CustomerBusinessPartnerName { get; set; }
    public Guid? JobCardId { get; set; }
    public string? JobCardNumber { get; set; }
    public string? JobCardTitle { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string? WorkOrderTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ReportedDate { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public bool IsWarrantyRelated { get; set; }
    public string? WarrantyCategory { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? FirstResponseDate { get; set; }
    public int? ResponseSlaDays { get; set; }
    public int? ResolutionSlaDays { get; set; }
    public decimal? RectificationCost { get; set; }
    public decimal? ChargeableAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class CreateProjectDefectLiabilityCaseDto
{
    public Guid? ProjectUnitId { get; set; }
    public Guid? CustomerBusinessPartnerId { get; set; }
    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string Status { get; set; } = "Reported";
    public DateTime? ReportedDate { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public bool IsWarrantyRelated { get; set; }
    public string? WarrantyCategory { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? FirstResponseDate { get; set; }
    public int? ResponseSlaDays { get; set; }
    public int? ResolutionSlaDays { get; set; }
    public decimal? RectificationCost { get; set; }
    public decimal? ChargeableAmount { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectDefectLiabilityCaseDto : CreateProjectDefectLiabilityCaseDto
{
}

public class ReorderProjectPhasesDto
{
    public Guid? ParentPhaseId { get; set; }
    public List<Guid> OrderedIds { get; set; } = new();
}

public class CreateProjectDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? ProjectCode { get; set; }
    public string? Summary { get; set; }
    public string? BusinessCase { get; set; }
    public string? Objectives { get; set; }
    public string? StrategicAlignment { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public Guid? ProjectPriorityId { get; set; }
    public Guid? TemplateId { get; set; }
    public Guid? PortfolioId { get; set; }
    public Guid? ProgramId { get; set; }
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
    public decimal? EstimatedBudget { get; set; }
    public string? ScopeStatement { get; set; }
    public string? Assumptions { get; set; }
    public string? Constraints { get; set; }
    public string? ExpectedBenefits { get; set; }
    public string? FundingSource { get; set; }
    public bool? ApprovalRequired { get; set; }
    public bool? ExternalPortalAccessEnabled { get; set; }
    public bool? ExternalCollaborationEnabled { get; set; }
    public UpsertProjectDevelopmentProfileDto? DevelopmentProfile { get; set; }
}

public class UpdateProjectDto : CreateProjectDto
{
    public decimal? ApprovedBudget { get; set; }
    public decimal? ActualCost { get; set; }
    public string? BudgetStatus { get; set; }
    public string? StatusRemarks { get; set; }
}

public class ProjectInitiationVersionDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProjectMemberDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class AddProjectMemberDto
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public string Role { get; set; } = "TeamMember";
}

public class ProjectWorkItemDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserDisplayName { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public decimal PercentComplete { get; set; }
    public bool IsRollupEnabled { get; set; }
    public decimal? EffortEstimateHours { get; set; }
    public decimal? ActualEffortHours { get; set; }
    public DateTime? BaselinePlannedStartDate { get; set; }
    public DateTime? BaselinePlannedEndDate { get; set; }
    public int BaselineVarianceDays { get; set; }
    public bool IsOffBaseline { get; set; }
    public bool CanExternalUpdate { get; set; }
    public bool CanExternalComment { get; set; }
    public List<ProjectWorkItemDto> Children { get; set; } = new();
}

public class CreateProjectWorkItemDto
{
    public Guid? ParentId { get; set; }
    [Required]
    public string NodeType { get; set; } = "Task";
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "New";
    public string Priority { get; set; } = "Normal";
    public Guid? AssignedToUserId { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public decimal PercentComplete { get; set; }
    public bool IsRollupEnabled { get; set; } = true;
    public decimal? EffortEstimateHours { get; set; }
    public decimal? ActualEffortHours { get; set; }
    public string? ScheduleChangeReason { get; set; }
}

public class ReorderProjectWorkItemsDto
{
    public Guid? ParentId { get; set; }
    public List<Guid> OrderedIds { get; set; } = new();
}

public class UpdateProjectWorkItemProgressDto
{
    [Required]
    public string Status { get; set; } = "In Progress";
    public decimal PercentComplete { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public string? Notes { get; set; }
}

public class ProjectMilestoneDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime TargetDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool RequiresApproval { get; set; }
}

public class ProjectResourceAllocationDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public string AllocationRole { get; set; } = string.Empty;
    public string AllocationType { get; set; } = string.Empty;
    public decimal AllocationValue { get; set; }
    public decimal? PlannedHours { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BookingType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> RequiredCertifications { get; set; } = new();
    public string RoutingPolicy { get; set; } = "Balanced";
    public Guid? SourceAllocationId { get; set; }
    public Guid? ReplacementAllocationId { get; set; }
    public string? SubstitutionReason { get; set; }
    public bool CanSubstitute { get; set; }
    public bool HasConflict { get; set; }
    public decimal CapacityUtilizationPercent { get; set; }
    public decimal QualificationMatchPercent { get; set; }
    public string QualificationRisk { get; set; } = "NotEvaluated";
    public List<string> MissingSkills { get; set; } = new();
    public List<string> MissingCertifications { get; set; } = new();
    public Guid? RecommendedUserId { get; set; }
    public string? RecommendedUserDisplayName { get; set; }
    public string? RoutingRecommendation { get; set; }
}

public class CreateProjectResourceAllocationDto
{
    public Guid? WorkItemId { get; set; }
    [Required]
    public Guid UserId { get; set; }
    [Required]
    public string AllocationRole { get; set; } = "TeamMember";
    public string AllocationType { get; set; } = "Hours";
    public decimal AllocationValue { get; set; }
    public decimal? PlannedHours { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BookingType { get; set; } = "Soft";
    public string Status { get; set; } = "Requested";
    public string? Notes { get; set; }
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> RequiredCertifications { get; set; } = new();
    public string RoutingPolicy { get; set; } = "Balanced";
}

public class SubstituteProjectResourceAllocationDto
{
    [Required]
    public Guid ReplacementUserId { get; set; }
    public Guid? WorkItemId { get; set; }
    public decimal? TransferAllocationValue { get; set; }
    public decimal? TransferPlannedHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool FullReplacement { get; set; }
    public bool ApproveReplacement { get; set; }
    public string? BookingType { get; set; }
    public string? Reason { get; set; }
}

public class ProjectResourceSubstitutionResultDto
{
    public ProjectResourceAllocationDto SourceAllocation { get; set; } = new();
    public ProjectResourceAllocationDto ReplacementAllocation { get; set; } = new();
}

public class CreateProjectMilestoneDto
{
    public Guid? WorkItemId { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime TargetDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public string Status { get; set; } = "Draft";
    public bool RequiresApproval { get; set; }
}

public class ProjectBillingScheduleDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? ContractMilestoneId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BillingType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? BillingPercentage { get; set; }
    public DateTime BillingDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsBillable { get; set; }
    public string? ContractMilestoneName { get; set; }
}

public class CreateProjectBillingScheduleDto
{
    public Guid? ContractId { get; set; }
    public Guid? ContractMilestoneId { get; set; }
    public Guid? MilestoneId { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    public string BillingType { get; set; } = "Milestone";
    public decimal Amount { get; set; }
    public decimal? BillingPercentage { get; set; }
    public DateTime BillingDate { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Description { get; set; }
    public bool IsBillable { get; set; } = true;
}

public class ProjectInvoiceRequestDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? BillingScheduleId { get; set; }
    public Guid? ContractId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ExternalReference { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectInvoiceRequestDto
{
    public Guid? BillingScheduleId { get; set; }
    public Guid? ContractId { get; set; }
    public decimal RequestedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string? ExternalReference { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectInvoiceRequestWorkflowDto
{
    public string? ExternalReference { get; set; }
    public string? Comments { get; set; }
}

public class ProjectRiskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerDisplayName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Probability { get; set; }
    public int Impact { get; set; }
    public int Exposure { get; set; }
    public string ResponseStrategy { get; set; } = string.Empty;
    public string? MitigationPlan { get; set; }
    public DateTime? DueDate { get; set; }
}

public class CreateProjectRiskDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public string Status { get; set; } = "Open";
    public string Category { get; set; } = "General";
    public int Probability { get; set; }
    public int Impact { get; set; }
    public string ResponseStrategy { get; set; } = "Monitor";
    public string? MitigationPlan { get; set; }
    public DateTime? DueDate { get; set; }
}

public class ProjectIssueDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerDisplayName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime? TargetResolutionDate { get; set; }
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
}

public class CreateProjectIssueDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public string Status { get; set; } = "Open";
    public string Severity { get; set; } = "Medium";
    public DateTime? TargetResolutionDate { get; set; }
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
}

public class ProjectQualityCheckpointDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid? QaOwnerId { get; set; }
    public string? QaOwnerDisplayName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public bool RequiresQaSignOff { get; set; }
    public DateTime? SignedOffAt { get; set; }
    public Guid? SignedOffById { get; set; }
    public string? SignedOffByDisplayName { get; set; }
    public string? SignOffNotes { get; set; }
}

public class CreateProjectQualityCheckpointDto
{
    public Guid? WorkItemId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid? QaOwnerId { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime? DueDate { get; set; }
    public bool RequiresQaSignOff { get; set; }
}

public class ProjectNonConformanceDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? QualityCheckpointId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerDisplayName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReportedAt { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
    public string? ResolutionNotes { get; set; }
}

public class CreateProjectNonConformanceDto
{
    public Guid? QualityCheckpointId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid? OwnerId { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public DateTime? TargetResolutionDate { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
}

public class ProjectChangeRequestDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? BusinessImpact { get; set; }
    public string? RiskImpact { get; set; }
    public decimal? CostImpact { get; set; }
    public int? ScheduleImpactDays { get; set; }
}

public class CreateProjectChangeRequestDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ChangeType { get; set; } = "Scope";
    public string Status { get; set; } = "Draft";
    public string? BusinessImpact { get; set; }
    public string? RiskImpact { get; set; }
    public decimal? CostImpact { get; set; }
    public int? ScheduleImpactDays { get; set; }
}

public class ProjectDocumentDto
{
    public Guid Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string VersionLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsExternalVisible { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ArtifactLabel { get; set; }
}

public class AttachProjectDocumentDto
{
    [Required]
    public string DocumentName { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string DocumentType { get; set; } = "Attachment";
    [Required]
    public string FilePath { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string VersionLabel { get; set; } = "1.0";
    public string Status { get; set; } = "Active";
    public DateTime? EffectiveDate { get; set; }
    public bool IsExternalVisible { get; set; }
}

public class ProjectCommentDto
{
    public Guid Id { get; set; }
    public Guid? WorkItemId { get; set; }
    public string CommentType { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? MentionedUsersJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateProjectCommentDto
{
    public Guid? WorkItemId { get; set; }
    public string CommentType { get; set; } = "Comment";
    [Required]
    public string Body { get; set; } = string.Empty;
    public string? MentionedUsersJson { get; set; }
}

public class ProjectDeliverableDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? MilestoneId { get; set; }
    public Guid? SubmittedDocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
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
    public string? AcceptanceNotes { get; set; }
    public string? ExternalApprovalNotes { get; set; }
    public bool CanExternalSubmit { get; set; }
    public bool CanExternalApprove { get; set; }
    public List<ProjectDeliverableExternalReviewDto> ExternalReviews { get; set; } = new();
}

public class CreateProjectDeliverableDto
{
    public Guid? WorkItemId { get; set; }
    public Guid? MilestoneId { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? TargetDate { get; set; }
    public bool ExternalSubmissionAllowed { get; set; }
    public bool ExternalSignOffRequired { get; set; }
    public bool IsExternalVisible { get; set; }
}

public class SubmitProjectDeliverableDto
{
    public Guid? SubmittedDocumentId { get; set; }
    public string? Notes { get; set; }
}

public class ProjectDeliverableExternalReviewDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid DeliverableId { get; set; }
    public DateTime ReviewDate { get; set; }
    public Guid? ReviewedById { get; set; }
    public Guid? SubmittedDocumentId { get; set; }
    public string? SubmittedDocumentName { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? StatusSnapshot { get; set; }
    public string? Notes { get; set; }
}

public class ProjectTaskDependencyDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid PredecessorWorkItemId { get; set; }
    public Guid SuccessorWorkItemId { get; set; }
    public string DependencyType { get; set; } = string.Empty;
    public int LagDays { get; set; }
    public bool IsEnforced { get; set; }
}

public class CreateProjectTaskDependencyDto
{
    [Required]
    public Guid PredecessorWorkItemId { get; set; }
    [Required]
    public Guid SuccessorWorkItemId { get; set; }
    public string DependencyType { get; set; } = "FS";
    public int LagDays { get; set; }
    public bool IsEnforced { get; set; } = true;
}

public class ProjectInterdependencyDto
{
    public Guid Id { get; set; }
    public Guid SourceProjectId { get; set; }
    public string SourceProjectCode { get; set; } = string.Empty;
    public string SourceProjectTitle { get; set; } = string.Empty;
    public Guid TargetProjectId { get; set; }
    public string TargetProjectCode { get; set; } = string.Empty;
    public string TargetProjectTitle { get; set; } = string.Empty;
    public string DependencyType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ImpactLevel { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public DateTime? DueDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MitigationPlan { get; set; }
}

public class CreateProjectInterdependencyDto
{
    [Required]
    public Guid SourceProjectId { get; set; }

    [Required]
    public Guid TargetProjectId { get; set; }

    [Required]
    public string DependencyType { get; set; } = "Schedule";

    public string Status { get; set; } = "Open";
    public string ImpactLevel { get; set; } = "Medium";
    public Guid? OwnerId { get; set; }
    public DateTime? DueDate { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? MitigationPlan { get; set; }
}

public class ProjectBaselineDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedOn { get; set; }
    public decimal SnapshotProgressPercent { get; set; }
    public decimal? SnapshotApprovedBudget { get; set; }
    public DateTime? SnapshotFinishDate { get; set; }
}

public class CreateProjectBaselineDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ProjectBaselineComparisonDto
{
    public Guid BaselineId { get; set; }
    public string BaselineName { get; set; } = string.Empty;
    public DateTime? BaselineCreatedOn { get; set; }
    public decimal BaselineProgressPercent { get; set; }
    public decimal CurrentProgressPercent { get; set; }
    public DateTime? BaselineFinishDate { get; set; }
    public DateTime? CurrentFinishDate { get; set; }
    public int ScheduleVarianceDays { get; set; }
    public decimal BudgetVariance { get; set; }
    public int ChangedWorkItemCount { get; set; }
    public int ChangedMilestoneCount { get; set; }
    public List<ProjectBaselineWorkItemChangeDto> WorkItemChanges { get; set; } = new();
    public List<ProjectBaselineMilestoneChangeDto> MilestoneChanges { get; set; } = new();
}

public class ProjectBaselineWorkItemChangeDto
{
    public Guid WorkItemId { get; set; }
    public string WorkItemTitle { get; set; } = string.Empty;
    public DateTime? BaselinePlannedStartDate { get; set; }
    public DateTime? CurrentPlannedStartDate { get; set; }
    public DateTime? BaselinePlannedEndDate { get; set; }
    public DateTime? CurrentPlannedEndDate { get; set; }
    public decimal BaselinePercentComplete { get; set; }
    public decimal CurrentPercentComplete { get; set; }
    public int ScheduleVarianceDays { get; set; }
}

public class ProjectBaselineMilestoneChangeDto
{
    public Guid MilestoneId { get; set; }
    public string MilestoneTitle { get; set; } = string.Empty;
    public DateTime BaselineTargetDate { get; set; }
    public DateTime CurrentTargetDate { get; set; }
    public string BaselineStatus { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public int ScheduleVarianceDays { get; set; }
}

public class ProjectTimesheetEntryDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string? ProjectTitle { get; set; }
    public Guid? WorkItemId { get; set; }
    public string? WorkItemTitle { get; set; }
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public DateTime EntryDate { get; set; }
    public decimal Hours { get; set; }
    public bool IsBillable { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal CostAmount { get; set; }
    public string WorkType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByDisplayName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class CreateProjectTimesheetEntryDto
{
    public Guid? WorkItemId { get; set; }
    [Required]
    public Guid UserId { get; set; }
    public DateTime EntryDate { get; set; }
    public decimal Hours { get; set; }
    public bool IsBillable { get; set; }
    public decimal HourlyRate { get; set; }
    public string WorkType { get; set; } = "Standard";
    public string? Notes { get; set; }
    public string Status { get; set; } = "Draft";
}

public class ProjectExpenseDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string? ProjectTitle { get; set; }
    public Guid? WorkItemId { get; set; }
    public string? WorkItemTitle { get; set; }
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public bool IsBillable { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ReceiptDocumentId { get; set; }
    public string? Notes { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByDisplayName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class CreateProjectExpenseDto
{
    public Guid? WorkItemId { get; set; }
    [Required]
    public Guid UserId { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = "General";
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public bool IsBillable { get; set; }
    public Guid? ReceiptDocumentId { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Draft";
}

public class ProjectApprovalQueueSummaryDto
{
    public int DraftCount { get; set; }
    public int SubmittedCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public decimal TotalHours { get; set; }
    public decimal TotalAmount { get; set; }
}

public class ProjectTimesheetApprovalQueueItemDto
{
    public Guid EntryId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string? WorkItemTitle { get; set; }
    public DateTime EntryDate { get; set; }
    public decimal Hours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal CostAmount { get; set; }
    public bool IsBillable { get; set; }
    public string WorkType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string QueueStage { get; set; } = string.Empty;
    public int DaysOpen { get; set; }
    public string? Notes { get; set; }
}

public class ProjectExpenseApprovalQueueItemDto
{
    public Guid ExpenseId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string? WorkItemTitle { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsBillable { get; set; }
    public string Status { get; set; } = string.Empty;
    public string QueueStage { get; set; } = string.Empty;
    public int DaysOpen { get; set; }
    public string? Notes { get; set; }
}

public class ProjectRevenueRecognitionDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? InvoiceRequestId { get; set; }
    public string RecognitionPeriod { get; set; } = string.Empty;
    public decimal RecognizedRevenue { get; set; }
    public decimal RecognizedCost { get; set; }
    public decimal GrossMargin { get; set; }
    public decimal CashCollected { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ProjectAssetLinkDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? MaintenanceAssetId { get; set; }
    public Guid? CompanyAssetId { get; set; }
    public Guid? JobCardId { get; set; }
    public string LinkType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? AssetName { get; set; }
    public string? JobCardNumber { get; set; }
}

public class CreateProjectAssetLinkDto
{
    public Guid? MaintenanceAssetId { get; set; }
    public Guid? CompanyAssetId { get; set; }
    public Guid? JobCardId { get; set; }
    public string LinkType { get; set; } = "Asset";
    public string Status { get; set; } = "Linked";
    public string? Notes { get; set; }
}

public class ProjectExternalAccessPolicyDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string ArtifactType { get; set; } = string.Empty;
    public Guid? ArtifactId { get; set; }
    public string AccessLevel { get; set; } = string.Empty;
    public bool CanComment { get; set; }
    public bool CanUpload { get; set; }
    public bool CanApprove { get; set; }
    public string? Notes { get; set; }
    public string? BusinessPartnerName { get; set; }
    public string? ArtifactLabel { get; set; }
}

public class CreateProjectExternalAccessPolicyDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }
    [Required]
    public string ArtifactType { get; set; } = "Project";
    public Guid? ArtifactId { get; set; }
    public string AccessLevel { get; set; } = "Read";
    public bool CanComment { get; set; }
    public bool CanUpload { get; set; }
    public bool CanApprove { get; set; }
    public string? Notes { get; set; }
}

public class ProjectDecisionDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime DecisionDate { get; set; }
    public Guid? ApproverId { get; set; }
    public string? ApproverDisplayName { get; set; }
    public string? Rationale { get; set; }
    public string? AlternativesConsidered { get; set; }
    public string? ImpactSummary { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ApprovedAt { get; set; }
}

public class CreateProjectDecisionDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
    public DateTime? DecisionDate { get; set; }
    public Guid? ApproverId { get; set; }
    public string? Rationale { get; set; }
    public string? AlternativesConsidered { get; set; }
    public string? ImpactSummary { get; set; }
    public string Status { get; set; } = "Draft";
}

public class ProjectMeetingMinuteDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime MeetingDate { get; set; }
    public Guid? FacilitatorId { get; set; }
    public string? FacilitatorDisplayName { get; set; }
    public string MeetingType { get; set; } = string.Empty;
    public string? Minutes { get; set; }
    public string? AttendeesJson { get; set; }
}

public class CreateProjectMeetingMinuteDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
    public DateTime? MeetingDate { get; set; }
    public Guid? FacilitatorId { get; set; }
    public string MeetingType { get; set; } = "Status";
    public string? Minutes { get; set; }
    public string? AttendeesJson { get; set; }
}

public class ProjectActionItemDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? MeetingMinuteId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerDisplayName { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? MeetingTitle { get; set; }
    public string? WorkItemTitle { get; set; }
}

public class CreateProjectActionItemDto
{
    public Guid? MeetingMinuteId { get; set; }
    public Guid? WorkItemId { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "Open";
    public string Priority { get; set; } = "Normal";
}

public class ProjectLessonLearnedDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Recommendation { get; set; }
    public string? AppliedPhase { get; set; }
    public string Visibility { get; set; } = string.Empty;
}

public class CreateProjectLessonLearnedDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string? Description { get; set; }
    public string? Recommendation { get; set; }
    public string? AppliedPhase { get; set; }
    public string Visibility { get; set; } = "Internal";
}

public class ProjectClosureDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public decimal? FinalBudget { get; set; }
    public decimal? FinalCost { get; set; }
    public bool DeliverablesAccepted { get; set; }
    public bool TasksCompletedOrWaived { get; set; }
    public bool AssetsReconciled { get; set; }
    public bool OpenItemsDisposed { get; set; }
    public string? ClosureChecklistJson { get; set; }
    public string? OpenItemsDisposition { get; set; }
    public string? AssetReconciliationNotes { get; set; }
    public string? LessonsLearnedSummary { get; set; }
    public string? PostImplementationReview { get; set; }
    public string? OverrideReason { get; set; }
    public string? RejectionReason { get; set; }
}

public class UpsertProjectClosureDto
{
    public decimal? FinalBudget { get; set; }
    public decimal? FinalCost { get; set; }
    public bool DeliverablesAccepted { get; set; }
    public bool TasksCompletedOrWaived { get; set; }
    public bool AssetsReconciled { get; set; }
    public bool OpenItemsDisposed { get; set; }
    public string? ClosureChecklistJson { get; set; }
    public string? OpenItemsDisposition { get; set; }
    public string? AssetReconciliationNotes { get; set; }
    public string? LessonsLearnedSummary { get; set; }
    public string? PostImplementationReview { get; set; }
    public string? OverrideReason { get; set; }
}

public class ProjectAiInsightDto
{
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public class ProjectScheduleAnalysisDto
{
    public Guid ProjectId { get; set; }
    public int DependencyCount { get; set; }
    public int CriticalPathTaskCount { get; set; }
    public List<Guid> CriticalPathWorkItemIds { get; set; } = new();
    public DateTime? ForecastFinishDate { get; set; }
    public int TotalSlackDays { get; set; }
    public bool HasCircularDependencies { get; set; }
    public int RecalculatedItemCount { get; set; }
    public List<ProjectScheduleViolationDto> Violations { get; set; } = new();
}

public class ProjectScheduleViolationDto
{
    public Guid WorkItemId { get; set; }
    public string WorkItemTitle { get; set; } = string.Empty;
    public Guid? BlockingWorkItemId { get; set; }
    public string? BlockingWorkItemTitle { get; set; }
    public string DependencyType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
    public DateTime? ExpectedDate { get; set; }
}

public class ProjectResourceOptimizationSuggestionDto
{
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public Guid? SuggestedReplacementUserId { get; set; }
    public string? SuggestedReplacementUserDisplayName { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<string> MatchedSkills { get; set; } = new();
    public int MatchedSkillCount { get; set; }
    public int MatchedCertifiedSkillCount { get; set; }
    public int ReplacementVerifiedSkillCount { get; set; }
    public int ReplacementCertifiedSkillCount { get; set; }
    public int ReplacementExpiringCertificationCount { get; set; }
    public int ReplacementExpiredCertificationCount { get; set; }
    public string ReplacementQualificationRisk { get; set; } = "Healthy";
    public List<Guid> AffectedAllocationIds { get; set; } = new();
}

public class ProjectMobileAssignmentDto
{
    public Guid ProjectId { get; set; }
    public Guid WorkItemId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string WorkItemTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal PercentComplete { get; set; }
    public DateTime? PlannedEndDate { get; set; }
}

public class ProjectMobileSummaryDto
{
    public int AssignmentCount { get; set; }
    public int OverdueCount { get; set; }
    public decimal PendingHours { get; set; }
    public decimal PendingExpenses { get; set; }
    public List<ProjectMobileAssignmentDto> Assignments { get; set; } = new();
}

public class ProjectTypeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresSponsor { get; set; }
    public bool RequiresApproval { get; set; }
    public string? MandatoryFieldsJson { get; set; }
}

public class CreateProjectTypeDto
{
    [Required]
    public string Code { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequiresSponsor { get; set; } = true;
    public bool RequiresApproval { get; set; } = true;
    public string? MandatoryFieldsJson { get; set; }
}

public class ProjectPriorityDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProjectPriorityDto
{
    [Required]
    public string Code { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProjectTemplateDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public string? ProjectTypeName { get; set; }
    public string VersionLabel { get; set; } = string.Empty;
    public string? TemplateDefinitionJson { get; set; }
    public bool IsActive { get; set; }
}

public class ProjectPhaseTemplateDto
{
    public Guid Id { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public string? ProjectTypeName { get; set; }
    public Guid? ParentPhaseTemplateId { get; set; }
    public string? ParentPhaseTemplateName { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DefaultStatus { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsOptional { get; set; }
    public bool IsStageGateRequired { get; set; }
    public bool IsActive { get; set; }
    public string? AppliesToDeliveryStructure { get; set; }
    public string? AppliesToDevelopmentType { get; set; }
    public List<ProjectStageGateRuleDto> StageGateRules { get; set; } = new();
}

public class CreateProjectPhaseTemplateDto
{
    public Guid? ProjectTypeId { get; set; }
    public Guid? ParentPhaseTemplateId { get; set; }
    public string? Code { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string DefaultStatus { get; set; } = "NotStarted";
    public int SortOrder { get; set; }
    public bool IsOptional { get; set; }
    public bool IsStageGateRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public string? AppliesToDeliveryStructure { get; set; }
    public string? AppliesToDevelopmentType { get; set; }
}

public class UpdateProjectPhaseTemplateDto : CreateProjectPhaseTemplateDto
{
}

public class ProjectStageGateRuleDto
{
    public Guid Id { get; set; }
    public Guid ProjectPhaseTemplateId { get; set; }
    public string ProjectPhaseTemplateName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RequirementType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int? MinimumCount { get; set; }
    public int? MaximumCount { get; set; }
    public bool IsBlocking { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProjectStageGateRuleDto
{
    [Required]
    public Guid ProjectPhaseTemplateId { get; set; }

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string RequirementType { get; set; } = "Packages";
    public string Scope { get; set; } = "Phase";
    public int? MinimumCount { get; set; }
    public int? MaximumCount { get; set; }
    public bool IsBlocking { get; set; } = true;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateProjectStageGateRuleDto : CreateProjectStageGateRuleDto
{
}

public class ProjectPortfolioDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? StrategicObjective { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? SponsorId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }
    public decimal? BudgetCap { get; set; }
    public int ProgramCount { get; set; }
    public int ProjectCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public decimal TotalEstimatedBudget { get; set; }
    public decimal TotalActualCost { get; set; }
}

public class CreateProjectPortfolioDto
{
    [Required]
    public string Code { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Active";
    public string? StrategicObjective { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? SponsorId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }
    public decimal? BudgetCap { get; set; }
}

public class ProjectProgramDto
{
    public Guid Id { get; set; }
    public Guid? PortfolioId { get; set; }
    public string? PortfolioName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ProgramManagerId { get; set; }
    public Guid? SponsorId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }
    public decimal? BudgetCap { get; set; }
    public int ProjectCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public decimal TotalEstimatedBudget { get; set; }
    public decimal TotalActualCost { get; set; }
}

public class CreateProjectProgramDto
{
    [Required]
    public string Code { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? PortfolioId { get; set; }
    public string Status { get; set; } = "Active";
    public Guid? ProgramManagerId { get; set; }
    public Guid? SponsorId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }
    public decimal? BudgetCap { get; set; }
}

public class CreateProjectTemplateDto
{
    [Required]
    public string Code { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public string VersionLabel { get; set; } = "1.0";
    public string? TemplateDefinitionJson { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProjectManagementSettingsDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ProjectNumberFormat { get; set; } = string.Empty;
    public bool RequireSponsor { get; set; }
    public bool DefaultApprovalRequired { get; set; }
    public Guid? DefaultProjectTypeId { get; set; }
    public Guid? DefaultProjectPriorityId { get; set; }
    public Guid? DefaultTemplateId { get; set; }
    public string? MandatoryFieldsByTypeJson { get; set; }
    public string? Notes { get; set; }
}

public class ProjectCatalogEntryDto
{
    public Guid Id { get; set; }
    public string CatalogType { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProjectCatalogEntryDto
{
    [Required]
    public string CatalogType { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateProjectManagementSettingsDto
{
    [Required]
    public string ProjectNumberFormat { get; set; } = "PRJ-{YYYY}-{####}";
    public bool RequireSponsor { get; set; } = true;
    public bool DefaultApprovalRequired { get; set; } = true;
    public Guid? DefaultProjectTypeId { get; set; }
    public Guid? DefaultProjectPriorityId { get; set; }
    public Guid? DefaultTemplateId { get; set; }
    public string? MandatoryFieldsByTypeJson { get; set; }
    public string? Notes { get; set; }
}

public class ProjectDashboardDto
{
    public int TotalProjects { get; set; }
    public int DraftProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int PendingApprovalProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int OverdueTasks { get; set; }
    public int DueMilestonesThisMonth { get; set; }
    public int OverdueMilestones { get; set; }
    public int OpenRisks { get; set; }
    public int OpenIssues { get; set; }
    public decimal TotalEstimatedBudget { get; set; }
    public decimal TotalApprovedBudget { get; set; }
    public decimal TotalActualCost { get; set; }
    public List<ProjectDto> AtRiskProjects { get; set; } = new();
}

public class ProjectWorkspaceDto
{
    public ProjectDetailDto Project { get; set; } = new();
    public ProjectFinancialControlSummaryDto? FinancialSummary { get; set; }
    public ProjectCommercialSummaryDto? CommercialSummary { get; set; }
    public ProjectIntegrationSummaryDto? IntegrationSummary { get; set; }
    public ProjectGovernanceSummaryDto? GovernanceSummary { get; set; }
    public ProjectPostHandoverSummaryDto? PostHandoverSummary { get; set; }
    public List<ProjectPhaseGateEvaluationDto> PhaseGateEvaluations { get; set; } = new();
    public ProjectLinkOptionsDto LinkOptions { get; set; } = new();
}

public class ProjectPostHandoverSummaryDto
{
    public Guid ProjectId { get; set; }
    public int OpenHandoverItemCount { get; set; }
    public int ActiveDefectLiabilityCount { get; set; }
    public int WarrantyCaseCount { get; set; }
    public int ChargeableCaseCount { get; set; }
    public int ResponseBreachCount { get; set; }
    public int ResolutionBreachCount { get; set; }
    public int WarrantyExpiringSoonCount { get; set; }
    public decimal TotalRectificationExposure { get; set; }
    public decimal ChargeableExposure { get; set; }
    public decimal WarrantyExposure { get; set; }
    public List<ProjectPostHandoverAlertDto> Alerts { get; set; } = new();
}

public class ProjectPostHandoverAlertDto
{
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public Guid? ProjectUnitId { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string? ProjectUnitName { get; set; }
    public Guid? ProjectDefectLiabilityCaseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
}

public class ProjectSalesAgreementLinkOptionDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string AgreementTitle { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? PropertyReference { get; set; }
    public string AgreementType { get; set; } = string.Empty;
    public string AgreementStatus { get; set; } = string.Empty;
}

public class ProjectSalesOrderLinkOptionDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? PropertyReference { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ProjectJobCardLinkOptionDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string JobCardNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AssetName { get; set; }
}

public class ProjectWorkOrderLinkOptionDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AssetName { get; set; }
    public Guid? JobCardId { get; set; }
    public string? JobCardNumber { get; set; }
}

public class ProjectLinkOptionsDto
{
    public List<ProjectSalesAgreementLinkOptionDto> SalesAgreements { get; set; } = new();
    public List<ProjectSalesOrderLinkOptionDto> SalesOrders { get; set; } = new();
    public List<ProjectJobCardLinkOptionDto> JobCards { get; set; } = new();
    public List<ProjectWorkOrderLinkOptionDto> WorkOrders { get; set; } = new();
}

public class ProjectTaskAgingReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid WorkItemId { get; set; }
    public string WorkItemTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? PlannedEndDate { get; set; }
    public int DaysOverdue { get; set; }
}

public class ProjectMilestoneTrackerReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid MilestoneId { get; set; }
    public string MilestoneTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime TargetDate { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysFromToday { get; set; }
}

public class ProjectBudgetActualReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BudgetStatus { get; set; } = string.Empty;
    public decimal? EstimatedBudget { get; set; }
    public decimal? ApprovedBudget { get; set; }
    public decimal? ActualCost { get; set; }
    public decimal BudgetVariance { get; set; }
    public decimal ProgressPercent { get; set; }
}

public class ProjectRiskIssueSummaryReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public int OpenRiskCount { get; set; }
    public int HighRiskCount { get; set; }
    public int OpenIssueCount { get; set; }
}

public class ProjectPortfolioSummaryReportItemDto
{
    public Guid PortfolioId { get; set; }
    public string PortfolioCode { get; set; } = string.Empty;
    public string PortfolioName { get; set; } = string.Empty;
    public int ProgramCount { get; set; }
    public int ProjectCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public decimal TotalEstimatedBudget { get; set; }
    public decimal TotalActualCost { get; set; }
    public int HighRiskProjectCount { get; set; }
}

public class ProjectProgramSummaryReportItemDto
{
    public Guid ProgramId { get; set; }
    public Guid? PortfolioId { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string? PortfolioName { get; set; }
    public int ProjectCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public decimal TotalEstimatedBudget { get; set; }
    public decimal TotalActualCost { get; set; }
    public decimal AverageProgressPercent { get; set; }
}

public class ProjectResourceCapacityReportItemDto
{
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public int AllocationCount { get; set; }
    public decimal TotalAllocatedHours { get; set; }
    public decimal TotalAllocatedPercent { get; set; }
    public decimal StandardCapacityHours { get; set; }
    public decimal EffectiveCapacityHours { get; set; }
    public decimal ApprovedLeaveHours { get; set; }
    public decimal ApprovedLeaveDays { get; set; }
    public int LeaveRequestCount { get; set; }
    public decimal CapacityUtilizationPercent { get; set; }
    public int ConflictCount { get; set; }
    public int VerifiedSkillCount { get; set; }
    public int CertifiedSkillCount { get; set; }
    public int ExpiringCertificationCount { get; set; }
    public int ExpiredCertificationCount { get; set; }
    public string QualificationRisk { get; set; } = "Healthy";
    public List<Guid> AllocationIds { get; set; } = new();
}

public class ProjectResourceCapacityRecommendationDto
{
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public decimal CapacityUtilizationPercent { get; set; }
    public decimal EffectiveCapacityHours { get; set; }
    public decimal ApprovedLeaveHours { get; set; }
    public decimal ApprovedLeaveDays { get; set; }
    public int LeaveRequestCount { get; set; }
    public int ConflictCount { get; set; }
    public int VerifiedSkillCount { get; set; }
    public int CertifiedSkillCount { get; set; }
    public int ExpiringCertificationCount { get; set; }
    public int ExpiredCertificationCount { get; set; }
    public string QualificationRisk { get; set; } = "Healthy";
    public string Severity { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public decimal SuggestedReductionHours { get; set; }
    public List<string> ProjectCodes { get; set; } = new();
    public Guid? SuggestedReplacementUserId { get; set; }
    public string? SuggestedReplacementUserDisplayName { get; set; }
    public List<string> MatchedSkills { get; set; } = new();
    public List<Guid> AffectedAllocationIds { get; set; } = new();
}

public class ProjectBillingSummaryReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid? ContractId { get; set; }
    public int ReadyBillingScheduleCount { get; set; }
    public int OverdueBillingScheduleCount { get; set; }
    public int DraftInvoiceRequestCount { get; set; }
    public int SubmittedInvoiceRequestCount { get; set; }
    public int SentToFinanceInvoiceRequestCount { get; set; }
    public int InvoicedInvoiceRequestCount { get; set; }
    public int PaidInvoiceRequestCount { get; set; }
    public decimal ScheduledBillingAmount { get; set; }
    public decimal InvoiceRequestedAmount { get; set; }
    public decimal CollectedCashAmount { get; set; }
    public decimal UnbilledAmount { get; set; }
    public decimal BillingCoveragePercent { get; set; }
    public decimal RecognizedRevenue { get; set; }
    public decimal RevenueCoveragePercent { get; set; }
    public decimal RevenueGapAmount { get; set; }
    public decimal ActualCost { get; set; }
    public decimal MarginAmount { get; set; }
    public decimal MarginPercent { get; set; }
}

public class ProjectInvoiceRequestQueueItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid InvoiceRequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public Guid? BillingScheduleId { get; set; }
    public string? BillingScheduleName { get; set; }
    public DateTime? BillingDate { get; set; }
    public Guid? ContractId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ExternalReference { get; set; }
    public string? Notes { get; set; }
    public int DaysOutstanding { get; set; }
    public string QueueStage { get; set; } = string.Empty;
    public bool CanMarkInvoiced { get; set; }
    public bool CanMarkPaid { get; set; }
}

public class ProjectWorkflowApprovalQueueItemDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int DaysPending { get; set; }
    public string QueueStage { get; set; } = string.Empty;
}

public class ProjectExternalCollaborationReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool ExternalPortalAccessEnabled { get; set; }
    public bool ExternalCollaborationEnabled { get; set; }
    public int PolicyCount { get; set; }
    public int ExternalVisibleDocumentCount { get; set; }
    public int ExternalVisibleDeliverableCount { get; set; }
    public int PendingExternalSubmissionCount { get; set; }
    public int PendingExternalSignOffCount { get; set; }
    public int ExternalCommentCount { get; set; }
    public DateTime? LastExternalCommentAt { get; set; }
    public string CollaborationState { get; set; } = string.Empty;
}

public class ProjectContractLookupDto
{
    public Guid Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ContractTitle { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class ProjectTenderLookupDto
{
    public Guid Id { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public decimal? EstimatedValue { get; set; }
}

public class ProjectProcurementPlanItemLookupDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public decimal? EstimatedTotalCost { get; set; }
}

public class ProjectPurchaseRequisitionLookupDto
{
    public Guid Id { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public decimal TotalAmount { get; set; }
}

public class ProjectPurchaseOrderLookupDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
}

public class ProjectContractMilestoneLookupDto
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public string MilestoneName { get; set; } = string.Empty;
    public decimal PaymentPercentage { get; set; }
    public decimal PaymentAmount { get; set; }
    public DateTime? PlannedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
}

public class ProjectExternalSummaryDto
{
    public Guid Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }
    public decimal ProgressPercent { get; set; }
    public bool ExternalCollaborationEnabled { get; set; }
    public int OpenMilestoneCount { get; set; }
}

public class ProjectExternalDetailDto : ProjectExternalSummaryDto
{
    public string Methodology { get; set; } = "Hybrid";
    public string? StatusRemarks { get; set; }
    public bool CanCollaborate { get; set; }
    public bool CanComment { get; set; }
    public bool CanUploadDocuments { get; set; }
    public int ActionableWorkItemCount { get; set; }
    public int BlockedWorkItemCount { get; set; }
    public int PendingExternalSubmissionCount { get; set; }
    public int PendingExternalSignOffCount { get; set; }
    public List<ProjectWorkItemDto> WorkItems { get; set; } = new();
    public List<ProjectMilestoneDto> Milestones { get; set; } = new();
    public List<ProjectDeliverableDto> Deliverables { get; set; } = new();
    public List<ProjectDocumentDto> Documents { get; set; } = new();
    public List<ProjectCommentDto> Comments { get; set; } = new();
}

public class ProjectPerformanceAnalyticsReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal BudgetBaseline { get; set; }
    public decimal ProgressPercent { get; set; }
    public decimal EarnedValue { get; set; }
    public decimal PlannedValue { get; set; }
    public decimal ActualCost { get; set; }
    public decimal? CostPerformanceIndex { get; set; }
    public decimal EstimateAtCompletion { get; set; }
    public decimal EstimateToComplete { get; set; }
    public decimal ProjectedVariance { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
}

public class ProjectPortfolioPrioritizationReportItemDto
{
    public Guid ProjectId { get; set; }
    public Guid? PortfolioId { get; set; }
    public Guid? ProgramId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PortfolioName { get; set; }
    public string? ProgramName { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public decimal BudgetBaseline { get; set; }
    public decimal ActualCost { get; set; }
    public decimal ProjectedVariance { get; set; }
    public int OpenRiskCount { get; set; }
    public int OpenIssueCount { get; set; }
    public int OverdueMilestoneCount { get; set; }
    public decimal PriorityScore { get; set; }
    public string PriorityBand { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
}

public class ProjectDependencyWatchReportItemDto
{
    public Guid InterdependencyId { get; set; }
    public Guid SourceProjectId { get; set; }
    public Guid TargetProjectId { get; set; }
    public string SourceProjectCode { get; set; } = string.Empty;
    public string SourceProjectTitle { get; set; } = string.Empty;
    public string TargetProjectCode { get; set; } = string.Empty;
    public string TargetProjectTitle { get; set; } = string.Empty;
    public Guid? PortfolioId { get; set; }
    public string? PortfolioName { get; set; }
    public Guid? ProgramId { get; set; }
    public string? ProgramName { get; set; }
    public string DependencyType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ImpactLevel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysToDue { get; set; }
    public string CoordinationState { get; set; } = string.Empty;
}

public class ProjectStrategicInitiativeReportItemDto
{
    public string Initiative { get; set; } = string.Empty;
    public int ProjectCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public int AtRiskProjectCount { get; set; }
    public int DelayedProjectCount { get; set; }
    public int HighRiskItemCount { get; set; }
    public decimal TotalEstimatedBudget { get; set; }
    public decimal TotalActualCost { get; set; }
    public decimal AverageProgressPercent { get; set; }
    public List<string> PortfolioNames { get; set; } = new();
    public List<string> ProgramNames { get; set; } = new();
}

public class ProjectMaterialReconciliationReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RequisitionCount { get; set; }
    public int PendingRequisitionCount { get; set; }
    public int IssuedRequisitionCount { get; set; }
    public decimal RequestedValue { get; set; }
    public decimal IssuedValue { get; set; }
    public decimal ReturnedValue { get; set; }
    public decimal NetIssuedValue { get; set; }
    public decimal TrackedMaterialCost { get; set; }
    public decimal MaterialCostVariance { get; set; }
    public int MaterialLedgerEntryCount { get; set; }
    public int MissingSourceLinkCount { get; set; }
    public int ReversalGapCount { get; set; }
    public string ReconciliationStatus { get; set; } = string.Empty;
}

public class ProjectProcurementReconciliationReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int PurchaseRequisitionCount { get; set; }
    public int OpenPurchaseRequisitionCount { get; set; }
    public decimal PurchaseRequisitionAmount { get; set; }
    public int PurchaseOrderCount { get; set; }
    public int OpenPurchaseOrderCount { get; set; }
    public decimal PurchaseOrderAmount { get; set; }
    public int PurchaseReceiptCount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal AcceptedReceiptAmount { get; set; }
    public decimal PendingInspectionAmount { get; set; }
    public decimal SupplierReturnAmount { get; set; }
    public decimal IssuedInventoryValue { get; set; }
    public decimal NetIssuedInventoryValue { get; set; }
    public decimal PostedMaterialCost { get; set; }
    public decimal ReceiptToIssueVariance { get; set; }
    public decimal IssueToPostingVariance { get; set; }
    public int ProcurementLedgerEntryCount { get; set; }
    public int MissingSourceLinkCount { get; set; }
    public int ReversalGapCount { get; set; }
    public string ReconciliationStatus { get; set; } = string.Empty;
}

public class ProjectPhaseGateReadinessReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public Guid ProjectPhaseId { get; set; }
    public string ProjectPhaseCode { get; set; } = string.Empty;
    public string ProjectPhaseName { get; set; } = string.Empty;
    public int ProjectPhaseSortOrder { get; set; }
    public bool IsStageGateRequired { get; set; }
    public bool HasConfiguredRules { get; set; }
    public bool IsReady { get; set; }
    public int ConfiguredRuleCount { get; set; }
    public int BlockingRuleCount { get; set; }
    public int BlockingFailureCount { get; set; }
    public int SatisfiedRuleCount { get; set; }
    public string GateStatus { get; set; } = string.Empty;
    public string? TopBlockingMessage { get; set; }
}

public class ProjectApprovalWatchReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public Guid ApprovalRegisterItemId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public string ApprovalType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string WatchState { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? AuthorityName { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? TargetDecisionDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? DaysToTargetDecision { get; set; }
    public int? DaysToExpiry { get; set; }
}

public class ProjectCommercialAdministrationReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal ApprovedBudget { get; set; }
    public decimal PackageForecastAmount { get; set; }
    public decimal ForecastVarianceAmount { get; set; }
    public int PackageCount { get; set; }
    public int UnassignedPackageCount { get; set; }
    public int VariationOrderCount { get; set; }
    public decimal ApprovedVariationAmount { get; set; }
    public int InterimValuationCount { get; set; }
    public decimal NetValuationAmount { get; set; }
    public int PaymentCertificateCount { get; set; }
    public decimal NetCertifiedAmount { get; set; }
    public decimal RetentionHeldAmount { get; set; }
    public int ExtensionOfTimeCount { get; set; }
    public int ApprovedExtensionDays { get; set; }
    public string? FinalAccountStatus { get; set; }
    public int AlertCount { get; set; }
    public string WatchState { get; set; } = string.Empty;
    public string? TopAlertMessage { get; set; }
}

public class ProjectPostHandoverWatchReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public int OpenHandoverItemCount { get; set; }
    public int ActiveDefectLiabilityCount { get; set; }
    public int WarrantyCaseCount { get; set; }
    public int ChargeableCaseCount { get; set; }
    public int ResponseBreachCount { get; set; }
    public int ResolutionBreachCount { get; set; }
    public int WarrantyExpiringSoonCount { get; set; }
    public int AlertCount { get; set; }
    public string HighestSeverity { get; set; } = string.Empty;
    public string WatchState { get; set; } = string.Empty;
    public decimal TotalRectificationExposure { get; set; }
    public decimal ChargeableExposure { get; set; }
    public decimal WarrantyExposure { get; set; }
}

public class ProjectDesignControlReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? ProjectPackageName { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string WatchState { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime? ActionDueDate { get; set; }
    public int? DaysToActionDue { get; set; }
    public string? ResponsibleParty { get; set; }
    public bool IsAsBuilt { get; set; }
}

public class ProjectSiteControlReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public string? ProjectPhaseName { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? ProjectPackageName { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string WatchState { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime? ActionDueDate { get; set; }
    public int? DaysToActionDue { get; set; }
    public string? ResponsibleParty { get; set; }
    public decimal? EstimatedCostImpact { get; set; }
    public int? ScheduleImpactDays { get; set; }
}

public class ProjectUnitCommercializationReportItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public Guid ProjectUnitId { get; set; }
    public string? ProjectBuildingName { get; set; }
    public string? ProjectFloorName { get; set; }
    public string? ProjectUnitReleaseBatchName { get; set; }
    public string? UnitCode { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CommercialStatus { get; set; } = string.Empty;
    public string? CommercialIntent { get; set; }
    public string HandoverStatus { get; set; } = string.Empty;
    public bool IsReleasedForMarket { get; set; }
    public string ReleaseState { get; set; } = string.Empty;
    public string? CustomerBusinessPartnerName { get; set; }
    public string? SalesAgreementNumber { get; set; }
    public string? SalesOrderNumber { get; set; }
    public decimal? AreaSquareMeters { get; set; }
    public decimal? BasePrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string WatchState { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? WatchMessage { get; set; }
}

public class ProjectMaterialCostEntryDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public string PostingState { get; set; } = string.Empty;
    public bool AffectsActualCost { get; set; }
    public bool IsReversed { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public string? SourceDocumentNumber { get; set; }
    public string? SourceTransactionType { get; set; }
    public Guid? SourceTransactionId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public string? InventoryItemCode { get; set; }
    public string? InventoryItemName { get; set; }
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool HasMissingSourceLink { get; set; }
    public bool HasReversalGap { get; set; }
    public string? Notes { get; set; }
}

public class ProjectBudgetRevisionDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int VersionNumber { get; set; }
    public string RevisionName { get; set; } = string.Empty;
    public string RevisionType { get; set; } = string.Empty;
    public decimal EstimatedBudget { get; set; }
    public decimal ApprovedBudget { get; set; }
    public decimal CommittedCost { get; set; }
    public decimal ForecastCost { get; set; }
    public decimal ThresholdWarningPercent { get; set; }
    public decimal ThresholdCriticalPercent { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ChangeReason { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
}

public class CreateProjectBudgetRevisionDto
{
    public string RevisionName { get; set; } = string.Empty;
    public string RevisionType { get; set; } = "Revision";
    public decimal EstimatedBudget { get; set; }
    public decimal ApprovedBudget { get; set; }
    public decimal CommittedCost { get; set; }
    public decimal ForecastCost { get; set; }
    public decimal ThresholdWarningPercent { get; set; } = 75m;
    public decimal ThresholdCriticalPercent { get; set; } = 90m;
    public DateTime? EffectiveDate { get; set; }
    public string? ChangeReason { get; set; }
    public string? Notes { get; set; }
}

public class ProjectForecastVersionDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int VersionNumber { get; set; }
    public string VersionName { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }
    public decimal ForecastCost { get; set; }
    public decimal EstimateAtCompletion { get; set; }
    public decimal ForecastRevenue { get; set; }
    public decimal ForecastMargin { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class CreateProjectForecastVersionDto
{
    public string VersionName { get; set; } = string.Empty;
    public DateTime? AsOfDate { get; set; }
    public decimal ForecastCost { get; set; }
    public decimal EstimateAtCompletion { get; set; }
    public decimal ForecastRevenue { get; set; }
    public decimal ForecastMargin { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

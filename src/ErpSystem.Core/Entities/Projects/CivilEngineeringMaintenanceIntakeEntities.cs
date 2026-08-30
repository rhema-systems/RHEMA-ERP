using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;

namespace ErpSystem.Core.Entities.Projects;

/// <summary>
/// Civil Engineering's governed intake overlay for existing Maintenance schedules,
/// Estate buildings/properties and Helpdesk complaint tickets.  It does not own
/// work execution, ticket resolution, DMS bytes or financial postings.
/// </summary>
[Table("CivilEngineeringMaintenanceIntakes")]
public sealed class CivilEngineeringMaintenanceIntake : TenantEntity
{
    [Required, StringLength(40)] public string IntakeNumber { get; set; } = string.Empty;
    public CivilEngineeringWorkClassification WorkClassification { get; set; }
    public CivilEngineeringRequestSource Source { get; set; }
    public CivilEngineeringUrgency Urgency { get; set; }
    [Required, StringLength(300)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Description { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public Guid? MaintenanceAssetId { get; set; }
    public Guid? EstateManagedAssetId { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
    public Guid? HelpdeskTicketId { get; set; }
    public Guid RequesterUserId { get; set; }
    public Guid PriorityLevelId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(80)] public string EvidenceMetadataTemplateCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Status { get; set; } = CivilEngineeringMaintenanceIntakeStatuses.Logged;
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project? Project { get; set; }
    public MaintenanceAsset? MaintenanceAsset { get; set; }
    public EstateManagedAsset? EstateManagedAsset { get; set; }
    public MaintenanceSchedule? MaintenanceSchedule { get; set; }
    public EhcTicket? HelpdeskTicket { get; set; }
    public ApplicationUser RequesterUser { get; set; } = null!;
    public PriorityLevel PriorityLevel { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public CentralDocumentMetadataTemplate EvidenceMetadataTemplate { get; set; } = null!;
    public ICollection<CivilEngineeringMaintenanceIntakeRevision> Revisions { get; set; } = [];
}

public static class CivilEngineeringMaintenanceIntakeStatuses
{
    public const string Logged = "Logged";
    public const string AssessmentInProgress = "AssessmentInProgress";
    public const string AssessmentReturned = "AssessmentReturned";
    public const string Assessed = "Assessed";
}

[Table("CivilEngineeringMaintenanceIntakeRevisions")]
public sealed class CivilEngineeringMaintenanceIntakeRevision : TenantEntity
{
    public Guid IntakeId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public CivilEngineeringMaintenanceIntake Intake { get; set; } = null!;
}

/// <summary>
/// Civil Engineering's assessment and remedy recommendation for a governed maintenance intake.
/// The assessment retains the intake's frozen configuration and uses the central Workflow and
/// DMS owners; it does not create a Maintenance job card or work order.
/// </summary>
[Table("CivilEngineeringMaintenanceAssessments")]
public sealed class CivilEngineeringMaintenanceAssessment : TenantEntity, ICivilEngineeringWorkflowRecord
{
    public Guid IntakeId { get; set; }
    public Guid HodUserId { get; set; }
    public Guid SupervisingCivilEngineerUserId { get; set; }
    public Guid? CivilEngineerUserId { get; set; }
    public Guid? CurrentAssigneeUserId { get; set; }
    public DateTime? CurrentDueAt { get; set; }
    public Guid? DefectCategoryId { get; set; }
    [StringLength(4000)] public string? SiteAssessment { get; set; }
    [StringLength(4000)] public string? ScopeRecommendation { get; set; }
    [StringLength(4000)] public string? RemedyRecommendation { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? EstimatedCost { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Stage { get; set; } = CivilEngineeringMaintenanceAssessmentStages.SceAssignment;
    [Required, StringLength(30)] public string Status { get; set; } = CivilEngineeringMaintenanceAssessmentStatuses.InProgress;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = CivilEngineeringMaintenanceAssessmentApprovalStatuses.Draft;
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public CivilEngineeringMaintenanceIntake Intake { get; set; } = null!;
    public ApplicationUser HodUser { get; set; } = null!;
    public ApplicationUser SupervisingCivilEngineerUser { get; set; } = null!;
    public ApplicationUser? CivilEngineerUser { get; set; }
    public ApplicationUser? CurrentAssigneeUser { get; set; }
    public ProjectCatalogEntry? DefectCategory { get; set; }
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
    public ICollection<CivilEngineeringMaintenanceAssessmentRevision> Revisions { get; set; } = [];
}

public static class CivilEngineeringMaintenanceAssessmentStages
{
    public const string SceAssignment = "SceAssignment";
    public const string CivilEngineerAssessment = "CivilEngineerAssessment";
    public const string SceAssessmentReview = "SceAssessmentReview";
    public const string HodFinalReview = "HodFinalReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class CivilEngineeringMaintenanceAssessmentStatuses
{
    public const string InProgress = "InProgress";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class CivilEngineeringMaintenanceAssessmentApprovalStatuses
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

[Table("CivilEngineeringMaintenanceAssessmentRevisions")]
public sealed class CivilEngineeringMaintenanceAssessmentRevision : TenantEntity
{
    public Guid AssessmentId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [StringLength(40)] public string? FromStage { get; set; }
    [Required, StringLength(40)] public string ToStage { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public CivilEngineeringMaintenanceAssessment Assessment { get; set; } = null!;
}

/// <summary>
/// Civil Engineering's auditable handoff record for an approved maintenance scope. It stores
/// only controlled references and status projections; QS, Projects/Finance and Procurement
/// remain authoritative for estimates, budgets, requisitions, contracts and awards.
/// </summary>
[Table("CivilEngineeringMaintenanceCostingHandoffs")]
public sealed class CivilEngineeringMaintenanceCostingHandoff : TenantEntity, ICivilEngineeringWorkflowRecord
{
    public Guid AssessmentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid QuantitySurveyEstimateVersionId { get; set; }
    public Guid? ProjectBudgetRevisionId { get; set; }
    public Guid? PurchaseRequisitionId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid CostingWorkflowDefinitionId { get; set; }
    public Guid ContractorEngagementWorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Stage { get; set; } = CivilEngineeringMaintenanceCostingHandoffStages.Draft;
    [Required, StringLength(30)] public string Status { get; set; } = CivilEngineeringMaintenanceCostingHandoffStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = CivilEngineeringMaintenanceCostingHandoffApprovalStatuses.Draft;
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public DateTime? LastRevalidatedAt { get; set; }
    [StringLength(1000)] public string? LastRevalidationSummary { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public CivilEngineeringMaintenanceAssessment Assessment { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public QuantitySurveyEstimateVersion QuantitySurveyEstimateVersion { get; set; } = null!;
    public ProjectBudgetRevision? ProjectBudgetRevision { get; set; }
    public PurchaseRequisition? PurchaseRequisition { get; set; }
    public Contract? Contract { get; set; }
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public ICollection<CivilEngineeringMaintenanceCostingHandoffRevision> Revisions { get; set; } = [];
}

public static class CivilEngineeringMaintenanceCostingHandoffStages
{
    public const string Draft = "Draft";
    public const string CostingReview = "CostingReview";
    public const string ProcurementAndAward = "ProcurementAndAward";
    public const string Awarded = "Awarded";
    public const string Rejected = "Rejected";
}

public static class CivilEngineeringMaintenanceCostingHandoffStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Awarded = "Awarded";
    public const string Rejected = "Rejected";
}

public static class CivilEngineeringMaintenanceCostingHandoffApprovalStatuses
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

[Table("CivilEngineeringMaintenanceCostingHandoffRevisions")]
public sealed class CivilEngineeringMaintenanceCostingHandoffRevision : TenantEntity
{
    public Guid HandoffId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [StringLength(40)] public string? FromStage { get; set; }
    [Required, StringLength(40)] public string ToStage { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public CivilEngineeringMaintenanceCostingHandoff Handoff { get; set; } = null!;
}

/// <summary>
/// A Civil Engineering lineage envelope around the authoritative Maintenance job-card/work-order
/// records. It never owns or changes the Maintenance lifecycle; it only preserves the approved
/// Civil scope and controlled project/asset connection used to start or follow that lifecycle.
/// </summary>
[Table("CivilEngineeringMaintenanceExecutionLinks")]
public sealed class CivilEngineeringMaintenanceExecutionLink : TenantEntity
{
    public Guid HandoffId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid MaintenanceAssetId { get; set; }
    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }
    [Required, StringLength(30)] public string LinkMode { get; set; } = CivilEngineeringMaintenanceExecutionLinkModes.CreateJobCard;
    [Required, StringLength(40)] public string Stage { get; set; } = CivilEngineeringMaintenanceExecutionLinkStages.AwaitingJobCardApproval;
    [Required, StringLength(30)] public string Status { get; set; } = CivilEngineeringMaintenanceExecutionLinkStatuses.Pending;
    [StringLength(1000)] public string? LastOwnerStatusSummary { get; set; }
    public DateTime? LastRevalidatedAt { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public CivilEngineeringMaintenanceCostingHandoff Handoff { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public MaintenanceAsset MaintenanceAsset { get; set; } = null!;
    public JobCard? JobCard { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public ICollection<CivilEngineeringMaintenanceExecutionLinkRevision> Revisions { get; set; } = [];
}

public static class CivilEngineeringMaintenanceExecutionLinkModes
{
    public const string CreateJobCard = "CreateJobCard";
    public const string LinkExisting = "LinkExisting";
}

public static class CivilEngineeringMaintenanceExecutionLinkStages
{
    public const string AwaitingJobCardApproval = "AwaitingJobCardApproval";
    public const string AwaitingWorkOrder = "AwaitingWorkOrder";
    public const string WorkInProgress = "WorkInProgress";
    public const string Completed = "Completed";
    public const string Blocked = "Blocked";
}

public static class CivilEngineeringMaintenanceExecutionLinkStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string Blocked = "Blocked";
}

[Table("CivilEngineeringMaintenanceExecutionLinkRevisions")]
public sealed class CivilEngineeringMaintenanceExecutionLinkRevision : TenantEntity
{
    public Guid ExecutionLinkId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [StringLength(40)] public string? FromStage { get; set; }
    [Required, StringLength(40)] public string ToStage { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public CivilEngineeringMaintenanceExecutionLink ExecutionLink { get; set; } = null!;
}

/// <summary>
/// Civil's completion and direction envelope for an authoritative Maintenance execution link.
/// It deliberately stores no Maintenance work-order state, inspection record or Finance posting.
/// </summary>
[Table("CivilEngineeringMaintenanceCompletionControls")]
public sealed class CivilEngineeringMaintenanceCompletionControl : TenantEntity
{
    public Guid ExecutionLinkId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid MaintenanceAssetId { get; set; }
    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid CivilEngineerUserId { get; set; }
    public Guid SupervisingCivilEngineerUserId { get; set; }
    public Guid HodUserId { get; set; }
    [Required, StringLength(40)] public string Stage { get; set; } = CivilEngineeringMaintenanceCompletionStages.SceReview;
    [Required, StringLength(30)] public string Status { get; set; } = CivilEngineeringMaintenanceCompletionStatuses.Pending;
    [Required, StringLength(4000)] public string CompletionSummary { get; set; } = string.Empty;
    public Guid CompletionDocumentRecordId { get; set; }
    public Guid CompletionDocumentVersionId { get; set; }
    public DateTime CompletionReportedAt { get; set; }
    public Guid? SceReviewedById { get; set; }
    public DateTime? SceReviewedAt { get; set; }
    public Guid? HodReviewedById { get; set; }
    public DateTime? HodReviewedAt { get; set; }
    [Required, StringLength(30)] public string InspectionStatus { get; set; } = CivilEngineeringMaintenanceInspectionStatuses.NotDirected;
    public Guid? InspectionDirectionDocumentRecordId { get; set; }
    public Guid? InspectionDirectionDocumentVersionId { get; set; }
    public Guid? InspectionOutcomeDocumentRecordId { get; set; }
    public Guid? InspectionOutcomeDocumentVersionId { get; set; }
    [StringLength(2000)] public string? InspectionOutcomeNote { get; set; }
    public Guid? InspectionRecordedById { get; set; }
    public DateTime? InspectionRecordedAt { get; set; }
    [Required, StringLength(30)] public string PaymentDirectionStatus { get; set; } = CivilEngineeringMaintenancePaymentDirectionStatuses.NotDirected;
    public Guid? PaymentDirectionDocumentRecordId { get; set; }
    public Guid? PaymentDirectionDocumentVersionId { get; set; }
    [StringLength(2000)] public string? PaymentDirectionNote { get; set; }
    public Guid? PaymentDirectedById { get; set; }
    public DateTime? PaymentDirectedAt { get; set; }
    public Guid? ClosureDocumentRecordId { get; set; }
    public Guid? ClosureDocumentVersionId { get; set; }
    public Guid? ClosedById { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public CivilEngineeringMaintenanceExecutionLink ExecutionLink { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public MaintenanceAsset MaintenanceAsset { get; set; } = null!;
    public JobCard? JobCard { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public ApplicationUser CivilEngineerUser { get; set; } = null!;
    public ApplicationUser SupervisingCivilEngineerUser { get; set; } = null!;
    public ApplicationUser HodUser { get; set; } = null!;
    public ICollection<CivilEngineeringMaintenanceCompletionRevision> Revisions { get; set; } = [];
}

public static class CivilEngineeringMaintenanceCompletionStages
{
    public const string SceReview = "SceReview";
    public const string HodReview = "HodReview";
    public const string AwaitingInspectionDirection = "AwaitingInspectionDirection";
    public const string InspectionInProgress = "InspectionInProgress";
    public const string AwaitingPaymentDirection = "AwaitingPaymentDirection";
    public const string AwaitingClosure = "AwaitingClosure";
    public const string RemediationRequired = "RemediationRequired";
    public const string Closed = "Closed";
    public const string Returned = "Returned";
}

public static class CivilEngineeringMaintenanceCompletionStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Returned = "Returned";
    public const string Blocked = "Blocked";
    public const string Closed = "Closed";
}

public static class CivilEngineeringMaintenanceInspectionStatuses
{
    public const string NotDirected = "NotDirected";
    public const string Directed = "Directed";
    public const string Passed = "Passed";
    public const string Failed = "Failed";
}

public static class CivilEngineeringMaintenancePaymentDirectionStatuses
{
    public const string NotDirected = "NotDirected";
    public const string Directed = "Directed";
}

[Table("CivilEngineeringMaintenanceCompletionRevisions")]
public sealed class CivilEngineeringMaintenanceCompletionRevision : TenantEntity
{
    public Guid CompletionControlId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [StringLength(40)] public string? FromStage { get; set; }
    [Required, StringLength(40)] public string ToStage { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public CivilEngineeringMaintenanceCompletionControl CompletionControl { get; set; } = null!;
}

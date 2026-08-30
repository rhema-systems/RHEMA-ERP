using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringMaintenanceIntakeLookupOptionDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringMaintenanceIntakeDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringMaintenanceIntakeLookupsDto
{
    public IReadOnlyList<CivilEngineeringRequestSource> Sources { get; init; } = [];
    public IReadOnlyList<CivilEngineeringUrgency> Urgencies { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWorkClassification> WorkClassifications { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto> Projects { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto> MaintenanceAssets { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto> BuildingsOrProperties { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto> MaintenanceSchedules { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto> ComplaintTickets { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto> Requesters { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto> Priorities { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeDocumentLookupDto> Documents { get; init; } = [];
}

public sealed class CivilEngineeringMaintenanceIntakeDto
{
    public Guid Id { get; init; }
    public string IntakeNumber { get; init; } = string.Empty;
    public CivilEngineeringWorkClassification WorkClassification { get; init; }
    public CivilEngineeringRequestSource Source { get; init; }
    public CivilEngineeringUrgency Urgency { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public Guid? MaintenanceAssetId { get; init; }
    public string? MaintenanceAssetName { get; init; }
    public Guid? EstateManagedAssetId { get; init; }
    public string? BuildingOrPropertyName { get; init; }
    public Guid? MaintenanceScheduleId { get; init; }
    public string? MaintenanceScheduleName { get; init; }
    public Guid? HelpdeskTicketId { get; init; }
    public string? ComplaintTicketNumber { get; init; }
    public Guid RequesterUserId { get; init; }
    public string RequesterName { get; init; } = string.Empty;
    public Guid PriorityLevelId { get; init; }
    public string PriorityName { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string? EvidenceReference { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid WorkflowDefinitionId { get; init; }
    public DateTime CreatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CreateCivilEngineeringMaintenanceIntakeRequest
{
    public Guid ClientRequestId { get; set; }
    public CivilEngineeringWorkClassification WorkClassification { get; set; }
    public CivilEngineeringRequestSource Source { get; set; }
    public CivilEngineeringUrgency Urgency { get; set; }
    [Required, StringLength(300, MinimumLength = 3)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 3)] public string Description { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public Guid? MaintenanceAssetId { get; set; }
    public Guid? EstateManagedAssetId { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
    public Guid? HelpdeskTicketId { get; set; }
    public Guid RequesterUserId { get; set; }
    public Guid PriorityLevelId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

public sealed class CivilEngineeringMaintenanceIntakeRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class CivilEngineeringMaintenanceAssessmentLookupsDto
{
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto> SupervisingCivilEngineers { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto> CivilEngineers { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeLookupOptionDto> DefectCategories { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeDocumentLookupDto> Documents { get; init; } = [];
}

public sealed class CivilEngineeringMaintenanceAssessmentDto
{
    public Guid Id { get; init; }
    public Guid IntakeId { get; init; }
    public string IntakeNumber { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid HodUserId { get; init; }
    public string HodName { get; init; } = string.Empty;
    public Guid SupervisingCivilEngineerUserId { get; init; }
    public string SupervisingCivilEngineerName { get; init; } = string.Empty;
    public Guid? CivilEngineerUserId { get; init; }
    public string? CivilEngineerName { get; init; }
    public Guid? CurrentAssigneeUserId { get; init; }
    public DateTime? CurrentDueAt { get; init; }
    public Guid? DefectCategoryId { get; init; }
    public string? DefectCategoryLabel { get; init; }
    public string? SiteAssessment { get; init; }
    public string? ScopeRecommendation { get; init; }
    public string? RemedyRecommendation { get; init; }
    public decimal? EstimatedCost { get; init; }
    public Guid? CentralDocumentRecordId { get; init; }
    public Guid? CentralDocumentVersionId { get; init; }
    public string? EvidenceReference { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class StartCivilEngineeringMaintenanceAssessmentRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid SupervisingCivilEngineerUserId { get; set; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Direction { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }
}

public enum CivilEngineeringMaintenanceAssessmentAction
{
    AssignCivilEngineer = 0,
    SubmitAssessment = 1,
    ReturnAssessment = 2,
    SubmitToHod = 3,
    Approve = 4,
    Reject = 5
}

public sealed class CivilEngineeringMaintenanceAssessmentTransitionRequest
{
    public Guid ClientRequestId { get; set; }
    public CivilEngineeringMaintenanceAssessmentAction Action { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public Guid? DefectCategoryId { get; set; }
    [StringLength(4000)] public string? SiteAssessment { get; set; }
    [StringLength(4000)] public string? ScopeRecommendation { get; set; }
    [StringLength(4000)] public string? RemedyRecommendation { get; set; }
    [Range(0, double.MaxValue)] public decimal? EstimatedCost { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    public DateTime? DueAt { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CivilEngineeringMaintenanceAssessmentRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? FromStage { get; init; }
    public string ToStage { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string? Reason { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class CivilEngineeringMaintenanceCostingHandoffLookupOptionDto
{
    public Guid Id { get; init; }
    public Guid? ProjectId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class CivilEngineeringMaintenanceCostingHandoffLookupsDto
{
    public IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffLookupOptionDto> ApprovedAssessments { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffLookupOptionDto> Projects { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffLookupOptionDto> ApprovedEstimates { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffLookupOptionDto> ApprovedProjectBudgets { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffLookupOptionDto> PurchaseRequisitions { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffLookupOptionDto> Contracts { get; init; } = [];
}

public sealed class CivilEngineeringMaintenanceCostingHandoffDto
{
    public Guid Id { get; init; }
    public Guid AssessmentId { get; init; }
    public string IntakeNumber { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public string ProjectLabel { get; init; } = string.Empty;
    public Guid QuantitySurveyEstimateVersionId { get; init; }
    public string EstimateLabel { get; init; } = string.Empty;
    public decimal EstimateAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public Guid? ProjectBudgetRevisionId { get; init; }
    public string? ProjectBudgetLabel { get; init; }
    public Guid? PurchaseRequisitionId { get; init; }
    public string? PurchaseRequisitionLabel { get; init; }
    public Guid? ContractId { get; init; }
    public string? ContractLabel { get; init; }
    public string Stage { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid? WorkflowInstanceId { get; init; }
    public DateTime? LastRevalidatedAt { get; init; }
    public string? LastRevalidationSummary { get; init; }
    public string? RejectionReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CreateCivilEngineeringMaintenanceCostingHandoffRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid QuantitySurveyEstimateVersionId { get; set; }
    public Guid? ProjectBudgetRevisionId { get; set; }
    public Guid? PurchaseRequisitionId { get; set; }
    public Guid? ContractId { get; set; }
}

public enum CivilEngineeringMaintenanceCostingHandoffAction
{
    SubmitCosting = 0,
    ApproveCosting = 1,
    RejectCosting = 2,
    RefreshAuthoritativeStatus = 3
}

public sealed class CivilEngineeringMaintenanceCostingHandoffActionRequest
{
    public Guid ClientRequestId { get; set; }
    public CivilEngineeringMaintenanceCostingHandoffAction Action { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CivilEngineeringMaintenanceCostingHandoffRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? FromStage { get; init; }
    public string ToStage { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string? Reason { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class CivilEngineeringMaintenanceExecutionLookupOptionDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? MaintenanceAssetId { get; init; }
}

public sealed class CivilEngineeringMaintenanceExecutionLookupsDto
{
    public IReadOnlyList<CivilEngineeringMaintenanceExecutionLookupOptionDto> AwardedHandoffs { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceExecutionLookupOptionDto> MaintenanceTypes { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceExecutionLookupOptionDto> PriorityLevels { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceExecutionLookupOptionDto> JobCards { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceExecutionLookupOptionDto> WorkOrders { get; init; } = [];
}

public sealed class CivilEngineeringMaintenanceExecutionLinkDto
{
    public Guid Id { get; init; }
    public Guid HandoffId { get; init; }
    public string IntakeNumber { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public string ProjectLabel { get; init; } = string.Empty;
    public Guid MaintenanceAssetId { get; init; }
    public string MaintenanceAssetLabel { get; init; } = string.Empty;
    public Guid? JobCardId { get; init; }
    public string? JobCardNumber { get; init; }
    public string? JobCardStatus { get; init; }
    public Guid? WorkOrderId { get; init; }
    public string? WorkOrderNumber { get; init; }
    public string? WorkOrderStatus { get; init; }
    public string LinkMode { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? LastOwnerStatusSummary { get; init; }
    public DateTime? LastRevalidatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CreateCivilEngineeringMaintenanceExecutionLinkRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid HandoffId { get; set; }
    [Required] public string LinkMode { get; set; } = string.Empty;
    public Guid? MaintenanceTypeId { get; set; }
    public Guid? PriorityLevelId { get; set; }
    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }
}

public sealed class RefreshCivilEngineeringMaintenanceExecutionLinkRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CivilEngineeringMaintenanceExecutionLinkRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? FromStage { get; init; }
    public string ToStage { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string? Reason { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class CivilEngineeringMaintenanceCompletionLookupsDto
{
    public IReadOnlyList<CivilEngineeringMaintenanceExecutionLookupOptionDto> CompletedExecutionLinks { get; init; } = [];
    public IReadOnlyList<CivilEngineeringMaintenanceIntakeDocumentLookupDto> Documents { get; init; } = [];
    public bool RequireInspectionBeforeClosure { get; init; }
    public bool RequireClosureEvidence { get; init; }
}

public sealed class CivilEngineeringMaintenanceCompletionControlDto
{
    public Guid Id { get; init; }
    public Guid ExecutionLinkId { get; init; }
    public string IntakeNumber { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public string ProjectLabel { get; init; } = string.Empty;
    public Guid MaintenanceAssetId { get; init; }
    public string MaintenanceAssetLabel { get; init; } = string.Empty;
    public Guid? JobCardId { get; init; }
    public string? JobCardNumber { get; init; }
    public Guid? WorkOrderId { get; init; }
    public string? WorkOrderNumber { get; init; }
    public string? WorkOrderStatus { get; init; }
    public Guid CivilEngineerUserId { get; init; }
    public string CivilEngineerName { get; init; } = string.Empty;
    public Guid SupervisingCivilEngineerUserId { get; init; }
    public string SupervisingCivilEngineerName { get; init; } = string.Empty;
    public Guid HodUserId { get; init; }
    public string HodName { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CompletionSummary { get; init; } = string.Empty;
    public Guid CompletionDocumentRecordId { get; init; }
    public Guid CompletionDocumentVersionId { get; init; }
    public string CompletionEvidenceReference { get; init; } = string.Empty;
    public DateTime CompletionReportedAt { get; init; }
    public string InspectionStatus { get; init; } = string.Empty;
    public string PaymentDirectionStatus { get; init; } = string.Empty;
    public DateTime? ClosedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CreateCivilEngineeringMaintenanceCompletionControlRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid ExecutionLinkId { get; set; }
    [Required, StringLength(4000, MinimumLength = 3)] public string CompletionSummary { get; set; } = string.Empty;
    public Guid CompletionDocumentRecordId { get; set; }
    public Guid CompletionDocumentVersionId { get; set; }
}

public enum CivilEngineeringMaintenanceCompletionAction
{
    SubmitToHod = 1,
    ReturnToCivilEngineer = 2,
    ApproveCompletion = 3,
    DirectInspection = 4,
    RecordInspectionPassed = 5,
    RecordInspectionFailed = 6,
    DirectPayment = 7,
    Close = 8
}

public sealed class ProcessCivilEngineeringMaintenanceCompletionControlRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public CivilEngineeringMaintenanceCompletionAction Action { get; set; }
    [StringLength(2000)] public string? Note { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
}

public sealed class CivilEngineeringMaintenanceCompletionRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? FromStage { get; init; }
    public string ToStage { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string? Reason { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

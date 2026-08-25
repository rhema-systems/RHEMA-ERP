using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementSourcingCaseSearchRequest
{
    public string? Search { get; set; }
    public ProcurementSourcingCaseStatus? Status { get; set; }
    public ProcurementMethodType? Method { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class ProcurementSourcingCaseSummaryDto
{
    public int TotalCount { get; set; }
    public int ReadyCount { get; set; }
    public int InProgressCount { get; set; }
    public int ClosedCount { get; set; }
    public int CancelledCount { get; set; }
    public int StaleCount { get; set; }
}

public sealed class ProcurementSourcingCasePageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementSourcingCaseDto> Items { get; set; } = new();
}

public sealed class ProcurementSourcingCaseSourceOptionDto
{
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string RequisitionStatus { get; set; } = string.Empty;
    public Guid SourcingReleaseId { get; set; }
    public string ReleaseReference { get; set; } = string.Empty;
    public Guid? SourcePlanId { get; set; }
    public Guid? SourcePlanItemId { get; set; }
    public string? SourcePlanNumber { get; set; }
    public string? SourcePlanItemDescription { get; set; }
    public ProcurementCategoryClass Category { get; set; }
    public decimal EstimatedValue { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public Guid? CurrentCaseId { get; set; }
    public string? CurrentCaseNumber { get; set; }
}

public sealed class ProcurementSourcingCaseLineOptionDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal EstimatedUnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class ProcurementSourcingCaseReadinessDto
{
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public bool IsReleaseCurrent { get; set; }
    public bool IsMethodCompliant { get; set; }
    public bool CanCreate { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ProcurementMethodType? RequestedMethod { get; set; }
    public ProcurementMethodType? RecommendedMethod { get; set; }
    public ProcurementMethodType? SelectedMethod { get; set; }
    public ProcurementMethodType? SuggestedMethod { get; set; }
    public ProcurementSourcingMethodSelectionBasis MethodSelectionBasis { get; set; }
    public ProcurementSourcingMethodOverrideReadinessDto? Override { get; set; }
    public PurchaseRequisitionSourcingReleaseDto? CurrentRelease { get; set; }
    public ProcurementSourcingCaseDto? CurrentCase { get; set; }
    public List<ProcurementComplianceMethodCandidateDto> MethodCandidates { get; set; } = new();
    public List<ProcurementComplianceFindingDto> HardStops { get; set; } = new();
    public List<ProcurementComplianceFindingDto> ReviewRequirements { get; set; } = new();
    public List<ProcurementSourcingCaseLineOptionDto> Lines { get; set; } = new();
}

public sealed class CreateProcurementSourcingCaseLotRequest
{
    [StringLength(50)] public string? LotCode { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [MinLength(1)] public List<Guid> PurchaseRequisitionItemIds { get; set; } = new();
}

public sealed class CreateProcurementSourcingCaseRequest
{
    public Guid RequisitionId { get; set; }
    public ProcurementMethodType? SelectedMethod { get; set; }
    [StringLength(1000)] public string? Justification { get; set; }
    [StringLength(1000, MinimumLength = 5)] public string? MethodOverrideReason { get; set; }
    [MinLength(1)] public List<CreateProcurementSourcingCaseLotRequest> Lots { get; set; } = new();
}

public sealed class ProcurementSourcingMethodOverrideReadinessDto
{
    public bool IsRequested { get; set; }
    public bool IsEligible { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? RuleId { get; set; }
    public string? RuleCode { get; set; }
    public string? RuleName { get; set; }
    public string? ExceptionType { get; set; }
    public string? ApproverRole { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? ApprovalReference { get; set; }
    public string? EvidenceReference { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public List<Guid> ApprovalActorUserIds { get; set; } = new();
}

public sealed class ProcurementSourcingCaseActionRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(500, MinimumLength = 5)] public string Reason { get; set; } = string.Empty;
}

public sealed class ProcurementSourcingCaseLotItemDto
{
    public Guid RequisitionItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal EstimatedUnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class ProcurementSourcingCaseLotDto
{
    public Guid Id { get; set; }
    public int LotNumber { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal EstimatedValue { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public List<ProcurementSourcingCaseLotItemDto> Items { get; set; } = new();
}

public sealed class ProcurementSourcingCaseSourceRequestDto
{
    public Guid Id { get; set; }
    public int RequestSequence { get; set; }
    public string RequestReference { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public ProcurementMethodType Method { get; set; }
    public ProcurementSourcingCaseSourceRequestStatus Status { get; set; }
    public List<Guid> LotIds { get; set; } = new();
    public DateTime PlannedAtUtc { get; set; }
    public Guid? SourceEntityId { get; set; }
    public string? SourceEntityReference { get; set; }
    public DateTime? RegisteredAtUtc { get; set; }
    public Guid? RegisteredById { get; set; }
    public string? RegisteredByName { get; set; }
}

public sealed class ProcurementSourcingCaseDto
{
    public Guid Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public int CaseSequence { get; set; }
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public Guid SourcingReleaseId { get; set; }
    public string SourcingReleaseReference { get; set; } = string.Empty;
    public Guid? SourcePlanId { get; set; }
    public Guid? SourcePlanItemId { get; set; }
    public string? SourcePlanNumber { get; set; }
    public string? SourcePlanItemDescription { get; set; }
    public ProcurementCategoryClass Category { get; set; }
    public ProcurementMethodType RecommendedMethod { get; set; }
    public ProcurementMethodType SelectedMethod { get; set; }
    public ProcurementSourcingMethodSelectionBasis MethodSelectionBasis { get; set; }
    public decimal EstimatedValue { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public Guid PolicySetId { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public int PolicyVersion { get; set; }
    public Guid MethodRuleId { get; set; }
    public string MethodRuleCode { get; set; } = string.Empty;
    public Guid ThresholdRuleId { get; set; }
    public string ThresholdRuleCode { get; set; } = string.Empty;
    public Guid? AuthorityRouteId { get; set; }
    public string? AuthorityRouteReference { get; set; }
    public Guid? ApprovedExceptionRuleId { get; set; }
    public Guid? MethodOverrideWorkflowInstanceId { get; set; }
    public string? MethodOverrideReason { get; set; }
    public List<Guid> MethodOverrideApprovalActorUserIds { get; set; } = new();
    public DateTime? MethodOverrideApprovedAtUtc { get; set; }
    public string? ExceptionApprovalReference { get; set; }
    public string? ExceptionEvidenceReference { get; set; }
    public string Justification { get; set; } = string.Empty;
    public ProcurementSourcingCaseStatus Status { get; set; }
    public bool IsSourceCurrent { get; set; }
    public string SourceStateCode { get; set; } = string.Empty;
    public string SourceStateMessage { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? StartedAtUtc { get; set; }
    public string? StartedByName { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedByName { get; set; }
    public string? ClosureReason { get; set; }
    public string SourceControlFingerprint { get; set; } = string.Empty;
    public string CaseFingerprint { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementSourcingCaseLotDto> Lots { get; set; } = new();
    public List<ProcurementSourcingCaseSourceRequestDto> SourceRequests { get; set; } = new();
}

public sealed class ProcurementSourcingCaseEntryGateDto
{
    public Guid? SourcingCaseId { get; set; }
    public string SourcingCaseNumber { get; set; } = string.Empty;
    public Guid SourcingReleaseId { get; set; }
    public Guid? SourcePlanItemId { get; set; }
    public ProcurementMethodType SelectedMethod { get; set; }
    public Guid? MethodRuleId { get; set; }
    public string MethodRuleCode { get; set; } = string.Empty;
    public int MinimumQuotationCount { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public decimal EstimatedValue { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}

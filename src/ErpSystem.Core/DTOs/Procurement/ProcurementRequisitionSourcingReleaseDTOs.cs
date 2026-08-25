using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ReleasePurchaseRequisitionForSourcingRequest
{
    [Required, StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class PurchaseRequisitionSourcingRequirementDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Satisfied { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? EvidenceReference { get; set; }
}

public sealed class PurchaseRequisitionSourcingReleaseDto
{
    public Guid Id { get; set; }
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public string ReleaseReference { get; set; } = string.Empty;
    public Guid? SourcePlanId { get; set; }
    public Guid? SourcePlanItemId { get; set; }
    public Guid? AppSubmissionId { get; set; }
    public int? AppSubmissionAttemptNumber { get; set; }
    public string? AppAcknowledgementReference { get; set; }
    public Guid? ApprovedExceptionRuleId { get; set; }
    public string? ExceptionApprovalReference { get; set; }
    public Guid? SpecificationTemplateId { get; set; }
    public string? SpecificationTemplateCode { get; set; }
    public int? SpecificationTemplateVersion { get; set; }
    public Guid? BudgetCommitmentId { get; set; }
    public string? BudgetCommitmentReference { get; set; }
    public Guid? AuthorityRouteId { get; set; }
    public string? AuthorityRouteReference { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime ReleasedAtUtc { get; set; }
    public Guid ReleasedById { get; set; }
    public string ReleasedByName { get; set; } = string.Empty;
    public string ReleaseReason { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string ControlFingerprint { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class PurchaseRequisitionSourcingReadinessDto
{
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
    public bool CanRelease { get; set; }
    public bool IsReleased { get; set; }
    public bool HasStaleRelease { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; }
    public string ControlFingerprint { get; set; } = string.Empty;
    public Guid? SourcePlanId { get; set; }
    public Guid? SourcePlanItemId { get; set; }
    public Guid? AppSubmissionId { get; set; }
    public int? AppSubmissionAttemptNumber { get; set; }
    public string? AppAcknowledgementReference { get; set; }
    public Guid? ApprovedExceptionRuleId { get; set; }
    public Guid? ExceptionWorkflowInstanceId { get; set; }
    public string? ExceptionApprovalReference { get; set; }
    public Guid? SpecificationTemplateId { get; set; }
    public string? SpecificationTemplateCode { get; set; }
    public int? SpecificationTemplateVersion { get; set; }
    public Guid? BudgetCommitmentId { get; set; }
    public string? BudgetCommitmentReference { get; set; }
    public Guid? AuthorityRouteId { get; set; }
    public string? AuthorityRouteReference { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public PurchaseRequisitionSourcingReleaseDto? CurrentRelease { get; set; }
    public List<PurchaseRequisitionSourcingRequirementDto> Requirements { get; set; } = new();
    public List<string> RequiredActions { get; set; } = new();
}

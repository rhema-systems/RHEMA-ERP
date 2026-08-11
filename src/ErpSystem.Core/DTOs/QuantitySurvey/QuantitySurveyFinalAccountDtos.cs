using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed record QuantitySurveyFinalAccountContractLookupDto(
    Guid Id,
    string ContractNumber,
    string Title,
    string Contractor,
    string Currency,
    decimal ContractValue);

public sealed record QuantitySurveyFinalAccountReconciliationLineDto(
    string Category,
    string Label,
    Guid? SourceId,
    string? SourceReference,
    decimal Amount,
    string Effect,
    string Status);

public sealed record QuantitySurveyFinalAccountRevisionDto(
    Guid Id,
    string Action,
    Guid ActorUserId,
    string ActorName,
    string? ActorRoles,
    string CorrelationId,
    string? Reason,
    string? BeforeJson,
    string? AfterJson,
    DateTime CreatedAt);

public sealed class QuantitySurveyFinalAccountDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ContractId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractTitle { get; init; } = string.Empty;
    public string Contractor { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public DateTime? SettlementDate { get; init; }
    public Guid? ApprovedBoqVersionId { get; init; }
    public int? ApprovedBoqVersionNumber { get; init; }
    public decimal ApprovedBoqValue { get; init; }
    public decimal OriginalContractValue { get; init; }
    public decimal ApprovedVariationAmount { get; init; }
    public decimal ApprovedClaimAmount { get; init; }
    public decimal ApprovedEscalationAmount { get; init; }
    public decimal GrossFinalAccountValue { get; init; }
    public decimal AdvanceRecoveryAmount { get; init; }
    public decimal MaterialDeductionAmount { get; init; }
    public decimal OtherDeductionAmount { get; init; }
    public decimal TotalDeductionAmount { get; init; }
    public decimal FinalAccountValue { get; init; }
    public decimal CertifiedToDate { get; init; }
    public decimal RetentionHeldAmount { get; init; }
    public decimal RetentionReleasedAmount { get; init; }
    public decimal RetentionOutstandingAmount { get; init; }
    public decimal PaidToDateAmount { get; init; }
    public decimal FinalPaymentAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public Guid? PreparedById { get; init; }
    public DateTime? PreparedAt { get; init; }
    public Guid? SubmittedById { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public Guid? ClosedById { get; init; }
    public DateTime? ClosedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string? ClosureReason { get; init; }
    public bool HasPendingCommercialRecords { get; init; }
    public bool CanClose { get; init; }
    public IReadOnlyList<string> ClosureBlockers { get; init; } = [];
    public IReadOnlyList<QuantitySurveyFinalAccountReconciliationLineDto> Lines { get; init; } = [];
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class QuantitySurveyFinalAccountWorkspaceDto
{
    public QuantitySurveyFinalAccountDto? FinalAccount { get; init; }
    public IReadOnlyList<QuantitySurveyFinalAccountContractLookupDto> Contracts { get; init; } = [];
}

public sealed class PrepareQuantitySurveyFinalAccountRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid ContractId { get; init; }
    public DateTime SettlementDate { get; init; }
    public string? RowVersion { get; init; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
    [StringLength(2000)] public string? Notes { get; init; }
}

public sealed class QuantitySurveyFinalAccountActionRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
}

public class QuantitySurveyFinalAccountException(string message) : Exception(message);
public sealed class QuantitySurveyFinalAccountValidationException(string message) : QuantitySurveyFinalAccountException(message);
public sealed class QuantitySurveyFinalAccountConflictException(string message) : QuantitySurveyFinalAccountException(message);
public sealed class QuantitySurveyFinalAccountNotFoundException(string message) : QuantitySurveyFinalAccountException(message);

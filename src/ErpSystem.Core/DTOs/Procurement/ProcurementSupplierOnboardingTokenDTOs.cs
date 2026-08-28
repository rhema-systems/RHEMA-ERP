using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementSupplierOnboardingTokenSearchRequest
{
    public string? Search { get; set; }
    public ProcurementSupplierOnboardingTokenStatus? Status { get; set; }
    public ProcurementSupplierOnboardingPaymentStatus? PaymentStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class ProcurementSupplierOnboardingTokenPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<ProcurementSupplierOnboardingTokenListItemDto> Items { get; set; } =
        Array.Empty<ProcurementSupplierOnboardingTokenListItemDto>();
}

public sealed class ProcurementSupplierOnboardingTokenSummaryDto
{
    public int TotalCount { get; set; }
    public int AwaitingPaymentCount { get; set; }
    public int ActiveCount { get; set; }
    public int ExpiredCount { get; set; }
    public int PendingReconciliationCount { get; set; }
    public decimal PostedAmount { get; set; }
}

public sealed class ProcurementSupplierOnboardingRegistrationOptionDto
{
    public Guid RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public ProcurementSupplierRegistrationCategory? RegistrationCategory { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ProcurementSupplierOnboardingTokenListItemDto
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string TokenReference { get; set; } = string.Empty;
    public string MaskedToken { get; set; } = string.Empty;
    public int Generation { get; set; }
    public ProcurementSupplierOnboardingTokenStatus Status { get; set; }
    public ProcurementSupplierOnboardingPaymentStatus PaymentStatus { get; set; }
    public ProcurementSupplierOnboardingFeeMode FeeMode { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? ExpiredAtUtc { get; set; }
    public string SourceConfigurationProfileCode { get; set; } = string.Empty;
    public int SourceConfigurationProfileVersion { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierOnboardingTokenDto :
    ProcurementSupplierOnboardingTokenListItemDto
{
    public string FeeType { get; set; } = string.Empty;
    public decimal TaxPercent { get; set; }
    public IReadOnlyList<string> PaymentChannels { get; set; } = Array.Empty<string>();
    public string ReceiptNumberFormat { get; set; } = string.Empty;
    public string ExemptionRule { get; set; } = string.Empty;
    public string RefundRule { get; set; } = string.Empty;
    public string RenewalRule { get; set; } = string.Empty;
    public Guid SourceConfigurationProfileId { get; set; }
    public Guid SourceConfigurationDecisionId { get; set; }
    public Guid? RevenueAccountId { get; set; }
    public Guid? TaxAccountId { get; set; }
    public Guid? ExemptionWorkflowDefinitionId { get; set; }
    public string DecisionSnapshotHash { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public string? ExpiryReason { get; set; }
    public string? ReissueReason { get; set; }
    public DateTime? ReissuedAtUtc { get; set; }
    public IReadOnlyList<ProcurementSupplierOnboardingPaymentDto> Payments { get; set; } =
        Array.Empty<ProcurementSupplierOnboardingPaymentDto>();
    public IReadOnlyList<ProcurementSupplierOnboardingExemptionDto> Exemptions { get; set; } =
        Array.Empty<ProcurementSupplierOnboardingExemptionDto>();
}

public sealed class ProcurementSupplierOnboardingPaymentDto
{
    public Guid Id { get; set; }
    public Guid? SubmittedByApplicantSessionId { get; set; }
    public bool SubmittedByApplicant => SubmittedByApplicantSessionId.HasValue;
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodCode { get; set; } = string.Empty;
    public string PaymentMethodName { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public ProcurementSupplierOnboardingPaymentStatus Status { get; set; }
    public DateTime PaidAtUtc { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateTime? ReceiptIssuedAtUtc { get; set; }
    public DateTime? ReconciledAtUtc { get; set; }
    public string? ReconciliationReference { get; set; }
    public string? ReconciliationNotes { get; set; }
    public string? FailureReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierOnboardingExemptionDto
{
    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ProcurementSupplierOnboardingExemptionStatus Status { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid RequestedById { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public Guid? DecidedById { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionComment { get; set; }
    public IReadOnlyList<ProcurementControlEventEvidenceReference> Evidence { get; set; } =
        Array.Empty<ProcurementControlEventEvidenceReference>();
    public string RowVersion { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierOnboardingTokenIssueResultDto
{
    public ProcurementSupplierOnboardingTokenDto Token { get; set; } = new();

    /// <summary>
    /// Returned only for the issuing or reissuing request. It is never persisted or logged.
    /// </summary>
    public string? PlaintextToken { get; set; }
}

public sealed class IssueProcurementSupplierOnboardingTokenRequest
{
    [Required]
    public Guid RegistrationId { get; set; }

    /// <summary>
    /// Retained for wire compatibility only. Token issue always resolves the
    /// policy effective at the trusted server time; callers cannot backdate or
    /// future-date fee-policy selection.
    /// </summary>
    public DateTime? EffectiveAtUtc { get; set; }
}

public sealed class RecordProcurementSupplierOnboardingPaymentRequest
{
    [Required]
    public Guid PaymentMethodId { get; set; }

    [StringLength(200)]
    public string? PaymentReference { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReconcileProcurementSupplierOnboardingPaymentRequest
{
    [Required, StringLength(200)]
    public string ReconciliationReference { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class RequestProcurementSupplierOnboardingExemptionRequest
{
    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [MinLength(1)]
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class DecideProcurementSupplierOnboardingExemptionRequest
{
    public bool Approve { get; set; }

    [Required, StringLength(1000)]
    public string Comment { get; set; } = string.Empty;

    [MinLength(1)]
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReissueProcurementSupplierOnboardingTokenRequest
{
    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierOnboardingPaymentMethodOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool RequiresReference { get; set; }
    public bool IsPostingReady { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class CreateQuantitySurveyAdvanceRecoveryRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid ContractId { get; init; }
    public Guid VendorPaymentId { get; init; }
    [Range(typeof(decimal), "0.0001", "100")] public decimal RecoveryPercentage { get; init; }
    [Required, MinLength(5), StringLength(2000)] public string Reason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyAdvanceRecoveryActionRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, MinLength(5), StringLength(2000)] public string Reason { get; init; } = string.Empty;
}

public sealed record QuantitySurveyAdvancePaymentLookupDto(
    Guid VendorPaymentId,
    Guid ContractId,
    string ContractNumber,
    string ContractorName,
    string PaymentNumber,
    DateTime PaymentDate,
    string Currency,
    decimal OriginalAmount,
    decimal FinanceAllocatedAmount,
    decimal FinanceAvailableAmount);

public sealed record QuantitySurveyAdvanceRecoveryLedgerEntryDto(
    Guid PaymentCertificateId,
    string CertificateNumber,
    DateTime IssueDate,
    string Status,
    decimal RecoveryAmount,
    Guid? VendorInvoiceId,
    decimal FinanceAppliedAmount,
    decimal RunningRecoveryBalance);

public sealed class QuantitySurveyAdvanceRecoveryAgreementDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ContractId { get; init; }
    public Guid VendorPaymentId { get; init; }
    public string RecoveryNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractorName { get; init; } = string.Empty;
    public string PaymentNumber { get; init; } = string.Empty;
    public DateTime PaymentDate { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal OriginalAdvanceAmount { get; init; }
    public decimal RecoveryPercentage { get; init; }
    public decimal ApprovedRecoveryAmount { get; init; }
    public decimal PendingRecoveryAmount { get; init; }
    public decimal RemainingRecoveryBalance { get; init; }
    public decimal FinanceAllocatedAmount { get; init; }
    public decimal FinanceAvailableAmount { get; init; }
    public decimal FinanceAppliedToQsCertificates { get; init; }
    public decimal ReconciliationDifference { get; init; }
    public Guid PreparedById { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime PreparedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyAdvanceRecoveryLedgerEntryDto> Ledger { get; init; } = [];
}

public sealed class QuantitySurveyAdvanceRecoveryWorkspaceDto
{
    public IReadOnlyList<QuantitySurveyAdvancePaymentLookupDto> EligibleAdvances { get; init; } = [];
    public IReadOnlyList<QuantitySurveyAdvanceRecoveryAgreementDto> Agreements { get; init; } = [];
}

public sealed record QuantitySurveyAdvanceRecoveryRevisionDto(
    Guid Id, string Action, Guid ActorUserId, string ActorName, string? ActorRoles,
    string CorrelationId, string? Reason, string? BeforeJson, string AfterJson, DateTime CreatedAt);

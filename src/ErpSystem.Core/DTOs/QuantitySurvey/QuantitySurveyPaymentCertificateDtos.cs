using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyPaymentCertificateLookupDto
{
    public Guid WorksheetId { get; init; }
    public Guid InterimValuationId { get; init; }
    public Guid? ContractId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal CertifiedToDateAmount { get; init; }
    public decimal PreviousCertificateAmount { get; init; }
    public decimal CurrentGrossAmount { get; init; }
    public decimal CurrentRetentionAmount { get; init; }
}

public sealed record QuantitySurveyPaymentCertificateAdvanceLookupDto(
    Guid AgreementId, Guid ContractId, string Label, string Currency,
    decimal RecoveryPercentage, decimal RemainingBalance);

public sealed record QuantitySurveyPaymentCertificateMaterialLookupDto(
    Guid ReconciliationId, Guid WorksheetId, Guid ContractId, string Label, string Currency,
    decimal MaterialOnSiteAmount, decimal MaterialOffSiteAmount, decimal TdcSuppliedDeductionAmount);

public sealed class QuantitySurveyPaymentCertificateLookupsDto
{
    public IReadOnlyList<QuantitySurveyPaymentCertificateLookupDto> EligibleValuations { get; init; } = [];
    public IReadOnlyList<QuantitySurveyPaymentCertificateAdvanceLookupDto> EligibleAdvanceRecoveries { get; init; } = [];
    public IReadOnlyList<QuantitySurveyPaymentCertificateMaterialLookupDto> ApprovedMaterialReconciliations { get; init; } = [];
}

public sealed class QuantitySurveyPaymentCertificateDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectInterimValuationId { get; init; }
    public Guid QuantitySurveyValuationWorksheetId { get; init; }
    public Guid? PreviousPaymentCertificateId { get; init; }
    public Guid? AdvanceRecoveryAgreementId { get; init; }
    public Guid? MaterialReconciliationId { get; init; }
    public Guid? VendorInvoiceId { get; init; }
    public string? VendorInvoiceNumber { get; init; }
    public string? CertificateNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public DateTime IssueDate { get; init; }
    public DateTime? PaymentDueDate { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal CertifiedToDateAmount { get; init; }
    public decimal PreviouslyCertifiedAmount { get; init; }
    public decimal GrossCertifiedAmount { get; init; }
    public decimal RetentionHeldAmount { get; init; }
    public decimal RetentionReleasedAmount { get; init; }
    public decimal AdvanceRecoveryAmount { get; init; }
    public decimal MaterialDeductionAmount { get; init; }
    public decimal MaterialOnSiteAmount { get; init; }
    public decimal MaterialOffSiteAmount { get; init; }
    public decimal OtherDeductionsAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal NetCertifiedAmount { get; init; }
    public string TaxHandling { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public string ApHandoffStatus { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public decimal? FinanceInvoiceAmount { get; init; }
    public decimal? FinancePaidAmount { get; init; }
    public decimal? FinanceBalanceAmount { get; init; }
    public Guid? FinanceInvoiceJournalEntryId { get; init; }
    public string FinancePostingStatus { get; init; } = string.Empty;
    public string ReconciliationStatus { get; init; } = string.Empty;
    public string? ApHandoffFailure { get; init; }
    public DateTime? PaymentStatusUpdatedAt { get; init; }
    public bool DocumentGenerated { get; init; }
    public DateTime? GeneratedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class GenerateQuantitySurveyPaymentCertificateRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid ValuationWorksheetId { get; init; }
    public Guid? AdvanceRecoveryAgreementId { get; init; }
    public Guid? MaterialReconciliationId { get; init; }
    public DateTime? PaymentDueDate { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal AdvanceRecoveryAmount { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal OtherDeductionsAmount { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
}

public sealed class UpdateQuantitySurveyPaymentCertificateRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    public DateTime? PaymentDueDate { get; init; }
    public Guid? MaterialReconciliationId { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal AdvanceRecoveryAmount { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal OtherDeductionsAmount { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
}

public sealed class QuantitySurveyPaymentCertificateActionRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; init; }
}

public sealed class QuantitySurveyPaymentCertificateRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string? BeforeJson { get; init; }
    public string AfterJson { get; init; } = string.Empty;
}

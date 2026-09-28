using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

// ═══════════════════════════════════════════════════════════════════════════
//  VENDOR INVOICE DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Vendor Invoice

public class VendorInvoiceDto
{
    public Guid? EstateAcquisitionId { get; set; }
    public EstatePayableKind? EstatePayableKind { get; set; }
    public bool IsProcurementAutoInvoice { get; set; }
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? SupplierInvoiceNumber { get; set; }

    public Guid BusinessPartnerId { get; set; }
    public Guid? BusinessPartnerRoleId { get; set; }
    public Guid? BusinessPartnerApProfileVersionId { get; set; }
    public string BusinessPartnerCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? BusinessPartnerLegalName { get; set; }
    public string? BusinessPartnerTaxIdentificationNumber { get; set; }


    public Guid? PurchaseOrderId { get; set; }
    public string? PurchaseOrderNumber { get; set; }

    public DateTime InvoiceDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? DueDate { get; set; }

    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;
    public string? CurrencyOverrideReason { get; set; }
    public decimal ExchangeRate { get; set; } = 1.0m;
    public Guid? ExchangeRateId { get; set; }
    public decimal BaseCurrencyAmount { get; set; }

    public int PaymentTermsDays { get; set; }
    public Guid? PaymentTermId { get; set; }

    // Early-payment discount
    public decimal EarlyPaymentDiscountPercentage { get; set; }
    public DateTime? EarlyPaymentDiscountDueDate { get; set; }
    public decimal EarlyPaymentDiscountAmount { get; set; }

    // Withholding tax
    public bool? ApplySupplierWithholdingDefaults { get; set; }
    public decimal? WithholdingTaxRateOverride { get; set; }
    public bool WithholdingDecisionPending { get; set; }
    public decimal WithholdingTaxRate { get; set; }
    public decimal WithholdingTaxAmount { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public string? WithholdingCertificateNumber { get; set; }
    public DateTime? WithholdingCertificateDate { get; set; }
    public string? WithholdingContractReference { get; set; }
    public WhtSupplyCategory? WithholdingSupplyCategory { get; set; }

    // Matching
    public InvoiceMatchingType MatchingType { get; set; }
    public InvoiceMatchingStatus MatchingStatus { get; set; }
    public string? MatchingNotes { get; set; }
    public Guid? MatchingControlEventId { get; set; }
    public string? MatchingSnapshotHash { get; set; }
    public DateTime? MatchingEvaluatedAtUtc { get; set; }
    public decimal MatchingPriceTolerancePercent { get; set; }
    public decimal MatchingQuantityTolerancePercent { get; set; }
    public Guid? MatchExceptionControlEventId { get; set; }
    public ProcurementAcceptedSupplyKind? AcceptedSupplyKind { get; set; }
    public Guid? AcceptedSupplySourceId { get; set; }
    public string? AcceptedSupplySourceReference { get; set; }
    public string? AcceptedSupplySnapshotHash { get; set; }
    public DateTime? AcceptedSupplyValidatedAtUtc { get; set; }

    // Status
    public VendorInvoiceStatus Status { get; set; }
    public string ApprovalStatus { get; set; } = "Draft";
    public bool ApprovalRequired { get; set; } = true;

    // GL
    public Guid? ExpenseAccountId { get; set; }
    public string? ExpenseAccountName { get; set; }
    public Guid? ApAccountId { get; set; }
    public string? ApAccountName { get; set; }
    public Guid? JournalEntryId { get; set; }

    public string? Notes { get; set; }
    public string? Reference { get; set; }
    public bool IsOpeningBalance { get; set; }

    public List<VendorInvoiceLineItemDto> LineItems { get; set; } = new();
    public List<VendorPaymentAllocationDto> PaymentAllocations { get; set; } = new();
    public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class VendorInvoiceCreateDto
{
    // Set only by the Estate controller after its source and stage authorization checks.
    [System.Text.Json.Serialization.JsonIgnore] public Guid? EstateAcquisitionId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public EstatePayableKind? EstatePayableKind { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public Guid? AutoInvoiceRequestId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? AutoInvoiceRequestHash { get; set; }
    /// <summary>Opt in only for a new draft; false preserves explicit No Tax and older-client behavior.</summary>
    public bool? ApplyBusinessPartnerDefaults { get; set; }
    public string? SupplierInvoiceNumber { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    /// <summary>
    /// Required when the partner has both active Supplier and Contractor roles; otherwise the
    /// server resolves the partner's single AP role for the invoice accounting date.
    /// </summary>
    public Guid? BusinessPartnerRoleId { get; set; }


    public Guid? PurchaseOrderId { get; set; }

    [Required]
    public DateTime InvoiceDate { get; set; }

    public DateTime? ReceivedDate { get; set; }
    public DateTime? DueDate { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? CurrencyOverrideReason { get; set; }
    public decimal ExchangeRate { get; set; } = 1.0m;
    public Guid? ExchangeRateId { get; set; }

    public int PaymentTermsDays { get; set; } = 30;
    public Guid? PaymentTermId { get; set; }

    // Early-payment discount
    public decimal EarlyPaymentDiscountPercentage { get; set; }
    public DateTime? EarlyPaymentDiscountDueDate { get; set; }

    // Withholding tax
    public bool? ApplySupplierWithholdingDefaults { get; set; }
    [Range(typeof(decimal), "0", "100")]
    public decimal? WithholdingTaxRateOverride { get; set; }
    public decimal WithholdingTaxRate { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public string? WithholdingCertificateNumber { get; set; }
    public DateTime? WithholdingCertificateDate { get; set; }
    [MaxLength(100)]
    public string? WithholdingContractReference { get; set; }
    public WhtSupplyCategory? WithholdingSupplyCategory { get; set; }

    // Matching
    public InvoiceMatchingType MatchingType { get; set; } = InvoiceMatchingType.None;

    public ProcurementAcceptedSupplyKind? AcceptedSupplyKind { get; set; }
    public Guid? AcceptedSupplySourceId { get; set; }

    /// <summary>
    /// Server-only flag used by the QS owner when handing an approved Works
    /// certificate to AP. It cannot be supplied by an API client.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsTrustedAcceptedSupplyHandoff { get; set; }

    // GL accounts
    public Guid? ExpenseAccountId { get; set; }
    public Guid? ApAccountId { get; set; }

    public string? Notes { get; set; }
    public string? Reference { get; set; }
    public bool IsOpeningBalance { get; set; }

    [Required]
    public List<VendorInvoiceLineItemCreateDto> LineItems { get; set; } = new();
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public class VendorInvoiceUpdateDto
{
    [Required]
    public Guid Id { get; set; }

    public string? SupplierInvoiceNumber { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public ProcurementAcceptedSupplyKind? AcceptedSupplyKind { get; set; }
    public Guid? AcceptedSupplySourceId { get; set; }

    public DateTime InvoiceDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? DueDate { get; set; }

    public string CurrencyCode { get; set; } = "USD";
    [MaxLength(500)]
    public string? CurrencyOverrideReason { get; set; }
    public decimal ExchangeRate { get; set; } = 1.0m;
    public Guid? ExchangeRateId { get; set; }

    public int PaymentTermsDays { get; set; } = 30;
    public Guid? PaymentTermId { get; set; }

    public decimal EarlyPaymentDiscountPercentage { get; set; }
    public DateTime? EarlyPaymentDiscountDueDate { get; set; }

    public bool? ApplySupplierWithholdingDefaults { get; set; }
    [Range(typeof(decimal), "0", "100")]
    public decimal? WithholdingTaxRateOverride { get; set; }
    public decimal WithholdingTaxRate { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public string? WithholdingCertificateNumber { get; set; }
    public DateTime? WithholdingCertificateDate { get; set; }
    [MaxLength(100)]
    public string? WithholdingContractReference { get; set; }
    public WhtSupplyCategory? WithholdingSupplyCategory { get; set; }

    public InvoiceMatchingType MatchingType { get; set; }

    public Guid? ExpenseAccountId { get; set; }
    public Guid? ApAccountId { get; set; }

    public string? Notes { get; set; }
    public string? Reference { get; set; }
    public bool IsOpeningBalance { get; set; }

    public List<VendorInvoiceLineItemCreateDto> LineItems { get; set; } = new();
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public class VendorInvoiceQueryDto
{
    public bool? ProcurementOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public VendorInvoiceStatus? Status { get; set; }
    public string? ApprovalStatus { get; set; }
    public InvoiceMatchingStatus? MatchingStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? DueFromDate { get; set; }
    public DateTime? DueToDate { get; set; }
    public bool? OverdueOnly { get; set; }
    public bool? IsOpeningBalance { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

public class VendorInvoiceLineItemDto
{
    public Guid? LandedCostItemId { get; set; }
    public Guid Id { get; set; }
    public Guid VendorInvoiceId { get; set; }
    public string LineItemType { get; set; } = "Expense";
    public Guid? GLAccountId { get; set; }
    public string? GLAccountName { get; set; }
    public Guid? BudgetEntryId { get; set; }
    public Guid? FixedAssetId { get; set; }
    public Guid? CapitalizationJournalEntryId { get; set; }
    public Guid? CapitalizationPostingEventId { get; set; }
    public DateTime? CapitalizedAt { get; set; }
    /// <summary>
    /// Legacy procurement PO line id only; finance PO/GRV lines use their receipt/invoice linkage instead.
    /// </summary>
    public Guid? PurchaseOrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? TaxGroupId { get; set; }
    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Standard;
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public string? TaxCode { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Unit { get; set; }
}

public class VendorInvoiceLineItemCreateDto
{
    // Never accept source links from a generic invoice request.
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? LandedCostItemId { get; set; }
    public Guid? Id { get; set; }
    public string LineItemType { get; set; } = "Expense";
    public Guid? GLAccountId { get; set; }
    public Guid? BudgetEntryId { get; set; }
    public Guid? FixedAssetId { get; set; }
    /// <summary>
    /// Legacy procurement PO line id only; do not send FinancePurchaseOrderItem ids in this field.
    /// </summary>
    public Guid? PurchaseOrderItemId { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    [Required]
    public decimal UnitPrice { get; set; }

    public Guid? TaxGroupId { get; set; }
    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Standard;
    public decimal TaxRate { get; set; }
    public string? TaxCode { get; set; }
    [Range(typeof(decimal), "0", "100")]
    public decimal DiscountPercentage { get; set; }
    public string? Unit { get; set; }
}

public class InvoiceMatchingResultDto
{
    public Guid VendorInvoiceId { get; set; }
    public InvoiceMatchingType MatchingType { get; set; }
    public InvoiceMatchingStatus MatchingStatus { get; set; }
    public bool IsMatched { get; set; }
    public List<MatchingDiscrepancyDto> Discrepancies { get; set; } = new();
    public decimal InvoiceTotal { get; set; }
    public decimal? PurchaseOrderTotal { get; set; }
    public decimal? GoodsReceiptTotal { get; set; }
    public decimal TolerancePercentage { get; set; } = 1.0m;
    public decimal PriceTolerancePercentage { get; set; } = 1.0m;
    public decimal QuantityTolerancePercentage { get; set; } = 1.0m;
    public bool IsRequired { get; set; }
    public bool ApprovalReady { get; set; }
    public bool ApprovedExceptionApplied { get; set; }
    public Guid? MatchingControlEventId { get; set; }
    public Guid? MatchExceptionControlEventId { get; set; }
    public string? SnapshotHash { get; set; }
    public DateTime? EvaluatedAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ConfigurationProfileCode { get; set; }
    public int? ConfigurationProfileVersion { get; set; }
    public List<string> DecisionKeys { get; set; } = new();
    public List<InvoiceMatchingCheckDto> Checks { get; set; } = new();
}

public class InvoiceMatchingCheckDto
{
    public string CheckKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public bool ExceptionEligible { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class MatchingDiscrepancyDto
{
    public string ItemDescription { get; set; } = string.Empty;
    public string DiscrepancyType { get; set; } = string.Empty; // Price, Quantity, Missing
    public decimal InvoiceValue { get; set; }
    public decimal? ExpectedValue { get; set; }
    public decimal Variance { get; set; }
    public decimal VariancePercentage { get; set; }
    public bool ExceptionEligible { get; set; }
}

public sealed class VendorInvoiceMatchControlException : Exception
{
    public VendorInvoiceMatchControlException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}

#endregion

#region Vendor Invoice Match Exception

public sealed class VendorInvoiceMatchExceptionEvidenceRequestDto
{
    [Required, StringLength(100)] public string RequirementKey { get; set; } = string.Empty;
    public VendorInvoiceMatchExceptionEvidenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required, StringLength(1000)] public string EvidenceReference { get; set; } = string.Empty;
}

public sealed class CreateVendorInvoiceMatchExceptionDto
{
    [Required, StringLength(100)] public string RootCauseCategory { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string RootCauseDescription { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Justification { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string CorrectiveAction { get; set; } = string.Empty;
    public Guid CorrectiveActionOwnerId { get; set; }
    public DateTime CorrectiveActionDueAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [MinLength(2)] public List<VendorInvoiceMatchExceptionEvidenceRequestDto> Evidence { get; set; } = new();
}

public sealed class DecideVendorInvoiceMatchExceptionDto
{
    public bool Approved { get; set; }
    [Required, StringLength(2000)] public string Comment { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CancelVendorInvoiceMatchExceptionDto
{
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CompleteVendorInvoiceMatchCorrectiveActionDto
{
    [Required, StringLength(2000)] public string CompletionNote { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
    [MinLength(1)] public List<VendorInvoiceMatchExceptionEvidenceRequestDto> Evidence { get; set; } = new();
}

public sealed class VendorInvoiceMatchExceptionVarianceDto
{
    public Guid Id { get; set; }
    public string VarianceType { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public decimal ActualValue { get; set; }
    public decimal ExpectedValue { get; set; }
    public decimal Variance { get; set; }
    public decimal VariancePercentage { get; set; }
    public decimal ConfiguredTolerancePercent { get; set; }
}

public sealed class VendorInvoiceMatchExceptionEvidenceDto
{
    public Guid Id { get; set; }
    public string RequirementKey { get; set; } = string.Empty;
    public VendorInvoiceMatchExceptionEvidenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
}

public sealed class VendorInvoiceMatchExceptionActionDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public string Action { get; set; } = string.Empty;
    public VendorInvoiceMatchExceptionStatus FromStatus { get; set; }
    public VendorInvoiceMatchExceptionStatus ToStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}

public sealed class VendorInvoiceMatchExceptionDto
{
    public Guid Id { get; set; }
    public Guid VendorInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public VendorInvoiceMatchExceptionStatus Status { get; set; }
    public string VarianceType { get; set; } = string.Empty;
    public decimal PriceTolerancePercent { get; set; }
    public decimal QuantityTolerancePercent { get; set; }
    public string RootCauseCategory { get; set; } = string.Empty;
    public string RootCauseDescription { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public string CorrectiveAction { get; set; } = string.Empty;
    public Guid CorrectiveActionOwnerId { get; set; }
    public string CorrectiveActionOwnerName { get; set; } = string.Empty;
    public DateTime CorrectiveActionDueAtUtc { get; set; }
    public VendorInvoiceMatchCorrectiveActionStatus CorrectiveActionStatus { get; set; }
    public DateTime? CorrectiveActionCompletedAtUtc { get; set; }
    public Guid? CorrectiveActionCompletedById { get; set; }
    public string? CorrectiveActionCompletionNote { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string InvoiceSnapshotHash { get; set; } = string.Empty;
    public Guid? ConfigurationProfileId { get; set; }
    public int? ConfigurationProfileVersion { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public Guid? FinalApprovedById { get; set; }
    public string? FinalApprovedByName { get; set; }
    public DateTime? FinalApprovedAtUtc { get; set; }
    public Guid? ApprovalControlEventId { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyList<VendorInvoiceMatchExceptionVarianceDto> Variances { get; set; } =
        Array.Empty<VendorInvoiceMatchExceptionVarianceDto>();
    public IReadOnlyList<VendorInvoiceMatchExceptionEvidenceDto> Evidence { get; set; } =
        Array.Empty<VendorInvoiceMatchExceptionEvidenceDto>();
    public IReadOnlyList<VendorInvoiceMatchExceptionActionDto> Actions { get; set; } =
        Array.Empty<VendorInvoiceMatchExceptionActionDto>();
}

public sealed class VendorInvoiceMatchExceptionOverviewDto
{
    public Guid VendorInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceMatchingResultDto MatchingReadiness { get; set; } = new();
    public bool CanRequest { get; set; }
    public bool CanDecide { get; set; }
    public bool CanCancel { get; set; }
    public bool CanCompleteCorrectiveAction { get; set; }
    public IReadOnlyList<string> RequiredEvidenceKeys { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> DecisionKeys { get; set; } = Array.Empty<string>();
    public VendorInvoiceMatchExceptionDto? Active { get; set; }
    public VendorInvoiceMatchExceptionDto? CorrectiveActionItem { get; set; }
    public IReadOnlyList<VendorInvoiceMatchExceptionDto> History { get; set; } =
        Array.Empty<VendorInvoiceMatchExceptionDto>();
}

public sealed class VendorInvoiceMatchExceptionReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public VendorInvoiceMatchExceptionStatus? Status { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public int TotalCount { get; set; }
    public int ApprovedCount { get; set; }
    public int ExpiredCount { get; set; }
    public int OpenCorrectiveActionCount { get; set; }
    public IReadOnlyList<VendorInvoiceMatchExceptionReportRowDto> Rows { get; set; } =
        Array.Empty<VendorInvoiceMatchExceptionReportRowDto>();
}

public sealed class VendorInvoiceMatchExceptionReportRowDto
{
    public Guid ExceptionId { get; set; }
    public Guid VendorInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public Guid? BusinessPartnerRoleId { get; set; }
    public Guid? BusinessPartnerApProfileVersionId { get; set; }
    public string BusinessPartnerCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? BusinessPartnerLegalName { get; set; }
    public string? BusinessPartnerTaxIdentificationNumber { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public VendorInvoiceMatchExceptionStatus Status { get; set; }
    public string VarianceType { get; set; } = string.Empty;
    public decimal MaximumVariancePercentage { get; set; }
    public string RootCauseCategory { get; set; } = string.Empty;
    public string RootCauseDescription { get; set; } = string.Empty;
    public string CorrectiveAction { get; set; } = string.Empty;
    public string CorrectiveActionOwnerName { get; set; } = string.Empty;
    public DateTime CorrectiveActionDueAtUtc { get; set; }
    public VendorInvoiceMatchCorrectiveActionStatus CorrectiveActionStatus { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public string? FinalApprovedByName { get; set; }
    public DateTime? FinalApprovedAtUtc { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovalControlEventId { get; set; }
    public int EvidenceCount { get; set; }
}

public class VendorInvoiceMatchExceptionControlException : Exception
{
    public VendorInvoiceMatchExceptionControlException(string code, string message, int statusCode = 422)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  VENDOR PAYMENT DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Vendor Payment

public class VendorPaymentDto
{
    public Guid Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public VendorPaymentMethod PaymentMethod { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? PaymentMethodName { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? BankAccountName { get; set; }
    public string? ChequeNumber { get; set; }
    public string? TransactionReference { get; set; }
    public decimal WithholdingTaxRate { get; set; }
    public decimal WithholdingTaxAmount { get; set; }
    public decimal WithholdingTaxBaseAmount { get; set; }
    public decimal WithholdingTaxCumulativeBefore { get; set; }
    public decimal? WithholdingTaxThresholdAmount { get; set; }
    public bool WithholdingTaxThresholdApplied { get; set; }
    public string? WithholdingTaxCalculationNote { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public string? WithholdingCertificateNumber { get; set; }
    public DateTime? WithholdingCertificateDate { get; set; }
    public decimal DiscountTaken { get; set; }
    public VendorPaymentStatus Status { get; set; }
    public bool ApprovalRequired { get; set; } = true;
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? AppliedApprovalPolicySetId { get; set; }
    public string? AppliedApprovalPolicyCode { get; set; }
    public string? ApprovalControlSnapshotHash { get; set; }
    public bool IsExceptionalPayment { get; set; }
    public string? ExceptionalPaymentReason { get; set; }
    public bool RequiresManagingDirectorApproval { get; set; }
    public Guid? ManagingDirectorApprovedById { get; set; }
    public DateTime? ManagingDirectorApprovedAt { get; set; }
    public bool EvidenceExceptionRequested { get; set; }
    public string? EvidenceExceptionReason { get; set; }
    public Guid? EvidenceExceptionRequestedById { get; set; }
    public DateTime? EvidenceExceptionRequestedAt { get; set; }
    public Guid? EvidenceExceptionApprovedById { get; set; }
    public DateTime? EvidenceExceptionApprovedAt { get; set; }
    public Guid? AuthorizedById { get; set; }
    public DateTime? AuthorizedDate { get; set; }
    public Guid? InvoicePaymentSodControlEventId { get; set; }
    public Guid? PaymentBatchId { get; set; }
    public string? PaymentBatchNumber { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public DateTime? ReversalDate { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedById { get; set; }
    public string? ReversalReason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<VendorPaymentAllocationDto> Allocations { get; set; } = new();
    public List<SupplierDebitNoteApplicationDto> SupplierDebitNoteApplications { get; set; } = new();
    public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
    public IReadOnlyList<FinanceSettlementDimensionComponentDto> SettlementDimensions { get; set; } =
        Array.Empty<FinanceSettlementDimensionComponentDto>();
}

/// <summary>
/// Restricted Finance-owned read model used by downstream contract controls. It deliberately
/// excludes bank, cheque and authorization details while proving that the supplier advance is
/// posted, active and still has an unapplied balance.
/// </summary>
public sealed class PostedSupplierAdvanceDto
{
    public Guid Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public VendorPaymentStatus Status { get; set; }
    public Guid JournalEntryId { get; set; }
}

public class VendorPaymentCreateDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    /// <summary>Required only when both Supplier and Contractor roles are active.</summary>
    public Guid? BusinessPartnerRoleId { get; set; }

    [Required]
    public DateTime PaymentDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    public VendorPaymentMethod PaymentMethod { get; set; } = VendorPaymentMethod.BankTransfer;
    public Guid? PaymentMethodId { get; set; }

    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1.0m;
    public Guid? ExchangeRateId { get; set; }

    public Guid? BankAccountId { get; set; }
    public string? ChequeNumber { get; set; }
    public string? TransactionReference { get; set; }

    public decimal WithholdingTaxRate { get; set; }
    public decimal? WithholdingTaxAmount { get; set; }
    public decimal? WithholdingTaxBaseAmount { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public string? WithholdingCertificateNumber { get; set; }
    public DateTime? WithholdingCertificateDate { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Optional document default and, for an unallocated supplier advance, its authoritative
    /// economic-line dimensions. Allocated invoice dimensions are inherited server-side and
    /// cannot be supplied through this payload.
    /// </summary>
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }

    /// <summary>
    /// Optional: allocations to create immediately with the payment.
    /// </summary>
    public List<VendorPaymentAllocationCreateDto>? Allocations { get; set; }
}

/// <summary>
/// Submits a direct vendor payment into the configured maker-checker route. Exceptional-payment
/// and evidence-exception decisions are explicit command data because silently deriving either
/// from free-form notes would make the authority decision impossible to audit reliably.
/// </summary>
public sealed class SubmitVendorPaymentDto
{
    public bool IsExceptionalPayment { get; set; }

    [MaxLength(1000)]
    public string? ExceptionalPaymentReason { get; set; }

    public bool RequestEvidenceException { get; set; }

    [MaxLength(1000)]
    public string? EvidenceExceptionReason { get; set; }
}

/// <summary>
/// Evidence/control readiness for a direct AP payment. The UI consumes this read model instead of
/// reconstructing policy rules from workflow JSON or guessing whether an uploaded file is usable.
/// </summary>
public sealed class VendorPaymentControlDto
{
    public bool CanUploadEvidence { get; set; }
    public bool ApprovalRequired { get; set; } = true;
    public Guid PaymentId { get; set; }
    public string? PolicyCode { get; set; }
    public Guid? PolicySetId { get; set; }
    public string? PolicySnapshotHash { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? WorkflowStatus { get; set; }
    public Guid? CurrentStepInstanceId { get; set; }
    public string? CurrentStepName { get; set; }
    public bool IsExceptionalPayment { get; set; }
    public bool RequiresManagingDirectorApproval { get; set; }
    public bool ManagingDirectorApprovalCompleted { get; set; }
    public bool EvidenceExceptionRequested { get; set; }
    public bool EvidenceExceptionApproved { get; set; }
    public bool EvidenceRequirementsSatisfied { get; set; }
    public bool CanSubmit { get; set; }
    public int MinimumExceptionReasonLength { get; set; } = 30;
    public List<VendorPaymentEvidenceRequirementStatusDto> EvidenceRequirements { get; set; } = new();
    public List<VendorPaymentEvidenceDocumentDto> EvidenceDocuments { get; set; } = new();
    public List<string> BlockingReasons { get; set; } = new();
}

public sealed class VendorPaymentEvidenceRequirementStatusDto
{
    public string RequirementKey { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public int MinimumDocuments { get; set; }
    public bool RequireVerification { get; set; }
    public int CurrentDocumentCount { get; set; }
    public int VerifiedDocumentCount { get; set; }
    public bool IsSatisfied { get; set; }
}

public sealed class VendorPaymentEvidenceDocumentDto
{
    public string EvidenceSource { get; set; } = "Workflow";
    public string? DownloadUrl { get; set; }
    public Guid Id { get; set; }
    public string AttachmentId { get; set; } = string.Empty;
    public string? RequirementKey { get; set; }
    public string? DocumentName { get; set; }
    public string? DocumentType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public string MalwareScanStatus { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public Guid UploadedById { get; set; }
    public Guid? VerifiedById { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerificationNotes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class VendorPaymentEvidenceUploadDto
{
    public string RequirementKey { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Command used to reverse a posted AP payment. The date is optional because the tenant's Finance
/// reversal policy is authoritative; when supplied it is treated as the user's preferred date and
/// must still satisfy that policy and the fiscal-period controls.
/// </summary>
public sealed class ReverseVendorPaymentDto
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public DateTime? ReversalDate { get; set; }
}

/// <summary>
/// Complete source-to-ledger trace for one vendor payment. The trace deliberately exposes stable
/// identifiers alongside display values so auditors and support staff can reconcile API output to
/// database and report evidence without relying on labels alone.
/// </summary>
public sealed class VendorPaymentTraceDto
{
    public VendorPaymentDto Payment { get; set; } = new();
    public List<FinancePostingTraceDto> Postings { get; set; } = new();
    public List<FinanceAuditTraceDto> AuditEvents { get; set; } = new();
}

public class VendorPaymentQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public VendorPaymentStatus? Status { get; set; }
    public VendorPaymentMethod? PaymentMethod { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public Guid? PaymentBatchId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

public class VendorPaymentAllocationDto
{
    public Guid Id { get; set; }
    public Guid VendorPaymentId { get; set; }
    public Guid VendorInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public decimal PaymentCurrencyAmount { get; set; }
    public string InvoiceCurrencyCode { get; set; } = string.Empty;
    public string PaymentCurrencyCode { get; set; } = string.Empty;
    public bool IsCrossCurrency { get; set; }
    public Guid? InvoiceSettlementExchangeRateId { get; set; }
    public decimal InvoiceSettlementExchangeRate { get; set; }
    public Guid? PaymentExchangeRateId { get; set; }
    public decimal PaymentExchangeRate { get; set; }
    public decimal PaymentFunctionalAmount { get; set; }
    public decimal SettlementFunctionalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountFunctionalAmount { get; set; }
    public decimal WithholdingTaxAmount { get; set; }
    public decimal WithholdingTaxFunctionalAmount { get; set; }
    public DateTime AllocationDate { get; set; }
    public string? Notes { get; set; }
    public bool IsReversal { get; set; }
    public Guid? PaymentReadinessControlEventId { get; set; }
    public string? PaymentReadinessSnapshotHash { get; set; }
    public DateTime? PaymentReadinessEvaluatedAtUtc { get; set; }
}

public class VendorPaymentAllocationCreateDto
{
    [Required]
    public Guid VendorInvoiceId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal AllocatedAmount { get; set; }

    /// <summary>
    /// Payment/advance-lot amount to consume in payment currency. AllocatedAmount always remains
    /// the invoice-currency reduction. Same-currency callers may omit this value; cross-currency
    /// callers must state both native amounts so Finance never invents a commercial conversion.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? PaymentCurrencyAmount { get; set; }

    /// <summary>
    /// Optional approved invoice-currency rate for the settlement/application date. If omitted,
    /// Finance resolves the active approved daily mid-rate. A posted advance retains its separate
    /// origin rate, allowing application-time realized FX to remain reproducible and auditable.
    /// </summary>
    public Guid? InvoiceSettlementExchangeRateId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; }
    [Range(0, double.MaxValue)]
    public decimal WithholdingTaxAmount { get; set; }
    public string? Notes { get; set; }
}

public class VendorPaymentAllocationResultDto
{
    public Guid PaymentId { get; set; }
    public decimal TotalAllocated { get; set; }
    public decimal RemainingUnallocated { get; set; }
    public List<VendorPaymentAllocationDto> Allocations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class OutstandingVendorInvoiceDto
{
    public bool? ApplySupplierWithholdingDefaults { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public decimal WithholdingTaxRate { get; set; }
    public decimal? WithholdingTaxRateOverride { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? SupplierInvoiceNumber { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    /// <summary>
    /// Currency in which the payable balance is denominated. Payment-entry clients must use
    /// this value instead of assuming that every outstanding invoice shares the bank currency.
    /// </summary>
    public string CurrencyCode { get; set; } = "GHS";
    public int DaysOverdue { get; set; }
    public decimal? EarlyPaymentDiscountPercentage { get; set; }
    public DateTime? EarlyPaymentDiscountDueDate { get; set; }
    public bool IsDiscountAvailable { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? WithholdingContractReference { get; set; }
    public WhtSupplyCategory? WithholdingSupplyCategory { get; set; }
    public VendorPaymentInvoiceReadinessDto? PaymentReadiness { get; set; }
}

/// <summary>
/// Authoritative, read-only view of whether one invoice can participate in a
/// direct allocation, supplier-advance application, posting, or payment batch.
/// </summary>
public class VendorPaymentInvoiceReadinessDto
{
    public Guid VendorInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public VendorInvoiceStatus InvoiceStatus { get; set; }
    public decimal OutstandingAmount { get; set; }
    public bool IsPaymentReady { get; set; }
    public bool InvoiceStateReady { get; set; }
    public bool ThreeWayMatchRequired { get; set; }
    public bool ThreeWayMatchReady { get; set; }
    public bool ReceiptInspectionReady { get; set; }
    public bool ApprovedExceptionApplied { get; set; }
    public bool PersistedMatchCurrent { get; set; }
    public Guid? MatchingControlEventId { get; set; }
    public Guid? MatchExceptionControlEventId { get; set; }
    public string? MatchSnapshotHash { get; set; }
    public Guid? PaymentReadinessControlEventId { get; set; }
    public string SnapshotHash { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ConfigurationProfileCode { get; set; }
    public int? ConfigurationProfileVersion { get; set; }
    public List<string> DecisionKeys { get; set; } = new();
    public List<InvoiceMatchingCheckDto> Checks { get; set; } = new();
}

public class EarlyPaymentDiscountResultDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal InvoiceBalance { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetPayableAmount { get; set; }
    public DateTime DiscountDueDate { get; set; }
    public bool IsEligible { get; set; }
    public int DaysUntilExpiry { get; set; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  PAYMENT BATCH DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Payment Batch

public class PaymentBatchDto
{
    public Guid Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime BatchDate { get; set; }
    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }
    public decimal TotalAmount { get; set; }
    public int PaymentCount { get; set; }
    public VendorPaymentMethod PaymentMethod { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? PaymentMethodName { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? BankAccountName { get; set; }
    public PaymentBatchStatus Status { get; set; }
    public bool ApprovalRequired { get; set; } = true;
    public Guid? CreatedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid? InvoicePaymentSodControlEventId { get; set; }
    public Guid? ProcessedById { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PaymentBatchItemDto> Items { get; set; } = new();
}

public class PaymentBatchCreateDto
{
    public string? Description { get; set; }

    [Required]
    public DateTime BatchDate { get; set; }

    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }

    public VendorPaymentMethod PaymentMethod { get; set; } = VendorPaymentMethod.BankTransfer;
    public Guid? PaymentMethodId { get; set; }
    public Guid? BankAccountId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// IDs of approved vendor invoices to create payments for.
    /// </summary>
    [Required]
    public List<Guid> InvoiceIds { get; set; } = new();
}

public class PaymentBatchQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public PaymentBatchStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

public class PaymentBatchItemDto
{
    public Guid Id { get; set; }
    public Guid VendorPaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ItemStatus { get; set; } = "Pending";
    public string? FailureReason { get; set; }
    public List<PaymentBatchInvoiceDto> Invoices { get; set; } = new();
}

public class PaymentBatchInvoiceDto
{
    public Guid Id { get; set; }
    public Guid VendorInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending";
    public string? FailureReason { get; set; }
    public Guid? PaymentReadinessControlEventId { get; set; }
    public string? PaymentReadinessSnapshotHash { get; set; }
    public DateTime? PaymentReadinessEvaluatedAtUtc { get; set; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  AP REPORT DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region AP Reports

public class ApAgingReportDto
{
    public DateTime AsOfDate { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public bool UsesSettlementReadModel { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal Current { get; set; }         // 0-30 days
    public decimal ThirtyDays { get; set; }      // 31-60 days
    public decimal SixtyDays { get; set; }       // 61-90 days
    public decimal NinetyPlusDays { get; set; }  // 90+ days
    public int TotalSuppliers { get; set; }
    public int TotalInvoices { get; set; }
    public List<SupplierAgingDetailDto> SupplierDetails { get; set; } = new();
    public List<SubledgerSettlementDiagnosticDto> Diagnostics { get; set; } = new();
}

public class SupplierAgingDetailDto
{
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierCode { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal Current { get; set; }
    public decimal ThirtyDays { get; set; }
    public decimal SixtyDays { get; set; }
    public decimal NinetyPlusDays { get; set; }
    public int InvoiceCount { get; set; }
    public DateTime? OldestInvoiceDate { get; set; }
    public List<ApAgingInvoiceDto> Invoices { get; set; } = new();
}

public class ApAgingInvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public decimal CreditedAmount { get; set; }
    public decimal WithheldAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public string DocumentCurrencyCode { get; set; } = "GHS";
    public decimal DocumentTotalAmount { get; set; }
    public decimal DocumentSettledAmount { get; set; }
    public decimal DocumentCreditedAmount { get; set; }
    public decimal DocumentWithheldAmount { get; set; }
    public decimal DocumentBalanceAmount { get; set; }
    public Guid? SourcePostingEventId { get; set; }
    public Guid? SourceJournalEntryId { get; set; }
    public string? SettlementStatus { get; set; }
    public string? DiagnosticFlags { get; set; }
    public int DaysOutstanding { get; set; }
    public string AgingBucket { get; set; } = string.Empty;
}

public class CashRequirementForecastDto
{
    public DateTime AsOfDate { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal TotalPayable { get; set; }
    public decimal OverdueAmount { get; set; }
    public List<CashRequirementPeriodDto> Periods { get; set; } = new();
}

public class CashRequirementPeriodDto
{
    public string Period { get; set; } = string.Empty; // "This Week", "Next Week", "Next 30 Days", etc.
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal AmountDue { get; set; }
    public int InvoiceCount { get; set; }
    public decimal DiscountAvailable { get; set; }
}

public class SupplierStatementDto
{
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierCode { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal OpeningBalance { get; set; }
    public decimal TotalInvoices { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<SupplierStatementLineDto> Lines { get; set; } = new();
}

public class SupplierStatementLineDto
{
    public DateTime Date { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string? Reference { get; set; }
    /// <summary>
    /// Debit movement in the AP control-account convention. Payments, discounts, WHT and
    /// supplier credits reduce the payable through this column.
    /// </summary>
    public decimal Debit { get; set; }
    /// <summary>
    /// Credit movement in the AP control-account convention. Supplier invoices and other
    /// liability-increasing adjustments appear in this column.
    /// </summary>
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public class SupplierDetailedLedgerReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public bool ShowSupplierCurrency { get; set; }
    public decimal TotalOpeningBalance { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal TotalClosingBalance { get; set; }
    public List<DetailedLedgerCurrencyTotalDto> CurrencyTotals { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<SupplierDetailedLedgerAccountDto> Suppliers { get; set; } = new();
}

public class SupplierDetailedLedgerAccountDto
{
    public Guid BusinessPartnerId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "GHS";
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<SupplierDetailedLedgerLineDto> Lines { get; set; } = new();
}

public class SupplierDetailedLedgerLineDto
{
    public Guid SourceDocumentId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string Description { get; set; } = string.Empty;
    public string TransactionCurrencyCode { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public class WithholdingTaxSummaryDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal TotalWithheld { get; set; }
    public int SupplierCount { get; set; }
    public int TransactionCount { get; set; }
    public List<WithholdingTaxBySupplierDto> BySupplier { get; set; } = new();
}

public class WithholdingTaxBySupplierDto
{
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public decimal TotalInvoiceAmount { get; set; }
    public decimal TotalWithholdingTax { get; set; }
    public decimal TotalNetPayment { get; set; }
    public int TransactionCount { get; set; }
}

public class ApSummaryDto
{
    public decimal TotalOutstanding { get; set; }
    public decimal TotalOverdue { get; set; }
    public int OutstandingInvoiceCount { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public decimal AverageDaysToPayment { get; set; }
    public decimal TotalPaidThisMonth { get; set; }
    public decimal DiscountsTaken { get; set; }
    public decimal DiscountsMissed { get; set; }
    public decimal WithholdingTaxThisMonth { get; set; }
    public int PendingApprovalCount { get; set; }
    public int PendingBatchCount { get; set; }
}

/// <summary>
/// Read-only AP-005/TDC-0508 reconciliation over authoritative Procurement,
/// Projects, AP, and central Finance posting records. Amounts are never
/// converted or combined across currencies.
/// </summary>
public sealed class ProcurementFinanceReconciliationReportDto
{
    public DateTime AsOfDate { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string RuleCode { get; set; } = "AP-005";
    public string TaskCode { get; set; } = "TDC-0508";
    public IReadOnlyList<string> DecisionKeys { get; set; } = Array.Empty<string>();
    public bool IsReconciled { get; set; }
    public int PurchaseOrderCount { get; set; }
    public int IssueCount { get; set; }
    public int UnbalancedPostingCount { get; set; }
    public int ControlledReversalCount { get; set; }
    public SubledgerControlReconciliationDto ApControlReconciliation { get; set; } = new();
    public IReadOnlyList<ProcurementFinanceReconciliationCurrencySummaryDto> CurrencySummaries { get; set; } =
        Array.Empty<ProcurementFinanceReconciliationCurrencySummaryDto>();
    public IReadOnlyList<ProcurementFinanceReconciliationRowDto> Rows { get; set; } =
        Array.Empty<ProcurementFinanceReconciliationRowDto>();
}

public sealed class ProcurementFinanceReconciliationCurrencySummaryDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal PurchaseOrderAmount { get; set; }
    public decimal CommitmentAmount { get; set; }
    public decimal AcceptedReceiptAmount { get; set; }
    public decimal InvoiceAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public decimal InvoicePostedAmount { get; set; }
    public decimal PaymentPostedAmount { get; set; }
    public decimal RetentionHeldAmount { get; set; }
    public decimal RetentionReleasedAmount { get; set; }
    public decimal MilestoneAmount { get; set; }
}

public sealed class ProcurementFinanceReconciliationRowDto
{
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string PurchaseOrderStatus { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public Guid? SourceRequisitionId { get; set; }
    public Guid? ContractId { get; set; }
    public decimal PurchaseOrderAmount { get; set; }
    public decimal CommitmentAmount { get; set; }
    public decimal CommitmentGroupOrderAmount { get; set; }
    public decimal AcceptedReceiptAmount { get; set; }
    public decimal InvoiceAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public decimal InvoicePostedAmount { get; set; }
    public decimal PaymentPostedAmount { get; set; }
    public decimal RetentionHeldAmount { get; set; }
    public decimal RetentionReleasedAmount { get; set; }
    public decimal RetentionOutstandingAmount { get; set; }
    public decimal MilestoneAmount { get; set; }
    public decimal CompletedMilestoneAmount { get; set; }
    public decimal InvoicedMilestoneAmount { get; set; }
    public decimal PaidMilestoneAmount { get; set; }
    public int InvoiceCount { get; set; }
    public int PaymentCount { get; set; }
    public int PostingCount { get; set; }
    public int ControlledReversalCount { get; set; }
    public bool IsReconciled { get; set; }
    public IReadOnlyList<ProcurementFinanceReconciliationIssueDto> Issues { get; set; } =
        Array.Empty<ProcurementFinanceReconciliationIssueDto>();
}

public sealed class ProcurementFinanceReconciliationIssueDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
    public string Area { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal? ExpectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public decimal? VarianceAmount { get; set; }
    public Guid? SourceDocumentId { get; set; }
}

#endregion

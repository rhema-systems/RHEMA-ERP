using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class WhtCertificateQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
}

public sealed class GenerateWhtCertificateDto
{
    public string? CertificateNumber { get; set; }
    public DateTime? CertificateDate { get; set; }
}

public sealed class ReissueWhtCertificateDto
{
    public DateTime? CertificateDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class CancelWhtCertificateDto
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class WhtCertificateDto
{
    public Guid? CertificateId { get; set; }
    public Guid VendorPaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public VendorPaymentStatus PaymentStatus { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierTin { get; set; }
    public DateTime PaymentDate { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public Guid? TaxId { get; set; }
    public string? TaxCode { get; set; }
    public string? TaxName { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal WithholdingAmount { get; set; }
    public decimal NetPaidAmount { get; set; }
    public string? TaxAccountNumber { get; set; }
    public string? TaxAccountName { get; set; }
    public string? CertificateNumber { get; set; }
    public DateTime? CertificateDate { get; set; }
    public string CertificateStatus { get; set; } = "Missing";
    public int VersionNumber { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public string? IssuedByName { get; set; }
    public string? LifecycleReason { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? RemittanceId { get; set; }
    public string? RemittanceNumber { get; set; }
    public string RemittanceStatus { get; set; } = "Unremitted";
    public List<WhtCertificateVersionDto> Versions { get; set; } = new();
}

public sealed class WhtCertificateVersionDto
{
    public Guid CertificateId { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public string IssuedByName { get; set; } = string.Empty;
    public string? LifecycleReason { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
}

public sealed class WhtCalculationRequestDto
{
    public Guid TaxId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal TaxableBase { get; set; }
    public Guid? ExcludeVendorPaymentId { get; set; }
    // The server resolves the persisted transaction decision/rate; clients cannot supply a free-form payment override.
    public List<Guid> VendorInvoiceIds { get; set; } = new();
    public string? ContractReference { get; set; }
    public WhtSupplyCategory? SupplyCategory { get; set; }
    /// <summary>Invoice-native gross liability settled; the server derives VAT-exclusive bases.</summary>
    public List<WhtInvoiceSettlementDto> InvoiceSettlements { get; set; } = new();
}

public sealed class WhtInvoiceSettlementDto
{
    public Guid VendorInvoiceId { get; set; }
    public decimal GrossSettlementAmount { get; set; }
    public Guid? ExchangeRateId { get; set; }
}

public sealed class WhtCalculationResultDto
{
    public Guid TaxId { get; set; }
    public string TaxCode { get; set; } = string.Empty;
    public string TaxName { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal CumulativeBefore { get; set; }
    public decimal CurrentPaymentTaxableBase { get; set; }
    public decimal CatchUpTaxableBase { get; set; }
    public decimal CatchUpWithholdingAmount { get; set; }
    public decimal CumulativeAfter { get; set; }
    public decimal? ThresholdAmount { get; set; }
    public decimal RemainingBeforeThreshold { get; set; }
    public bool ThresholdApplied { get; set; }
    public decimal WithholdingAmount { get; set; }
    public Guid? TaxPayableAccountId { get; set; }
    public string CalculationNote { get; set; } = string.Empty;
    public string ContractReference { get; set; } = string.Empty;
    public WhtSupplyCategory SupplyCategory { get; set; }
    public DateTime StatutoryPeriodStart { get; set; }
    public DateTime StatutoryPeriodEnd { get; set; }
}

public sealed class WhtRemittanceQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
    public string? SearchTerm { get; set; }
}

public sealed class CreateWhtRemittanceDto
{
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public DateTime? DueDate { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public List<Guid> VendorPaymentIds { get; set; } = new();
    public string? Notes { get; set; }
}

public sealed class SubmitWhtRemittanceDto
{
    public string SubmissionReference { get; set; } = string.Empty;
}

public sealed class PayWhtRemittanceDto
{
    public DateTime PaymentDate { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public string? AuthorityReceiptReference { get; set; }
}

public sealed class CancelWhtRemittanceDto
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class WhtRemittanceDto
{
    public Guid Id { get; set; }
    public string RemittanceNumber { get; set; } = string.Empty;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public DateTime DueDate { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public string Status { get; set; } = string.Empty;
    public decimal TotalWithholdingAmount { get; set; }
    public int LineCount { get; set; }
    public string? SubmissionReference { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? SubmittedByName { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? AuthorityReceiptReference { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? PaidByName { get; set; }
    public string? Notes { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public List<WhtRemittanceLineDto> Lines { get; set; } = new();
}

public sealed class WhtRemittanceLineDto
{
    public Guid Id { get; set; }
    public Guid VendorPaymentId { get; set; }
    public Guid? CertificateId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierTin { get; set; }
    public string? TaxCode { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal WithholdingAmount { get; set; }
    public Guid? JournalEntryId { get; set; }
}

public sealed class WhtRemittanceLiabilityDto
{
    public Guid VendorPaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierTin { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public string? TaxCode { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal WithholdingAmount { get; set; }
    public Guid? CertificateId { get; set; }
    public string? CertificateNumber { get; set; }
    public Guid? JournalEntryId { get; set; }
}

public sealed class WhtRegisterExportDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "text/csv; charset=utf-8";
    public string FileName { get; set; } = string.Empty;
}

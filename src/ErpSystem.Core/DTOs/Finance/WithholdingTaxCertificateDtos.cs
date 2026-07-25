using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class WhtCertificateQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? SupplierId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
}

public sealed class GenerateWhtCertificateDto
{
    public string? CertificateNumber { get; set; }
    public DateTime? CertificateDate { get; set; }
}

public sealed class WhtCertificateDto
{
    public Guid VendorPaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public VendorPaymentStatus PaymentStatus { get; set; }
    public Guid SupplierId { get; set; }
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
    public Guid? JournalEntryId { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Controlled lifecycle of a supplier WHT certificate. A certificate is never edited after
/// issue; reissue creates a new version and cancellation retains the issued snapshot.
/// </summary>
public enum WhtCertificateStatus
{
    Issued = 1,
    Superseded = 2,
    Cancelled = 3
}

/// <summary>
/// Finance-owned evidence status for a WHT remittance batch. The batch records statutory
/// submission/payment evidence; it deliberately does not create a second cash or GL posting
/// path because settlement must continue through the existing Finance payment journals.
/// </summary>
public enum WhtRemittanceStatus
{
    Draft = 1,
    Submitted = 2,
    Paid = 3,
    Cancelled = 4
}

/// <summary>
/// Immutable issued-certificate version backed by a posted AP payment. Snapshot fields make
/// the evidence reproducible even if a supplier name or tax configuration is later amended.
/// </summary>
[Table("WithholdingTaxCertificates")]
public sealed class WithholdingTaxCertificate : TenantEntity
{
    [Required]
    public Guid VendorPaymentId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CertificateNumber { get; set; } = string.Empty;

    public int VersionNumber { get; set; } = 1;

    public WhtCertificateStatus Status { get; set; } = WhtCertificateStatus.Issued;

    [Column(TypeName = "date")]
    public DateTime IssueDate { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    [Required]
    [MaxLength(200)]
    public string IssuedByName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? IssuedByUserId { get; set; }

    public Guid? SupersedesCertificateId { get; set; }

    public Guid? SupersededByCertificateId { get; set; }

    [MaxLength(1000)]
    public string? LifecycleReason { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    [MaxLength(200)]
    public string? CancelledByName { get; set; }

    [MaxLength(100)]
    public string? CancelledByUserId { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    // The fields below are the statutory/audit snapshot. Do not replace them with live
    // navigation values when rendering a previously issued certificate.
    [Required]
    [MaxLength(50)]
    public string PaymentNumber { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    [Required]
    [MaxLength(300)]
    public string SupplierName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SupplierTin { get; set; }

    [Column(TypeName = "date")]
    public DateTime PaymentDate { get; set; }

    [Required]
    [MaxLength(8)]
    public string CurrencyCode { get; set; } = "GHS";

    public Guid? TaxId { get; set; }

    [MaxLength(50)]
    public string? TaxCode { get; set; }

    [MaxLength(200)]
    public string? TaxName { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxableBase { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetPaidAmount { get; set; }

    public Guid? TaxAccountId { get; set; }

    [MaxLength(100)]
    public string? TaxAccountNumber { get; set; }

    [MaxLength(200)]
    public string? TaxAccountName { get; set; }

    public Guid? JournalEntryId { get; set; }

    /// <summary>
    /// Optimistic-concurrency token prevents two lifecycle decisions from silently replacing
    /// one another when accountants act on the same certificate at the same time.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [ForeignKey(nameof(VendorPaymentId))]
    public VendorPayment VendorPayment { get; set; } = null!;

    [ForeignKey(nameof(SupersedesCertificateId))]
    public WithholdingTaxCertificate? SupersedesCertificate { get; set; }

    [ForeignKey(nameof(SupersededByCertificateId))]
    public WithholdingTaxCertificate? SupersededByCertificate { get; set; }
}

/// <summary>
/// Finance compliance register for WHT liabilities submitted and paid to the tax authority.
/// A remittance groups immutable posted-payment snapshots while retaining the original payment
/// and journal links for drill-down and reconciliation.
/// </summary>
[Table("WithholdingTaxRemittances")]
public sealed class WithholdingTaxRemittance : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string RemittanceNumber { get; set; } = string.Empty;

    [Column(TypeName = "date")]
    public DateTime PeriodFrom { get; set; }

    [Column(TypeName = "date")]
    public DateTime PeriodTo { get; set; }

    [Column(TypeName = "date")]
    public DateTime DueDate { get; set; }

    [Required]
    [MaxLength(8)]
    public string CurrencyCode { get; set; } = "GHS";

    public WhtRemittanceStatus Status { get; set; } = WhtRemittanceStatus.Draft;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalWithholdingAmount { get; set; }

    [MaxLength(100)]
    public string? SubmissionReference { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    [MaxLength(200)]
    public string? SubmittedByName { get; set; }

    [MaxLength(100)]
    public string? SubmittedByUserId { get; set; }

    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    [Column(TypeName = "date")]
    public DateTime? PaymentDate { get; set; }

    [MaxLength(100)]
    public string? AuthorityReceiptReference { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    [MaxLength(200)]
    public string? PaidByName { get; set; }

    [MaxLength(100)]
    public string? PaidByUserId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    [MaxLength(200)]
    public string? CancelledByName { get; set; }

    [MaxLength(100)]
    public string? CancelledByUserId { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<WithholdingTaxRemittanceLine> Lines { get; set; } = new List<WithholdingTaxRemittanceLine>();
}

/// <summary>
/// Immutable liability snapshot assigned to one active remittance. Cancellation releases the
/// payment for a replacement batch without deleting this historical line.
/// </summary>
[Table("WithholdingTaxRemittanceLines")]
public sealed class WithholdingTaxRemittanceLine : TenantEntity
{
    public Guid RemittanceId { get; set; }
    public Guid VendorPaymentId { get; set; }
    public Guid? CertificateId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? TaxId { get; set; }
    public Guid? JournalEntryId { get; set; }

    [Required]
    [MaxLength(50)]
    public string PaymentNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string SupplierName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SupplierTin { get; set; }

    [MaxLength(50)]
    public string? TaxCode { get; set; }

    [Column(TypeName = "date")]
    public DateTime PaymentDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxableBase { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingAmount { get; set; }

    [ForeignKey(nameof(RemittanceId))]
    public WithholdingTaxRemittance Remittance { get; set; } = null!;

    [ForeignKey(nameof(VendorPaymentId))]
    public VendorPayment VendorPayment { get; set; } = null!;

    [ForeignKey(nameof(CertificateId))]
    public WithholdingTaxCertificate? Certificate { get; set; }
}

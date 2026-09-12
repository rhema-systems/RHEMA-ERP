using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.DocumentManagement;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Immutable payment-owned attachment; a clean upload is not a human approval.</summary>
public sealed class VendorPaymentEvidenceLink : TenantEntity
{
    public Guid VendorPaymentId { get; set; }
    [MaxLength(150)] public string RequirementKey { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    [MaxLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [MaxLength(255)] public string FileName { get; set; } = string.Empty;
    [MaxLength(200)] public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [MaxLength(64)] public string ChecksumSha256 { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public VendorPayment VendorPayment { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

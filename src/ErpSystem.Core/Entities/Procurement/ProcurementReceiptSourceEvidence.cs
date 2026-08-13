using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Immutable, centrally governed supplier document attached to a purchase-order receipt.
/// Replacements create a new row and supersede the previous current row.
/// </summary>
public sealed class ProcurementReceiptSourceEvidence : TenantEntity
{
    public Guid PurchaseOrderReceiptId { get; set; }
    public ProcurementReceiptSourceEvidenceKind EvidenceKind { get; set; }

    [Required, MaxLength(100)]
    public string ReferenceNumber { get; set; } = string.Empty;

    public DateTime DocumentDate { get; set; }
    public bool IsCurrent { get; set; } = true;
    public Guid ClientRequestId { get; set; }

    [Required, MaxLength(64)]
    public string RequestHash { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [Required, MaxLength(64)]
    public string ChecksumSha256 { get; set; } = string.Empty;

    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public DateTime? SupersededAtUtc { get; set; }
    public Guid? SupersededByEvidenceId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public PurchaseOrderReceipt PurchaseOrderReceipt { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementReceiptDocuments")]
public sealed class ProcurementReceiptDocument : TenantEntity
{
    public Guid PurchaseOrderReceiptId { get; set; }
    public Guid? GoodsReceiptNoteId { get; set; }
    public ProcurementReceiptDocumentKind DocumentKind { get; set; }
    [Required, StringLength(100)] public string DocumentNumber { get; set; } = string.Empty;
    [Required, StringLength(80)] public string TemplateCode { get; set; } = string.Empty;
    public ProcurementReceiptDocumentStatus Status { get; set; } = ProcurementReceiptDocumentStatus.Draft;
    public ProcurementReceiptDocumentReconciliationStatus ReconciliationStatus { get; set; } =
        ProcurementReceiptDocumentReconciliationStatus.Pending;
    [StringLength(1000)] public string? ReconciliationMessage { get; set; }
    public DateTime? ReconciledAtUtc { get; set; }

    public Guid ConfigurationProfileId { get; set; }
    public int ConfigurationProfileVersion { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string DecisionKeysJson { get; set; } = "[]";
    [Required, Column(TypeName = "nvarchar(max)")] public string DecisionSnapshotJson { get; set; } = "{}";
    [Required, Column(TypeName = "nvarchar(max)")] public string SourceSnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string SourceIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;

    public Guid PreparedByUserId { get; set; }
    [Required, StringLength(300)] public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedAtUtc { get; set; }
    public Guid? IssuedByUserId { get; set; }
    [StringLength(300)] public string? IssuedByName { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public Guid? CancelledByUserId { get; set; }
    [StringLength(300)] public string? CancelledByName { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    [StringLength(1000)] public string? CancellationReason { get; set; }

    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public PurchaseOrderReceipt PurchaseOrderReceipt { get; set; } = null!;
    public GoodsReceiptNote? GoodsReceiptNote { get; set; }
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
    public ICollection<ProcurementReceiptDocumentSignature> Signatures { get; set; } =
        new List<ProcurementReceiptDocumentSignature>();
    public ICollection<ProcurementReceiptDocumentAction> Actions { get; set; } =
        new List<ProcurementReceiptDocumentAction>();
}

[Table("ProcurementReceiptDocumentSignatures")]
public sealed class ProcurementReceiptDocumentSignature : TenantEntity
{
    public Guid ReceiptDocumentId { get; set; }
    [Required, StringLength(200)] public string RequiredRole { get; set; } = string.Empty;
    public Guid SignedByUserId { get; set; }
    [Required, StringLength(300)] public string SignedByName { get; set; } = string.Empty;
    public DateTime SignedAtUtc { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementReceiptDocument ReceiptDocument { get; set; } = null!;
}

[Table("ProcurementReceiptDocumentActions")]
public sealed class ProcurementReceiptDocumentAction : TenantEntity
{
    public Guid ReceiptDocumentId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(100)] public string FromStatus { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ToStatus { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string DetailsJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementReceiptDocument ReceiptDocument { get; set; } = null!;
}

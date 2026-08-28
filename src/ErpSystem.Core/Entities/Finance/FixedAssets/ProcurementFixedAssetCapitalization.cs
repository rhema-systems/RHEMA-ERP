using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance.FixedAssets;

/// <summary>
/// Immutable accounting handoff between Procurement's accepted-supply evidence and Finance's
/// fixed-asset register.  The record snapshots the producer evidence used by Finance, but never
/// changes purchase-order, receipt, inspection or inventory workflow state.
/// </summary>
public sealed class ProcurementFixedAssetCapitalization : TenantEntity
{
    public Guid FixedAssetId { get; set; }
    public FixedAsset FixedAsset { get; set; } = null!;

    public ProcurementAcceptedSupplyKind AcceptedSupplyKind { get; set; }
    public Guid AcceptedSupplySourceId { get; set; }

    [Required, MaxLength(100)]
    public string AcceptedSupplyReference { get; set; } = string.Empty;

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public Guid PurchaseOrderItemId { get; set; }
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;

    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal CapitalizedQuantity { get; set; }

    [Required, MaxLength(3)]
    public string SourceCurrencyCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SourceTransactionAmount { get; set; }

    [Required, MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal FunctionalAmount { get; set; }

    [Required, MaxLength(64)]
    public string SourceIntegrityHash { get; set; } = string.Empty;

    [Required, Column(TypeName = "nvarchar(max)")]
    public string SourceSnapshotJson { get; set; } = "{}";

    /// <summary>
    /// Finance-side proof identifying the inventory movements and posted receipt journals whose
    /// carrying value was reclassified.  JSON is intentional because one accepted PO line may be
    /// evidenced by several partial receipts, movements and posting events.
    /// </summary>
    [Required, Column(TypeName = "nvarchar(max)")]
    public string ReceiptPostingEvidenceJson { get; set; } = "{}";

    public ProcurementFixedAssetCapitalizationStatus Status { get; set; } =
        ProcurementFixedAssetCapitalizationStatus.Draft;

    [Required, MaxLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime CapitalizationDate { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public DateTime? ReversedAt { get; set; }

    [MaxLength(2000)]
    public string? FailureReason { get; set; }
}

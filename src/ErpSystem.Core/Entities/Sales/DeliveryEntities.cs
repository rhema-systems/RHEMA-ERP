using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Sales;

#region Delivery Note

/// <summary>
/// Goods dispatch / delivery document against a Sales Order.
/// Confirms delivery of goods to the customer and triggers stock deduction.
/// </summary>
public class DeliveryNote : DocumentEntity
{
    // ── Sales Order Link ────────────────────────────────────────────────

    [Required]
    public Guid SalesOrderId { get; set; }
    public virtual SalesOrder SalesOrder { get; set; } = null!;

    // ── Customer ────────────────────────────────────────────────────────

    [Required]
    public Guid BusinessPartnerId { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    // ── Delivery Status ─────────────────────────────────────────────────

    public DeliveryNoteStatus DeliveryStatus { get; set; } = DeliveryNoteStatus.Draft;

    // ── Shipping Details ────────────────────────────────────────────────

    public ShipmentMethod ShipmentMethod { get; set; } = Enums.ShipmentMethod.CompanyDelivery;

    [MaxLength(500)]
    public string? ShippingAddress { get; set; }

    [MaxLength(200)]
    public string? CarrierName { get; set; }

    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    public DateTime? ShippedDate { get; set; }

    public DateTime? DeliveredDate { get; set; }

    // ── Warehouse Source ────────────────────────────────────────────────

    public Guid? WarehouseId { get; set; }
    public virtual Warehouse? Warehouse { get; set; }

    // ── Personnel ───────────────────────────────────────────────────────

    public Guid? PackedById { get; set; }
    public virtual ApplicationUser? PackedBy { get; set; }

    public Guid? ShippedById { get; set; }
    public virtual ApplicationUser? ShippedBy { get; set; }

    public Guid? ReceivedById { get; set; }
    public virtual ApplicationUser? ReceivedBy { get; set; }

    // ── Receiver Confirmation ───────────────────────────────────────────

    [MaxLength(200)]
    public string? ReceiverName { get; set; }

    [MaxLength(500)]
    public string? ReceiverSignaturePath { get; set; }

    [MaxLength(1000)]
    public string? DeliveryNotes { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // ── Navigation Properties ───────────────────────────────────────────

    public virtual ICollection<DeliveryNoteLine> Lines { get; set; } = new List<DeliveryNoteLine>();
}

#endregion

#region Delivery Note Line

/// <summary>
/// Individual line item on a Delivery Note.
/// Links back to the Sales Order Line for quantity tracking.
/// </summary>
public class DeliveryNoteLine : BaseEntity
{
    [Required]
    public Guid DeliveryNoteId { get; set; }
    public virtual DeliveryNote DeliveryNote { get; set; } = null!;

    [Required]
    public Guid SalesOrderLineId { get; set; }
    public virtual SalesOrderLine SalesOrderLine { get; set; } = null!;

    public int LineNumber { get; set; } = 1;

    // ── Product Reference ───────────────────────────────────────────────

    public Guid? InventoryItemId { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ProductCode { get; set; }

    // ── Quantities ──────────────────────────────────────────────────────

    [Column(TypeName = "decimal(18,4)")]
    public decimal DispatchedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DeliveredQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DamagedQuantity { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    // ── Stock Tracking ──────────────────────────────────────────────────

    public Guid? WarehouseId { get; set; }
    public virtual Warehouse? Warehouse { get; set; }

    public Guid? LocationId { get; set; }
    public virtual WarehouseLocation? Location { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Whether inventory has been deducted for this line
    /// </summary>
    public bool IsStockDeducted { get; set; } = false;

    // ── Notes ────────────────────────────────────────────────────────────

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? DamageNotes { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

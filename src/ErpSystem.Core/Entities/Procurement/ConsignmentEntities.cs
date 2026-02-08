using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Settlement record created when consignment stock is consumed/issued out of a consignment warehouse.
/// This is the basis for supplier invoicing (pure consignment = invoice on consumption).
/// </summary>
public class ConsignmentSettlement : TenantEntity
{
    [Required]
    public Guid StockMovementId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    public Guid? LocationId { get; set; }

    [Required]
    [MaxLength(50)]
    public string MovementType { get; set; } = string.Empty; // Issue, TransferOut, Sale, Consumption

    /// <summary>
    /// Always stored as a positive quantity.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalValue { get; set; }

    public DateTime ConsumedAt { get; set; } = DateTime.UtcNow;

    public ReferenceType ReferenceType { get; set; } = ReferenceType.Manual;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public Guid? ReferenceId { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "PendingInvoice"; // PendingInvoice, Invoiced, Cancelled

    public DateTime? InvoicedAt { get; set; }

    public Guid? InvoicedById { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation
    public virtual StockMovement StockMovement { get; set; } = null!;
}

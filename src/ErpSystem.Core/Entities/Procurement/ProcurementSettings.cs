using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Procurement module settings for tenant-specific configuration
/// </summary>
public class ProcurementSettings : TenantEntity
{
    [MaxLength(30)]
    public string? PurchasePriceDifferenceHandling { get; set; }
    /// <summary>Actor separation for the procurement transaction chain. Other ERP modules retain their own controls.</summary>
    public bool EnforceSegregationOfDuties { get; set; } = true;

    /// <summary>Close published tenders at their UTC submission deadline; statutory opening remains a separate controlled action.</summary>
    public bool AutoCloseTenders { get; set; } = false;

    // Item Creation Settings
    /// <summary>
    /// When enabled, prompt users to create inventory items for new item names
    /// </summary>
    public bool AutoCreateInventoryItems { get; set; } = false;

    /// <summary>
    /// When enabled (requires AutoCreateInventoryItems), also create supplier catalog entries
    /// </summary>
    public bool AutoCreateSupplierItems { get; set; } = false;

    /// <summary>
    /// When enabled, allow PO items without inventory tracking (InventoryItemId = null)
    /// </summary>
    public bool AllowNonInventoryItems { get; set; } = true;

    // Default Values for New Items
    /// <summary>
    /// Default category for newly created inventory items
    /// </summary>
    public Guid? DefaultItemCategoryId { get; set; }

    /// <summary>
    /// Default unit of measure for newly created inventory items
    /// </summary>
    public Guid? DefaultUnitOfMeasureId { get; set; }

    /// <summary>
    /// Default valuation method for newly created inventory items (FIFO, WAC, Standard, LIFO)
    /// </summary>
    [MaxLength(20)]
    public string? DefaultValuationMethod { get; set; } = "FIFO";

    // Purchase Order Settings
    /// <summary>
    /// Number format for purchase requisitions (e.g., "PR-{YYYY}-{####}")
    /// </summary>
    [MaxLength(100)]
    public string? PurchaseRequisitionNumberFormat { get; set; } = "PR-{YYYY}-{####}";

    /// <summary>
    /// Number format for purchase orders (e.g., "PO-{YYYY}-{####}")
    /// </summary>
    [MaxLength(100)]
    public string? PurchaseOrderNumberFormat { get; set; } = "PO-{YYYY}-{####}";

    /// <summary>
    /// Number format for purchase order receipts / GRNs (e.g., "REC{YY}{####}" or "REC-{YY}-{####}")
    /// </summary>
    [MaxLength(100)]
    public string? PurchaseOrderReceiptNumberFormat { get; set; } = "REC{YY}{####}";

    /// <summary>
    /// Require approval for all purchase orders
    /// </summary>
    public bool RequireApprovalForPO { get; set; } = true;

    /// <summary>
    /// Auto-approve POs below this threshold amount
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? AutoApprovalThreshold { get; set; }

    /// <summary>
    /// Allow backorders (receiving more than ordered quantity)
    /// </summary>
    public bool AllowBackorders { get; set; } = true;

    /// <summary>
    /// Require delivery date on purchase orders
    /// </summary>
    public bool RequireDeliveryDate { get; set; } = true;

    // Supplier Settings
    /// <summary>
    /// Only allow items from supplier's catalog
    /// </summary>
    public bool EnforceSupplierCatalog { get; set; } = false;

    /// <summary>
    /// Allow multiple suppliers for the same inventory item
    /// </summary>
    public bool AllowMultipleSuppliersPerItem { get; set; } = true;

    // Validation Settings
    /// <summary>
    /// Validate budget availability before creating PO
    /// </summary>
    public bool ValidateBudgetBeforePO { get; set; } = false;

    /// <summary>
    /// Require contract reference for purchase orders
    /// </summary>
    public bool RequireContractForPO { get; set; } = false;

    // Additional Notes
    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    // Note: Category and UoM navigation would be added if cross-module references are allowed
}

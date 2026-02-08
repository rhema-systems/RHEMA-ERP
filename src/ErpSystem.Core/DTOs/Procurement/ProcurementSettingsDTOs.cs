namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// DTO for procurement settings
/// </summary>
public class ProcurementSettingsDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    // Item Creation Settings
    public bool AutoCreateInventoryItems { get; set; }
    public bool AutoCreateSupplierItems { get; set; }
    public bool AllowNonInventoryItems { get; set; }

    // Default Values for New Items
    public Guid? DefaultItemCategoryId { get; set; }
    public Guid? DefaultUnitOfMeasureId { get; set; }
    public string? DefaultValuationMethod { get; set; }

    // Purchase Order Settings
    public string? PurchaseRequisitionNumberFormat { get; set; }
    public string? PurchaseOrderNumberFormat { get; set; }
    public string? PurchaseOrderReceiptNumberFormat { get; set; }
    public bool RequireApprovalForPO { get; set; }
    public decimal? AutoApprovalThreshold { get; set; }
    public bool AllowBackorders { get; set; }
    public bool RequireDeliveryDate { get; set; }

    // Supplier Settings
    public bool EnforceSupplierCatalog { get; set; }
    public bool AllowMultipleSuppliersPerItem { get; set; }

    // Validation Settings
    public bool ValidateBudgetBeforePO { get; set; }
    public bool RequireContractForPO { get; set; }

    // Metadata
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for updating procurement settings
/// </summary>
public class UpdateProcurementSettingsDto
{
    // Item Creation Settings
    public bool AutoCreateInventoryItems { get; set; }
    public bool AutoCreateSupplierItems { get; set; }
    public bool AllowNonInventoryItems { get; set; }

    // Default Values for New Items
    public Guid? DefaultItemCategoryId { get; set; }
    public Guid? DefaultUnitOfMeasureId { get; set; }
    public string? DefaultValuationMethod { get; set; }

    // Purchase Order Settings
    public string? PurchaseRequisitionNumberFormat { get; set; }
    public string? PurchaseOrderNumberFormat { get; set; }
    public string? PurchaseOrderReceiptNumberFormat { get; set; }
    public bool RequireApprovalForPO { get; set; }
    public decimal? AutoApprovalThreshold { get; set; }
    public bool AllowBackorders { get; set; }
    public bool RequireDeliveryDate { get; set; }

    // Supplier Settings
    public bool EnforceSupplierCatalog { get; set; }
    public bool AllowMultipleSuppliersPerItem { get; set; }

    // Validation Settings
    public bool ValidateBudgetBeforePO { get; set; }
    public bool RequireContractForPO { get; set; }

    // Metadata
    public string? Notes { get; set; }
}

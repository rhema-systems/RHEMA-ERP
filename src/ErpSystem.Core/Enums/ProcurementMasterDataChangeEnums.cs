namespace ErpSystem.Core.Enums;

public enum ProcurementMasterDataResourceType
{
    SupplierProfile = 0,
    SupplierBankDetails = 1,
    SupplierTaxDetails = 2,
    InventoryItem = 3,
    InventoryCategory = 4,
    UnitOfMeasure = 5,
    Warehouse = 6,
    WarehouseLocation = 7,
    ProcurementPolicySensitive = 8,
    SupplierOwnershipDetails = 9,
    SupplierCategoryAssignments = 10,
    SupplierComplianceStatus = 11
}

public enum ProcurementMasterDataPolicyStatus
{
    Draft = 0,
    Active = 1,
    Retired = 2
}

public enum ProcurementMasterDataChangeStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Applied = 4,
    Cancelled = 5,
    RevalidationFailed = 6
}

public enum ProcurementMasterDataTargetKind
{
    BusinessPartner = 0,
    LegacySupplier = 1,
    InventoryItem = 2,
    InventoryCategory = 3,
    UnitOfMeasure = 4,
    Warehouse = 5,
    WarehouseLocation = 6,
    ProcurementSettings = 7
}

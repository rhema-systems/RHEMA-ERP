using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public sealed record ProcurementMasterDataResourceDefinition(
    ProcurementMasterDataResourceType ResourceType,
    string Code,
    string Name,
    string Description,
    IReadOnlyDictionary<ProcurementMasterDataTargetKind, IReadOnlySet<string>> TargetFields,
    bool AllowsTenantSingletonTarget = false,
    string SourceRequirements = "TDC-0007");

public static class ProcurementMasterDataResourceRegistry
{
    private static IReadOnlySet<string> Fields(params string[] values) =>
        new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ProcurementMasterDataResourceDefinition> Resources { get; } =
    [
        Define(
            ProcurementMasterDataResourceType.SupplierProfile,
            "SUPPLIER_PROFILE",
            "Supplier profile",
            "Controlled legal, contact, address, classification, commercial, and profile attributes.",
            new Dictionary<ProcurementMasterDataTargetKind, IReadOnlySet<string>>
            {
                [ProcurementMasterDataTargetKind.BusinessPartner] = Fields(
                    "PartnerName", "PartnerType", "LegalName", "BusinessRegistrationNumber", "RegistrationDate", "IncorporationDate",
                    "PrimaryContactName", "PrimaryContactTitle", "PrimaryEmail", "PrimaryPhone", "SecondaryPhone", "Website",
                    "PhysicalAddress", "PhysicalCity", "PhysicalState", "PhysicalCountry", "PhysicalPostalCode",
                    "MailingAddress", "MailingCity", "MailingState", "MailingCountry", "MailingPostalCode",
                    "IndustryClassification", "CompanySize", "GeographicCoverage", "IsPreferred", "PaymentTermId", "Currency", "Notes"),
                [ProcurementMasterDataTargetKind.LegacySupplier] = Fields(
                    "Name", "Description", "SupplierType", "Address", "City", "State", "ZipCode", "Country", "Phone", "Email", "Website",
                    "PrimaryContactName", "PrimaryContactTitle", "PrimaryContactPhone", "PrimaryContactEmail", "PaymentTerms", "ShippingTerms",
                    "CreditLimit", "LeadTimeDays", "IsPreferred", "Rating", "Notes", "ContractStartDate", "ContractEndDate",
                    "DefaultApAccountId", "DefaultArAccountId", "DefaultExpenseAccountId", "PaymentTermId")
            }),
        Define(
            ProcurementMasterDataResourceType.SupplierBankDetails,
            "SUPPLIER_BANK",
            "Supplier bank details",
            "Controlled supplier settlement-account attributes; raw bank values remain within tenant-authorized responses.",
            new Dictionary<ProcurementMasterDataTargetKind, IReadOnlySet<string>>
            {
                [ProcurementMasterDataTargetKind.BusinessPartner] = Fields("BankName", "BankAccountNumber", "BankAccountName", "BankBranch", "BankSwiftCode", "BankIBAN")
            }, source: "SUP-010; E2E-017"),
        Define(
            ProcurementMasterDataResourceType.SupplierTaxDetails,
            "SUPPLIER_TAX",
            "Supplier tax details",
            "Controlled supplier tax-registration and exemption attributes.",
            new Dictionary<ProcurementMasterDataTargetKind, IReadOnlySet<string>>
            {
                [ProcurementMasterDataTargetKind.BusinessPartner] = Fields("TaxIdentificationNumber", "VATNumber", "IsTaxExempt", "TaxExemptionNumber", "TaxExemptionExpiry"),
                [ProcurementMasterDataTargetKind.LegacySupplier] = Fields("TaxId")
            }, source: "SUP-010; E2E-017"),
        Define(
            ProcurementMasterDataResourceType.SupplierOwnershipDetails,
            "SUPPLIER_OWNERSHIP",
            "Supplier ownership",
            "Controlled beneficial-owner declarations, verification date, and parent ownership relationship.",
            One(ProcurementMasterDataTargetKind.BusinessPartner, Fields(
                "BeneficialOwnershipJson", "OwnershipVerifiedAtUtc", "ParentId")),
            source: "SUP-010; E2E-017"),
        Define(
            ProcurementMasterDataResourceType.SupplierCategoryAssignments,
            "SUPPLIER_CATEGORIES",
            "Supplier category assignments",
            "Controlled replacement of current supplier category membership using current-tenant active categories.",
            One(ProcurementMasterDataTargetKind.BusinessPartner, Fields("CategoryIds")),
            source: "SUP-010; E2E-017"),
        Define(
            ProcurementMasterDataResourceType.SupplierComplianceStatus,
            "SUPPLIER_COMPLIANCE",
            "Supplier compliance status",
            "Controlled supplier registration, activation, blacklist, risk, and compliance-review attributes.",
            One(ProcurementMasterDataTargetKind.BusinessPartner, Fields(
                "RegistrationStatus", "ApprovalStatus", "IsActive", "IsBlacklisted",
                "BlacklistReason", "BlacklistDate", "BlacklistExpiryDate", "RiskLevel",
                "ComplianceStatus", "ComplianceReviewDateUtc", "ComplianceValidUntilUtc", "ComplianceNotes")),
            source: "SUP-010; E2E-017"),
        Define(
            ProcurementMasterDataResourceType.InventoryItem,
            "INVENTORY_ITEM",
            "Inventory item",
            "Controlled item identity, classification, valuation configuration, replenishment, physical, tracking, and quality attributes; quantities and transaction-derived costs are excluded.",
            One(ProcurementMasterDataTargetKind.InventoryItem, Fields(
                "ItemCode", "Name", "Description", "CategoryId", "Brand", "Manufacturer", "Model", "UnitOfMeasure", "UnitOfMeasureScheduleId",
                "Barcode", "AlternateBarcode", "QRCode",
                "ValuationMethod", "IsValuationLocked", "IsProjectApplicable", "IsCostCentreApplicable", "DailyRentalRate", "StandardCost", "SalePrice", "MinimumLevel", "MaximumLevel", "ReorderLevel",
                "ReorderQuantity", "SafetyStock", "LeadTimeDays", "SafetyLeadTimeDays", "ItemType", "ABCClass", "Status", "DefaultTaxGroupId",
                "ShippingWeight", "Weight", "Length", "Width", "Height", "Volume", "IsSerialTracked", "IsLotTracked", "IsBatchTracked", "IsManufactureDateTracked", "IsExpirationTracked",
                "IsLocationTracked", "RequiresInspection", "ShelfLifeDays", "PrimarySupplier", "SupplierItemCode", "CustomFields"))),
        Define(
            ProcurementMasterDataResourceType.InventoryCategory,
            "INVENTORY_CATEGORY",
            "Inventory category",
            "Controlled inventory classification and category defaults.",
            One(ProcurementMasterDataTargetKind.InventoryCategory, Fields(
                "Name", "Code", "Description", "ParentCategoryId", "Color", "Icon", "IsActive", "DefaultUnitOfMeasure",
                "DefaultSerialTracking", "DefaultLotTracking", "DefaultBatchTracking", "DefaultManufactureDateTracking", "DefaultExpirationTracking",
                "EnforceFifoIssue", "MinimumShelfLifeDays", "DefaultRequiresInspection", "DefaultTaxGroupId"))),
        Define(
            ProcurementMasterDataResourceType.UnitOfMeasure,
            "UNIT_OF_MEASURE",
            "Unit of measure",
            "Controlled unit identity, classification, symbol, base-unit, activation, and ordering attributes.",
            One(ProcurementMasterDataTargetKind.UnitOfMeasure, Fields("Code", "Name", "Description", "Category", "Symbol", "IsBaseUnit", "IsActive", "SortOrder"))),
        Define(
            ProcurementMasterDataResourceType.Warehouse,
            "WAREHOUSE",
            "Warehouse",
            "Controlled warehouse identity, address, type, contact, activation, default, and consignment attributes.",
            One(ProcurementMasterDataTargetKind.Warehouse, Fields(
                "Name", "Code", "Description", "Address", "City", "State", "ZipCode", "Country", "IsActive", "IsDefault",
                "IsConsignmentWarehouse", "WarehouseType", "ContactPerson", "Phone", "Email"))),
        Define(
            ProcurementMasterDataResourceType.WarehouseLocation,
            "WAREHOUSE_LOCATION",
            "Warehouse location",
            "Controlled warehouse hierarchy, operational use, consignment, zone, position, picking, environmental, dedication, barcode, and capacity attributes; live quantities are excluded.",
            One(ProcurementMasterDataTargetKind.WarehouseLocation, Fields(
                "WarehouseId", "LocationCode", "Name", "Description", "LocationType", "ParentLocationId", "IsActive", "IsPickingLocation",
                "IsReceivingLocation", "IsConsignmentBin", "ConsignmentWarehouseId", "IsQuarantineLocation", "IsInspectionLocation",
                "IsInTransitLocation", "IsShippingLocation", "IsStagingLocation", "IsReturnLocation", "IsDamageLocation", "LocationHierarchyType",
                "Zone", "Aisle", "Rack", "Shelf", "Bin", "RowNumber", "ColumnNumber", "LevelNumber", "PickSequence", "ABCClass",
                "TemperatureZone", "DedicatedItemId", "DedicatedItemCode", "LocationBarcode", "MaxWeight", "MaxVolume", "MaxItems"))),
        Define(
            ProcurementMasterDataResourceType.ProcurementPolicySensitive,
            "PROCUREMENT_POLICY_SENSITIVE",
            "Procurement policy-sensitive settings",
            "Controlled tenant procurement settings. This adapter governs configuration only and does not execute PR, PO, receipt, payment, or inventory transactions.",
            One(ProcurementMasterDataTargetKind.ProcurementSettings, Fields(
                "AutoCreateInventoryItems", "AutoCreateSupplierItems", "AllowNonInventoryItems", "DefaultItemCategoryId", "DefaultUnitOfMeasureId",
                "DefaultValuationMethod", "PurchaseRequisitionNumberFormat", "PurchaseOrderNumberFormat", "PurchaseOrderReceiptNumberFormat",
                "RequireApprovalForPO", "AutoApprovalThreshold", "AllowBackorders", "RequireDeliveryDate", "EnforceSupplierCatalog",
                "AllowMultipleSuppliersPerItem", "ValidateBudgetBeforePO", "RequireContractForPO", "Notes")), true)
    ];

    public static ProcurementMasterDataResourceDefinition Get(ProcurementMasterDataResourceType resourceType) =>
        Resources.SingleOrDefault(item => item.ResourceType == resourceType)
        ?? throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, "Unknown procurement master-data resource type.");

    public static IReadOnlySet<string> GetAllowedFields(ProcurementMasterDataResourceType resourceType, ProcurementMasterDataTargetKind targetKind)
    {
        var definition = Get(resourceType);
        return definition.TargetFields.TryGetValue(targetKind, out var fields)
            ? fields
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ProcurementMasterDataResourceDefinitionDto> ToDtos() => Resources.Select(item =>
        new ProcurementMasterDataResourceDefinitionDto
        {
            ResourceType = item.ResourceType,
            Code = item.Code,
            Name = item.Name,
            Description = item.Description,
            AllowedFields = item.TargetFields.Values.SelectMany(fields => fields).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(field => field).ToList(),
            AllowsTenantSingletonTarget = item.AllowsTenantSingletonTarget,
            SourceRequirements = item.SourceRequirements
        }).ToList();

    private static IReadOnlyDictionary<ProcurementMasterDataTargetKind, IReadOnlySet<string>> One(
        ProcurementMasterDataTargetKind kind, IReadOnlySet<string> fields) =>
        new Dictionary<ProcurementMasterDataTargetKind, IReadOnlySet<string>> { [kind] = fields };

    private static ProcurementMasterDataResourceDefinition Define(
        ProcurementMasterDataResourceType type,
        string code,
        string name,
        string description,
        IReadOnlyDictionary<ProcurementMasterDataTargetKind, IReadOnlySet<string>> fields,
        bool singleton = false,
        string source = "TDC-0007") =>
        new(type, code, name, description, fields, singleton, source);
}

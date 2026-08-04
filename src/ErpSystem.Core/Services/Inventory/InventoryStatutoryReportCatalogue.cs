using ErpSystem.Core.DTOs.Reports;

namespace ErpSystem.Core.Services.Inventory;

public sealed record InventorySystemReportDefinition(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<string> Tags)
{
    public string Query => InventoryStatutoryReportCatalogue.QueryPrefix + Code;
}

public static class InventoryStatutoryReportCatalogue
{
    public const string QueryPrefix = "system://tdc/inventory/";
    public const string ReportType = "inventory";
    public const string ReadPermission = "procurement.reports.read";
    public const string ExportPermission = "procurement.reports.export";

    public const string BalanceCode = "balance-register";
    public const string MovementCode = "movement-register";
    public const string AgeingCode = "ageing-register";
    public const string ReorderCode = "reorder-register";
    public const string CountVarianceCode = "count-variance-register";
    public const string ValuationGlCode = "valuation-gl-register";
    public const string SlowNonMovingCode = "slow-non-moving-register";
    public const string ExpiryCode = "expiry-register";
    public const string DisposalCode = "disposal-register";

    public static IReadOnlyList<InventorySystemReportDefinition> Definitions { get; } =
    [
        Definition(BalanceCode, "Inventory Balance Register",
            "Current item, warehouse and exact-location quantities reconciled to the authoritative inventory balance owner.",
            C("ItemCode", "Item code"), C("ItemName", "Item"), C("Category", "Category"), C("UnitOfMeasure", "UOM"),
            C("WarehouseCode", "Warehouse code"), C("WarehouseName", "Warehouse"), C("LocationCode", "Location code"),
            C("LocationName", "Location"), C("QuantityOnHand", "On hand", "Decimal", "N2"),
            C("QuantityAllocated", "Allocated", "Decimal", "N2"), C("QuantityAvailable", "Available", "Decimal", "N2"),
            C("QuantityOnOrder", "On order", "Decimal", "N2"), C("AverageUnitCost", "Average cost", "Decimal", "N2"),
            C("InventoryValue", "Inventory value", "Decimal", "N2"), C("LastMovementDate", "Last movement", "DateTime"),
            C("LastCountDate", "Last count", "DateTime")),
        Definition(MovementCode, "Inventory Movement Register",
            "Immutable stock movement history with exact scope, source, tracking and running-balance lineage.",
            C("MovementDate", "Movement date", "DateTime"), C("ItemCode", "Item code"), C("ItemName", "Item"),
            C("WarehouseCode", "Warehouse code"), C("WarehouseName", "Warehouse"), C("LocationCode", "Location code"),
            C("LocationName", "Location"), C("MovementType", "Movement type"), C("Quantity", "Quantity", "Decimal", "N2"),
            C("UnitCost", "Unit cost", "Decimal", "N2"), C("TotalValue", "Total value", "Decimal", "N2"),
            C("RunningBalance", "Running balance", "Decimal", "N2"), C("ReferenceType", "Reference type"),
            C("ReferenceNumber", "Reference"), C("LotNumber", "Lot"), C("BatchNumber", "Batch"),
            C("SerialNumber", "Serial"), C("ProcessedBy", "Processed by")),
        Definition(AgeingCode, "Inventory Ageing Register",
            "Six-band item/location ageing reconciled to active valuation layers and current balance value.",
            C("ItemCode", "Item code"), C("ItemName", "Item"), C("WarehouseCode", "Warehouse code"),
            C("WarehouseName", "Warehouse"), C("LocationCode", "Location code"), C("LocationName", "Location"),
            C("BandKey", "Band key"), C("AgeingBand", "Ageing band"), C("FromDays", "From days", "Integer"),
            C("ToDays", "To days", "Integer"), C("Quantity", "Quantity", "Decimal", "N2"),
            C("Value", "Value", "Decimal", "N2"), C("AsOfUtc", "As of", "DateTime")),
        Definition(ReorderCode, "Inventory Reorder Register",
            "Current stockout/reorder exposure linked to the governed replenishment recommendation and generated requisition.",
            C("ItemCode", "Item code"), C("ItemName", "Item"), C("WarehouseCode", "Warehouse code"),
            C("WarehouseName", "Warehouse"), C("LocationCode", "Location code"), C("LocationName", "Location"),
            C("QuantityAvailable", "Available", "Decimal", "N2"), C("QuantityOnOrder", "On order", "Decimal", "N2"),
            C("ReorderLevel", "Reorder level", "Decimal", "N2"), C("AverageDailyDemand", "Average daily demand", "Decimal", "N2"),
            C("EstimatedDaysOfCover", "Days of cover", "Decimal", "N2"), C("CurrentStockoutDays", "Stockout days", "Integer"),
            C("RecommendationNumber", "Recommendation"), C("RecommendationStatus", "Recommendation status"),
            C("PurchaseRequisitionNumber", "Purchase requisition"), C("RecommendedAction", "Recommended action")),
        Definition(CountVarianceCode, "Inventory Count Variance Register",
            "Physical and cycle-count variances with recount, investigation and independent approval lineage.",
            C("CountNumber", "Count number"), C("CountType", "Count type"), C("Status", "Status"),
            C("CountDate", "Count date", "DateTime"), C("WarehouseName", "Warehouse"), C("LocationName", "Count location"),
            C("ItemCode", "Item code"), C("ItemName", "Item"), C("ItemLocation", "Item location"),
            C("SystemQuantity", "System quantity", "Decimal", "N2"), C("CountedQuantity", "Counted quantity", "Decimal", "N2"),
            C("VarianceQuantity", "Variance quantity", "Decimal", "N2"), C("UnitCost", "Unit cost", "Decimal", "N2"),
            C("VarianceValue", "Variance value", "Decimal", "N2"), C("RequiresRecount", "Requires recount", "Boolean"),
            C("RecountedQuantity", "Recounted quantity", "Decimal", "N2"), C("InvestigationNotes", "Investigation"),
            C("StoresApprovedAt", "Stores approved", "DateTime"), C("FinanceApprovedAt", "Finance approved", "DateTime"),
            C("AuditAttestedAt", "Audit attested", "DateTime"), C("StockAdjustmentId", "Adjustment ID")),
        Definition(ValuationGlCode, "Inventory Valuation to GL Register",
            "Inventory subledger, balance cache and General Ledger reconciliation snapshots with exception and freeze lineage.",
            C("ReconciliationNumber", "Reconciliation"), C("FiscalPeriodCode", "Fiscal period"), C("Status", "Status"),
            C("CutoffDate", "Cut-off", "DateTime"), C("Currency", "Currency"), C("ControlAccountCode", "Control account"),
            C("ReceiptInventoryValue", "Receipt value", "Decimal", "N2"), C("LandedCostInventoryValue", "Landed cost value", "Decimal", "N2"),
            C("InventorySubledgerValue", "Inventory subledger", "Decimal", "N2"), C("BalanceCacheValue", "Balance cache", "Decimal", "N2"),
            C("GeneralLedgerValue", "General ledger", "Decimal", "N2"), C("ReconciliationVariance", "Variance", "Decimal", "N2"),
            C("ToleranceAmount", "Tolerance", "Decimal", "N2"), C("ExceptionCount", "Exceptions", "Integer"),
            C("GeneratedAt", "Generated", "DateTime"), C("FrozenAt", "Frozen", "DateTime"), C("SnapshotHash", "Snapshot hash")),
        Definition(SlowNonMovingCode, "Slow and Non-moving Stock Register",
            "Current slow/non-moving item-location exposure using the authoritative demand and ageing classification.",
            C("ItemCode", "Item code"), C("ItemName", "Item"), C("Category", "Category"),
            C("WarehouseCode", "Warehouse code"), C("WarehouseName", "Warehouse"), C("LocationCode", "Location code"),
            C("LocationName", "Location"), C("Classification", "Classification"), C("DaysSinceActivity", "Days since activity", "Integer"),
            C("QuantityOnHand", "On hand", "Decimal", "N2"), C("InventoryValue", "Inventory value", "Decimal", "N2"),
            C("LastMovementDate", "Last movement", "DateTime"), C("OldestStockAgeDays", "Oldest age", "Integer"),
            C("OldestAgeingBand", "Oldest band"), C("DisposalCandidate", "Disposal candidate", "Boolean"),
            C("RecommendedAction", "Recommended action")),
        Definition(ExpiryCode, "Inventory Expiry Register",
            "Expired and near-expiry item/location exposure derived from the traceability ledger and current valuation.",
            C("ItemCode", "Item code"), C("ItemName", "Item"), C("Category", "Category"),
            C("WarehouseCode", "Warehouse code"), C("WarehouseName", "Warehouse"), C("LocationCode", "Location code"),
            C("LocationName", "Location"), C("ExpiredQuantity", "Expired quantity", "Decimal", "N2"),
            C("ExpiredValue", "Expired value", "Decimal", "N2"), C("ExpiringQuantity", "Expiring quantity", "Decimal", "N2"),
            C("ExpiringValue", "Expiring value", "Decimal", "N2"), C("ExpiryWarningDays", "Warning days", "Integer"),
            C("DisposalCandidate", "Disposal candidate", "Boolean"), C("RecommendedAction", "Recommended action"),
            C("AsOfUtc", "As of", "DateTime")),
        Definition(DisposalCode, "Inventory Disposal Register",
            "Governed disposal cases and exact item/location lines from identification through proceeds and stock/GL completion.",
            C("DisposalNumber", "Disposal number"), C("Status", "Status"), C("Method", "Method"),
            C("RequestedAt", "Requested", "DateTime"), C("WarehouseCode", "Warehouse code"), C("WarehouseName", "Warehouse"),
            C("ItemCode", "Item code"), C("ItemName", "Item"), C("LocationCode", "Location code"),
            C("Quantity", "Quantity", "Decimal", "N2"), C("UnitCost", "Unit cost", "Decimal", "N2"),
            C("LineValue", "Line value", "Decimal", "N2"), C("LotNumber", "Lot"), C("BatchNumber", "Batch"),
            C("SerialNumber", "Serial"), C("AuthorityRoute", "Authority route"), C("CommitteeReference", "Committee reference"),
            C("StockAdjustmentId", "Adjustment ID"), C("ProceedsAmount", "Proceeds", "Decimal", "N2"),
            C("ExecutionReference", "Execution reference"), C("CompletedAt", "Completed", "DateTime"))
    ];

    public static InventorySystemReportDefinition? Resolve(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || !query.StartsWith(QueryPrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var code = query[QueryPrefix.Length..].Trim();
        return Definitions.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    public static Dictionary<string, object> BuildParameters() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["startDate"] = Parameter("Start date", "date"),
        ["endDate"] = Parameter("End date", "date"),
        ["warehouseId"] = Parameter("Warehouse", "warehouse"),
        ["categoryId"] = Parameter("Category", "category"),
        ["status"] = Parameter("Status or classification", "text"),
        ["movementType"] = Parameter("Movement type", "text"),
        ["fiscalPeriodId"] = Parameter("Fiscal period", "fiscal-period"),
        ["slowMovingDays"] = Parameter("Slow-moving days", "number"),
        ["nonMovingDays"] = Parameter("Non-moving days", "number"),
        ["expiryWarningDays"] = Parameter("Expiry warning days", "number")
    };

    private static InventorySystemReportDefinition Definition(
        string code, string name, string description, params ReportColumnDto[] columns) =>
        new(code, name, description, columns, ["TDC-0702", "RPT-002", "statutory", "inventory", code]);

    private static ReportColumnDto C(string name, string displayName, string type = "String", string? format = null) =>
        new() { Name = name, DisplayName = displayName, DataType = type, Format = format, IsVisible = true };

    private static Dictionary<string, object> Parameter(string label, string type) => new()
    {
        ["label"] = label,
        ["type"] = type,
        ["required"] = false
    };
}

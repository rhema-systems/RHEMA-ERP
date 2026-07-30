namespace ErpSystem.Core.Enums;

/// <summary>
/// Authoritative governed source that permits a procurement purchase order to exist.
/// HistoricalMigration is migration-only and is rejected for every new purchase order.
/// </summary>
public enum ProcurementPurchaseOrderSourceType
{
    RfqAward = 0,
    TenderAward = 1,
    Contract = 2,
    ApprovedException = 3,
    FrameworkCallOff = 4,
    HistoricalMigration = 5
}

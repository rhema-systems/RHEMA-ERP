namespace ErpSystem.Core.Enums;

/// <summary>
/// Defines the type of reference document for stock movements and transactions
/// </summary>
public enum ReferenceType
{
    /// <summary>
    /// Purchase Order - receiving inventory
    /// </summary>
    PO = 1,

    /// <summary>
    /// Work Order - consuming inventory for maintenance/production
    /// </summary>
    WO = 2,

    /// <summary>
    /// Sales Order - selling inventory
    /// </summary>
    SO = 3,

    /// <summary>
    /// Transfer Order - moving inventory between locations
    /// </summary>
    Transfer = 4,

    /// <summary>
    /// Stock Adjustment - correcting inventory discrepancies
    /// </summary>
    Adjustment = 5,

    /// <summary>
    /// Production Order - manufacturing/assembly
    /// </summary>
    Production = 6,

    /// <summary>
    /// Return - returned goods (customer or supplier)
    /// </summary>
    Return = 7,

    /// <summary>
    /// Cycle Count - physical inventory count
    /// </summary>
    CycleCount = 8,

    /// <summary>
    /// Waste/Scrap - disposal of damaged or unusable inventory
    /// </summary>
    Waste = 9,

    /// <summary>
    /// Manual Issue - general inventory issue
    /// </summary>
    Manual = 10
}

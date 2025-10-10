namespace ErpSystem.Core.Enums;

/// <summary>
/// Defines the type of inventory item for classification and handling
/// </summary>
public enum ItemType
{
    /// <summary>
    /// Physical goods held in inventory
    /// </summary>
    StockItem = 1,

    /// <summary>
    /// Services provided (non-physical)
    /// </summary>
    Service = 2,

    /// <summary>
    /// Non-stock items (purchased for specific orders)
    /// </summary>
    NonStock = 3,

    /// <summary>
    /// Fixed assets
    /// </summary>
    FixedAsset = 4
}
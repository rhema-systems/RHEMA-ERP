namespace ErpSystem.Core.Enums;

/// <summary>
/// Defines the status of an inventory item for lifecycle management
/// </summary>
public enum ItemStatus
{
    /// <summary>
    /// Item is active and available for use
    /// </summary>
    Active = 1,

    /// <summary>
    /// Item is temporarily inactive but can be reactivated
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// Item has been discontinued and will not be restocked
    /// </summary>
    Discontinued = 3,

    /// <summary>
    /// Item is obsolete and should not be used
    /// </summary>
    Obsolete = 4
}
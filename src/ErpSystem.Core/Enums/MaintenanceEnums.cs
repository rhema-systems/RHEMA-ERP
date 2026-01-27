namespace ErpSystem.Core.Enums;

/// <summary>
/// Types of maintenance attachments
/// </summary>
public enum AttachmentType
{
    Photo = 0,
    Document = 1,
    Video = 2,
    Audio = 3
}

/// <summary>
/// Types of entities that can have attachments
/// </summary>
public enum AttachmentEntityType
{
    WorkOrder = 0,
    Asset = 1,
    Inspection = 2
}

/// <summary>
/// Categories for work order attachments
/// </summary>
public enum WorkOrderAttachmentCategory
{
    Before = 0,
    During = 1,
    After = 2,
    Reference = 3
}

/// <summary>
/// Categories for asset attachments
/// </summary>
public enum AssetAttachmentCategory
{
    Manual = 0,
    Specification = 1,
    Photo = 2,
    Warranty = 3,
    Certificate = 4,
    Drawing = 5
}

/// <summary>
/// Categories for inspection attachments
/// </summary>
public enum InspectionAttachmentCategory
{
    Evidence = 0,
    Issue = 1,
    Compliance = 2
}

/// <summary>
/// Types of attachment access
/// </summary>
public enum AttachmentAccessType
{
    View = 0,
    Download = 1,
    Preview = 2
}


/// <summary>
/// Asset criticality levels
/// </summary>
public enum AssetCriticality
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Asset status
/// </summary>
public enum AssetStatus
{
    Active = 0,
    Inactive = 1,
    Maintenance = 2,
    OutOfService = 3,
    Retired = 4,
    Disposed = 5,
    InUse = 6  // Vehicle/asset is currently being used for travel or work order execution
}

/// <summary>
/// Asset type classification for categories
/// </summary>
public enum AssetTypeClassification
{
    Equipment = 0,
    Vehicle = 1,
    Building = 2,
    Infrastructure = 3,
    ITAsset = 4,
    Furniture = 5,
    Tool = 6,
    Safety = 7,
    Other = 99
}

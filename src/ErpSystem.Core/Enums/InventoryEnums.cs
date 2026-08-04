namespace ErpSystem.Core.Enums;

/// <summary>
/// Inventory valuation method for cost calculation
/// </summary>
public enum ValuationMethod
{
    /// <summary>
    /// Weighted average cost method
    /// </summary>
    WeightedAverage = 1,

    /// <summary>
    /// First In First Out
    /// </summary>
    FIFO = 2,

    /// <summary>
    /// Last In First Out
    /// </summary>
    LIFO = 3,

    /// <summary>
    /// Standard cost method
    /// </summary>
    StandardCost = 4,

    /// <summary>
    /// Specific identification method
    /// </summary>
    SpecificIdentification = 5
}

/// <summary>
/// Purchase requisition type
/// </summary>
public enum PurchaseRequisitionType
{
    /// <summary>
    /// Stock replenishment for regular inventory
    /// </summary>
    StockReplenishment = 1,

    /// <summary>
    /// Capital purchase for fixed assets
    /// </summary>
    CapitalPurchase = 2,

    /// <summary>
    /// Emergency purchase requiring expedited processing
    /// </summary>
    EmergencyPurchase = 3,

    /// <summary>
    /// Project-specific purchase
    /// </summary>
    ProjectPurchase = 4,

    /// <summary>
    /// Service procurement
    /// </summary>
    ServiceProcurement = 5
}

/// <summary>
/// Warehouse location type for hierarchy
/// </summary>
public enum WarehouseLocationType
{
    /// <summary>
    /// Main warehouse zone
    /// </summary>
    Zone = 1,

    /// <summary>
    /// Aisle within a zone
    /// </summary>
    Aisle = 2,

    /// <summary>
    /// Rack/Shelf unit
    /// </summary>
    Rack = 3,

    /// <summary>
    /// Individual bin or slot
    /// </summary>
    Bin = 4,

    /// <summary>
    /// Floor storage area
    /// </summary>
    Floor = 5,

    /// <summary>
    /// Quarantine/Inspection area
    /// </summary>
    Quarantine = 6,

    /// <summary>
    /// In-transit virtual location
    /// </summary>
    InTransit = 7,

    /// <summary>
    /// Receiving dock area
    /// </summary>
    ReceivingDock = 8,

    /// <summary>
    /// Shipping dock area
    /// </summary>
    ShippingDock = 9
}

/// <summary>
/// Quality inspection result
/// </summary>
public enum InspectionResult
{
    /// <summary>
    /// Pending inspection
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Passed inspection
    /// </summary>
    Passed = 1,

    /// <summary>
    /// Failed inspection - reject
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Conditional pass - requires rework
    /// </summary>
    ConditionalPass = 3,

    /// <summary>
    /// Requires rework before acceptance
    /// </summary>
    Rework = 4
}

/// <summary>
/// Physical count type
/// </summary>
public enum CountType
{
    /// <summary>
    /// Full physical inventory count
    /// </summary>
    FullCount = 1,

    /// <summary>
    /// Cycle count (partial/periodic)
    /// </summary>
    CycleCount = 2,

    /// <summary>
    /// Spot check count
    /// </summary>
    SpotCheck = 3,

    /// <summary>
    /// ABC analysis count (focus on high-value items)
    /// </summary>
    ABCCount = 4
}

/// <summary>
/// Inventory transfer status
/// </summary>
public enum TransferStatus
{
    /// <summary>
    /// Transfer draft being prepared
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Transfer submitted for approval
    /// </summary>
    Submitted = 2,

    /// <summary>
    /// Transfer approved
    /// </summary>
    Approved = 3,

    /// <summary>
    /// Items picked and ready for transfer
    /// </summary>
    Picked = 4,

    /// <summary>
    /// Items in transit between locations
    /// </summary>
    InTransit = 5,

    /// <summary>
    /// Transfer received at destination
    /// </summary>
    Received = 6,

    /// <summary>
    /// Transfer completed
    /// </summary>
    Completed = 7,

    /// <summary>
    /// Transfer cancelled
    /// </summary>
    Cancelled = 8,

    /// <summary>
    /// Transfer rejected
    /// </summary>
    Rejected = 9
}

/// <summary>
/// Types of inventory transfers
/// </summary>
public enum TransferType
{
    /// <summary>
    /// Standard inter-warehouse transfer
    /// </summary>
    Standard = 1,

    /// <summary>
    /// Emergency transfer for urgent needs
    /// </summary>
    Emergency = 2,

    /// <summary>
    /// Replenishment transfer to restock
    /// </summary>
    Replenishment = 3,

    /// <summary>
    /// Return transfer back to source
    /// </summary>
    Return = 4,

    /// <summary>
    /// Consolidation transfer to combine stock
    /// </summary>
    Consolidation = 5,

    /// <summary>
    /// Redistribution transfer for balancing
    /// </summary>
    Redistribution = 6
}

/// <summary>
/// Goods receipt note status
/// </summary>
public enum GRNStatus
{
    /// <summary>
    /// GRN draft
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Goods received, pending inspection
    /// </summary>
    PendingInspection = 2,

    /// <summary>
    /// Inspection in progress
    /// </summary>
    InspectionInProgress = 3,

    /// <summary>
    /// Inspection completed, accepted
    /// </summary>
    Accepted = 4,

    /// <summary>
    /// Partially accepted
    /// </summary>
    PartiallyAccepted = 5,

    /// <summary>
    /// Rejected
    /// </summary>
    Rejected = 6,

    /// <summary>
    /// Stock updated
    /// </summary>
    StockUpdated = 7,

    /// <summary>
    /// GRN cancelled
    /// </summary>
    Cancelled = 8
}

/// <summary>
/// Landed cost type for allocation
/// </summary>
public enum LandedCostType
{
    /// <summary>
    /// Freight/Shipping cost
    /// </summary>
    Freight = 1,

    /// <summary>
    /// Customs duty
    /// </summary>
    CustomsDuty = 2,

    /// <summary>
    /// Insurance cost
    /// </summary>
    Insurance = 3,

    /// <summary>
    /// Handling charges
    /// </summary>
    Handling = 4,

    /// <summary>
    /// Brokerage fees
    /// </summary>
    Brokerage = 5,

    /// <summary>
    /// Storage/Warehousing cost
    /// </summary>
    Storage = 6,

    /// <summary>
    /// Other miscellaneous costs
    /// </summary>
    Other = 7
}

/// <summary>
/// Inventory requisition status
/// </summary>
public enum RequisitionStatus
{
    /// <summary>
    /// Requisition draft being prepared
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Requisition submitted for approval
    /// </summary>
    Submitted = 2,

    /// <summary>
    /// Requisition approved
    /// </summary>
    Approved = 3,

    /// <summary>
    /// Items being picked/prepared
    /// </summary>
    InProgress = 4,

    /// <summary>
    /// Partially issued
    /// </summary>
    PartiallyIssued = 5,

    /// <summary>
    /// Fully issued
    /// </summary>
    Issued = 6,

    /// <summary>
    /// Requisition completed
    /// </summary>
    Completed = 7,

    /// <summary>
    /// Requisition cancelled
    /// </summary>
    Cancelled = 8,

    /// <summary>
    /// Requisition rejected
    /// </summary>
    Rejected = 9
}

/// <summary>
/// Lifecycle of stock reserved for an approved project requisition line.
/// </summary>
public enum InventoryProjectReservationStatus
{
    Reserved = 1,
    PartiallyFulfilled = 2,
    Fulfilled = 3,
    Released = 4,
    Expired = 5,
    Substituted = 6
}

public enum InventoryProjectReservationActionType
{
    Reserved = 1,
    PartiallyFulfilled = 2,
    Fulfilled = 3,
    Released = 4,
    Expired = 5,
    Substituted = 6,
    NotificationCreated = 7
}

public enum InventoryReplenishmentRecommendationStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    ConvertedToRequisition = 5,
    Expired = 6,
    Cancelled = 7
}

public enum InventoryReplenishmentActionType
{
    Generated = 1,
    AlertSent = 2,
    Submitted = 3,
    Approved = 4,
    Rejected = 5,
    RequisitionCreated = 6,
    Cancelled = 7,
    Expired = 8,
    ApprovalProgressed = 9
}

/// <summary>
/// Types of inventory requisitions
/// </summary>
public enum RequisitionType
{
    /// <summary>
    /// Department requisition for internal use
    /// </summary>
    DepartmentRequisition = 1,

    /// <summary>
    /// Project requisition for project consumption
    /// </summary>
    ProjectRequisition = 2,

    /// <summary>
    /// Maintenance requisition for repairs/maintenance
    /// </summary>
    MaintenanceRequisition = 3,

    /// <summary>
    /// Production requisition for manufacturing
    /// </summary>
    ProductionRequisition = 4,

    /// <summary>
    /// Emergency requisition requiring expedited processing
    /// </summary>
    EmergencyRequisition = 5,

    /// <summary>
    /// Return to stock (negative requisition)
    /// </summary>
    ReturnToStock = 6
}


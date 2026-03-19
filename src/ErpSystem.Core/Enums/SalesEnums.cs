namespace ErpSystem.Core.Enums;

#region Sales Order

/// <summary>
/// Lifecycle status of a Sales Order
/// </summary>
public enum SalesOrderStatus
{
    Draft = 1,
    PendingApproval = 2,
    Confirmed = 3,
    PartiallyDelivered = 4,
    Delivered = 5,
    Invoiced = 6,
    Closed = 7,
    Cancelled = 8,
    OnHold = 9,
    Rejected = 10
}

/// <summary>
/// Type of sales order
/// </summary>
public enum SalesOrderType
{
    Standard = 1,
    PropertySale = 2,      // TDC: plot/house sale
    LeaseAgreement = 3,    // TDC: rental/lease
    ServiceContract = 4,
    Subscription = 5,
    ReturnOrder = 6
}

#endregion

#region Delivery

/// <summary>
/// Lifecycle status of a Delivery Note
/// </summary>
public enum DeliveryNoteStatus
{
    Draft = 1,
    Packed = 2,
    Shipped = 3,
    InTransit = 4,
    PartiallyDelivered = 5,
    Delivered = 6,
    Cancelled = 7
}

/// <summary>
/// Shipment method for deliveries
/// </summary>
public enum ShipmentMethod
{
    PickUp = 1,
    CompanyDelivery = 2,
    ThirdPartyCourier = 3,
    DigitalHandover = 4,   // TDC: for property documents
    Other = 99
}

#endregion

#region Sales Agreement

/// <summary>
/// Lifecycle status of a Sales Agreement
/// </summary>
public enum SalesAgreementStatus
{
    Draft = 1,
    PendingApproval = 2,
    Active = 3,
    Expiring = 4,
    Renewed = 5,
    Expired = 6,
    Terminated = 7,
    Suspended = 8
}

/// <summary>
/// Type of sales agreement
/// </summary>
public enum SalesAgreementType
{
    General = 1,
    VolumeBased = 2,
    PriceLock = 3,
    LeaseAgreement = 4,    // TDC: property lease
    TenancyAgreement = 5,  // TDC: tenancy
    PlotAllocation = 6,    // TDC: serviced plot allocation
    ServiceLevel = 7
}

#endregion

#region Refund & Credit Note

/// <summary>
/// Lifecycle status of a Refund
/// </summary>
public enum RefundStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Processing = 4,
    Completed = 5,
    Rejected = 6,
    Cancelled = 7
}

/// <summary>
/// Lifecycle status of a Credit Note
/// </summary>
public enum CreditNoteStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Applied = 4,
    Voided = 5
}

/// <summary>
/// Lifecycle status of a Return Order
/// </summary>
public enum ReturnOrderStatus
{
    Requested = 1,
    Approved = 2,
    Received = 3,
    Inspected = 4,
    CreditIssued = 5,
    Rejected = 6,
    Cancelled = 7
}

/// <summary>
/// Reason codes for returns
/// </summary>
public enum ReturnReasonCode
{
    Defective = 1,
    WrongItem = 2,
    DamagedInTransit = 3,
    QualityIssue = 4,
    CustomerChanged = 5,
    PricingError = 6,
    DuplicateOrder = 7,
    ContractCancellation = 8,  // TDC: property deal fell through
    Other = 99
}

#endregion

#region Commission

/// <summary>
/// Type of commission calculation
/// </summary>
public enum CommissionType
{
    FlatPercentage = 1,
    Tiered = 2,
    RuleBased = 3
}

/// <summary>
/// Status of a commission statement
/// </summary>
public enum CommissionStatementStatus
{
    Draft = 1,
    Calculated = 2,
    Approved = 3,
    Paid = 4,
    Disputed = 5
}

#endregion

#region Competitor Intelligence

/// <summary>
/// Threat level of a competitor on a deal
/// </summary>
public enum CompetitorThreatLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Deal outcome when competitor was involved
/// </summary>
public enum CompetitorDealOutcome
{
    InProgress = 1,
    Won = 2,
    Lost = 3,
    NoDecision = 4
}

#endregion

#region Forecasting

/// <summary>
/// Method used for sales forecasting
/// </summary>
public enum SalesForecastMethod
{
    Manual = 1,
    PipelineWeighted = 2,
    HistoricalTrend = 3,
    BottomUp = 4,
    TopDown = 5
}

/// <summary>
/// Status of a sales forecast
/// </summary>
public enum SalesForecastStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Locked = 4
}

#endregion

#region Opportunity Stage

/// <summary>
/// Standardized opportunity pipeline stages (replacing magic strings)
/// </summary>
public enum OpportunityStage
{
    Prospecting = 1,
    Qualification = 2,
    NeedsAnalysis = 3,
    ValueProposition = 4,
    Proposal = 5,
    Negotiation = 6,
    ClosedWon = 7,
    ClosedLost = 8
}

/// <summary>
/// Lead qualification status (replacing magic strings)
/// </summary>
public enum LeadStatus
{
    New = 1,
    Contacted = 2,
    Qualified = 3,
    Unqualified = 4,
    Converted = 5,
    Nurturing = 6,
    Dead = 7
}

/// <summary>
/// Property type classification for TDC property management
/// </summary>
public enum PropertyType
{
    ResidentialHouse = 1,
    ResidentialApartment = 2,
    ServicedPlot = 3,
    CommercialUnit = 4,
    IndustrialUnit = 5,
    MixedUse = 6,
    ShortTermRental = 7,  // Airbnb-style
    Land = 8
}

#endregion

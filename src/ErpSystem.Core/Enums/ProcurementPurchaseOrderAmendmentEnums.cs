namespace ErpSystem.Core.Enums;

public enum ProcurementPurchaseOrderAmendmentStatus
{
    Draft = 0,
    PendingApproval = 1,
    Applied = 2,
    Rejected = 3,
    Cancelled = 4,
    Dispatched = 5,
    Acknowledged = 6
}

public enum ProcurementPurchaseOrderDispatchChannel
{
    SupplierPortal = 0,
    Email = 1,
    Courier = 2,
    HandDelivery = 3,
    Other = 4
}

public enum ProcurementPurchaseOrderAcknowledgementOutcome
{
    Received = 0,
    Accepted = 1,
    Disputed = 2
}

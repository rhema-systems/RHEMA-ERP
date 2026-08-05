namespace ErpSystem.Core.Enums;

public enum ProcurementReceiptInspectionStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    QualityHold = 4,
    ReturnPending = 5,
    ReplacementPending = 6,
    ClosureReady = 7,
    Closed = 8,
    RevalidationFailed = 9,
    Cancelled = 10
}

public enum ProcurementReceiptDisposition
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    PartiallyAccepted = 3
}

public enum ProcurementReceiptSupplierAcknowledgementStatus
{
    NotRequired = 0,
    Pending = 1,
    Acknowledged = 2,
    Disputed = 3
}

public enum ProcurementReceiptResolutionKind
{
    None = 0,
    Return = 1,
    Replacement = 2
}

public enum ProcurementReceiptResolutionStatus
{
    NotRequired = 0,
    Required = 1,
    Authorized = 2,
    Dispatched = 3,
    ReplacementRequested = 4,
    ReplacementReceived = 5,
    Closed = 6
}

public enum ProcurementReceiptInspectionEvidenceKind
{
    WorkflowEvidenceDocument = 0,
    CentralDocumentUpload = 1
}

public enum ProcurementReceiptInspectionActionType
{
    Created = 0,
    Saved = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    RejectionNoteIssued = 5,
    SupplierAcknowledged = 6,
    SupplierDisputed = 7,
    ReturnAuthorized = 8,
    ReturnDispatched = 9,
    ReplacementRequested = 10,
    ReplacementReceived = 11,
    Closed = 12,
    RevalidationFailed = 13,
    Cancelled = 14
}

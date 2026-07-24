namespace ErpSystem.Core.Enums;

public enum ProcurementTenderControlStatus
{
    Advertised = 0,
    Opened = 1,
    TechnicalEvaluated = 2,
    FinancialEvaluated = 3,
    PendingApproval = 4,
    Approved = 5,
    Rejected = 6,
    Awarded = 7,
    Contracted = 8,
    Accepted = 9
}

public enum ProcurementTenderSubmissionDisposition
{
    OnTimeAccepted = 0,
    LateRejected = 1
}

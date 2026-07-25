namespace ErpSystem.Core.Enums;

public enum ProcurementPrequalificationStatus
{
    Draft = 0,
    Advertised = 1,
    Closed = 2,
    UnderEvaluation = 3,
    PendingApproval = 4,
    Approved = 5,
    Rejected = 6,
    Expired = 7
}

public enum ProcurementPrequalificationApplicationStatus
{
    Submitted = 0,
    EvaluatedQualified = 1,
    EvaluatedRejected = 2,
    Approved = 3,
    Rejected = 4
}

public enum ProcurementQualifiedListEntryStatus
{
    Active = 0,
    Expired = 1,
    Revoked = 2
}

namespace ErpSystem.Core.Enums;

public enum ProcurementFrameworkCallOffStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Issued = 3,
    Rejected = 4,
    Cancelled = 5
}

public enum ProcurementFrameworkBalanceMovementType
{
    Commitment = 0,
    Release = 1,
    Issue = 2
}

namespace ErpSystem.Core.Enums;

public enum ProcurementWorksCloseoutActionType
{
    InitialTakeover = 0,
    DefectRectification = 1,
    FinalTakeover = 2,
    WarrantyRelease = 3,
    PerformanceSecurityRelease = 4,
    RetentionRelease = 5,
    DisputeOpen = 6,
    DisputeResolve = 7,
    Termination = 8,
    FinalAccount = 9,
    Closeout = 10
}

public enum ProcurementWorksCloseoutActionStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2,
    RevalidationFailed = 3
}

public enum ProcurementWorksCloseoutCheckStatus
{
    Passed = 0,
    Failed = 1,
    NotRequired = 2,
    Pending = 3
}

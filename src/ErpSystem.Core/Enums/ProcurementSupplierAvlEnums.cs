namespace ErpSystem.Core.Enums;

public enum ProcurementSupplierAvlRegisterStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Published = 3,
    Rejected = 4,
    Retired = 5
}

public enum ProcurementSupplierAvlEntryStatus
{
    Active = 0,
    Suspended = 1,
    Expired = 2
}

public enum ProcurementSupplierAvlEntryAction
{
    Suspended = 0,
    Reinstated = 1,
    Expired = 2
}

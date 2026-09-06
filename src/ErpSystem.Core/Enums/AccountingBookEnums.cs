namespace ErpSystem.Core.Enums;

public enum AccountingBookType
{
    PrimaryFull = 1,
    ParallelFull = 2,
    Delta = 3
}

public enum AccountingBookLifecycleStatus
{
    Draft = 1,
    Configuring = 2,
    Initializing = 3,
    Active = 4,
    Suspended = 5,
    Retired = 6
}

public enum AccountingBookPeriodStatus
{
    Future = 1,
    Open = 2,
    Closed = 3,
    Locked = 4
}

public enum AccountingBookInitializationMode
{
    IndependentOpeningBalances = 1,
    BaseBookCopyAtCutoff = 2,
    BaseBalancesWithOpeningAdjustments = 3
}

public enum AccountingBookInitializationStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4
}

public enum AccountingBookApplicabilityPolicyStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Retired = 5
}

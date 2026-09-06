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

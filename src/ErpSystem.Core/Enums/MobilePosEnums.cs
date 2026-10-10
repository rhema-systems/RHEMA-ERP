namespace ErpSystem.Core.Enums;

public enum MobilePosStoreStatus
{
    Draft = 1,
    Active = 2,
    Suspended = 3,
    Retired = 4
}

public enum MobilePosTillStatus
{
    Draft = 1,
    Active = 2,
    Suspended = 3,
    Retired = 4
}

public enum MobilePosDeviceStatus
{
    Pending = 1,
    Active = 2,
    Suspended = 3,
    Revoked = 4,
    Retired = 5
}

public enum MobilePosOfflineGrantStatus
{
    Active = 1,
    Expired = 2,
    Revoked = 3,
    Consumed = 4
}

public enum MobilePosSaleStatus
{
    Pending = 1,
    Completed = 2,
    Rejected = 3,
    Reversed = 4
}

public enum MobilePosTenderStatus
{
    Pending = 1,
    Completed = 2,
    Rejected = 3,
    Reversed = 4
}

public enum MobileMutationReceiptStatus
{
    Processing = 1,
    Completed = 2,
    Rejected = 3
}

public enum MobilePosTillCloseSubmissionStatus
{
    ReadyForReview = 1,
    PendingSync = 2,
    SyncExceptionResolved = 3,
    Finalized = 4,
    ReturnedForRecount = 5
}

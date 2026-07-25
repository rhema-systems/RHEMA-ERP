namespace ErpSystem.Core.Enums;

public enum ProcurementCalendarProfileStatus
{
    Draft = 0,
    Published = 1,
    Retired = 2
}

public enum ProcurementCalendarEventType
{
    AppPreparation = 0,
    AppSubmission = 1,
    MidYearReview = 2,
    CycleCount = 3,
    YearEndClose = 4,
    Renewal = 5,
    GhanepsDeadline = 6
}

public enum ProcurementCalendarOccurrenceStatus
{
    Upcoming = 0,
    Due = 1,
    Acknowledged = 2,
    Escalated = 3,
    Completed = 4,
    Cancelled = 5,
    Failed = 6
}

public enum ProcurementCalendarRunStatus
{
    Running = 0,
    Completed = 1,
    CompletedWithWarnings = 2,
    Failed = 3
}

public enum ProcurementCalendarRunTrigger
{
    Scheduled = 0,
    Manual = 1,
    Publication = 2
}

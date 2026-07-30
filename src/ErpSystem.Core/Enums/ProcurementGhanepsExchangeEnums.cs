namespace ErpSystem.Core.Enums;

public enum ProcurementGhanepsSourceType
{
    RequestForQuotation = 0,
    Tender = 1,
    ExceptionalSourcing = 2
}

public enum ProcurementGhanepsEventFamily
{
    TenderPublication = 0,
    TenderReference = 1,
    AwardNotification = 2
}

public enum ProcurementGhanepsExchangeDirection
{
    Export = 0,
    Import = 1,
    Bidirectional = 2
}

public enum ProcurementGhanepsExchangeStatus
{
    Prepared = 0,
    Transferred = 1,
    PendingAcknowledgement = 2,
    Acknowledged = 3,
    Failed = 4,
    Reconciled = 5,
    ReconciliationException = 6
}

public enum ProcurementGhanepsAttemptOutcome
{
    Succeeded = 0,
    Failed = 1
}

public enum ProcurementGhanepsAcknowledgementOutcome
{
    Accepted = 0,
    Rejected = 1
}

public enum ProcurementGhanepsReconciliationOutcome
{
    Matched = 0,
    Mismatch = 1,
    Resolved = 2
}

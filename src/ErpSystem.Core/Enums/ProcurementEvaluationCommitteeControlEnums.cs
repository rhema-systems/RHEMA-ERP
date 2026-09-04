namespace ErpSystem.Core.Enums;

public enum ProcurementEvaluationSourceType
{
    Tender = 0,
    RequestForQuotation = 1
}

public enum ProcurementEvaluationPhase
{
    Technical = 0,
    Financial = 1,
    Combined = 2
}

public enum ProcurementEvaluationCommitteeControlStatus
{
    Draft = 0,
    Active = 1,
    Closed = 2,
    Retired = 3
}

public enum ProcurementEvaluationAppointmentStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Withdrawn = 3
}

public enum ProcurementEvaluationConflictOutcome
{
    NoConflict = 0,
    ConflictDeclared = 1,
    Withdrawn = 2
}

public enum ProcurementEvaluationMeetingStatus
{
    Draft = 0,
    QuorumConfirmed = 1,
    QuorumFailed = 2,
    Closed = 3
}

public enum ProcurementEvaluationScoreSheetStatus
{
    Locked = 0,
    Recalled = 1
}

public enum ProcurementEvaluationScoreRecallStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2
}

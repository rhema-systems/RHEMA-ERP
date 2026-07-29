namespace ErpSystem.Core.Enums;

public enum ProcurementAwardReadinessSourceType
{
    RequestForQuotation = 0,
    Tender = 1,
    ExceptionalSourcing = 2
}

public enum ProcurementAwardReadinessDecisionStatus
{
    Blocked = 0,
    Ready = 1
}

public enum ProcurementAwardReadinessPrerequisiteGroup
{
    Source = 0,
    Recommendation = 1,
    Evaluation = 2,
    ScoreIntegrity = 3,
    SupplierEligibility = 4,
    Prequalification = 5,
    VerificationAndDueDiligence = 6,
    AuthorityAndWorkflow = 7,
    Evidence = 8
}

public enum ProcurementAwardReadinessPrerequisiteStatus
{
    Passed = 0,
    Failed = 1,
    NotApplicable = 2
}

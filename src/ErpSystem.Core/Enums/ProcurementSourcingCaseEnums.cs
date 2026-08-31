namespace ErpSystem.Core.Enums;

public enum ProcurementSourcingCaseStatus
{
    Ready = 0,
    InProgress = 1,
    Closed = 2,
    Cancelled = 3
}

public enum ProcurementSourcingCaseSourceRequestStatus
{
    Planned = 0,
    Created = 1,
    Cancelled = 2
}

public enum ProcurementSourcingMethodSelectionBasis
{
    AutomaticRecommendation = 0,
    ApprovedOverride = 1
}

public enum ProcurementTenderSourceRecoveryBoundary
{
    Evaluation = 0,
    AwardAdministration = 1,
    AwardApproval = 2,
    ContractCreation = 3,
    PurchaseOrderCreation = 4
}

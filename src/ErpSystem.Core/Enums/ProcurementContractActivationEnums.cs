namespace ErpSystem.Core.Enums;

public enum ProcurementContractActivationStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2,
    Activated = 3,
    RevalidationFailed = 4,
    Cancelled = 5
}

public enum ProcurementContractActivationCheckStatus
{
    Passed = 0,
    Failed = 1,
    NotRequired = 2,
    Pending = 3
}

public enum ProcurementContractActivationEvidenceKind
{
    WorkflowEvidenceDocument = 0,
    CentralDocumentUpload = 1
}

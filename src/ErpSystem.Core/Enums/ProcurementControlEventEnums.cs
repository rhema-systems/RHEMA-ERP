namespace ErpSystem.Core.Enums;

public enum ProcurementControlEventResult
{
    Succeeded = 0,
    Rejected = 1,
    Allowed = 2,
    Denied = 3,
    ReviewRequired = 4,
    Failed = 5,
    Warning = 6
}

public enum ProcurementControlEvidenceReferenceKind
{
    WorkflowEvidenceDocument = 0,
    FileUploadRecord = 1,
    ExternalReference = 2
}

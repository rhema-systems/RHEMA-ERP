namespace ErpSystem.Core.Enums;

public enum ProcurementPolicyLifecycleStatus
{
    Draft = 0,
    Published = 1,
    Retired = 2
}

public enum ProcurementPolicyScopeType
{
    TenantBaseline = 0,
    TenantOverride = 1
}

public enum ProcurementPolicyRuleKind
{
    Category = 0,
    Method = 1,
    Threshold = 2,
    Authority = 3,
    Evidence = 4,
    Exception = 5,
    SegregationOfDuties = 6
}

public enum ProcurementPolicyOverrideAction
{
    Add = 0,
    Replace = 1,
    Disable = 2
}

public enum ProcurementEvidenceStage
{
    Requisition = 0,
    Sourcing = 1,
    Evaluation = 2,
    Award = 3,
    Contract = 4,
    PurchaseOrder = 5,
    Receipt = 6,
    Invoice = 7,
    Payment = 8,
    Inventory = 9
}

public enum ProcurementExceptionDisposition
{
    Prohibited = 0,
    ApprovalRequired = 1,
    Permitted = 2
}

public enum ProcurementSodEnforcement
{
    HardStop = 0,
    ApprovalRequired = 1,
    Warning = 2
}

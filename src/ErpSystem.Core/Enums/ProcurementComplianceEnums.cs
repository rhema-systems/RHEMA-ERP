namespace ErpSystem.Core.Enums;

public enum ProcurementComplianceOutcome
{
    Allowed = 0,
    ReviewRequired = 1,
    Blocked = 2
}

public enum ProcurementComplianceRouteStepType
{
    MethodWorkflow = 0,
    Authority = 1,
    ExceptionApproval = 2
}

public enum ProcurementComplianceFindingSeverity
{
    Information = 0,
    Warning = 1,
    HardStop = 2
}

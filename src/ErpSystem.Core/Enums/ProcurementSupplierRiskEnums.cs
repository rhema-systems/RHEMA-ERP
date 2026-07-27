namespace ErpSystem.Core.Enums;

public enum ProcurementSupplierRiskEligibilityAction
{
    AlertOnly = 0,
    EscalationRequired = 1,
    AwardHardStop = 2
}

public enum ProcurementSupplierRiskAlertType
{
    DataIncomplete = 0,
    MinimumScore = 1,
    Concentration = 2,
    SingleSourceDependency = 3
}

public enum ProcurementSupplierRiskAlertStatus
{
    Open = 0,
    Escalated = 1,
    Resolved = 2
}

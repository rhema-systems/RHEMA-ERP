namespace ErpSystem.Core.Enums;

public enum ProcurementBudgetCommitmentStatus
{
    Reserved = 1,
    Released = 2,
    Consumed = 3
}

public enum ProcurementBudgetCommitmentLedgerEntryType
{
    FormalCommitment = 1,
    Utilization = 2,
    Release = 3,
    PurchaseOrderAllocation = 4
}

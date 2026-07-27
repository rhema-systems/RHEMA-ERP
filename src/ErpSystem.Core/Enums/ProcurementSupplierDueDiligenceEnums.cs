namespace ErpSystem.Core.Enums;

public enum ProcurementSupplierDueDiligenceReviewType
{
    Initial = 0,
    Annual = 1
}

public enum ProcurementSupplierDueDiligenceStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4,
    Superseded = 5
}

public enum ProcurementSupplierDueDiligenceOutcome
{
    Pending = 0,
    Clear = 1,
    Adverse = 2
}

public enum ProcurementSupplierDueDiligenceCheckType
{
    PpaDebarment = 0,
    GraTaxClearance = 1,
    Sanctions = 2,
    BankVerification = 3,
    FinancialStability = 4,
    Reputation = 5
}

public enum ProcurementSupplierDueDiligenceCheckStatus
{
    Pending = 0,
    Clear = 1,
    Adverse = 2,
    NotApplicable = 3
}

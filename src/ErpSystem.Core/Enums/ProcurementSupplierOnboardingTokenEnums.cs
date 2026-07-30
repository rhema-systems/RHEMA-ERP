namespace ErpSystem.Core.Enums;

public enum ProcurementSupplierOnboardingFeeMode
{
    Free = 0,
    Paid = 1
}

public enum ProcurementSupplierOnboardingTokenStatus
{
    AwaitingPayment = 0,
    Active = 1,
    Expired = 2
}

public enum ProcurementSupplierOnboardingPaymentStatus
{
    NotRequired = 0,
    Pending = 1,
    Posted = 2,
    Reconciled = 3,
    Exempt = 4,
    Failed = 5
}

public enum ProcurementSupplierOnboardingExemptionStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2
}

public enum ProcurementSupplierApplicantVerificationChannel
{
    Email = 0,
    Sms = 1
}

public enum ProcurementSupplierApplicantAccessStatus
{
    ApplicationInProgress = 0,
    ApprovedPendingCredentialDelivery = 1,
    CredentialDelivered = 2,
    Activated = 3,
    Rejected = 4,
    ActivationFailed = 5
}

public enum ProcurementSupplierApplicantSessionStatus
{
    Active = 0,
    Revoked = 1
}

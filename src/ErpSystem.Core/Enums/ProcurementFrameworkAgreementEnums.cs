namespace ErpSystem.Core.Enums;

public enum ProcurementFrameworkAgreementStatus
{
    Draft = 0,
    PendingApproval = 1,
    Published = 2,
    Rejected = 3,
    Superseded = 4,
    Expired = 5,
    Terminated = 6
}

public enum ProcurementFrameworkAuthorityKind
{
    User = 0,
    Role = 1,
    OrganizationalUnit = 2,
    Permission = 3
}

public enum ProcurementFrameworkExtensionStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2
}

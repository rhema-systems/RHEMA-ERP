namespace ErpSystem.Core.Enums;

public enum ProcurementSupplierRegistrationCategory
{
    Goods = 0,
    Works = 1,
    Services = 2
}

public enum ProcurementSupplierEvidencePackStatus
{
    Draft = 0,
    PendingApproval = 1,
    Published = 2,
    Retired = 3
}

public enum ProcurementSupplierEvidenceRequirementKind
{
    Document = 0,
    Classification = 1,
    DocumentAndClassification = 2
}

public enum ProcurementSupplierEvidenceValidityMode
{
    NotApplicable = 0,
    CurrentOnSubmission = 1,
    MinimumRemainingDays = 2
}

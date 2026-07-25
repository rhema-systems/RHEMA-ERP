namespace ErpSystem.Core.Enums;

public enum ProcurementConfigurationProfileStatus
{
    Draft = 0,
    Published = 1,
    Retired = 2
}

public enum ProcurementConfigurationDecisionStatus
{
    Draft = 0,
    Proposed = 1,
    Approved = 2,
    Rejected = 3
}

public enum ProcurementConfigurationApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    NotRequired = 3
}

public enum ProcurementConfigurationEvidenceStatus
{
    Missing = 0,
    Attached = 1,
    Verified = 2
}

public enum ProcurementCategoryClass
{
    Goods = 0,
    Works = 1,
    TechnicalServices = 2,
    ConsultancyServices = 3,
    GeneralServices = 4
}

public enum ProcurementMethodType
{
    RequestForQuotation = 0,
    NationalCompetitiveTendering = 1,
    InternationalCompetitiveTendering = 2,
    RestrictedTendering = 3,
    SingleSource = 4,
    PettyPurchase = 5,
    FrameworkCallOff = 6,
    QualityBasedSelection = 7,
    QualityAndCostBasedSelection = 8
}

public enum ProcurementSignatureMode
{
    Electronic = 0,
    UploadedManualEvidence = 1,
    ElectronicOrManualEvidence = 2
}

public enum ProcurementNegativeStockPolicy
{
    Prohibited = 0,
    ControlledEmergencyOverride = 1
}

public enum ProcurementReceiptDocumentType
{
    Grn = 0,
    Mrn = 1,
    GrnAndMrn = 2
}

public enum ProcurementReceiptCoexistenceRule
{
    MutuallyExclusive = 0,
    BothFromSingleReceipt = 1,
    SequentialDocuments = 2
}

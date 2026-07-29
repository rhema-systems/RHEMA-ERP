using System.ComponentModel;

namespace ErpSystem.Core.Enums;

public enum LandAcquisitionStatus
{
    [Description("Pending Identification")]
    PendingIdentification = 0,
    [Description("Submitted")]
    Submitted = 1,
    [Description("Pending Approval")]
    PendingApproval = 2,
    [Description("Rejected")]
    Rejected = 3,
    [Description("Completed")]
    Completed = 4,
    [Description("Asset Created")]
    AssetCreated = 5
}

public enum AcquisitionProcedure
{
    LandIdentification = 0,
    SuitabilityApproval = 1,
    CadastralSurvey = 2,
    SurveyVerification = 3,
    OwnershipClassification = 4,
    OwnershipVerification = 5,
    AgreementNegotiation = 6,
    AgreementApproval = 7,
    LandInstrumentExecution = 8,
    StatutoryConsent = 9,
    StatutoryConsentApproval = 10,
    StampDutyAssessment = 11,
    StampDutyAssessmentApproval = 12,
    StampDutyPayment = 13,
    LandsCommissionRegistration = 14,
    LandAssetCreation = 15
}

public enum LandOwnershipType
{
    Unknown = 0,
    StoolOrSkin = 1,
    Family = 2,
    PrivateIndividual = 3,
    StateOrVested = 4,
    MixedInterest = 5
}

public enum LandAcquisitionMethod
{
    Purchase = 0,
    Gift = 1,
    Inheritance = 2,
    CourtOrder = 3,
    Leasehold = 4,
    Assignment = 5,
    Conveyance = 6
}

public enum EstateManagedAssetType
{
    Land = 0,
    Property = 1,
    Facility = 2
}

public enum EstateManagedAssetSourceType
{
    Manual = 0,
    LandAcquisition = 1,
    ProjectUnit = 2
}

public enum EstateManagedAssetStatus
{
    LandBank = 0,
    UnderDevelopment = 1,
    Available = 2,
    Reserved = 3,
    Leased = 4,
    Occupied = 5,
    Sold = 6,
    Retired = 7
}

using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Estate;

public class LandAcquisition : TenantEntity
{
    [Required, MaxLength(80)]
    public string ProjectReference { get; set; } = string.Empty;

    [MaxLength(200)]
    public string IntendedUse { get; set; } = string.Empty;

    public decimal EstimatedSize { get; set; }

    [Required, MaxLength(300)]
    public string Location { get; set; } = string.Empty;

    public AcquisitionProcedure CurrentStage { get; set; } = AcquisitionProcedure.LandIdentification;
    public LandAcquisitionStatus Status { get; set; } = LandAcquisitionStatus.PendingIdentification;
    public LandOwnershipType OwnershipType { get; set; } = LandOwnershipType.Unknown;

    [MaxLength(2000)]
    public string? Coordinates { get; set; }

    public int StageOrder { get; set; }
    public bool PlanningUploaded { get; set; }
    public bool InternalApproved { get; set; }
    public bool SuitableForDueDiligence { get; set; }

    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    public string? WorkspaceDataJson { get; set; }

    public ICollection<LandAcquisitionDocument> Documents { get; set; } = new List<LandAcquisitionDocument>();
    public ICollection<LandAcquisitionNote> Notes { get; set; } = new List<LandAcquisitionNote>();
    public ICollection<LandAcquisitionChecklistResponse> ChecklistResponses { get; set; } = new List<LandAcquisitionChecklistResponse>();
    public ICollection<CadastralSurvey> CadastralSurveys { get; set; } = new List<CadastralSurvey>();
    public ICollection<OwnershipHistory> OwnershipHistories { get; set; } = new List<OwnershipHistory>();
    public ICollection<NegotiationOffer> NegotiationOffers { get; set; } = new List<NegotiationOffer>();
    public ICollection<LandRegistration> Registrations { get; set; } = new List<LandRegistration>();
    public ICollection<LandAsset> LandAssets { get; set; } = new List<LandAsset>();

    public LandPhysicalAssessment? PhysicalAssessment { get; set; }
    public LandAgreement? Agreement { get; set; }
    public LandInstrument? LandInstrument { get; set; }
    public StatutoryConsent? StatutoryConsent { get; set; }
    public StampDutyAssessment? StampDutyAssessment { get; set; }
    public StampDutyPayment? StampDutyPayment { get; set; }
}

public class LandAcquisitionDocument : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    [Required, MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(80)]
    public string DocumentType { get; set; } = "Other";

    public AcquisitionProcedure Procedure { get; set; }
}

public class LandPhysicalAssessment : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    public bool PlanningCompatible { get; set; }
    public bool AccessConfirmed { get; set; }
    public bool EnvironmentalClearance { get; set; }
    public bool UtilityAvailability { get; set; }
    public bool IsFloodProne { get; set; }

    [MaxLength(100)]
    public string? ZoningClassification { get; set; }

    [MaxLength(80)]
    public string? SoilType { get; set; }

    [MaxLength(80)]
    public string? Topography { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class CadastralSurvey : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    [MaxLength(160)]
    public string? SurveyorName { get; set; }

    public DateTime? SurveyDate { get; set; }
    public DateTime? SurveyorSignedDate { get; set; }

    [MaxLength(100)]
    public string? PlanNumber { get; set; }

    [MaxLength(100)]
    public string? MapSheetNumber { get; set; }

    [MaxLength(100)]
    public string? BeaconCount { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public decimal? AreaSize { get; set; }

    [MaxLength(40)]
    public string? AreaUnit { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? NorthEastLat { get; set; }
    public decimal? NorthEastLng { get; set; }
    public decimal? NorthWestLat { get; set; }
    public decimal? NorthWestLng { get; set; }
    public decimal? SouthEastLat { get; set; }
    public decimal? SouthEastLng { get; set; }
    public decimal? SouthWestLat { get; set; }
    public decimal? SouthWestLng { get; set; }

    [MaxLength(4000)]
    public string? BoundaryCoordinates { get; set; }

    [MaxLength(160)]
    public string? RegionalSurveyorName { get; set; }

    public DateTime? RegionalSurveyorSignedDate { get; set; }
    public bool MainPortion { get; set; }

    [MaxLength(2000)]
    public string? CoordinateReference { get; set; }

    public bool CadastralMatch { get; set; }
    public bool OverlapCleared { get; set; }
    public bool BoundaryConfirmed { get; set; }

    [MaxLength(120)]
    public string? VerificationReference { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class OwnershipHistory : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    public Guid? BusinessPartnerId { get; set; }

    [MaxLength(200)]
    public string OwnerName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? ContactNumber { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public LandOwnershipType OwnershipType { get; set; } = LandOwnershipType.Unknown;
    public LandAcquisitionMethod AcquisitionMethod { get; set; } = LandAcquisitionMethod.Purchase;

    [MaxLength(80)]
    public string? TenureType { get; set; }

    public DateTime? OwnershipStartDate { get; set; }
    public DateTime? OwnershipEndDate { get; set; }
    public decimal? OwnershipPercentage { get; set; }
    public bool IsCurrentOwner { get; set; }

    [MaxLength(120)]
    public string? InterestHeld { get; set; }

    [MaxLength(80)]
    public string? RiskLevel { get; set; }

    public bool TitleSearchCompleted { get; set; }
    public bool OwnerIdentityVerified { get; set; }
    public bool AuthorityToSellVerified { get; set; }

    [MaxLength(120)]
    public string? SearchReference { get; set; }

    [MaxLength(80)]
    public string? IdentificationType { get; set; }

    [MaxLength(120)]
    public string? IdentificationNumber { get; set; }

    [MaxLength(2000)]
    public string? DateGapReason { get; set; }

    [MaxLength(160)]
    public string? WitnessName1 { get; set; }

    [MaxLength(80)]
    public string? WitnessContact1 { get; set; }

    [MaxLength(120)]
    public string? WitnessRelationship1 { get; set; }

    [MaxLength(500)]
    public string? WitnessAddress1 { get; set; }

    public bool WitnessSwornOath1 { get; set; }

    [MaxLength(160)]
    public string? WitnessOathSwornBefore1 { get; set; }

    public DateTime? WitnessOathSwornDate1 { get; set; }

    [MaxLength(160)]
    public string? WitnessName2 { get; set; }

    [MaxLength(80)]
    public string? WitnessContact2 { get; set; }

    [MaxLength(120)]
    public string? WitnessRelationship2 { get; set; }

    [MaxLength(500)]
    public string? WitnessAddress2 { get; set; }

    public bool WitnessSwornOath2 { get; set; }

    [MaxLength(160)]
    public string? WitnessOathSwornBefore2 { get; set; }

    public DateTime? WitnessOathSwornDate2 { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class NegotiationOffer : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    public decimal OpeningOffer { get; set; }
    public decimal? CounterOffer { get; set; }
    public decimal? NegotiatedValue { get; set; }
    public decimal? SellerQuote { get; set; }

    [MaxLength(300)]
    public string? PaymentTerms { get; set; }

    [MaxLength(80)]
    public string? PaymentType { get; set; }

    [MaxLength(20)]
    public string? AgreementDay { get; set; }

    [MaxLength(20)]
    public string? AgreementMonth { get; set; }

    [MaxLength(20)]
    public string? AgreementYear { get; set; }

    public bool AgreementGenerated { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool IsAccepted { get; set; }
}

public class LandAgreement : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    public bool LegalReviewComplete { get; set; }
    public bool FinanceReviewComplete { get; set; }

    [MaxLength(120)]
    public string? BoardApprovalReference { get; set; }

    [MaxLength(2000)]
    public string? ApprovalConditions { get; set; }

    public DateTime? AgreementDate { get; set; }
    public bool IsFamilyLand { get; set; }
    public bool IsStoolLand { get; set; }

    [MaxLength(1000)]
    public string? RootOfTitle { get; set; }

    [MaxLength(2000)]
    public string? SpecialConditions { get; set; }

    [MaxLength(1000)]
    public string? PartyDetails { get; set; }

    [MaxLength(1000)]
    public string? PaymentSchedule { get; set; }

    [MaxLength(1000)]
    public string? WitnessDetails { get; set; }
}

public class LandInstrument : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    [MaxLength(80)]
    public string? InstrumentType { get; set; }

    [MaxLength(120)]
    public string? InstrumentNumber { get; set; }

    public DateTime? ExecutionDate { get; set; }

    [MaxLength(160)]
    public string? ExecutedBy { get; set; }

    [MaxLength(160)]
    public string? CounterpartySignatory { get; set; }

    [MaxLength(1000)]
    public string? WitnessDetails { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool IsExecuted { get; set; }
}

public class StatutoryConsent : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    [MaxLength(160)]
    public string? ConsentAuthority { get; set; }

    [MaxLength(120)]
    public string? ApplicationNumber { get; set; }

    public DateTime? ConsentDate { get; set; }
    public DateTime? SubmissionDate { get; set; }

    [MaxLength(120)]
    public string? ApprovalReference { get; set; }

    public DateTime? ApprovalDate { get; set; }

    [MaxLength(2000)]
    public string? Conditions { get; set; }

    public bool IsApproved { get; set; }
}

public class StampDutyAssessment : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    public decimal AssessedValue { get; set; }
    public decimal DutyAmount { get; set; }

    [MaxLength(160)]
    public string? AssessmentAuthority { get; set; }

    [MaxLength(120)]
    public string? AssessmentReference { get; set; }

    public DateTime? AssessmentDate { get; set; }

    [MaxLength(120)]
    public string? FinanceApprovalReference { get; set; }

    [MaxLength(160)]
    public string? ApproverName { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool IsApproved { get; set; }
}

public class StampDutyPayment : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    public Guid? AccountsPayableSupplierId { get; set; }
    public Guid? AccountsPayableInvoiceId { get; set; }
    public Guid? AccountsPayablePaymentId { get; set; }

    [MaxLength(120)]
    public string? ReceiptNumber { get; set; }

    [MaxLength(120)]
    public string? PaymentReference { get; set; }

    public DateTime? PaymentDate { get; set; }
    public decimal AmountPaid { get; set; }

    [MaxLength(80)]
    public string? PaymentMethod { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool IsPaid { get; set; }
}

public class LandRegistration : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    [MaxLength(160)]
    public string? RegistryOffice { get; set; }

    [MaxLength(120)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(60)]
    public string? Volume { get; set; }

    [MaxLength(60)]
    public string? Folio { get; set; }

    public DateTime? RegistrationDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool IsRegistered { get; set; }
}

public class LandAsset : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    [MaxLength(120)]
    public string? AssetCode { get; set; }

    [MaxLength(120)]
    public string? AssetNumber { get; set; }

    [MaxLength(120)]
    public string? ParcelIdentifier { get; set; }

    [MaxLength(120)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(200)]
    public string? OwnerName { get; set; }

    [MaxLength(300)]
    public string? Location { get; set; }

    [MaxLength(120)]
    public string? AssetCategory { get; set; }

    public decimal? Size { get; set; }

    [MaxLength(40)]
    public string? SizeUnit { get; set; }

    [MaxLength(80)]
    public string? Status { get; set; }

    [MaxLength(200)]
    public string? Purpose { get; set; }

    [MaxLength(100)]
    public string? ZoningClassification { get; set; }

    [MaxLength(120)]
    public string? OwnershipVerification { get; set; }

    public decimal CapitalizationValue { get; set; }

    [MaxLength(120)]
    public string? GlAccount { get; set; }

    [MaxLength(160)]
    public string? Custodian { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class LandAcquisitionNote : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string Note { get; set; } = string.Empty;

    public AcquisitionProcedure Stage { get; set; }
    public int StageOrder { get; set; }

    [MaxLength(80)]
    public string InputType { get; set; } = "General";
}

public class LandAcquisitionChecklistResponse : TenantEntity
{
    public Guid LandAcquisitionId { get; set; }
    public LandAcquisition LandAcquisition { get; set; } = null!;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public AcquisitionProcedure Procedure { get; set; }
    public int StageOrder { get; set; }
    public bool IsChecked { get; set; }
    public Guid? UserId { get; set; }
}

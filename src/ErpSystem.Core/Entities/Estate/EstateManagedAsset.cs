using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Estate;

public class EstateManagedAsset : TenantEntity
{
    [Required, MaxLength(80)]
    public string AssetCode { get; set; } = string.Empty;

    [Required, MaxLength(240)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(200)]
    public string? Purpose { get; set; }

    [MaxLength(120)]
    public string? ZoningClassification { get; set; }

    [MaxLength(80)]
    public string? PlanningComplianceStatus { get; set; }

    [MaxLength(160)]
    public string? GisLayerReference { get; set; }

    [MaxLength(40)]
    public string GisProvider { get; set; } = "GeoServer";

    [MaxLength(240)]
    public string? GisFeatureId { get; set; }

    [MaxLength(80)]
    public string? GisSourceCrs { get; set; }

    [MaxLength(40)]
    public string GisSyncStatus { get; set; } = "NotLinked";

    public DateTime? GisLastSyncedAt { get; set; }

    public bool BoundaryVerified { get; set; }

    [MaxLength(4000)]
    public string? BoundaryCoordinates { get; set; }

    [MaxLength(120)]
    public string? SurveyPlanNumber { get; set; }

    [MaxLength(120)]
    public string? MapSheetNumber { get; set; }

    [MaxLength(240)]
    public string? CadastreDescription { get; set; }
    [MaxLength(120)] public string? Region { get; set; }
    [MaxLength(120)] public string? District { get; set; }
    [MaxLength(120)] public string? Town { get; set; }
    public decimal? AreaValue { get; set; }
    [MaxLength(40)] public string? AreaUnit { get; set; }
    [MaxLength(160)] public string? SurveyorName { get; set; }
    public DateTime? SurveyDate { get; set; }
    public int? BeaconCount { get; set; }
    public string? OwnershipHistoryJson { get; set; }
    public bool IsReadyForProjectManagement { get; set; }

    [MaxLength(120)]
    public string? BlockName { get; set; }

    [MaxLength(120)]
    public string? FloorLabel { get; set; }

    public EstateManagedAssetType AssetType { get; set; } = EstateManagedAssetType.Property;
    public EstateManagedAssetStatus Status { get; set; } = EstateManagedAssetStatus.Available;
    public EstateManagedAssetSourceType SourceType { get; set; } = EstateManagedAssetSourceType.Manual;

    public Guid? LandAcquisitionId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }

    [MaxLength(80)]
    public string? ProjectCode { get; set; }

    [MaxLength(240)]
    public string? ProjectTitle { get; set; }

    [MaxLength(120)]
    public string? ProjectUnitCode { get; set; }

    [MaxLength(120)]
    public string? UnitType { get; set; }

    public DateTime? DateOfTenancy { get; set; }
    public DateTime? RightOfEntryDate { get; set; }
    public int? LeaseTermYears { get; set; }
    public decimal? GroundRentPayable { get; set; }
    public decimal? GroundRentRatePerAcre { get; set; }
    public decimal? GroundRentComputed { get; set; }
    public Guid? CustomerBusinessPartnerId { get; set; }

    [MaxLength(240)]
    public string? LesseeName { get; set; }

    [MaxLength(500)]
    public string? LesseeAddress { get; set; }

    [MaxLength(120)]
    public string? PropertyFileReference { get; set; }

    public decimal? AreaSquareMeters { get; set; }
    public decimal? ValuationAmount { get; set; }
    public decimal? OwnerConsiderationCost { get; set; }
    public decimal? ExternalSurveyorCost { get; set; }
    public decimal? StampDutyCost { get; set; }
    public decimal? OtherAcquisitionCost { get; set; }
    public decimal? TotalCapitalizedCost { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "GHS";

    public bool IsAvailableForLease { get; set; } = true;
    public bool IsAvailableForSale { get; set; }
    public bool IsPublishedFromProject { get; set; }
    public DateTime? PublishedFromProjectAt { get; set; }
    public bool IsPublishedToExternalPortal { get; set; }

    [MaxLength(40)]
    public string ExternalListingType { get; set; } = "None";

    [MaxLength(40)]
    public string ExternalListingStatus { get; set; } = "Draft";

    public decimal? ExternalListingPrice { get; set; }

    public decimal? ExternalSalePrice { get; set; }

    public decimal? ExternalMonthlyRent { get; set; }

    public DateTime? RentBillingActivatedAt { get; set; }
    public DateTime? NextRentBillingDate { get; set; }
    public Guid? LastRentInvoiceId { get; set; }

    [MaxLength(80)]
    public string? LastRentInvoiceNumber { get; set; }

    public bool AutoGenerateRentInvoices { get; set; }

    public int RentGracePeriodDays { get; set; }

    [MaxLength(20)]
    public string RentPenaltyMethod { get; set; } = "None";

    public decimal RentPenaltyValue { get; set; }
    public decimal? RentPenaltyCapAmount { get; set; }
    public Guid? LastRentPenaltyInvoiceId { get; set; }

    [MaxLength(80)]
    public string? LastRentPenaltyInvoiceNumber { get; set; }

    public Guid? LastRentPenaltySourceInvoiceId { get; set; }

    public int? ExternalLeaseTermMonths { get; set; }

    [MaxLength(10)]
    public string ExternalListingCurrency { get; set; } = "GHS";

    [MaxLength(2000)]
    public string? ExternalListingNotes { get; set; }

    public DateTime? ExternalPublishedAt { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public ICollection<EstateManagedAssetDocument> Documents { get; set; } = new List<EstateManagedAssetDocument>();
    public ICollection<EstateLandDemarcation> Demarcations { get; set; } = new List<EstateLandDemarcation>();
}

public class EstateLandDemarcation : TenantEntity
{
    public Guid EstateManagedAssetId { get; set; }
    public EstateManagedAsset EstateManagedAsset { get; set; } = null!;
    public Guid? ParentDemarcationId { get; set; }
    public EstateLandDemarcation? ParentDemarcation { get; set; }
    public ICollection<EstateLandDemarcation> ChildDemarcations { get; set; } = new List<EstateLandDemarcation>();
    public int DemarcationNumber { get; set; }
    [Required, MaxLength(1000)] public string Description { get; set; } = string.Empty;
    public int BeaconCount { get; set; }
    [Required] public string BoundaryCoordinates { get; set; } = string.Empty;
    public decimal AreaSquareFeet { get; set; }
    public bool BoundaryVerified { get; set; }
    [MaxLength(40)] public string CostAllocationMethod { get; set; } = "NotSet";
    public decimal? AllocatedCost { get; set; }
    public decimal? CostPerAcre { get; set; }
    public decimal? TargetSalePrice { get; set; }
    public decimal? GroundRentPayable { get; set; }
    public decimal? GroundRentRatePerAcre { get; set; }
    public decimal? GroundRentComputed { get; set; }
    [MaxLength(120)] public string? ParentLandAssetReference { get; set; }
    [MaxLength(120)] public string? ParentFixedAssetReference { get; set; }
    [MaxLength(120)] public string? ChildFixedAssetReference { get; set; }
    [MaxLength(40)] public string FixedAssetPostingStatus { get; set; } = "NotReady";
    public DateTime? FixedAssetPostedAt { get; set; }
    public bool IsReadyForProjectManagement { get; set; }
    public bool IsPublishedToExternalPortal { get; set; }
    [MaxLength(40)] public string ExternalListingType { get; set; } = "None";
    [MaxLength(40)] public string ExternalListingStatus { get; set; } = "Draft";
    public decimal? ExternalListingPrice { get; set; }
    public decimal? ExternalSalePrice { get; set; }
    public decimal? ExternalMonthlyRent { get; set; }
    public int? ExternalLeaseTermMonths { get; set; }
    [MaxLength(10)] public string ExternalListingCurrency { get; set; } = "GHS";
    [MaxLength(2000)] public string? ExternalListingNotes { get; set; }
    public DateTime? ExternalPublishedAt { get; set; }
}

public class EstateManagedAssetDocument : TenantEntity
{
    public Guid EstateManagedAssetId { get; set; }
    public EstateManagedAsset EstateManagedAsset { get; set; } = null!;
    [Required, MaxLength(260)] public string FileName { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string FilePath { get; set; } = string.Empty;
    [MaxLength(120)] public string DocumentType { get; set; } = "Other";
    [MaxLength(160)] public string? DocumentName { get; set; }
    [MaxLength(160)] public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public bool IsListingImage { get; set; }
    public bool IsPrimaryListingImage { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    [MaxLength(80)] public string? CentralDocumentReference { get; set; }
    public DateTime? PublishedToCentralDmsAt { get; set; }
}

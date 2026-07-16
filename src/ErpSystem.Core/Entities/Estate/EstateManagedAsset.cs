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

    public decimal? AreaSquareMeters { get; set; }
    public decimal? ValuationAmount { get; set; }

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

    [MaxLength(10)]
    public string ExternalListingCurrency { get; set; } = "GHS";

    [MaxLength(2000)]
    public string? ExternalListingNotes { get; set; }

    public DateTime? ExternalPublishedAt { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public ICollection<EstateManagedAssetDocument> Documents { get; set; } = new List<EstateManagedAssetDocument>();
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

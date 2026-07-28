using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Estate;

public class EstateManagedAssetDto
{
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Purpose { get; set; }
    public string? ZoningClassification { get; set; }
    public string? PlanningComplianceStatus { get; set; }
    public string? GisLayerReference { get; set; }
    public string GisProvider { get; set; } = "GeoServer";
    public string? GisFeatureId { get; set; }
    public string? GisSourceCrs { get; set; }
    public string GisSyncStatus { get; set; } = "NotLinked";
    public DateTime? GisLastSyncedAt { get; set; }
    public bool BoundaryVerified { get; set; }
    public string? BoundaryCoordinates { get; set; }
    public string? SurveyPlanNumber { get; set; }
    public string? MapSheetNumber { get; set; }
    public string? CadastreDescription { get; set; }
    public string? Region { get; set; }
    public string? District { get; set; }
    public string? Town { get; set; }
    public decimal? AreaValue { get; set; }
    public string? AreaUnit { get; set; }
    public string? SurveyorName { get; set; }
    public DateTime? SurveyDate { get; set; }
    public int? BeaconCount { get; set; }
    public IReadOnlyList<ExistingLandOwnerDto> OwnershipHistory { get; set; } = Array.Empty<ExistingLandOwnerDto>();
    public bool IsReadyForProjectManagement { get; set; }
    public string? BlockName { get; set; }
    public string? FloorLabel { get; set; }
    public EstateManagedAssetType AssetType { get; set; }
    public EstateManagedAssetStatus Status { get; set; }
    public EstateManagedAssetSourceType SourceType { get; set; }
    public Guid? LandAcquisitionId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProjectUnitId { get; set; }
    public string? ProjectCode { get; set; }
    public string? ProjectTitle { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string? UnitType { get; set; }
    public decimal? AreaSquareMeters { get; set; }
    public decimal? ValuationAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public bool IsAvailableForLease { get; set; }
    public bool IsAvailableForSale { get; set; }
    public bool IsPublishedFromProject { get; set; }
    public DateTime? PublishedFromProjectAt { get; set; }
    public bool IsPublishedToExternalPortal { get; set; }
    public string ExternalListingType { get; set; } = "None";
    public string ExternalListingStatus { get; set; } = "Draft";
    public decimal? ExternalListingPrice { get; set; }
    public string ExternalListingCurrency { get; set; } = "GHS";
    public string? ExternalListingNotes { get; set; }
    public DateTime? ExternalPublishedAt { get; set; }
    public Guid? PrimaryListingImageDocumentId { get; set; }
    public string? Notes { get; set; }
}

public class ExistingLandOwnerDto
{
    public string OwnerName { get; set; } = string.Empty;
    public string OwnershipType { get; set; } = string.Empty;
    public string InterestHeld { get; set; } = string.Empty;
    public string IdentificationType { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime? OwnershipStartDate { get; set; }
    public DateTime? OwnershipEndDate { get; set; }
    public decimal OwnershipPercentage { get; set; }
    public bool IsCurrentOwner { get; set; }
}

public class EstateManagedAssetDocumentDto
{
    public Guid Id { get; set; }
    public Guid EstateManagedAssetId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentName { get; set; }
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
    public bool IsListingImage { get; set; }
    public bool IsPrimaryListingImage { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public string? CentralDocumentReference { get; set; }
    public DateTime? PublishedToCentralDmsAt { get; set; }
}

public class RegisterEstateManagedAssetDocumentDto
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentName { get; set; }
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public bool IsListingImage { get; set; }
    public bool IsPrimaryListingImage { get; set; }
}

public class UpdateEstateManagedAssetListingDto
{
    public bool IsPublishedToExternalPortal { get; set; }
    public string ExternalListingType { get; set; } = "None";
    public string ExternalListingStatus { get; set; } = "Draft";
    public decimal? ExternalListingPrice { get; set; }
    public string ExternalListingCurrency { get; set; } = "GHS";
    public string? ExternalListingNotes { get; set; }
}

public class CreateManualExistingLandDto
{
    public string? AssetCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string ZoningClassification { get; set; } = string.Empty;
    public string PlanningComplianceStatus { get; set; } = string.Empty;
    public string GisLayerReference { get; set; } = string.Empty;
    public string CadastreDescription { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Town { get; set; } = string.Empty;
    public decimal AreaValue { get; set; }
    public string AreaUnit { get; set; } = string.Empty;
    public decimal AreaSquareMeters { get; set; }
    public string SurveyorName { get; set; } = string.Empty;
    public DateTime SurveyDate { get; set; }
    public string SurveyPlanNumber { get; set; } = string.Empty;
    public string MapSheetNumber { get; set; } = string.Empty;
    public int BeaconCount { get; set; }
    public string BoundaryCoordinates { get; set; } = string.Empty;
    public bool BoundaryVerified { get; set; }
    public decimal ValuationAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public string Notes { get; set; } = string.Empty;
    public bool IsReadyForProjectManagement { get; set; }
    public List<ExistingLandOwnerDto> OwnershipHistory { get; set; } = [];
}

public class UpdateEstateManagedLandDemarcationDto
{
    public string CadastreDescription { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Town { get; set; } = string.Empty;
    public decimal AreaValue { get; set; }
    public string AreaUnit { get; set; } = string.Empty;
    public decimal? AreaSquareMeters { get; set; }
    public string SurveyorName { get; set; } = string.Empty;
    public DateTime? SurveyDate { get; set; }
    public string SurveyPlanNumber { get; set; } = string.Empty;
    public string MapSheetNumber { get; set; } = string.Empty;
    public int BeaconCount { get; set; }
    public string BoundaryCoordinates { get; set; } = string.Empty;
    public bool BoundaryVerified { get; set; }
    public bool IsReadyForProjectManagement { get; set; }
}

public class EstateManagedAssetQuery
{
    public EstateManagedAssetType? AssetType { get; set; }
    public EstateManagedAssetStatus? Status { get; set; }
    public List<EstateManagedAssetStatus> ExcludedStatuses { get; set; } = new();
    public string? Search { get; set; }
    public bool? AvailableForLease { get; set; }
    public bool? AvailableForSale { get; set; }
    public bool? AvailableForSaleOrLease { get; set; }
    public int Take { get; set; } = 100;
}

public class ProjectUnitEstateHandoffDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid ProjectUnitId { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string ProjectUnitName { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;
    public string UnitStatus { get; set; } = string.Empty;
    public string? BlockName { get; set; }
    public string? FloorLabel { get; set; }
    public string? Location { get; set; }
    public decimal? AreaSquareMeters { get; set; }
    public decimal? ValuationAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public bool IsAvailableForLease { get; set; } = true;
    public bool IsAvailableForSale { get; set; }
    public DateTime? HandoverDate { get; set; }
    public string? Notes { get; set; }
}

public class LandAcquisitionEstateHandoffDto
{
    public Guid LandAcquisitionId { get; set; }
    public string ProjectReference { get; set; } = string.Empty;
    public string? AssetCode { get; set; }
    public string? AssetNumber { get; set; }
    public string? ParcelIdentifier { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Purpose { get; set; }
    public decimal? AreaSquareMeters { get; set; }
    public decimal? AreaValue { get; set; }
    public string? AreaUnit { get; set; }
    public decimal? ValuationAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public string? BoundaryCoordinates { get; set; }
    public string? SurveyPlanNumber { get; set; }
    public string? MapSheetNumber { get; set; }
    public string? CadastreDescription { get; set; }
    public string? Region { get; set; }
    public string? District { get; set; }
    public string? Town { get; set; }
    public string? ZoningClassification { get; set; }
    public string? PlanningComplianceStatus { get; set; }
    public string? GisLayerReference { get; set; }
    public string? SurveyorName { get; set; }
    public DateTime? SurveyDate { get; set; }
    public int? BeaconCount { get; set; }
    public List<ExistingLandOwnerDto> OwnershipHistory { get; set; } = [];
    public bool BoundaryVerified { get; set; }
    public bool IsReadyForProjectManagement { get; set; }
    public string? Notes { get; set; }
}

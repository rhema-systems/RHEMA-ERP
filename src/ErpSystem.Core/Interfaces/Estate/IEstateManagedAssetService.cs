using ErpSystem.Core.DTOs.Estate;

namespace ErpSystem.Core.Interfaces.Estate;

public interface IEstateManagedAssetService
{
    Task<IReadOnlyList<EstateManagedAssetDto>> GetManagedAssetsAsync(EstateManagedAssetQuery query);
    Task<EstateManagedAssetDto> PublishLandAcquisitionAsync(LandAcquisitionEstateHandoffDto handoff);
    Task<EstateManagedAssetDto> PublishProjectUnitAsync(ProjectUnitEstateHandoffDto handoff);
    Task<EstateManagedAssetDto> CreateManualExistingLandAsync(CreateManualExistingLandDto request);
    Task<EstateManagedAssetDto> MarkReadyForProjectManagementAsync(Guid assetId);
    Task<EstateManagedAssetDto> UpdateExternalListingAsync(Guid assetId, UpdateEstateManagedAssetListingDto request);
    Task<EstateManagedAssetDocumentDto> RegisterDocumentAsync(Guid assetId, RegisterEstateManagedAssetDocumentDto document);
    Task<EstateManagedAssetDocumentDto> SetPrimaryListingImageAsync(Guid assetId, Guid documentId);
    Task<IReadOnlyList<EstateManagedAssetDocumentDto>> GetDocumentsAsync(Guid assetId);
    Task<(EstateManagedAssetDocumentDto Document, string FilePath)> GetDocumentAsync(Guid assetId, Guid documentId);
}

using ErpSystem.Core.DTOs.Estate;

namespace ErpSystem.Core.Interfaces.Estate;

public interface IEstateManagedAssetService
{
    Task<IReadOnlyList<EstateManagedAssetDto>> GetManagedAssetsAsync(EstateManagedAssetQuery query);
    Task<EstateManagedAssetDto> PublishLandAcquisitionAsync(LandAcquisitionEstateHandoffDto handoff);
    Task<EstateManagedAssetDto> PublishProjectUnitAsync(ProjectUnitEstateHandoffDto handoff);
    Task WithdrawProjectUnitAsync(Guid projectUnitId);
    Task<EstateManagedAssetDto> CreateManualExistingLandAsync(CreateManualExistingLandDto request);
    Task<IReadOnlyList<EstateLandDemarcationDto>> GetLandDemarcationsAsync(Guid assetId);
    Task<IReadOnlyList<ProjectReadyLandDemarcationDto>> GetProjectReadyLandDemarcationsAsync(Guid? projectId = null);
    Task<EstateLandDemarcationDto> CreateLandDemarcationAsync(Guid assetId, SaveEstateLandDemarcationDto request);
    Task<EstateLandDemarcationDto> UpdateLandDemarcationAsync(Guid assetId, Guid demarcationId, SaveEstateLandDemarcationDto request);
    Task DeleteLandDemarcationAsync(Guid assetId, Guid demarcationId);
    Task<EstateManagedAssetDto> MarkReadyForProjectManagementAsync(Guid assetId);
    Task<EstateManagedAssetDto> UpdateRegisterAsync(Guid assetId, UpdateEstateManagedAssetRegisterDto request);
    Task<EstateManagedAssetDto> UpdateExternalListingAsync(Guid assetId, UpdateEstateManagedAssetListingDto request);
    Task<EstateManagedAssetDocumentDto> RegisterDocumentAsync(Guid assetId, RegisterEstateManagedAssetDocumentDto document);
    Task<EstateManagedAssetDocumentDto> SetPrimaryListingImageAsync(Guid assetId, Guid documentId);
    Task<IReadOnlyList<EstateManagedAssetDocumentDto>> GetDocumentsAsync(Guid assetId);
    Task<(EstateManagedAssetDocumentDto Document, string FilePath)> GetDocumentAsync(Guid assetId, Guid documentId);
}

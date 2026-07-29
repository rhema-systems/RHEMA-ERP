using ErpSystem.Core.DTOs.Estate;

namespace ErpSystem.Api.Services.Estate;

public interface IEstateGisIntegrationService
{
    Task<EstateGisConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken);
    Task<EstateGisRuntimeConfigurationDto> GetRuntimeConfigurationAsync(
        CancellationToken cancellationToken);
    Task<EstateGisConfigurationDto> SaveConfigurationAsync(
        UpdateEstateGisConfigurationDto request,
        CancellationToken cancellationToken);
    Task<EstateGisConnectionTestDto> TestConnectionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<EstateGisLayerDto>> GetLayersAsync(CancellationToken cancellationToken);
    Task<EstateAssetGisLinkDto> LinkAssetAsync(
        Guid assetId,
        LinkEstateAssetGisFeatureDto request,
        CancellationToken cancellationToken);
    Task UnlinkAssetAsync(Guid assetId, CancellationToken cancellationToken);
    Task<string?> GetAssetGeometryAsync(Guid assetId, CancellationToken cancellationToken);
}

namespace ErpSystem.Core.DTOs.Estate;

public sealed class EstateGisConfigurationDto
{
    public Guid? Id { get; set; }
    public bool IsEnabled { get; set; }
    public string? SqlServerHost { get; set; }
    public int? SqlServerPort { get; set; }
    public string? SqlServerDatabase { get; set; }
    public string? SqlServerSchema { get; set; }
    public string? GeoServerBaseUrl { get; set; }
    public string? GeoServerWorkspace { get; set; }
    public string? DefaultFeatureLayer { get; set; }
    public string? ArcGisFeatureServiceUrl { get; set; }
    public string? BaseMapTileUrl { get; set; }
    public string SourceCrs { get; set; } = string.Empty;
    public string DisplayCrs { get; set; } = "EPSG:4326";
    public bool HasGeoServerCredentials { get; set; }
    public bool HasArcGisToken { get; set; }
    public string ConnectionStatus { get; set; } = "NotTested";
    public DateTime? LastConnectionTestAt { get; set; }
    public string? LastConnectionMessage { get; set; }
}

public sealed class EstateGisRuntimeConfigurationDto
{
    public bool IsEnabled { get; set; }
    public bool HasGeoServer { get; set; }
    public bool HasArcGis { get; set; }
    public string? DefaultFeatureLayer { get; set; }
    public string? BaseMapTileUrl { get; set; }
    public string SourceCrs { get; set; } = string.Empty;
    public string DisplayCrs { get; set; } = "EPSG:4326";
}

public sealed class UpdateEstateGisConfigurationDto
{
    public bool IsEnabled { get; set; }
    public string? SqlServerHost { get; set; }
    public int? SqlServerPort { get; set; }
    public string? SqlServerDatabase { get; set; }
    public string? SqlServerSchema { get; set; }
    public string? GeoServerBaseUrl { get; set; }
    public string? GeoServerWorkspace { get; set; }
    public string? DefaultFeatureLayer { get; set; }
    public string? ArcGisFeatureServiceUrl { get; set; }
    public string? BaseMapTileUrl { get; set; }
    public string SourceCrs { get; set; } = string.Empty;
    public string DisplayCrs { get; set; } = "EPSG:4326";
    public string? GeoServerUsername { get; set; }
    public string? GeoServerPassword { get; set; }
    public string? ArcGisToken { get; set; }
    public bool ClearGeoServerCredentials { get; set; }
    public bool ClearArcGisToken { get; set; }
}

public sealed class EstateGisLayerDto
{
    public string Provider { get; set; } = string.Empty;
    public string LayerReference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? SourceCrs { get; set; }
}

public sealed class EstateGisProviderTestDto
{
    public string Provider { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class EstateGisConnectionTestDto
{
    public bool Success { get; set; }
    public DateTime TestedAt { get; set; }
    public IReadOnlyList<EstateGisProviderTestDto> Providers { get; set; } = Array.Empty<EstateGisProviderTestDto>();
}

public sealed class LinkEstateAssetGisFeatureDto
{
    public string Provider { get; set; } = "GeoServer";
    public string LayerReference { get; set; } = string.Empty;
    public string FeatureId { get; set; } = string.Empty;
    public string SourceCrs { get; set; } = string.Empty;
}

public sealed class EstateAssetGisLinkDto
{
    public Guid AssetId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string LayerReference { get; set; } = string.Empty;
    public string FeatureId { get; set; } = string.Empty;
    public string SourceCrs { get; set; } = string.Empty;
    public string SyncStatus { get; set; } = string.Empty;
    public DateTime? LastSyncedAt { get; set; }
}

using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Estate;

public sealed class EstateGisConfiguration : TenantEntity
{
    public bool IsEnabled { get; set; }

    [MaxLength(500)]
    public string? SqlServerHost { get; set; }

    public int? SqlServerPort { get; set; }

    [MaxLength(160)]
    public string? SqlServerDatabase { get; set; }

    [MaxLength(80)]
    public string? SqlServerSchema { get; set; }

    [MaxLength(500)]
    public string? GeoServerBaseUrl { get; set; }

    [MaxLength(160)]
    public string? GeoServerWorkspace { get; set; }

    [MaxLength(240)]
    public string? DefaultFeatureLayer { get; set; }

    [MaxLength(500)]
    public string? ArcGisFeatureServiceUrl { get; set; }

    [MaxLength(1000)]
    public string? BaseMapTileUrl { get; set; }

    [MaxLength(80)]
    public string SourceCrs { get; set; } = string.Empty;

    [MaxLength(80)]
    public string DisplayCrs { get; set; } = "EPSG:4326";

    public string? ProtectedCredentials { get; set; }

    [MaxLength(40)]
    public string ConnectionStatus { get; set; } = "NotTested";

    public DateTime? LastConnectionTestAt { get; set; }

    [MaxLength(1000)]
    public string? LastConnectionMessage { get; set; }
}

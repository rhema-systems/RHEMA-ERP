using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Estate;

public sealed class EstateGisIntegrationService : IEstateGisIntegrationService
{
    private const string ProtectorPurpose = "Estate.Gis.Credentials.v1";
    private const int MaxGeometryResponseBytes = 10 * 1024 * 1024;
    private static readonly string[] SupportedProviders = ["GeoServer", "ArcGIS"];

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly HttpClient _httpClient;
    private readonly IDataProtector _protector;
    private readonly ILogger<EstateGisIntegrationService> _logger;

    public EstateGisIntegrationService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        HttpClient httpClient,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<EstateGisIntegrationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _httpClient = httpClient;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _logger = logger;
    }

    public async Task<EstateGisConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await LoadConfigurationAsync(cancellationToken);
        return configuration == null
            ? new EstateGisConfigurationDto()
            : ToDto(configuration, ReadCredentials(configuration));
    }

    public async Task<EstateGisRuntimeConfigurationDto> GetRuntimeConfigurationAsync(
        CancellationToken cancellationToken)
    {
        var configuration = await LoadConfigurationAsync(cancellationToken);
        return configuration == null
            ? new EstateGisRuntimeConfigurationDto()
            : new EstateGisRuntimeConfigurationDto
            {
                IsEnabled = configuration.IsEnabled,
                HasGeoServer = !string.IsNullOrWhiteSpace(configuration.GeoServerBaseUrl),
                HasArcGis = !string.IsNullOrWhiteSpace(configuration.ArcGisFeatureServiceUrl),
                DefaultFeatureLayer = configuration.DefaultFeatureLayer,
                BaseMapTileUrl = configuration.BaseMapTileUrl,
                SourceCrs = configuration.SourceCrs,
                DisplayCrs = configuration.DisplayCrs
            };
    }

    public async Task<EstateGisConfigurationDto> SaveConfigurationAsync(
        UpdateEstateGisConfigurationDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        ValidateConfiguration(request);
        var sourceCrs = Required(request.SourceCrs, "Source CRS is required.").ToUpperInvariant();
        var displayCrs = Required(request.DisplayCrs, "Display CRS is required.").ToUpperInvariant();

        var configuration = await LoadConfigurationAsync(cancellationToken);
        var credentials = configuration == null
            ? new GisCredentials()
            : ReadCredentials(configuration);

        if (request.ClearGeoServerCredentials)
        {
            credentials.GeoServerUsername = null;
            credentials.GeoServerPassword = null;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.GeoServerUsername))
                credentials.GeoServerUsername = request.GeoServerUsername.Trim();
            if (!string.IsNullOrWhiteSpace(request.GeoServerPassword))
                credentials.GeoServerPassword = request.GeoServerPassword;
        }

        if (request.ClearArcGisToken)
            credentials.ArcGisToken = null;
        else if (!string.IsNullOrWhiteSpace(request.ArcGisToken))
            credentials.ArcGisToken = request.ArcGisToken.Trim();

        var isNew = configuration == null;
        configuration ??= new EstateGisConfiguration
        {
            TenantId = tenantId,
            CreatedBy = _currentUser.UserName,
            CreatedById = ParseUserId(_currentUser.UserId)
        };

        configuration.IsEnabled = request.IsEnabled;
        configuration.SqlServerHost = Clean(request.SqlServerHost);
        configuration.SqlServerPort = request.SqlServerPort;
        configuration.SqlServerDatabase = Clean(request.SqlServerDatabase);
        configuration.SqlServerSchema = Clean(request.SqlServerSchema);
        configuration.GeoServerBaseUrl = NormalizeBaseUrl(request.GeoServerBaseUrl);
        configuration.GeoServerWorkspace = Clean(request.GeoServerWorkspace);
        configuration.DefaultFeatureLayer = Clean(request.DefaultFeatureLayer);
        configuration.ArcGisFeatureServiceUrl = NormalizeBaseUrl(request.ArcGisFeatureServiceUrl);
        configuration.BaseMapTileUrl = Clean(request.BaseMapTileUrl);
        configuration.SourceCrs = sourceCrs;
        configuration.DisplayCrs = displayCrs;
        configuration.ProtectedCredentials = HasCredentials(credentials)
            ? _protector.Protect(JsonSerializer.Serialize(credentials))
            : null;
        configuration.ConnectionStatus = "NotTested";
        configuration.LastConnectionTestAt = null;
        configuration.LastConnectionMessage = null;
        configuration.UpdatedAt = DateTime.UtcNow;
        configuration.UpdatedBy = _currentUser.UserName;
        configuration.LastModifiedById = ParseUserId(_currentUser.UserId);

        if (isNew)
            _db.Set<EstateGisConfiguration>().Add(configuration);

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(configuration, credentials);
    }

    public async Task<EstateGisConnectionTestDto> TestConnectionsAsync(CancellationToken cancellationToken)
    {
        var configuration = await RequireConfigurationAsync(cancellationToken);
        var credentials = ReadCredentials(configuration);
        var providerResults = new List<EstateGisProviderTestDto>();

        if (!string.IsNullOrWhiteSpace(configuration.SqlServerHost))
            providerResults.Add(await TestSqlServerEndpointAsync(configuration, cancellationToken));

        if (!string.IsNullOrWhiteSpace(configuration.GeoServerBaseUrl))
            providerResults.Add(await TestGeoServerAsync(configuration, credentials, cancellationToken));

        if (!string.IsNullOrWhiteSpace(configuration.ArcGisFeatureServiceUrl))
            providerResults.Add(await TestArcGisAsync(configuration, credentials, cancellationToken));

        if (providerResults.Count == 0)
            throw new InvalidOperationException("Configure a GeoServer or ArcGIS endpoint before testing.");

        var testedAt = DateTime.UtcNow;
        var success = providerResults.All(item => item.Success);
        configuration.ConnectionStatus = success ? "Connected" : "Failed";
        configuration.LastConnectionTestAt = testedAt;
        configuration.LastConnectionMessage = string.Join(
            " | ",
            providerResults.Select(item => $"{item.Provider}: {item.Message}"));
        configuration.UpdatedAt = testedAt;
        configuration.UpdatedBy = _currentUser.UserName;
        configuration.LastModifiedById = ParseUserId(_currentUser.UserId);
        await _db.SaveChangesAsync(cancellationToken);

        return new EstateGisConnectionTestDto
        {
            Success = success,
            TestedAt = testedAt,
            Providers = providerResults
        };
    }

    public async Task<IReadOnlyList<EstateGisLayerDto>> GetLayersAsync(CancellationToken cancellationToken)
    {
        var configuration = await RequireConfigurationAsync(cancellationToken);
        var credentials = ReadCredentials(configuration);
        var layers = new List<EstateGisLayerDto>();

        if (!string.IsNullOrWhiteSpace(configuration.GeoServerBaseUrl))
            layers.AddRange(await GetGeoServerLayersAsync(configuration, credentials, cancellationToken));

        if (!string.IsNullOrWhiteSpace(configuration.ArcGisFeatureServiceUrl))
            layers.AddRange(await GetArcGisLayersAsync(configuration, credentials, cancellationToken));

        return layers
            .OrderBy(item => item.Provider)
            .ThenBy(item => item.Title)
            .ToList();
    }

    public async Task<EstateAssetGisLinkDto> LinkAssetAsync(
        Guid assetId,
        LinkEstateAssetGisFeatureDto request,
        CancellationToken cancellationToken)
    {
        var provider = NormalizeProvider(request.Provider);
        var layerReference = Required(request.LayerReference, "GIS layer is required.");
        var featureId = Required(request.FeatureId, "GIS feature ID is required.");
        var sourceCrs = Required(request.SourceCrs, "GIS source CRS is required.").ToUpperInvariant();
        ValidateCrs(sourceCrs, "GIS source CRS");

        var configuration = await RequireConfigurationAsync(cancellationToken);
        if (!configuration.IsEnabled)
            throw new InvalidOperationException("Estate GIS integration is not enabled.");
        if (provider == "GeoServer" && string.IsNullOrWhiteSpace(configuration.GeoServerBaseUrl))
            throw new InvalidOperationException("GeoServer is not configured.");
        if (provider == "ArcGIS" && string.IsNullOrWhiteSpace(configuration.ArcGisFeatureServiceUrl))
            throw new InvalidOperationException("ArcGIS is not configured.");

        if (provider == "ArcGIS" && !int.TryParse(layerReference, out _))
            throw new InvalidOperationException("ArcGIS layer reference must be its numeric layer ID.");

        var asset = await LoadLandAssetAsync(assetId, cancellationToken);
        asset.GisProvider = provider;
        asset.GisLayerReference = layerReference;
        asset.GisFeatureId = featureId;
        asset.GisSourceCrs = sourceCrs;
        asset.GisSyncStatus = "Linked";
        asset.GisLastSyncedAt = DateTime.UtcNow;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUser.UserName;
        asset.LastModifiedById = ParseUserId(_currentUser.UserId);
        await _db.SaveChangesAsync(cancellationToken);

        return ToAssetLinkDto(asset);
    }

    public async Task UnlinkAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var asset = await LoadLandAssetAsync(assetId, cancellationToken);
        asset.GisProvider = "GeoServer";
        asset.GisLayerReference = null;
        asset.GisFeatureId = null;
        asset.GisSourceCrs = null;
        asset.GisSyncStatus = "NotLinked";
        asset.GisLastSyncedAt = null;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUser.UserName;
        asset.LastModifiedById = ParseUserId(_currentUser.UserId);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetAssetGeometryAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var configuration = await RequireConfigurationAsync(cancellationToken);
        if (!configuration.IsEnabled)
            throw new InvalidOperationException("Estate GIS integration is not enabled.");

        var asset = await LoadLandAssetAsync(assetId, cancellationToken);
        if (string.IsNullOrWhiteSpace(asset.GisFeatureId) || string.IsNullOrWhiteSpace(asset.GisLayerReference))
            return null;

        var credentials = ReadCredentials(configuration);
        return asset.GisProvider.Equals("ArcGIS", StringComparison.OrdinalIgnoreCase)
            ? await GetArcGisGeometryAsync(configuration, credentials, asset, cancellationToken)
            : await GetGeoServerGeometryAsync(configuration, credentials, asset, cancellationToken);
    }

    private async Task<EstateGisProviderTestDto> TestGeoServerAsync(
        EstateGisConfiguration configuration,
        GisCredentials credentials,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateGeoServerRequest(
                BuildGeoServerUrl(configuration.GeoServerBaseUrl!, new Dictionary<string, string?>
                {
                    ["service"] = "WMS",
                    ["version"] = "1.3.0",
                    ["request"] = "GetCapabilities"
                }),
                credentials);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return ProviderResult("GeoServer", true, "WMS capabilities are available.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Estate GeoServer connection test failed for tenant {TenantId}.", RequireTenantId());
            return ProviderResult("GeoServer", false, SafeConnectionMessage(ex));
        }
    }

    private async Task<EstateGisProviderTestDto> TestSqlServerEndpointAsync(
        EstateGisConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(
                configuration.SqlServerHost!,
                configuration.SqlServerPort ?? 1433,
                cancellationToken);
            return ProviderResult("SQL Server", true, "SQL Server endpoint is reachable.");
        }
        catch (Exception ex) when (ex is SocketException or TaskCanceledException or ArgumentException)
        {
            _logger.LogWarning(
                ex,
                "Estate GIS SQL Server endpoint test failed for tenant {TenantId}.",
                RequireTenantId());
            return ProviderResult("SQL Server", false, SafeConnectionMessage(ex));
        }
    }

    private async Task<EstateGisProviderTestDto> TestArcGisAsync(
        EstateGisConfiguration configuration,
        GisCredentials credentials,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = BuildArcGisUrl(configuration.ArcGisFeatureServiceUrl!, credentials, new Dictionary<string, string?>
            {
                ["f"] = "json"
            });
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.TryGetProperty("error", out var error))
                throw new HttpRequestException(ReadArcGisError(error));
            return ProviderResult("ArcGIS", true, "Feature service is available.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Estate ArcGIS connection test failed for tenant {TenantId}.", RequireTenantId());
            return ProviderResult("ArcGIS", false, SafeConnectionMessage(ex));
        }
    }

    private async Task<IReadOnlyList<EstateGisLayerDto>> GetGeoServerLayersAsync(
        EstateGisConfiguration configuration,
        GisCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var request = CreateGeoServerRequest(
            BuildGeoServerUrl(configuration.GeoServerBaseUrl!, new Dictionary<string, string?>
            {
                ["service"] = "WFS",
                ["version"] = "2.0.0",
                ["request"] = "GetCapabilities"
            }),
            credentials);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return xml.Descendants()
            .Where(element => element.Name.LocalName == "FeatureType")
            .Select(element => new
            {
                Name = element.Elements().FirstOrDefault(item => item.Name.LocalName == "Name")?.Value,
                Title = element.Elements().FirstOrDefault(item => item.Name.LocalName == "Title")?.Value,
                Crs = element.Elements().FirstOrDefault(item =>
                    item.Name.LocalName is "DefaultCRS" or "DefaultSRS")?.Value
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Where(item => string.IsNullOrWhiteSpace(configuration.GeoServerWorkspace)
                || item.Name!.StartsWith($"{configuration.GeoServerWorkspace}:", StringComparison.OrdinalIgnoreCase))
            .Select(item => new EstateGisLayerDto
            {
                Provider = "GeoServer",
                LayerReference = item.Name!,
                Title = string.IsNullOrWhiteSpace(item.Title) ? item.Name! : item.Title!,
                SourceCrs = item.Crs
            })
            .ToList();
    }

    private async Task<IReadOnlyList<EstateGisLayerDto>> GetArcGisLayersAsync(
        EstateGisConfiguration configuration,
        GisCredentials credentials,
        CancellationToken cancellationToken)
    {
        var url = BuildArcGisUrl(configuration.ArcGisFeatureServiceUrl!, credentials, new Dictionary<string, string?>
        {
            ["f"] = "json"
        });
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (document.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException(ReadArcGisError(error));

        if (!document.RootElement.TryGetProperty("layers", out var layers) || layers.ValueKind != JsonValueKind.Array)
            return Array.Empty<EstateGisLayerDto>();

        return layers.EnumerateArray()
            .Where(layer => layer.TryGetProperty("id", out _) && layer.TryGetProperty("name", out _))
            .Select(layer => new EstateGisLayerDto
            {
                Provider = "ArcGIS",
                LayerReference = layer.GetProperty("id").GetInt32().ToString(),
                Title = layer.GetProperty("name").GetString() ?? $"Layer {layer.GetProperty("id").GetInt32()}",
                SourceCrs = configuration.DisplayCrs
            })
            .ToList();
    }

    private async Task<string> GetGeoServerGeometryAsync(
        EstateGisConfiguration configuration,
        GisCredentials credentials,
        EstateManagedAsset asset,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.GeoServerBaseUrl))
            throw new InvalidOperationException("GeoServer is not configured.");

        var url = BuildGeoServerUrl(configuration.GeoServerBaseUrl, new Dictionary<string, string?>
        {
            ["service"] = "WFS",
            ["version"] = "2.0.0",
            ["request"] = "GetFeature",
            ["typeNames"] = asset.GisLayerReference,
            ["resourceId"] = asset.GisFeatureId,
            ["outputFormat"] = "application/json",
            ["srsName"] = configuration.DisplayCrs
        });
        using var request = CreateGeoServerRequest(url, credentials);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadBoundedJsonAsync(response, cancellationToken);
    }

    private async Task<string> GetArcGisGeometryAsync(
        EstateGisConfiguration configuration,
        GisCredentials credentials,
        EstateManagedAsset asset,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.ArcGisFeatureServiceUrl))
            throw new InvalidOperationException("ArcGIS feature service is not configured.");
        if (!int.TryParse(asset.GisLayerReference, out var layerId))
            throw new InvalidOperationException("The linked ArcGIS layer ID is invalid.");
        if (!long.TryParse(asset.GisFeatureId, out var objectId))
            throw new InvalidOperationException("The linked ArcGIS object ID is invalid.");

        var layerUrl = $"{configuration.ArcGisFeatureServiceUrl.TrimEnd('/')}/{layerId}/query";
        var url = BuildArcGisUrl(layerUrl, credentials, new Dictionary<string, string?>
        {
            ["f"] = "geojson",
            ["objectIds"] = objectId.ToString(),
            ["outFields"] = "*",
            ["returnGeometry"] = "true",
            ["outSR"] = ParseEpsgNumber(configuration.DisplayCrs)
        });
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadBoundedJsonAsync(response, cancellationToken);
    }

    private async Task<string> ReadBoundedJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxGeometryResponseBytes)
            throw new InvalidOperationException("The GIS geometry response exceeded the 10 MB limit.");

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        var totalBytes = 0;

        while (true)
        {
            var bytesRead = await responseStream.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
                break;

            totalBytes += bytesRead;
            if (totalBytes > MaxGeometryResponseBytes)
                throw new InvalidOperationException("The GIS geometry response exceeded the 10 MB limit.");

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }

        var json = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, totalBytes);

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException(ReadArcGisError(error));
        return json;
    }

    private async Task<EstateGisConfiguration?> LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        return await _db.Set<EstateGisConfiguration>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
    }

    private async Task<EstateGisConfiguration> RequireConfigurationAsync(CancellationToken cancellationToken)
        => await LoadConfigurationAsync(cancellationToken)
            ?? throw new InvalidOperationException("Estate GIS integration has not been configured.");

    private async Task<EstateManagedAsset> LoadLandAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        return await _db.EstateManagedAssets
            .FirstOrDefaultAsync(item =>
                item.Id == assetId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.AssetType == EstateManagedAssetType.Land, cancellationToken)
            ?? throw new KeyNotFoundException("Land asset was not found.");
    }

    private GisCredentials ReadCredentials(EstateGisConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.ProtectedCredentials))
            return new GisCredentials();

        try
        {
            var json = _protector.Unprotect(configuration.ProtectedCredentials);
            return JsonSerializer.Deserialize<GisCredentials>(json) ?? new GisCredentials();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Estate GIS credentials could not be decrypted for tenant {TenantId}.", configuration.TenantId);
            throw new InvalidOperationException("Stored GIS credentials could not be read. Re-enter them in GIS Integration.");
        }
    }

    private Guid RequireTenantId()
        => _currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new InvalidOperationException("A tenant context is required for Estate GIS.");

    private static void ValidateConfiguration(UpdateEstateGisConfigurationDto request)
    {
        if (request.SqlServerPort is <= 0 or > 65535)
            throw new InvalidOperationException("SQL Server port must be between 1 and 65535.");

        ValidateOptionalHttpUrl(request.GeoServerBaseUrl, "GeoServer URL");
        ValidateOptionalHttpUrl(request.ArcGisFeatureServiceUrl, "ArcGIS feature service URL");
        ValidateOptionalHttpUrl(request.BaseMapTileUrl, "Base-map tile URL");

        if (request.IsEnabled
            && string.IsNullOrWhiteSpace(request.GeoServerBaseUrl)
            && string.IsNullOrWhiteSpace(request.ArcGisFeatureServiceUrl))
        {
            throw new InvalidOperationException("Configure GeoServer or ArcGIS before enabling Estate GIS.");
        }

        ValidateCrs(Required(request.SourceCrs, "Source CRS is required."), "Source CRS");
        ValidateCrs(Required(request.DisplayCrs, "Display CRS is required."), "Display CRS");
    }

    private static void ValidateOptionalHttpUrl(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException($"{label} must be an absolute HTTP or HTTPS URL without embedded credentials.");
        }
    }

    private static void ValidateCrs(string value, string label)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (!normalized.StartsWith("EPSG:", StringComparison.Ordinal)
            || !int.TryParse(normalized[5..], out var srid)
            || srid <= 0)
        {
            throw new InvalidOperationException($"{label} must use the EPSG:<number> format.");
        }
    }

    private static string NormalizeProvider(string value)
    {
        var provider = SupportedProviders.FirstOrDefault(item =>
            item.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return provider ?? throw new InvalidOperationException("GIS provider must be GeoServer or ArcGIS.");
    }

    private static HttpRequestMessage CreateGeoServerRequest(string url, GisCredentials credentials)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(credentials.GeoServerUsername))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{credentials.GeoServerUsername}:{credentials.GeoServerPassword ?? string.Empty}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
        return request;
    }

    private static string BuildGeoServerUrl(string baseUrl, IReadOnlyDictionary<string, string?> query)
    {
        var normalized = baseUrl.TrimEnd('/');
        if (!normalized.EndsWith("/ows", StringComparison.OrdinalIgnoreCase))
            normalized += "/ows";
        return QueryHelpers.AddQueryString(normalized, query);
    }

    private static string BuildArcGisUrl(
        string baseUrl,
        GisCredentials credentials,
        IReadOnlyDictionary<string, string?> query)
    {
        var parameters = query.ToDictionary(item => item.Key, item => item.Value);
        if (!string.IsNullOrWhiteSpace(credentials.ArcGisToken))
            parameters["token"] = credentials.ArcGisToken;
        return QueryHelpers.AddQueryString(baseUrl, parameters);
    }

    private static string ParseEpsgNumber(string crs)
        => crs.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase) ? crs[5..] : crs;

    private static EstateGisConfigurationDto ToDto(
        EstateGisConfiguration configuration,
        GisCredentials credentials) => new()
    {
        Id = configuration.Id,
        IsEnabled = configuration.IsEnabled,
        SqlServerHost = configuration.SqlServerHost,
        SqlServerPort = configuration.SqlServerPort,
        SqlServerDatabase = configuration.SqlServerDatabase,
        SqlServerSchema = configuration.SqlServerSchema,
        GeoServerBaseUrl = configuration.GeoServerBaseUrl,
        GeoServerWorkspace = configuration.GeoServerWorkspace,
        DefaultFeatureLayer = configuration.DefaultFeatureLayer,
        ArcGisFeatureServiceUrl = configuration.ArcGisFeatureServiceUrl,
        BaseMapTileUrl = configuration.BaseMapTileUrl,
        SourceCrs = configuration.SourceCrs,
        DisplayCrs = configuration.DisplayCrs,
        HasGeoServerCredentials = !string.IsNullOrWhiteSpace(credentials.GeoServerUsername),
        HasArcGisToken = !string.IsNullOrWhiteSpace(credentials.ArcGisToken),
        ConnectionStatus = configuration.ConnectionStatus,
        LastConnectionTestAt = configuration.LastConnectionTestAt,
        LastConnectionMessage = configuration.LastConnectionMessage
    };

    private static EstateAssetGisLinkDto ToAssetLinkDto(EstateManagedAsset asset) => new()
    {
        AssetId = asset.Id,
        Provider = asset.GisProvider,
        LayerReference = asset.GisLayerReference ?? string.Empty,
        FeatureId = asset.GisFeatureId ?? string.Empty,
        SourceCrs = asset.GisSourceCrs ?? string.Empty,
        SyncStatus = asset.GisSyncStatus,
        LastSyncedAt = asset.GisLastSyncedAt
    };

    private static EstateGisProviderTestDto ProviderResult(string provider, bool success, string message)
        => new() { Provider = provider, Success = success, Message = message };

    private static string ReadArcGisError(JsonElement error)
    {
        if (error.TryGetProperty("message", out var message) && !string.IsNullOrWhiteSpace(message.GetString()))
            return message.GetString()!;
        return "ArcGIS returned an error.";
    }

    private static string SafeConnectionMessage(Exception exception)
        => exception is TaskCanceledException
            ? "Connection timed out."
            : string.IsNullOrWhiteSpace(exception.Message)
                ? "Connection failed."
                : exception.Message.Length <= 300
                    ? exception.Message
                    : exception.Message[..300];

    private static Guid? ParseUserId(string? value)
        => Guid.TryParse(value, out var userId) ? userId : null;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeBaseUrl(string? value)
        => Clean(value)?.TrimEnd('/');

    private static string Required(string? value, string message)
        => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(message) : value.Trim();

    private static bool HasCredentials(GisCredentials credentials)
        => !string.IsNullOrWhiteSpace(credentials.GeoServerUsername)
            || !string.IsNullOrWhiteSpace(credentials.GeoServerPassword)
            || !string.IsNullOrWhiteSpace(credentials.ArcGisToken);

    private sealed class GisCredentials
    {
        public string? GeoServerUsername { get; set; }
        public string? GeoServerPassword { get; set; }
        public string? ArcGisToken { get; set; }
    }
}

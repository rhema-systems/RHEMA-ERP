using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.DTOs.Estate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/gis")]
[Authorize]
public sealed class EstateGisController : ControllerBase
{
    private const string GisReadRoles =
        "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer,Survey Officer";
    private const string GisAdministrationRoles =
        "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin";
    private const string GisLinkRoles =
        "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Manager,Land Registry Officer,Survey Officer";

    private readonly IEstateGisIntegrationService _gisService;

    public EstateGisController(IEstateGisIntegrationService gisService)
    {
        _gisService = gisService;
    }

    [HttpGet("configuration")]
    [Authorize(Roles = GisAdministrationRoles)]
    public async Task<IActionResult> GetConfiguration(CancellationToken cancellationToken)
    {
        var configuration = await _gisService.GetConfigurationAsync(cancellationToken);
        return Ok(new { success = true, data = configuration });
    }

    [HttpGet("runtime")]
    [Authorize(Roles = GisReadRoles)]
    public async Task<IActionResult> GetRuntimeConfiguration(CancellationToken cancellationToken)
    {
        var configuration = await _gisService.GetRuntimeConfigurationAsync(cancellationToken);
        return Ok(new { success = true, data = configuration });
    }

    [HttpPut("configuration")]
    [Authorize(Roles = GisAdministrationRoles)]
    public async Task<IActionResult> SaveConfiguration(
        [FromBody] UpdateEstateGisConfigurationDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var configuration = await _gisService.SaveConfigurationAsync(request, cancellationToken);
            return Ok(new
            {
                success = true,
                data = configuration,
                message = "Estate GIS configuration saved."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("test")]
    [Authorize(Roles = GisAdministrationRoles)]
    public async Task<IActionResult> TestConnections(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _gisService.TestConnectionsAsync(cancellationToken);
            return result.Success
                ? Ok(new { success = true, data = result, message = "GIS connections verified." })
                : StatusCode(StatusCodes.Status502BadGateway, new
                {
                    success = false,
                    data = result,
                    message = "One or more GIS connections failed."
                });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("layers")]
    [Authorize(Roles = GisLinkRoles)]
    public async Task<IActionResult> GetLayers(CancellationToken cancellationToken)
    {
        try
        {
            var layers = await _gisService.GetLayersAsync(cancellationToken);
            return Ok(new { success = true, data = layers });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                message = $"GIS layer discovery failed: {ex.Message}"
            });
        }
    }

    [HttpPut("assets/{assetId:guid}/link")]
    [Authorize(Roles = GisLinkRoles)]
    public async Task<IActionResult> LinkAsset(
        Guid assetId,
        [FromBody] LinkEstateAssetGisFeatureDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var link = await _gisService.LinkAssetAsync(assetId, request, cancellationToken);
            return Ok(new { success = true, data = link, message = "Land asset linked to GIS." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("assets/{assetId:guid}/link")]
    [Authorize(Roles = GisLinkRoles)]
    public async Task<IActionResult> UnlinkAsset(Guid assetId, CancellationToken cancellationToken)
    {
        try
        {
            await _gisService.UnlinkAssetAsync(assetId, cancellationToken);
            return Ok(new { success = true, message = "Land asset unlinked from GIS." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("assets/{assetId:guid}/geometry")]
    [Authorize(Roles = GisReadRoles)]
    public async Task<IActionResult> GetAssetGeometry(Guid assetId, CancellationToken cancellationToken)
    {
        try
        {
            var geoJson = await _gisService.GetAssetGeometryAsync(assetId, cancellationToken);
            return geoJson == null
                ? NotFound(new { success = false, message = "This land asset is not linked to a GIS feature." })
                : Content(geoJson, "application/geo+json");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                message = $"GIS geometry retrieval failed: {ex.Message}"
            });
        }
    }
}

using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

/// <summary>
/// API controller for procurement settings management
/// </summary>
[ApiController]
[Route("api/procurement/[controller]")]
[Authorize]
public class ProcurementSettingsController : ControllerBase
{
    private readonly IProcurementSettingsService _settingsService;
    private readonly ILogger<ProcurementSettingsController> _logger;
    private readonly IProcurementMasterDataChangeService? _masterDataChanges;

    public ProcurementSettingsController(
        IProcurementSettingsService settingsService,
        ILogger<ProcurementSettingsController> logger,
        IProcurementMasterDataChangeService? masterDataChanges = null)
    {
        _settingsService = settingsService;
        _logger = logger;
        _masterDataChanges = masterDataChanges;
    }

    /// <summary>
    /// Get procurement settings for current tenant
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<ProcurementSettingsDto>> GetSettings()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving procurement settings");
            return StatusCode(500, "An error occurred while retrieving procurement settings");
        }
    }

    /// <summary>
    /// Update procurement settings
    /// </summary>
    [HttpPut]
    [Authorize(Policy = "procurement.access.manage")]
    public async Task<ActionResult<ProcurementSettingsDto>> UpdateSettings([FromBody] UpdateProcurementSettingsDto dto)
    {
        try
        {
            if (_masterDataChanges is not null)
            {
                var decision = await _masterDataChanges.CheckDirectMutationAsync(
                    new[] { ProcurementMasterDataResourceType.ProcurementPolicySensitive }, null,
                    "ProcurementSettings.Update", HttpContext.TraceIdentifier, HttpContext.RequestAborted);
                if (!decision.Allowed)
                    return Conflict(new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Staged procurement-settings change required",
                        Detail = decision.Message,
                        Instance = HttpContext.Request.Path,
                        Extensions = { ["code"] = decision.Code, ["correlationId"] = decision.CorrelationId, ["policyId"] = decision.PolicyId }
                    });
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var settings = await _settingsService.UpdateSettingsAsync(dto);
            _logger.LogInformation("Updated procurement settings");
            return Ok(settings);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating procurement settings");
            return StatusCode(500, "An error occurred while updating procurement settings");
        }
    }

    /// <summary>
    /// Check if auto-create inventory items is enabled
    /// </summary>
    [HttpGet("auto-create-inventory-items")]
    [AllowAnonymous] // Allow all authenticated users to check this setting
    [Authorize]
    public async Task<ActionResult<bool>> ShouldAutoCreateInventoryItems()
    {
        try
        {
            var result = await _settingsService.ShouldAutoCreateInventoryItemsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking auto-create inventory items setting");
            return StatusCode(500, "An error occurred while checking the setting");
        }
    }

    /// <summary>
    /// Check if auto-create supplier items is enabled
    /// </summary>
    [HttpGet("auto-create-supplier-items")]
    [AllowAnonymous] // Allow all authenticated users to check this setting
    [Authorize]
    public async Task<ActionResult<bool>> ShouldAutoCreateSupplierItems()
    {
        try
        {
            var result = await _settingsService.ShouldAutoCreateSupplierItemsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking auto-create supplier items setting");
            return StatusCode(500, "An error occurred while checking the setting");
        }
    }

    /// <summary>
    /// Check if non-inventory items are allowed
    /// </summary>
    [HttpGet("allow-non-inventory-items")]
    [AllowAnonymous] // Allow all authenticated users to check this setting
    [Authorize]
    public async Task<ActionResult<bool>> AllowNonInventoryItems()
    {
        try
        {
            var result = await _settingsService.AllowNonInventoryItemsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking allow non-inventory items setting");
            return StatusCode(500, "An error occurred while checking the setting");
        }
    }
}

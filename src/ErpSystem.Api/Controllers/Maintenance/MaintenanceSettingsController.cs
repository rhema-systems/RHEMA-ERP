using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/[controller]")]
[Authorize(Roles = "SuperAdmin,TenantAdmin")]
public class MaintenanceSettingsController : ControllerBase
{
    private readonly IMaintenanceSettingsService _settingsService;
    private readonly ILogger<MaintenanceSettingsController> _logger;

    public MaintenanceSettingsController(IMaintenanceSettingsService settingsService, ILogger<MaintenanceSettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<MaintenanceSettingsDto>> GetSettings()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance settings");
            return StatusCode(500, "An error occurred while retrieving maintenance settings");
        }
    }

    [HttpPut]
    public async Task<ActionResult<MaintenanceSettingsDto>> UpdateSettings([FromBody] UpdateMaintenanceSettingsDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updated = await _settingsService.UpdateSettingsAsync(dto);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance settings");
            return StatusCode(500, "An error occurred while updating maintenance settings");
        }
    }
}


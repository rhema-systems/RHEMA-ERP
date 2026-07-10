using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/reports")]
[Authorize(Policy = "MaintenanceRead")]
public sealed class MaintenanceReportsController : ControllerBase
{
    private readonly IMaintenanceOperationalReportsService _reportsService;
    private readonly ILogger<MaintenanceReportsController> _logger;

    public MaintenanceReportsController(
        IMaintenanceOperationalReportsService reportsService,
        ILogger<MaintenanceReportsController> logger)
    {
        _reportsService = reportsService;
        _logger = logger;
    }

    [HttpGet("operational")]
    [ProducesResponseType(typeof(MaintenanceOperationalReportsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceOperationalReportsDto>> GetOperationalReports(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] Guid? assetId = null)
    {
        try
        {
            return Ok(await _reportsService.GetOperationalReportsAsync(fromUtc, toUtc, assetId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance operational reports");
            return StatusCode(500, new { message = "An error occurred while retrieving maintenance reports." });
        }
    }
}

using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/drivers")]
[Authorize(Policy = "MaintenanceRead")]
public sealed class FleetDriversController : ControllerBase
{
    private readonly IFleetDriverDirectoryService _driverDirectoryService;
    private readonly ILogger<FleetDriversController> _logger;

    public FleetDriversController(
        IFleetDriverDirectoryService driverDirectoryService,
        ILogger<FleetDriversController> logger)
    {
        _driverDirectoryService = driverDirectoryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<FleetDriverDirectoryDto>> GetDrivers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? licenseStatus = null,
        [FromQuery] bool? assigned = null)
    {
        try
        {
            return Ok(await _driverDirectoryService.GetDriversPagedAsync(
                page,
                pageSize,
                searchTerm,
                licenseStatus,
                assigned));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Fleet drivers from HR");
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Error retrieving Fleet drivers",
                detail: "Fleet could not load driver and licence information from HR.");
        }
    }
}

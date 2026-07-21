using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Serves the organogram visualisation: each endpoint returns a flat list of uniform
/// <see cref="OrganogramNodeDto"/> for one dimension (units, positions, people, locations, teams).
/// The client builds the tree. All data is tenant-scoped by the DbContext query filters.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganogramController : ControllerBase
{
    private readonly IOrganogramService _service;
    private readonly ILogger<OrganogramController> _logger;

    public OrganogramController(IOrganogramService service, ILogger<OrganogramController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("units")]
    [ProducesResponseType(typeof(OrganogramResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganogramResponseDto>> GetUnits(CancellationToken cancellationToken)
        => await SafeAsync(() => _service.GetUnitsAsync(cancellationToken), "units");

    [HttpGet("positions")]
    [ProducesResponseType(typeof(OrganogramResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganogramResponseDto>> GetPositions(CancellationToken cancellationToken)
        => await SafeAsync(() => _service.GetPositionsAsync(cancellationToken), "positions");

    [HttpGet("people")]
    [ProducesResponseType(typeof(OrganogramResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganogramResponseDto>> GetPeople(CancellationToken cancellationToken)
        => await SafeAsync(() => _service.GetPeopleAsync(cancellationToken), "people");

    [HttpGet("locations")]
    [ProducesResponseType(typeof(OrganogramResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrganogramResponseDto>> GetLocations([FromQuery] Guid structureId, CancellationToken cancellationToken)
    {
        if (structureId == Guid.Empty)
            return BadRequest("A locationStructureId (structureId) is required.");

        return await SafeAsync(() => _service.GetLocationsAsync(structureId, cancellationToken), "locations");
    }

    [HttpGet("teams")]
    [ProducesResponseType(typeof(OrganogramResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganogramResponseDto>> GetTeams(CancellationToken cancellationToken)
        => await SafeAsync(() => _service.GetTeamsAsync(cancellationToken), "teams");

    private async Task<ActionResult<OrganogramResponseDto>> SafeAsync(
        Func<Task<OrganogramResponseDto>> action, string dimension)
    {
        try
        {
            return Ok(await action());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building {Dimension} organogram", dimension);
            return StatusCode(500, $"Failed to build the {dimension} organogram.");
        }
    }
}

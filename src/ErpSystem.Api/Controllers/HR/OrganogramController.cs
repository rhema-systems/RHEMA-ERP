using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Serves the organogram visualisation: each endpoint returns a flat list of uniform
/// <see cref="OrganogramNodeDto"/> for one dimension (units, positions, people, locations, teams).
/// The client builds the tree. Every read is scoped to the authenticated tenant by the service.
/// </summary>
/// <remarks>
/// <para>
/// The gate is deliberately split, and the split is the whole point rather than an oversight.
/// Four of the five dimensions describe the <i>company</i> — its units, its posts, its sites, its
/// teams — which is exactly what an org chart exists to make common knowledge, so they stay open to
/// any authenticated user. Restricting them would be gating the noticeboard.
/// </para>
/// <para>
/// <c>people</c> is not that. It is the personnel register: slice 0 measured a plain
/// <c>Employee</c> pulling all 6,237 staff records out of it in one unpaged call, every one of them
/// carrying a work email address in <c>meta</c>. That is a staff-directory export wearing a chart's
/// clothes, and it is HR's to hold.
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class OrganogramController : ControllerBase
{
    /// <summary>
    /// Who may read the people dimension. Matches the company-profile gate from slice 1 — if TDC
    /// wants the reporting chart visible company-wide, adding <c>Constants.Roles.Employee</c> here
    /// is the whole change, but do it knowing the payload includes everyone's email.
    /// </summary>
    private const string PeopleRoles =
        Constants.Roles.SuperAdmin + "," + Constants.Roles.TenantAdmin + "," + Constants.Roles.Hr;

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
    [Authorize(Roles = PeopleRoles)]
    [ProducesResponseType(typeof(OrganogramResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

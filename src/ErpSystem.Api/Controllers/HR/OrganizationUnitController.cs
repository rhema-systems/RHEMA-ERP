using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Controller for managing organization units
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class OrganizationUnitController : ControllerBase
{
    /// <summary>
    /// Who may change the organisation structure. Reads stay open on purpose — the unit tree is the
    /// company noticeboard, it is what the organogram's unit view renders to everybody, and every
    /// unit picker in HR reads it.
    /// </summary>
    /// <remarks>
    /// ⚠ Added in areas 19–23 slice 12, and it was overdue: this controller was a bare
    /// <c>[Authorize]</c>, so <b>any authenticated user could create, rename, reparent, delete a
    /// unit or appoint its head</b>. Slice 12 is what made that urgent rather than merely wrong — it
    /// puts the move and change-head endpoints behind buttons, and wiring a screen to an ungated
    /// write is how a defect acquires a user.
    ///
    /// The gate costs nothing in reach, and that was measured before it was chosen (slice 9's rule):
    /// every frontend caller of a unit write lives under <c>/administration/hr/organization/units</c>.
    /// Same split as the teams register two slices over — reads open, writes gated. W3 slice 11
    /// converted the role gate to the HR.Employee family (writes → Write, delete → Admin).
    /// </remarks>
    private readonly IOrganizationUnitService _organizationUnitService;
    private readonly ILogger<OrganizationUnitController> _logger;

    public OrganizationUnitController(
        IOrganizationUnitService organizationUnitService,
        ILogger<OrganizationUnitController> logger)
    {
        _organizationUnitService = organizationUnitService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all organization units
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrganizationUnitDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _organizationUnitService.GetAllAsync();
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all organization units");
            return StatusCode(500, "An error occurred while retrieving organization units");
        }
    }

    /// <summary>
    /// Retrieves organization units with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<OrganizationUnitDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _organizationUnitService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged organization units");
            return StatusCode(500, "An error occurred while retrieving organization units");
        }
    }

    /// <summary>
    /// Retrieves all organization units as summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationUnitSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSummary()
    {
        try
        {
            var response = await _organizationUnitService.GetAllSummaryAsync();
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization unit summaries");
            return StatusCode(500, "An error occurred while retrieving organization unit summaries");
        }
    }

    /// <summary>
    /// Retrieves an organization unit by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _organizationUnitService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization unit with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the organization unit");
        }
    }

    /// <summary>
    /// Retrieves detailed organization unit by ID
    /// </summary>
    [HttpGet("{id:guid}/detail")]
    [ProducesResponseType(typeof(OrganizationUnitDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailById(Guid id)
    {
        try
        {
            var response = await _organizationUnitService.GetDetailByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization unit detail with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the organization unit detail");
        }
    }

    /// <summary>
    /// Retrieves organization units by level ID
    /// </summary>
    [HttpGet("level/{levelId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationUnitDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByLevelId(Guid levelId)
    {
        try
        {
            var response = await _organizationUnitService.GetByLevelIdAsync(levelId);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization units for level {LevelId}", levelId);
            return StatusCode(500, "An error occurred while retrieving organization units");
        }
    }

    /// <summary>
    /// Retrieves child units of a parent unit
    /// </summary>
    [HttpGet("{parentUnitId:guid}/children")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationUnitDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildUnits(Guid parentUnitId)
    {
        try
        {
            var response = await _organizationUnitService.GetChildUnitsAsync(parentUnitId);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving child units for parent {ParentUnitId}", parentUnitId);
            return StatusCode(500, "An error occurred while retrieving child units");
        }
    }

    /// <summary>
    /// Retrieves root organization units
    /// </summary>
    [HttpGet("root")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationUnitDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRootUnits()
    {
        try
        {
            var response = await _organizationUnitService.GetRootUnitsAsync();
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving root organization units");
            return StatusCode(500, "An error occurred while retrieving root organization units");
        }
    }

    /// <summary>
    /// Retrieves full organization hierarchy tree
    /// </summary>
    [HttpGet("hierarchy/tree")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationUnitTreeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHierarchyTree()
    {
        try
        {
            var response = await _organizationUnitService.GetHierarchyTreeAsync();
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization hierarchy tree");
            return StatusCode(500, "An error occurred while retrieving the organization hierarchy tree");
        }
    }

    /// <summary>
    /// Retrieves hierarchy starting from a specific unit
    /// </summary>
    [HttpGet("{unitId:guid}/hierarchy")]
    [ProducesResponseType(typeof(OrganizationUnitHierarchyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHierarchyFromUnit(Guid unitId)
    {
        try
        {
            var response = await _organizationUnitService.GetHierarchyFromUnitAsync(unitId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hierarchy from unit {UnitId}", unitId);
            return StatusCode(500, "An error occurred while retrieving the unit hierarchy");
        }
    }

    /// <summary>
    /// Searches organization units by name or code
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationUnitDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string searchTerm)
    {
        try
        {
            var response = await _organizationUnitService.SearchAsync(searchTerm);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching organization units with term {SearchTerm}", searchTerm);
            return StatusCode(500, "An error occurred while searching organization units");
        }
    }

    /// <summary>
    /// Creates a new organization unit
    /// </summary>
    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(OrganizationUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationUnitDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _organizationUnitService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating organization unit");
            return StatusCode(500, "An error occurred while creating the organization unit");
        }
    }

    /// <summary>
    /// Updates an existing organization unit
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(OrganizationUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganizationUnitDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _organizationUnitService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating organization unit with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the organization unit");
        }
    }

    /// <summary>
    /// Deletes an organization unit
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _organizationUnitService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting organization unit with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the organization unit");
        }
    }

    /// <summary>
    /// Moves an organization unit to a new parent (restructure)
    /// </summary>
    [HttpPost("{unitId:guid}/move")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MoveUnit(Guid unitId, [FromBody] MoveUnitRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _organizationUnitService.MoveUnitAsync(unitId, request.NewParentId, request.ChangeReason);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving organization unit {UnitId}", unitId);
            return StatusCode(500, "An error occurred while moving the organization unit");
        }
    }

    /// <summary>
    /// Changes the head employee of an organization unit
    /// </summary>
    [HttpPost("{unitId:guid}/change-head")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangeHeadEmployee(Guid unitId, [FromBody] ChangeHeadRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _organizationUnitService.ChangeHeadEmployeeAsync(unitId, request.NewHeadEmployeeId, request.ChangeReason);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. Without this the service's 42 rules all
            // reached the caller as a canned 500 and said nothing.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing head employee for organization unit {UnitId}", unitId);
            return StatusCode(500, "An error occurred while changing the head employee");
        }
    }
}

/// <summary>
/// Request model for moving an organization unit
/// </summary>
public class MoveUnitRequest
{
    public Guid? NewParentId { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
}

/// <summary>
/// Request model for changing head employee
/// </summary>
public class ChangeHeadRequest
{
    public Guid? NewHeadEmployeeId { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
}

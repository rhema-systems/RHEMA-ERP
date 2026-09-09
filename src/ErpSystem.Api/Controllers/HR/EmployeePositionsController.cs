using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class EmployeePositionsController : ControllerBase
{
    private readonly IEmployeePositionService _service;

    public EmployeePositionsController(IEmployeePositionService service)
    {
        _service = service;
    }

    #region CRUD

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePositionDto>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionDto>>> GetActive()
        => Ok(await _service.GetActivePositionsAsync());

    [HttpGet("code/{code}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePositionDto>> GetByCode(string code)
    {
        var result = await _service.GetByCodeAsync(code);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeePositionDto>> Create([FromBody] CreateEmployeePositionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreatePositionAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. The global middleware would answer 400 with a canned
            // sentence; the reports-to rules (C1) exist to explain themselves.
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeePositionDto>> Update(Guid id, [FromBody] UpdateEmployeePositionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var updated = await _service.UpdatePositionAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeletePositionAsync(id);
        return NoContent();
    }

    #endregion

    /// <summary>
    /// Positions in a unit; with <c>includeAncestors=true</c>, in every unit above it as well —
    /// the reports-to option source (demo feedback round 2, C1). Had no caller before that slice.
    /// </summary>
    [HttpGet("organization-unit/{organizationUnitId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionDto>>> GetByOrganizationUnit(Guid organizationUnitId, [FromQuery] bool includeAncestors = false)
    {
        return Ok(await _service.GetByOrganizationUnitAsync(organizationUnitId, includeAncestors));
    }

    [HttpGet("department/{departmentId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionDto>>> GetByDepartment(Guid departmentId)
    {
        return Ok(await _service.GetByDepartmentAsync(departmentId));
    }
}

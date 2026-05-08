using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeePositionsController : ControllerBase
{
    private readonly IEmployeePositionService _service;

    public EmployeePositionsController(IEmployeePositionService service)
    {
        _service = service;
    }

    #region CRUD

    [HttpGet("{id:guid}")]
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
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePositionDto>> GetByCode(string code)
    {
        var result = await _service.GetByCodeAsync(code);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeePositionDto>> Create([FromBody] CreateEmployeePositionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreatePositionAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeePositionDto>> Update(Guid id, [FromBody] UpdateEmployeePositionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _service.UpdatePositionAsync(id, dto);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeletePositionAsync(id);
        return NoContent();
    }

    #endregion

    [HttpGet("organization-unit/{organizationUnitId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionDto>>> GetByOrganizationUnit(Guid organizationUnitId)
    {
        return Ok(await _service.GetByOrganizationUnitAsync(organizationUnitId));
    }

    [HttpGet("department/{departmentId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionDto>>> GetByDepartment(Guid departmentId)
    {
        return Ok(await _service.GetByDepartmentAsync(departmentId));
    }
}

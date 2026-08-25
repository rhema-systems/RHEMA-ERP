using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/hr/skills")]
[Authorize(Policy = "InternalOnly")]
public sealed class SkillsController : ControllerBase
{
    private readonly ISkillService _service;

    public SkillsController(ISkillService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SkillDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SkillDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<SkillDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SkillDto>>> GetActive()
        => Ok(await _service.GetActiveSkillsAsync());

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SkillDto>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("name/{name}")]
    [ProducesResponseType(typeof(SkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SkillDto>> GetByName(string name)
    {
        var result = await _service.GetByNameAsync(name);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(IEnumerable<SkillDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SkillDto>>> GetByCategory(string category)
        => Ok(await _service.GetByCategoryAsync(category));

    [HttpGet("categories")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories()
        => Ok(await _service.GetSkillCategoriesAsync());

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(SkillDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SkillDto>> Create([FromBody] CreateSkillDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateSkillAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(SkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SkillDto>> Update(Guid id, [FromBody] CreateSkillDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _service.UpdateSkillAsync(id, dto);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteSkillAsync(id);
        return NoContent();
    }
}

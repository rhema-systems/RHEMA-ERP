using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Full CRUD controller for the Qualification master catalogue.
/// Mirrors SkillsController in structure.
/// </summary>
[ApiController]
[Route("api/hr/qualifications")]
[Authorize]
public sealed class QualificationController : ControllerBase
{
    private readonly IQualificationCatalogueService _service;

    public QualificationController(IQualificationCatalogueService service)
        => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<QualificationCatalogueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<QualificationCatalogueDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<QualificationCatalogueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<QualificationCatalogueDto>>> GetActive()
        => Ok(await _service.GetActiveAsync());

    [HttpGet("by-type/{type}")]
    [ProducesResponseType(typeof(IEnumerable<QualificationCatalogueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<QualificationCatalogueDto>>> GetByType(QualificationType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(QualificationCatalogueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QualificationCatalogueDto>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(QualificationCatalogueDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QualificationCatalogueDto>> Create([FromBody] CreateQualificationCatalogueDto dto)
    {
        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(QualificationCatalogueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QualificationCatalogueDto>> Update(Guid id, [FromBody] CreateQualificationCatalogueDto dto)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, dto));
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? NotFound(new { message = ex.Message }) : BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}


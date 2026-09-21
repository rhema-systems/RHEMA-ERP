using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The language catalogue (round 3, lane C1). Gated like the other people-reference lookups:
/// reads on the employee read policy, writes on the employee write policy, delete on admin.
/// The anonymous careers form reads it through <c>PublicRecruitmentController</c> instead.
/// </summary>
[ApiController]
[Route("api/hr/languages")]
[Authorize(Policy = "InternalOnly")]
public class LanguagesController : ControllerBase
{
    private readonly ILanguageService _service;

    public LanguagesController(ILanguageService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<ActionResult<IEnumerable<LanguageDto>>> GetAll([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(activeOnly, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<ActionResult<LanguageDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result == null ? NotFound(new { message = "Language not found." }) : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<ActionResult<LanguageDto>> Create([FromBody] CreateLanguageDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<ActionResult<LanguageDto>> Update(Guid id, [FromBody] UpdateLanguageDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateAsync(id, dto, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        try { await _service.DeactivateAsync(id, cancellationToken); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try { await _service.DeleteAsync(id, cancellationToken); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return StatusCode(422, new { message = ex.Message }); }
    }
}

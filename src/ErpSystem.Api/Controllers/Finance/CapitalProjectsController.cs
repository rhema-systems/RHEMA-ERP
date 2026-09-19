using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/capital-projects")]
public class CapitalProjectsController : ControllerBase
{
    private readonly ICapitalProjectService _service;

    public CapitalProjectsController(ICapitalProjectService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CapitalProjectListDto>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CapitalProjectDetailDto>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CapitalProjectDetailDto>> Create(CreateCapitalProjectDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CapitalProjectDetailDto>> Update(Guid id, UpdateCapitalProjectDto dto)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, dto));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult<CapitalProjectDetailDto>> UpdateStatus(Guid id, UpdateProjectStatusDto dto)
    {
        try
        {
            return Ok(await _service.UpdateStatusAsync(id, dto));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/costs")]
    public async Task<ActionResult<CapitalProjectDetailDto>> PostCost(Guid id, AddProjectCostDto dto)
    {
        try
        {
            return Ok(await _service.PostCostToProjectAsync(id, dto));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id}/costs/{costId}")]
    public async Task<ActionResult<CapitalProjectDetailDto>> RemoveCost(Guid id, Guid costId)
    {
        try
        {
            return Ok(await _service.RemoveCostFromProjectAsync(id, costId));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/settlement-rules")]
    public async Task<ActionResult<CapitalProjectDetailDto>> AddSettlementRule(Guid id, AddSettlementRuleDto dto)
    {
        try
        {
            return Ok(await _service.AddSettlementRuleAsync(id, dto));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id}/settlement-rules/{ruleId}")]
    public async Task<ActionResult<CapitalProjectDetailDto>> RemoveSettlementRule(Guid id, Guid ruleId)
    {
        try
        {
            return Ok(await _service.RemoveSettlementRuleAsync(id, ruleId));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/capitalize")]
    public async Task<ActionResult<CapitalProjectDetailDto>> Capitalize(
        Guid id,
        [FromBody] CapitalizeCapitalProjectDto? dto = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.CapitalizeProjectAsync(id, dto, cancellationToken));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

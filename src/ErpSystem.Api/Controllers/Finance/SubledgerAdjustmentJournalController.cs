using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/subledger-adjustment-journals")]
public class SubledgerAdjustmentJournalController : ControllerBase
{
    private readonly ISubledgerAdjustmentJournalService _service;

    public SubledgerAdjustmentJournalController(ISubledgerAdjustmentJournalService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubledgerAdjustmentJournalDto>>> GetAll(
        [FromQuery] string? module = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetAllAsync(module, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubledgerAdjustmentJournalDto>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adjustment = await _service.GetByIdAsync(id, cancellationToken);
        return adjustment == null ? NotFound() : Ok(adjustment);
    }

    [HttpPost]
    public async Task<ActionResult<SubledgerAdjustmentJournalDto>> CreateAndPost(
        [FromBody] CreateSubledgerAdjustmentJournalDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var adjustment = await _service.CreateAndPostAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = adjustment.Id }, adjustment);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reverse")]
    public async Task<ActionResult<SubledgerAdjustmentJournalDto>> Reverse(
        Guid id,
        [FromBody] ReverseSubledgerAdjustmentJournalDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.ReverseAsync(id, dto, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

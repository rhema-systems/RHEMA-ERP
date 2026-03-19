using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/leases")]
public class LeaseAccountingController : ControllerBase
{
    private readonly ILeaseAccountingService _service;

    public LeaseAccountingController(ILeaseAccountingService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaseContractListDto>>> GetAll()
    {
        return Ok(await _service.GetLeasesAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LeaseContractDetailDto>> GetById(Guid id)
    {
        var result = await _service.GetLeaseByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("preview")]
    public async Task<ActionResult<List<LeaseScheduleLineDto>>> PreviewSchedule(CreateLeaseContractDto dto)
    {
        try
        {
            return Ok(await _service.PreviewScheduleAsync(dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<LeaseContractDetailDto>> Create(CreateLeaseContractDto dto)
    {
        try
        {
            var result = await _service.CreateLeaseAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/activate")]
    public async Task<ActionResult<LeaseContractDetailDto>> Activate(Guid id)
    {
        try
        {
            return Ok(await _service.ActivateLeaseAsync(id));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/schedule-lines/{lineId}/post")]
    public async Task<ActionResult<LeaseContractDetailDto>> PostPeriodJournal(Guid id, Guid lineId)
    {
        try
        {
            return Ok(await _service.PostPeriodJournalAsync(id, lineId));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

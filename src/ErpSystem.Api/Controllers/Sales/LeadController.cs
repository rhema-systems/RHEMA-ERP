using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize]
[ApiController]
[Route("api/sales/leads")]
public class LeadController : ControllerBase
{
    private readonly ILeadService _leadService;

    public LeadController(ILeadService leadService)
    {
        _leadService = leadService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? source = null,
        [FromQuery] Guid? assignedToId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _leadService.GetAllAsync(page, pageSize, search, status, source, assignedToId, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var lead = await _leadService.GetByIdAsync(id);
        return lead == null ? NotFound() : Ok(lead);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeadDto dto)
    {
        var lead = await _leadService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = lead.Id }, lead);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLeadDto dto)
    {
        var lead = await _leadService.UpdateAsync(id, dto);
        return Ok(lead);
    }

    [HttpPost("{id:guid}/qualify")]
    public async Task<IActionResult> Qualify(Guid id, [FromQuery] int? score = null)
    {
        var lead = await _leadService.QualifyAsync(id, score);
        return Ok(lead);
    }

    [HttpPost("{id:guid}/disqualify")]
    public async Task<IActionResult> Disqualify(Guid id, [FromQuery] string? reason = null)
    {
        var lead = await _leadService.DisqualifyAsync(id, reason);
        return Ok(lead);
    }

    [HttpPost("{id:guid}/convert")]
    public async Task<IActionResult> Convert(Guid id, [FromBody] ConvertLeadDto dto)
    {
        var lead = await _leadService.ConvertToCustomerAsync(id, dto);
        return Ok(lead);
    }

    [HttpGet("by-status/{status}")]
    public async Task<IActionResult> GetByStatus(string status)
    {
        var leads = await _leadService.GetByStatusAsync(status);
        return Ok(leads);
    }

    [HttpGet("upcoming-followups")]
    public async Task<IActionResult> GetUpcomingFollowUps([FromQuery] int daysAhead = 7)
    {
        var leads = await _leadService.GetUpcomingFollowUpsAsync(daysAhead);
        return Ok(leads);
    }
}

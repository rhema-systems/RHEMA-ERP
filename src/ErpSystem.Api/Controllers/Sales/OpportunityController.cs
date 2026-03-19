using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize]
[ApiController]
[Route("api/sales/opportunities")]
public class OpportunityController : ControllerBase
{
    private readonly IOpportunityService _opportunityService;

    public OpportunityController(IOpportunityService opportunityService)
    {
        _opportunityService = opportunityService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? stage = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? assignedToId = null,
        [FromQuery] string? opportunityType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _opportunityService.GetAllAsync(page, pageSize, search, stage, customerId, assignedToId, opportunityType, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var opp = await _opportunityService.GetByIdAsync(id);
        return opp == null ? NotFound() : Ok(opp);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOpportunityDto dto)
    {
        var opp = await _opportunityService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = opp.Id }, opp);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOpportunityDto dto)
    {
        var opp = await _opportunityService.UpdateAsync(id, dto);
        return Ok(opp);
    }

    [HttpPost("{id:guid}/advance")]
    public async Task<IActionResult> AdvanceStage(Guid id, [FromQuery] string newStage, [FromQuery] string? notes = null)
    {
        var opp = await _opportunityService.AdvanceStageAsync(id, newStage, notes);
        return Ok(opp);
    }

    [HttpPost("{id:guid}/close-won")]
    public async Task<IActionResult> CloseWon(Guid id, [FromQuery] string? notes = null)
    {
        var opp = await _opportunityService.CloseWonAsync(id, notes);
        return Ok(opp);
    }

    [HttpPost("{id:guid}/close-lost")]
    public async Task<IActionResult> CloseLost(Guid id, [FromBody] CloseOpportunityDto dto)
    {
        var opp = await _opportunityService.CloseLostAsync(id, dto);
        return Ok(opp);
    }

    [HttpGet("pipeline")]
    public async Task<IActionResult> GetPipeline([FromQuery] Guid? assignedToId = null)
    {
        var pipeline = await _opportunityService.GetPipelineAsync(assignedToId);
        return Ok(pipeline);
    }

    [HttpGet("by-customer/{customerId:guid}")]
    public async Task<IActionResult> GetByCustomer(Guid customerId)
    {
        var opps = await _opportunityService.GetByCustomerAsync(customerId);
        return Ok(opps);
    }
}

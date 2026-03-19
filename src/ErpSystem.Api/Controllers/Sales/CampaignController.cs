using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize]
[ApiController]
[Route("api/sales/campaigns")]
public class CampaignController : ControllerBase
{
    private readonly ICampaignService _campaignService;

    public CampaignController(ICampaignService campaignService)
    {
        _campaignService = campaignService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? campaignType = null,
        [FromQuery] Guid? managerId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _campaignService.GetAllAsync(page, pageSize, search, status, campaignType, managerId, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var campaign = await _campaignService.GetByIdAsync(id);
        return campaign == null ? NotFound() : Ok(campaign);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCampaignDto dto)
    {
        var campaign = await _campaignService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = campaign.Id }, campaign);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCampaignDto dto)
    {
        var campaign = await _campaignService.UpdateAsync(id, dto);
        return Ok(campaign);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var campaign = await _campaignService.ActivateAsync(id);
        return Ok(campaign);
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id)
    {
        var campaign = await _campaignService.PauseAsync(id);
        return Ok(campaign);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var campaign = await _campaignService.CompleteAsync(id);
        return Ok(campaign);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] string? reason = null)
    {
        var campaign = await _campaignService.CancelAsync(id, reason);
        return Ok(campaign);
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var members = await _campaignService.GetMembersAsync(id);
        return Ok(members);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddCampaignMemberDto dto)
    {
        var member = await _campaignService.AddMemberAsync(id, dto);
        return Ok(member);
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId)
    {
        await _campaignService.RemoveMemberAsync(id, memberId);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;

namespace ErpSystem.Api.Controllers.Sales;

/// <summary>
/// REST API controller for Sales Agreement management.
/// </summary>
[ApiController]
[Route("api/sales/agreements")]
[Authorize]
public class SalesAgreementController : ControllerBase
{
    private readonly ISalesAgreementService _service;

    public SalesAgreementController(ISalesAgreementService service)
    {
        _service = service;
    }

    // ── CRUD ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? agreementType = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] DateTime? startDateFrom = null,
        [FromQuery] DateTime? startDateTo = null,
        [FromQuery] bool projectLinkedOnly = false,
        [FromQuery] bool releasedUnitsOnly = false)
    {
        var (items, totalCount) = await _service.GetAllAsync(page, pageSize, search, status, agreementType, customerId, startDateFrom, startDateTo, projectLinkedOnly, releasedUnitsOnly);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesAgreementDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSalesAgreementDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return Ok(result);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitForApproval(Guid id)
    {
        var result = await _service.SubmitForApprovalAsync(id);
        return Ok(result);
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ProcessApproval(Guid id, [FromBody] SalesAgreementApprovalDto dto)
    {
        var result = await _service.ProcessApprovalAsync(id, dto);
        return Ok(result);
    }

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var result = await _service.ActivateAsync(id);
        return Ok(result);
    }

    [HttpPost("{id}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, [FromQuery] string? reason = null)
    {
        var result = await _service.SuspendAsync(id, reason);
        return Ok(result);
    }

    [HttpPost("{id}/resume")]
    public async Task<IActionResult> Resume(Guid id)
    {
        var result = await _service.ResumeAsync(id);
        return Ok(result);
    }

    [HttpPost("{id}/terminate")]
    public async Task<IActionResult> Terminate(Guid id, [FromBody] TerminateAgreementDto dto)
    {
        var result = await _service.TerminateAsync(id, dto);
        return Ok(result);
    }

    [HttpPost("{id}/renew")]
    public async Task<IActionResult> Renew(Guid id, [FromBody] RenewAgreementDto dto)
    {
        var result = await _service.RenewAsync(id, dto);
        return Ok(result);
    }

    // ── Milestones ──────────────────────────────────────────────────────

    [HttpPatch("milestones/{milestoneId}")]
    public async Task<IActionResult> UpdateMilestone(Guid milestoneId, [FromBody] UpdateMilestoneStatusDto dto)
    {
        var result = await _service.UpdateMilestoneStatusAsync(milestoneId, dto);
        return Ok(result);
    }

    // ── Queries ─────────────────────────────────────────────────────────

    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiring([FromQuery] int daysAhead = 30)
    {
        var result = await _service.GetExpiringAgreementsAsync(daysAhead);
        return Ok(result);
    }

    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetByCustomer(Guid customerId)
    {
        var result = await _service.GetByCustomerAsync(customerId);
        return Ok(result);
    }
}

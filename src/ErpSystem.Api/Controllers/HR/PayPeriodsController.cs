using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/pay-periods")]
[Authorize(Policy = "InternalOnly")]
public class PayPeriodsController : AttendanceControllerBase
{
    private readonly IPayPeriodService _service;

    public PayPeriodsController(IPayPeriodService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<PayPeriodSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PayPeriodDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("current")]
    public async Task<ActionResult<PayPeriodDto?>> GetCurrentOpenPeriod(CancellationToken ct = default)
        => Ok(await _service.GetCurrentOpenPeriodAsync(ct));

    [HttpGet("covering/{date}")]
    public async Task<ActionResult<PayPeriodDto?>> GetPeriodCoveringDate(DateOnly date, CancellationToken ct = default)
        => Ok(await _service.GetPeriodCoveringDateAsync(date, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<PayPeriodSummaryDto>>> GetByStatus(
        PayPeriodStatus status, CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<PayPeriodSummaryDto>>> GetByType(
        PayPeriodType type, CancellationToken ct = default)
        => Ok(await _service.GetByTypeAsync(type, ct));

    [HttpGet("{id:guid}/summaries")]
    public async Task<ActionResult<PayPeriodDto>> GetWithSummaries(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetWithSummariesAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<PayPeriodDto>> Create(
        [FromBody] CreatePayPeriodDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PayPeriodDto>> Update(
        Guid id, [FromBody] UpdatePayPeriodDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<PayPeriodDto>> Close(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.CloseAsync(id, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

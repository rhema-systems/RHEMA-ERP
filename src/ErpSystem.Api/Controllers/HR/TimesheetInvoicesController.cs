using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/timesheet-invoices")]
[Authorize(Policy = "InternalOnly")]
public class TimesheetInvoicesController : AttendanceControllerBase
{
    private readonly ITimesheetInvoiceService _service;

    public TimesheetInvoicesController(ITimesheetInvoiceService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<TimesheetInvoiceSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TimesheetInvoiceDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("number/{invoiceNumber}")]
    public async Task<ActionResult<TimesheetInvoiceDto?>> GetByInvoiceNumber(
        string invoiceNumber, CancellationToken ct = default)
        => Ok(await _service.GetByInvoiceNumberAsync(invoiceNumber, ct));

    [HttpGet("engagement/{engagementId:guid}")]
    public async Task<ActionResult<IEnumerable<TimesheetInvoiceSummaryDto>>> GetByEngagementId(
        Guid engagementId, CancellationToken ct = default)
        => Ok(await _service.GetByEngagementIdAsync(engagementId, ct));

    [HttpGet("client/{clientId:guid}")]
    public async Task<ActionResult<IEnumerable<TimesheetInvoiceSummaryDto>>> GetByClientId(
        Guid clientId, CancellationToken ct = default)
        => Ok(await _service.GetByClientIdAsync(clientId, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<TimesheetInvoiceSummaryDto>>> GetByStatus(
        TimesheetInvoiceStatus status, CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("overdue")]
    public async Task<ActionResult<IEnumerable<TimesheetInvoiceSummaryDto>>> GetOverdueInvoices(
        CancellationToken ct = default)
        => Ok(await _service.GetOverdueInvoicesAsync(ct));

    [HttpGet("period")]
    public async Task<ActionResult<IEnumerable<TimesheetInvoiceSummaryDto>>> GetByPeriod(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
        => Ok(await _service.GetByPeriodAsync(from, to, ct));

    [HttpPost("generate")]
    public async Task<ActionResult<TimesheetInvoiceDto>> Generate(
        [FromBody] CreateTimesheetInvoiceDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.GenerateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TimesheetInvoiceDto>> Update(
        Guid id, [FromBody] UpdateTimesheetInvoiceDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/send")]
    public async Task<ActionResult<TimesheetInvoiceDto>> Send(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.SendAsync(id, employeeId, ct));
    }

    [HttpPost("{id:guid}/mark-paid")]
    public async Task<ActionResult<TimesheetInvoiceDto>> MarkPaid(
        Guid id, [FromBody] MarkInvoicePaidRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.MarkPaidAsync(id, request.PaidDate, request.PaidAmount, employeeId, ct));
    }

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<TimesheetInvoiceDto>> Void(
        Guid id, [FromBody] VoidInvoiceRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.VoidAsync(id, request.Reason, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    public sealed class MarkInvoicePaidRequest
    {
        public DateOnly PaidDate { get; set; }
        public decimal PaidAmount { get; set; }
    }

    public sealed class VoidInvoiceRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}

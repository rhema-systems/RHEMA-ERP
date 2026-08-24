using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/consultant-timesheets")]
[Authorize(Policy = "InternalOnly")]
public class ConsultantTimesheetsController : AttendanceControllerBase
{
    private readonly IConsultantTimesheetService _service;

    public ConsultantTimesheetsController(IConsultantTimesheetService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<ConsultantTimesheetSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConsultantTimesheetDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("number/{timesheetNumber}")]
    public async Task<ActionResult<ConsultantTimesheetDto?>> GetByTimesheetNumber(
        string timesheetNumber, CancellationToken ct = default)
        => Ok(await _service.GetByTimesheetNumberAsync(timesheetNumber, ct));

    [HttpGet("consultant/{consultantEmployeeId:guid}")]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetByConsultantId(
        Guid consultantEmployeeId, CancellationToken ct = default)
        => Ok(await _service.GetByConsultantIdAsync(consultantEmployeeId, ct));

    [HttpGet("engagement/{engagementId:guid}")]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetByEngagementId(
        Guid engagementId, CancellationToken ct = default)
        => Ok(await _service.GetByEngagementIdAsync(engagementId, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetByStatus(
        TimesheetStatus status, CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("period")]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetByPeriod(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
        => Ok(await _service.GetByPeriodAsync(from, to, ct));

    [HttpGet("pending-approval")]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetPendingApproval(
        CancellationToken ct = default)
        => Ok(await _service.GetPendingApprovalAsync(ct));

    [HttpGet("pending-client-confirmation")]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetPendingClientConfirmation(
        CancellationToken ct = default)
        => Ok(await _service.GetPendingClientConfirmationAsync(ct));

    [HttpGet("approved-for-invoicing")]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetApprovedForInvoicing(
        [FromQuery] Guid? clientId = null, CancellationToken ct = default)
        => Ok(await _service.GetApprovedForInvoicingAsync(clientId, ct));

    [HttpPost]
    public async Task<ActionResult<ConsultantTimesheetDto>> Create(
        [FromBody] CreateConsultantTimesheetDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ConsultantTimesheetDto>> Update(
        Guid id, [FromBody] UpdateConsultantTimesheetDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ConsultantTimesheetDto>> Submit(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.SubmitAsync(id, employeeId, ct));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ConsultantTimesheetDto>> Approve(
        Guid id, [FromBody] ApproveTimesheetRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.ApproveAsync(id, request.Comments, employeeId, ct));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ConsultantTimesheetDto>> Reject(
        Guid id, [FromBody] RejectTimesheetRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.RejectAsync(id, request.RejectionReason, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/entries")]
    public async Task<ActionResult<ConsultantTimesheetEntryDto>> AddEntry(
        Guid id, [FromBody] CreateConsultantTimesheetEntryDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        dto.TimesheetId = id;
        return Ok(await _service.AddEntryAsync(dto, tenantId, employeeId, ct));
    }

    [HttpGet("{id:guid}/entries")]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetEntryDto>>> GetEntries(
        Guid id, CancellationToken ct = default)
        => Ok(await _service.GetEntriesAsync(id, ct));

    [HttpPut("entries/{entryId:guid}")]
    public async Task<ActionResult<ConsultantTimesheetEntryDto>> UpdateEntry(
        Guid entryId, [FromBody] UpdateConsultantTimesheetEntryDto dto, CancellationToken ct = default)
    {
        if (entryId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateEntryAsync(dto, employeeId, ct));
    }

    [HttpDelete("entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid entryId, CancellationToken ct = default)
    {
        await _service.DeleteEntryAsync(entryId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/send-confirmation")]
    public async Task<ActionResult<SendTimesheetConfirmationResultDto>> SendConfirmation(
        Guid id,
        [FromBody] SendTimesheetConfirmationRequestDto request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        try
        {
            return Ok(await _service.SendConfirmationAsync(id, request, tenantId, employeeId, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/resend-confirmation")]
    public async Task<ActionResult<SendTimesheetConfirmationResultDto>> ResendConfirmation(
        Guid id,
        CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        try
        {
            return Ok(await _service.ResendConfirmationAsync(id, employeeId, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/confirmation")]
    public async Task<ActionResult<ClientTimesheetConfirmationDto?>> GetConfirmation(
        Guid id, CancellationToken ct = default)
        => Ok(await _service.GetConfirmationAsync(id, ct));

    public sealed class ApproveTimesheetRequest
    {
        public string? Comments { get; set; }
    }

    public sealed class RejectTimesheetRequest
    {
        public string RejectionReason { get; set; } = string.Empty;
    }
}

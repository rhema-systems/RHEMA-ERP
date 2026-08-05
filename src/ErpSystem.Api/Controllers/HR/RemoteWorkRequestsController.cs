using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/remote-work-requests")]
[Authorize]
public class RemoteWorkRequestsController : AttendanceControllerBase
{
    private readonly IRemoteWorkRequestService _service;

    public RemoteWorkRequestsController(IRemoteWorkRequestService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<RemoteWorkRequestSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RemoteWorkRequestDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("number/{requestNumber}")]
    public async Task<ActionResult<RemoteWorkRequestDto?>> GetByRequestNumber(
        string requestNumber, CancellationToken ct = default)
        => Ok(await _service.GetByRequestNumberAsync(requestNumber, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<RemoteWorkRequestSummaryDto>>> GetByEmployeeId(
        Guid employeeId, CancellationToken ct = default)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<RemoteWorkRequestSummaryDto>>> GetByStatus(
        RemoteWorkRequestStatus status, CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("pending-approval")]
    public async Task<ActionResult<IEnumerable<RemoteWorkRequestSummaryDto>>> GetPendingApproval(
        CancellationToken ct = default)
        => Ok(await _service.GetPendingApprovalAsync(ct));

    [HttpGet("range")]
    public async Task<ActionResult<IEnumerable<RemoteWorkRequestSummaryDto>>> GetByDateRange(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
        => Ok(await _service.GetByDateRangeAsync(from, to, ct));

    [HttpPost]
    public async Task<ActionResult<RemoteWorkRequestDto>> Create(
        [FromBody] CreateRemoteWorkRequestDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RemoteWorkRequestDto>> Update(
        Guid id, [FromBody] UpdateRemoteWorkRequestDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<RemoteWorkRequestDto>> Approve(
        Guid id, [FromBody] ApproveRemoteWorkRequestDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.ApproveAsync(id, dto.ApprovalComments, employeeId, ct));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<RemoteWorkRequestDto>> Reject(
        Guid id, [FromBody] RejectRemoteWorkRequestDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.RejectAsync(id, dto.RejectionReason, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

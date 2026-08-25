using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-bulk-attendance-imports")]
[Authorize(Policy = "InternalOnly")]
public class StaffBulkAttendanceImportsController : AttendanceControllerBase
{
    private readonly IStaffBulkAttendanceImportService _service;

    public StaffBulkAttendanceImportsController(
        IStaffBulkAttendanceImportService service,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<PagedResult<StaffBulkAttendanceImportSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<StaffBulkAttendanceImportDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("reference/{importReference}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<StaffBulkAttendanceImportDto?>> GetByImportReference(
        string importReference, CancellationToken ct = default)
        => Ok(await _service.GetByImportReferenceAsync(importReference, ct));

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffBulkAttendanceImportSummaryDto>>> GetByStatus(
        AttendanceImportStatus status, CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("{id:guid}/rows")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffBulkAttendanceImportRowDto>>> GetRows(
        Guid id, CancellationToken ct = default)
        => Ok(await _service.GetRowsAsync(id, ct));

    [HttpGet("{id:guid}/rows/failed")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffBulkAttendanceImportRowDto>>> GetFailedRows(
        Guid id, CancellationToken ct = default)
        => Ok(await _service.GetFailedRowsAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<StaffBulkAttendanceImportDto>> Initiate(
        [FromBody] CreateStaffBulkAttendanceImportDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.InitiateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/process")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<StaffBulkAttendanceImportDto>> Process(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.ProcessAsync(id, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

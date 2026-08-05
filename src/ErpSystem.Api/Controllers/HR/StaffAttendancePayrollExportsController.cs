using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-attendance-payroll-exports")]
[Authorize]
public class StaffAttendancePayrollExportsController : AttendanceControllerBase
{
    private readonly IStaffAttendancePayrollExportService _service;

    public StaffAttendancePayrollExportsController(
        IStaffAttendancePayrollExportService service,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<StaffAttendancePayrollExportSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffAttendancePayrollExportDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("reference/{exportReference}")]
    public async Task<ActionResult<StaffAttendancePayrollExportDto?>> GetByExportReference(
        string exportReference, CancellationToken ct = default)
        => Ok(await _service.GetByExportReferenceAsync(exportReference, ct));

    [HttpGet("pay-period/{payPeriodId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffAttendancePayrollExportSummaryDto>>> GetByPayPeriodId(
        Guid payPeriodId, CancellationToken ct = default)
        => Ok(await _service.GetByPayPeriodIdAsync(payPeriodId, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffAttendancePayrollExportSummaryDto>>> GetByStatus(
        PayrollExportStatus status, CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("pay-period/{payPeriodId:guid}/latest-successful")]
    public async Task<ActionResult<StaffAttendancePayrollExportDto?>> GetLatestSuccessfulExportForPeriod(
        Guid payPeriodId, CancellationToken ct = default)
        => Ok(await _service.GetLatestSuccessfulExportForPeriodAsync(payPeriodId, ct));

    [HttpPost("export")]
    public async Task<ActionResult<StaffAttendancePayrollExportDto>> Export(
        [FromBody] ExportPayrollRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        var result = await _service.ExportAsync(request.PayPeriodId, request.TargetSystem, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    public sealed class ExportPayrollRequest
    {
        public Guid PayPeriodId { get; set; }
        public string? TargetSystem { get; set; }
    }
}

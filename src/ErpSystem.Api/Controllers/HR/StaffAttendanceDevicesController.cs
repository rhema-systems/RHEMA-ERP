using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-attendance-devices")]
[Authorize]
public class StaffAttendanceDevicesController : AttendanceControllerBase
{
    private readonly IStaffAttendanceDeviceService _service;

    public StaffAttendanceDevicesController(IStaffAttendanceDeviceService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffAttendanceDeviceSummaryDto>>> GetAll(CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<StaffAttendanceDeviceSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffAttendanceDeviceDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("external/{externalDeviceId}")]
    public async Task<ActionResult<StaffAttendanceDeviceDto?>> GetByExternalDeviceId(
        string externalDeviceId, CancellationToken ct = default)
        => Ok(await _service.GetByExternalDeviceIdAsync(externalDeviceId, ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceDeviceSummaryDto>>> GetActiveDevices(
        CancellationToken ct = default)
        => Ok(await _service.GetActiveDevicesAsync(ct));

    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceDeviceSummaryDto>>> GetByLocationId(
        Guid locationId, CancellationToken ct = default)
        => Ok(await _service.GetByLocationIdAsync(locationId, ct));

    [HttpGet("overdue-sync")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceDeviceSummaryDto>>> GetDevicesOverdueForSync(
        [FromQuery] int hoursThreshold = 24, CancellationToken ct = default)
        => Ok(await _service.GetDevicesOverdueForSyncAsync(hoursThreshold, ct));

    [HttpPost]
    public async Task<ActionResult<StaffAttendanceDeviceDto>> Register(
        [FromBody] CreateStaffAttendanceDeviceDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.RegisterAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffAttendanceDeviceDto>> Update(
        Guid id, [FromBody] UpdateStaffAttendanceDeviceDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/sync")]
    public async Task<ActionResult<StaffAttendanceDeviceDto>> RecordSync(
        Guid id, [FromBody] RecordDeviceSyncRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.RecordSyncAsync(id, request.PendingSyncCount, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    public sealed class RecordDeviceSyncRequest
    {
        public int? PendingSyncCount { get; set; }
    }
}

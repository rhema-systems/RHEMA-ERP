using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/facilities/duty-roster")]
[Authorize]
public sealed class FacilitiesDutyRosterController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public FacilitiesDutyRosterController(
        ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoster(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var query = _db.EstateFacilityDutyRosters
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted);

        if (from is not null)
        {
            query = query.Where(item => item.EndDate == null || item.EndDate >= from.Value.Date);
        }

        if (to is not null)
        {
            query = query.Where(item => item.StartDate <= to.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => item.CompletionStatus == status || item.AttendanceStatus == status);
        }

        var items = await query
            .OrderBy(item => item.StartDate)
            .ThenBy(item => item.ShiftStart)
            .ThenBy(item => item.ServiceAreaName)
            .Select(item => ToDto(item))
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpPost]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager")]
    public async Task<IActionResult> CreateRosterItem(
        [FromBody] UpsertEstateFacilityDutyRosterDto request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return BadRequest(new { success = false, message = validation });
        }

        var now = DateTime.UtcNow;
        var item = new EstateFacilityDutyRoster
        {
            TenantId = GetTenantId(),
            RosterReference = await NextRosterReferenceAsync(cancellationToken),
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        ApplyRequest(item, request, now, isCreate: true);

        _db.EstateFacilityDutyRosters.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = ToDto(item),
            message = "Facilities duty roster item created."
        });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager")]
    public async Task<IActionResult> UpdateRosterItem(
        Guid id,
        [FromBody] UpsertEstateFacilityDutyRosterDto request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return BadRequest(new { success = false, message = validation });
        }

        var tenantId = GetTenantId();
        var item = await _db.EstateFacilityDutyRosters
            .FirstOrDefaultAsync(row => row.Id == id && row.TenantId == tenantId && !row.IsDeleted, cancellationToken);

        if (item is null)
        {
            return NotFound(new { success = false, message = "Facilities duty roster item was not found." });
        }

        ApplyRequest(item, request, DateTime.UtcNow, isCreate: false);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = ToDto(item),
            message = "Facilities duty roster item updated."
        });
    }

    [HttpPost("{id:guid}/attendance")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager")]
    public async Task<IActionResult> UpdateAttendance(
        Guid id,
        [FromBody] UpdateEstateFacilityDutyAttendanceDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var item = await _db.EstateFacilityDutyRosters
            .FirstOrDefaultAsync(row => row.Id == id && row.TenantId == tenantId && !row.IsDeleted, cancellationToken);

        if (item is null)
        {
            return NotFound(new { success = false, message = "Facilities duty roster item was not found." });
        }

        var now = DateTime.UtcNow;
        item.AttendanceStatus = TrimOrDefault(request.AttendanceStatus, "Present");
        item.CompletionStatus = TrimOrDefault(request.CompletionStatus, "Completed");
        item.QualityStatus = TrimOrDefault(request.QualityStatus, "Pending inspection");
        item.LinkedMaintenanceReference = TrimToNull(request.LinkedMaintenanceReference) ?? item.LinkedMaintenanceReference;
        item.LinkedComplaintReference = TrimToNull(request.LinkedComplaintReference) ?? item.LinkedComplaintReference;
        item.LastAttendanceAt = now;
        item.Notes = MergeNotes(item.Notes, request.Notes);
        item.UpdatedAt = now;
        item.UpdatedBy = _currentUserService.UserName ?? "System";
        item.LastModifiedById = GetUserId();

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = ToDto(item),
            message = "Facilities duty attendance updated."
        });
    }

    private Guid GetTenantId()
        => _currentUserService.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new UnauthorizedAccessException("Tenant context is required.");

    private Guid? GetUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private async Task<string> NextRosterReferenceAsync(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var prefix = $"FAC-DR-{DateTime.UtcNow:yyyyMMdd}";
        var count = await _db.EstateFacilityDutyRosters
            .AsNoTracking()
            .CountAsync(item => item.TenantId == tenantId && item.RosterReference.StartsWith(prefix), cancellationToken);

        return $"{prefix}-{count + 1:000}";
    }

    private void ApplyRequest(EstateFacilityDutyRoster item, UpsertEstateFacilityDutyRosterDto request, DateTime now, bool isCreate)
    {
        item.EmployeeProfileId = request.EmployeeProfileId;
        item.EmployeeNumber = TrimToNull(request.EmployeeNumber);
        item.StaffName = TrimOrDefault(request.StaffName, string.Empty);
        item.StaffType = TrimOrDefault(request.StaffType, "Cleaner");
        item.DutyType = TrimOrDefault(request.DutyType, "Cleaning");
        item.PropertyReference = TrimToNull(request.PropertyReference);
        item.PropertyUnit = TrimToNull(request.PropertyUnit);
        item.ServiceAreaType = TrimOrDefault(request.ServiceAreaType, "Common Area");
        item.ServiceAreaName = TrimOrDefault(request.ServiceAreaName, string.Empty);
        item.Frequency = TrimOrDefault(request.Frequency, "Daily");
        item.DayPattern = TrimToNull(request.DayPattern);
        item.StartDate = request.StartDate.Date == default ? now.Date : request.StartDate.Date;
        item.EndDate = request.EndDate?.Date;
        item.ShiftStart = TrimOrDefault(request.ShiftStart, "08:00");
        item.ShiftEnd = TrimOrDefault(request.ShiftEnd, "17:00");
        item.SupervisorName = TrimToNull(request.SupervisorName);
        item.ToolsIssued = TrimToNull(request.ToolsIssued);
        item.SuppliesIssued = TrimToNull(request.SuppliesIssued);
        item.Checklist = TrimToNull(request.Checklist);
        item.AttendanceStatus = TrimOrDefault(request.AttendanceStatus, "Pending");
        item.CompletionStatus = TrimOrDefault(request.CompletionStatus, "Scheduled");
        item.QualityStatus = TrimOrDefault(request.QualityStatus, "Not inspected");
        item.LinkedMaintenanceReference = TrimToNull(request.LinkedMaintenanceReference);
        item.LinkedComplaintReference = TrimToNull(request.LinkedComplaintReference);
        item.LinkedProcedureCaseReference = TrimToNull(request.LinkedProcedureCaseReference);
        item.Notes = TrimToNull(request.Notes);

        if (!isCreate)
        {
            item.UpdatedAt = now;
            item.UpdatedBy = _currentUserService.UserName ?? "System";
            item.LastModifiedById = GetUserId();
        }
    }

    private static string? ValidateRequest(UpsertEstateFacilityDutyRosterDto request)
    {
        if (string.IsNullOrWhiteSpace(request.StaffName))
        {
            return "Cleaner or staff name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ServiceAreaName))
        {
            return "Service area, apartment, unit, floor, or route is required.";
        }

        if (request.EndDate is not null && request.StartDate.Date > request.EndDate.Value.Date)
        {
            return "End date cannot be before start date.";
        }

        return null;
    }

    private static EstateFacilityDutyRosterDto ToDto(EstateFacilityDutyRoster item) =>
        new(
            item.Id,
            item.RosterReference,
            item.EmployeeProfileId,
            item.EmployeeNumber,
            item.StaffName,
            item.StaffType,
            item.DutyType,
            item.PropertyReference,
            item.PropertyUnit,
            item.ServiceAreaType,
            item.ServiceAreaName,
            item.Frequency,
            item.DayPattern,
            item.StartDate,
            item.EndDate,
            item.ShiftStart,
            item.ShiftEnd,
            item.SupervisorName,
            item.ToolsIssued,
            item.SuppliesIssued,
            item.Checklist,
            item.AttendanceStatus,
            item.CompletionStatus,
            item.QualityStatus,
            item.LinkedMaintenanceReference,
            item.LinkedComplaintReference,
            item.LinkedProcedureCaseReference,
            item.LastAttendanceAt,
            item.Notes,
            item.CreatedAt,
            item.UpdatedAt);

    private static string TrimOrDefault(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? MergeNotes(string? existingNotes, string? newNotes)
    {
        if (string.IsNullOrWhiteSpace(newNotes))
        {
            return existingNotes;
        }

        if (string.IsNullOrWhiteSpace(existingNotes))
        {
            return newNotes.Trim();
        }

        return $"{existingNotes.Trim()}\n{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC: {newNotes.Trim()}";
    }
}

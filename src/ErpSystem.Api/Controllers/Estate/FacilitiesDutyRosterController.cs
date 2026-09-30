using ErpSystem.Core.DTOs.Estate;
using System.Globalization;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
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

    [HttpGet("staff")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager")]
    public async Task<IActionResult> SearchStaff([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Ok(new { success = true, data = Array.Empty<object>() });

        var tenantId = GetTenantId();
        var employees = await _db.Employees.AsNoTracking()
            .Where(employee => employee.TenantId == tenantId && !employee.IsDeleted && employee.IsActive
                && (employee.EmployeeNumber.Contains(term) || employee.FirstName.Contains(term)
                    || employee.LastName.Contains(term)
                    || (employee.FirstName + " " + employee.LastName).Contains(term)
                    || (employee.FirstName + " " + employee.MiddleName + " " + employee.LastName).Contains(term)))
            .OrderByDescending(employee => employee.EmployeeNumber == term)
            .ThenBy(employee => employee.LastName).ThenBy(employee => employee.FirstName)
            .Take(20)
            .Select(employee => new
            {
                employee.Id,
                employee.EmployeeNumber,
                StaffName = employee.FirstName + " " + (employee.MiddleName == null ? "" : employee.MiddleName + " ") + employee.LastName,
                Department = _db.Departments
                    .Where(department => department.TenantId == tenantId && department.Id == employee.DepartmentId)
                    .Select(department => department.Name).FirstOrDefault(),
                Position = _db.EmployeePositions
                    .Where(position => position.TenantId == tenantId && position.Id == employee.PositionId)
                    .Select(position => position.Title).FirstOrDefault(),
                EmployeeProfileId = _db.PayrollEmployeeProfiles
                    .Where(profile => profile.TenantId == tenantId && !profile.IsDeleted && profile.EmployeeId == employee.Id)
                    .Select(profile => (Guid?)profile.Id).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = employees });
    }

    [HttpGet("properties")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager")]
    public async Task<IActionResult> SearchProperties([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Ok(new { success = true, data = Array.Empty<object>() });

        var tenantId = GetTenantId();
        var assets = await _db.EstateManagedAssets.AsNoTracking()
            .Where(asset => asset.TenantId == tenantId && !asset.IsDeleted
                && (asset.Status == EstateManagedAssetStatus.Available
                    || asset.Status == EstateManagedAssetStatus.Leased
                    || asset.Status == EstateManagedAssetStatus.Occupied)
                && (asset.AssetCode.Contains(term) || asset.Name.Contains(term)
                    || (asset.ProjectCode != null && asset.ProjectCode.Contains(term))
                    || (asset.ProjectTitle != null && asset.ProjectTitle.Contains(term))
                    || (asset.Location != null && asset.Location.Contains(term))))
            .OrderByDescending(asset => asset.AssetCode == term)
            .ThenBy(asset => asset.Name).ThenBy(asset => asset.AssetCode)
            .Take(20)
            .Select(asset => new
            {
                asset.Id, asset.AssetCode, asset.Name, asset.Location,
                asset.ProjectCode, asset.ProjectTitle, asset.BlockName, asset.FloorLabel,
                asset.UnitType, asset.AssetType
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = assets });
    }

    [HttpGet("issue-vouchers")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager")]
    public async Task<IActionResult> SearchIssueVouchers([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Ok(new { success = true, data = Array.Empty<object>() });

        var vouchers = await _db.InventoryIssueVouchers.AsNoTracking()
            .Include(item => item.Lines).ThenInclude(line => line.InventoryItem)
            .Where(item => item.TenantId == GetTenantId() && !item.IsDeleted
                && item.VoucherNumber.Contains(term))
            .OrderByDescending(item => item.IssuedAtUtc).Take(20)
            .ToListAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            data = vouchers.Select(item => new
            {
                item.Id, item.VoucherNumber, Status = item.Status.ToString(), item.IssuedAtUtc,
                Supplies = FormatIssuedSupplies(item)
            })
        });
    }

    [HttpGet("properties/{propertyId:guid}/units")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager")]
    public async Task<IActionResult> SearchUnits(Guid propertyId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var property = await _db.EstateManagedAssets.AsNoTracking()
            .Where(asset => asset.TenantId == tenantId && !asset.IsDeleted && asset.Id == propertyId)
            .Select(asset => new { asset.Id, asset.ProjectCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (property is null) return NotFound(new { success = false, message = "Property was not found." });

        var term = search?.Trim();
        var query = _db.EstateManagedAssets.AsNoTracking()
            .Where(asset => asset.TenantId == tenantId && !asset.IsDeleted
                && (asset.Status == EstateManagedAssetStatus.Available
                    || asset.Status == EstateManagedAssetStatus.Leased
                    || asset.Status == EstateManagedAssetStatus.Occupied));
        query = string.IsNullOrWhiteSpace(property.ProjectCode)
            ? query.Where(asset => asset.Id == property.Id)
            : query.Where(asset => asset.ProjectCode == property.ProjectCode);
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(asset => asset.AssetCode.Contains(term) || asset.Name.Contains(term)
                || (asset.ProjectUnitCode != null && asset.ProjectUnitCode.Contains(term))
                || (asset.BlockName != null && asset.BlockName.Contains(term))
                || (asset.FloorLabel != null && asset.FloorLabel.Contains(term)));

        var units = await query.OrderBy(asset => asset.BlockName).ThenBy(asset => asset.FloorLabel)
            .ThenBy(asset => asset.Name).Take(20)
            .Select(asset => new
            {
                asset.Id, asset.AssetCode, asset.Name, asset.ProjectUnitCode,
                asset.BlockName, asset.FloorLabel, asset.UnitType, asset.AssetType
            })
            .ToListAsync(cancellationToken);
        return Ok(new { success = true, data = units });
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

        var items = await query
            .OrderBy(item => item.StartDate)
            .ThenBy(item => item.ShiftStart)
            .ThenBy(item => item.ServiceAreaName)
            .ToListAsync(cancellationToken);

        var date = from?.Date == to?.Date && from.HasValue ? from.Value.Date : (DateTime?)null;
        var attendanceByRosterId = new Dictionary<Guid, EstateFacilityDutyAttendance>();
        if (date.HasValue && items.Count > 0)
        {
            var rosterIds = items.Select(item => item.Id).ToList();
            attendanceByRosterId = await _db.EstateFacilityDutyAttendances.AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted
                    && item.DutyDate == date.Value && rosterIds.Contains(item.DutyRosterId))
                .ToDictionaryAsync(item => item.DutyRosterId, cancellationToken);
        }
        var roster = items
            .Where(item => !date.HasValue || IsScheduledOn(item, date.Value))
            .Select(item => ToDto(item, date,
                attendanceByRosterId.GetValueOrDefault(item.Id)))
            .Where(item => string.IsNullOrWhiteSpace(status) || string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.CompletionStatus, status, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.AttendanceStatus, status, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Ok(new { success = true, data = roster });
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMyRoster(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var employeeId = _currentUserService.EmployeeId;
        if (employeeId is null || employeeId == Guid.Empty)
        {
            return Unauthorized(new { success = false, message = "Your account is not linked to an employee record." });
        }

        var employee = await _db.Employees.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.Id == employeeId.Value)
            .Select(item => new { item.Id, item.EmployeeNumber })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return NotFound(new { success = false, message = "Your employee record was not found." });
        }

        var profileIds = await _db.PayrollEmployeeProfiles.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.EmployeeId == employee.Id)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var fromDate = (from ?? DateTime.UtcNow.Date.AddDays(-7)).Date;
        var toDate = (to ?? DateTime.UtcNow.Date.AddDays(30)).Date;
        if (toDate < fromDate)
        {
            return BadRequest(new { success = false, message = "End date cannot be before start date." });
        }

        var query = _db.EstateFacilityDutyRosters
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && (item.EmployeeNumber == employee.EmployeeNumber
                    || (item.EmployeeProfileId.HasValue && profileIds.Contains(item.EmployeeProfileId.Value)))
                && (item.EndDate == null || item.EndDate >= fromDate)
                && item.StartDate <= toDate);

        var items = await query
            .OrderBy(item => item.StartDate)
            .ThenBy(item => item.ShiftStart)
            .ThenBy(item => item.ServiceAreaName)
            .ToListAsync(cancellationToken);

        var rosterIds = items.Select(item => item.Id).ToList();
        var attendances = new List<EstateFacilityDutyAttendance>();
        if (rosterIds.Count > 0)
        {
            attendances = await _db.EstateFacilityDutyAttendances.AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted
                    && rosterIds.Contains(item.DutyRosterId)
                    && item.DutyDate >= fromDate
                    && item.DutyDate <= toDate)
                .ToListAsync(cancellationToken);
        }
        var attendanceByKey = attendances.ToDictionary(
            item => (item.DutyRosterId, item.DutyDate.Date),
            item => item);

        var roster = new List<EstateFacilityDutyRosterDto>();
        foreach (var item in items)
        {
            for (var date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                if (!IsScheduledOn(item, date))
                {
                    continue;
                }

                var dto = ToDto(item, date, attendanceByKey.GetValueOrDefault((item.Id, date)));
                if (!string.IsNullOrWhiteSpace(status)
                    && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(dto.CompletionStatus, status, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(dto.AttendanceStatus, status, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                roster.Add(dto);
            }
        }

        return Ok(new { success = true, data = roster });
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

        var sourceError = await ResolveDutySourcesAsync(request, cancellationToken);
        if (sourceError is not null)
            return BadRequest(new { success = false, message = sourceError });

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

        var sourceError = await ResolveDutySourcesAsync(request, cancellationToken);
        if (sourceError is not null)
            return BadRequest(new { success = false, message = sourceError });

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
        if (!IsScheduledOn(item, now.Date))
            return BadRequest(new { success = false, message = "This staff duty is not scheduled for today." });
        var attendanceStatus = TrimOrDefault(request.AttendanceStatus, "Present");
        var completionStatus = TrimOrDefault(request.CompletionStatus, "Completed");
        var qualityStatus = TrimOrDefault(request.QualityStatus, "Pending inspection");
        if (IsInspectionDecision(qualityStatus))
        {
            if (item.SupervisorEmployeeId is null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Assign a supervisor from HR before inspection can be marked."
                });
            }

            if (_currentUserService.EmployeeId is not { } employeeId
                || employeeId == Guid.Empty
                || employeeId != item.SupervisorEmployeeId.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "Only the assigned supervisor can mark this inspection."
                });
            }

            var wasCompleted = string.Equals(item.AttendanceStatus, "Present", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.CompletionStatus, "Completed", StringComparison.OrdinalIgnoreCase);
            var remainsCompleted = string.Equals(attendanceStatus, "Present", StringComparison.OrdinalIgnoreCase)
                && string.Equals(completionStatus, "Completed", StringComparison.OrdinalIgnoreCase);
            if (!wasCompleted && !remainsCompleted)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Inspection can only be marked after the duty is present and completed."
                });
            }
        }

        item.AttendanceStatus = attendanceStatus;
        item.CompletionStatus = completionStatus;
        item.QualityStatus = qualityStatus;
        item.LinkedMaintenanceReference = TrimToNull(request.LinkedMaintenanceReference) ?? item.LinkedMaintenanceReference;
        item.LinkedComplaintReference = TrimToNull(request.LinkedComplaintReference) ?? item.LinkedComplaintReference;
        item.LastAttendanceAt = now;
        item.Notes = MergeNotes(item.Notes, request.Notes);
        item.UpdatedAt = now;
        item.UpdatedBy = _currentUserService.UserName ?? "System";
        item.LastModifiedById = GetUserId();

        var attendance = await _db.EstateFacilityDutyAttendances.FirstOrDefaultAsync(row =>
            row.TenantId == tenantId && row.DutyRosterId == id && row.DutyDate == now.Date && !row.IsDeleted,
            cancellationToken);
        if (attendance is null)
        {
            attendance = new EstateFacilityDutyAttendance
            {
                TenantId = tenantId,
                DutyRosterId = id,
                DutyDate = now.Date,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = GetUserId()
            };
            _db.EstateFacilityDutyAttendances.Add(attendance);
        }
        attendance.AttendanceStatus = item.AttendanceStatus;
        attendance.CompletionStatus = item.CompletionStatus;
        attendance.QualityStatus = item.QualityStatus;
        attendance.LinkedMaintenanceReference = item.LinkedMaintenanceReference;
        attendance.LinkedComplaintReference = item.LinkedComplaintReference;
        attendance.Notes = MergeNotes(attendance.Notes, request.Notes);
        attendance.RecordedAt = now;
        attendance.UpdatedAt = now;
        attendance.UpdatedBy = _currentUserService.UserName ?? "System";
        attendance.LastModifiedById = GetUserId();

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

    private async Task<string?> ResolveDutySourcesAsync(
        UpsertEstateFacilityDutyRosterDto request, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        if (!string.IsNullOrWhiteSpace(request.EmployeeNumber))
        {
            var number = request.EmployeeNumber.Trim();
            var employee = await _db.Employees.AsNoTracking()
                .Where(row => row.TenantId == tenantId && !row.IsDeleted && row.IsActive
                    && row.EmployeeNumber == number)
                .Select(row => new { row.Id, row.FirstName, row.MiddleName, row.LastName, row.EmployeeNumber })
                .FirstOrDefaultAsync(cancellationToken);
            if (employee is null) return "Select an active employee from HR.";

            request.EmployeeNumber = employee.EmployeeNumber;
            request.StaffName = string.Join(" ", new[] { employee.FirstName, employee.MiddleName, employee.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            request.EmployeeProfileId = await _db.PayrollEmployeeProfiles.AsNoTracking()
                .Where(profile => profile.TenantId == tenantId && !profile.IsDeleted && profile.EmployeeId == employee.Id)
                .Select(profile => (Guid?)profile.Id).FirstOrDefaultAsync(cancellationToken);
        }

        if (request.SupervisorEmployeeId is { } supervisorEmployeeId)
        {
            var supervisor = await _db.Employees.AsNoTracking()
                .Where(row => row.TenantId == tenantId && !row.IsDeleted && row.IsActive
                    && row.Id == supervisorEmployeeId)
                .Select(row => new { row.Id, row.FirstName, row.MiddleName, row.LastName })
                .FirstOrDefaultAsync(cancellationToken);
            if (supervisor is null) return "Select an active supervisor from HR.";

            request.SupervisorName = string.Join(" ", new[] { supervisor.FirstName, supervisor.MiddleName, supervisor.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        if (!string.IsNullOrWhiteSpace(request.PropertyReference))
        {
            var reference = request.PropertyReference.Trim();
            var property = await _db.EstateManagedAssets.AsNoTracking()
                .Where(asset => asset.TenantId == tenantId && !asset.IsDeleted && asset.AssetCode == reference)
                .Select(asset => new { asset.Id, asset.AssetCode, asset.ProjectCode, asset.Status })
                .FirstOrDefaultAsync(cancellationToken);
            if (property is null) return "Select a property or site from Estate.";
            if (property.Status is not (EstateManagedAssetStatus.Available
                or EstateManagedAssetStatus.Leased or EstateManagedAssetStatus.Occupied))
                return "Staff duties cannot be assigned to a reserved, blocked, retired, or otherwise unavailable property.";
            request.PropertyReference = property.AssetCode;

            if (!string.IsNullOrWhiteSpace(request.PropertyUnit))
            {
                var unitReference = request.PropertyUnit.Trim();
                var unit = await _db.EstateManagedAssets.AsNoTracking()
                    .Where(asset => asset.TenantId == tenantId && !asset.IsDeleted
                        && (asset.AssetCode == unitReference || asset.ProjectUnitCode == unitReference)
                        && (asset.Id == property.Id
                            || (property.ProjectCode != null && asset.ProjectCode == property.ProjectCode)))
                    .Select(asset => new { asset.AssetCode, asset.ProjectUnitCode, asset.Status })
                    .FirstOrDefaultAsync(cancellationToken);
                if (unit is null) return "Select a unit or parcel belonging to the selected property.";
                if (unit.Status is not (EstateManagedAssetStatus.Available
                    or EstateManagedAssetStatus.Leased or EstateManagedAssetStatus.Occupied))
                    return "Staff duties cannot be assigned to a reserved, blocked, retired, or otherwise unavailable unit.";
                request.PropertyUnit = unit.ProjectUnitCode ?? unit.AssetCode;
            }
        }

        if (request.InventoryIssueVoucherId is { } voucherId)
        {
            var voucher = await _db.InventoryIssueVouchers.AsNoTracking()
                .Include(item => item.Lines).ThenInclude(line => line.InventoryItem)
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == voucherId
                    && !item.IsDeleted && (item.Status == InventoryIssueVoucherStatus.Issued
                        || item.Status == InventoryIssueVoucherStatus.Acknowledged), cancellationToken);
            if (voucher is null) return "Select an issued Inventory voucher belonging to this tenant.";
            var supplies = FormatIssuedSupplies(voucher);
            if (supplies.Length > 500) return "The Inventory voucher contains too many items for a duty summary.";
            request.InventoryIssueVoucherNumber = voucher.VoucherNumber;
            request.SuppliesIssued = supplies;
        }

        return null;
    }

    private static string FormatIssuedSupplies(InventoryIssueVoucher voucher)
        => string.Join(", ", voucher.Lines.Where(line => !line.IsDeleted)
            .Select(line => $"{line.InventoryItem?.Name ?? line.InventoryItemId.ToString()} x{line.Quantity:0.####} {line.UnitOfMeasure}"));

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
        item.SupervisorEmployeeId = request.SupervisorEmployeeId;
        item.ToolsIssued = TrimToNull(request.ToolsIssued);
        item.SuppliesIssued = TrimToNull(request.SuppliesIssued);
        item.InventoryIssueVoucherId = request.InventoryIssueVoucherId;
        item.InventoryIssueVoucherNumber = request.InventoryIssueVoucherId.HasValue
            ? TrimToNull(request.InventoryIssueVoucherNumber) : null;
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

    internal static string? ValidateRequest(UpsertEstateFacilityDutyRosterDto request)
    {
        if (string.IsNullOrWhiteSpace(request.StaffType)
            || string.Equals(request.StaffType, "Cleaner", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.EmployeeNumber))
                return "Select a cleaner from HR.";
            if (string.IsNullOrWhiteSpace(request.PropertyReference))
                return "Select a property or site from Estate.";
        }

        if (string.IsNullOrWhiteSpace(request.StaffName))
        {
            return "Cleaner or staff name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ServiceAreaName))
        {
            return "Service area, apartment, unit, floor, or route is required.";
        }

        if (request.SupervisorEmployeeId is null)
        {
            return "Select the supervisor from HR employees.";
        }

        if (request.EndDate is not null && request.StartDate.Date > request.EndDate.Value.Date)
        {
            return "End date cannot be before start date.";
        }

        if (!new[] { "Daily", "Weekly", "One-off" }.Contains(request.Frequency, StringComparer.OrdinalIgnoreCase))
            return "Frequency must be Daily, Weekly, or One-off.";
        if (!TimeOnly.TryParseExact(request.ShiftStart, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !TimeOnly.TryParseExact(request.ShiftEnd, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
            || end <= start)
            return "Enter a valid shift with an end time after the start time.";
        if (request.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.DayPattern))
            return "Select the scheduled day or days for a weekly duty.";
        if (!string.IsNullOrWhiteSpace(request.DayPattern) && !TryParseDays(request.DayPattern, out _))
            return "Use day names such as Mon-Fri or Mon, Wed, Fri.";

        return null;
    }

    internal static bool IsScheduledOn(EstateFacilityDutyRoster item, DateTime date)
    {
        if (date.Date < item.StartDate.Date || item.EndDate is { } end && date.Date > end.Date)
            return false;
        if (item.Frequency.Equals("One-off", StringComparison.OrdinalIgnoreCase))
            return date.Date == item.StartDate.Date;
        if (string.IsNullOrWhiteSpace(item.DayPattern))
            return !item.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase)
                || date.DayOfWeek == item.StartDate.DayOfWeek;
        return TryParseDays(item.DayPattern, out var days) && days.Contains(date.DayOfWeek);
    }

    private static bool TryParseDays(string pattern, out HashSet<DayOfWeek> days)
    {
        days = [];
        var labels = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        foreach (var part in pattern.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var range = part.Split('-', StringSplitOptions.TrimEntries);
            if (range.Length is < 1 or > 2) return false;
            var first = Array.FindIndex(labels, label => label.Equals(range[0], StringComparison.OrdinalIgnoreCase));
            var last = range.Length == 2
                ? Array.FindIndex(labels, label => label.Equals(range[1], StringComparison.OrdinalIgnoreCase))
                : first;
            if (first < 0 || last < first) return false;
            for (var day = first; day <= last; day++) days.Add((DayOfWeek)day);
        }
        return days.Count > 0;
    }

    private static EstateFacilityDutyRosterDto ToDto(
        EstateFacilityDutyRoster item,
        DateTime? date = null,
        EstateFacilityDutyAttendance? attendance = null)
    {
        var recordedForDate = item.LastAttendanceAt?.Date == (date ?? DateTime.UtcNow.Date);
        return new(
            item.Id,
            item.RosterReference,
            date,
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
            item.SupervisorEmployeeId,
            item.ToolsIssued,
            item.SuppliesIssued,
            item.InventoryIssueVoucherId,
            item.InventoryIssueVoucherNumber,
            item.Checklist,
            attendance?.AttendanceStatus ?? (recordedForDate ? item.AttendanceStatus : "Pending"),
            attendance?.CompletionStatus ?? (recordedForDate ? item.CompletionStatus : "Scheduled"),
            attendance?.QualityStatus ?? (recordedForDate ? item.QualityStatus : "Not inspected"),
            attendance is not null ? attendance.LinkedMaintenanceReference
                : recordedForDate ? item.LinkedMaintenanceReference : null,
            attendance is not null ? attendance.LinkedComplaintReference
                : recordedForDate ? item.LinkedComplaintReference : null,
            item.LinkedProcedureCaseReference,
            attendance?.RecordedAt ?? (recordedForDate ? item.LastAttendanceAt : null),
            attendance is not null ? attendance.Notes : recordedForDate ? item.Notes : null,
            item.CreatedAt,
            item.UpdatedAt);
    }

    private static string TrimOrDefault(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsInspectionDecision(string qualityStatus)
        => string.Equals(qualityStatus, "Passed inspection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(qualityStatus, "Failed inspection", StringComparison.OrdinalIgnoreCase);

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

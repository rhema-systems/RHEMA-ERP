using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Models.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF ATTENDANCE RECORD SERVICE
// ============================================================================

#region Staff Attendance Record Service

public class StaffAttendanceRecordService : IStaffAttendanceRecordService
{
    private readonly IStaffAttendanceRecordRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendanceRecordService> _logger;

    public StaffAttendanceRecordService(
        IStaffAttendanceRecordRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendanceRecordService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // An attendance record owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffAttendanceRecord> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Attendance record '{id}' not found.");
        return entity;
    }

    public async Task<StaffAttendanceRecordDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceRecordDto?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetByEmployeeAndDateAsync(employeeId, date);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeIdAsync(employeeId, from, to))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByDateAsync(date))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByDateRangeAsync(from, to))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByStatusAsync(StaffAttendanceStatus status, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByStatusAsync(status, from, to))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffAttendanceRecordSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // EmployeeName is on the summary DTO, so Employee must be loaded.
        var query = _repository.GetQueryable().Include(r => r.Employee).Where(r => r.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.Date)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffAttendanceRecordSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<StaffAttendanceRecordDto> CreateAsync(CreateStaffAttendanceRecordDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var existing = await _repository.GetByEmployeeAndDateAsync(dto.EmployeeId, dto.Date);
        if (existing != null && existing.TenantId == current)
            throw new InvalidOperationException($"An attendance record already exists for this employee on {dto.Date}.");

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Attendance record created for employee {EmployeeId} on {Date}", dto.EmployeeId, dto.Date);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceRecordDto> UpdateAsync(UpdateStaffAttendanceRecordDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// STAFF DAILY ATTENDANCE SERVICE
// ============================================================================

#region Staff Daily Attendance Service

public class StaffDailyAttendanceService : IStaffDailyAttendanceService
{
    private readonly IStaffDailyAttendanceRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDailyAttendanceService> _logger;

    public StaffDailyAttendanceService(
        IStaffDailyAttendanceRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDailyAttendanceService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A daily attendance record owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffDailyAttendance> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Daily attendance '{id}' not found.");
        return entity;
    }

    private async Task<StaffDailyAttendance> GetOwnedWithDetailsAsync(Guid id)
    {
        var entity = await _repository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Daily attendance record '{id}' not found.");
        return entity;
    }

    public async Task<StaffDailyAttendanceDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedWithDetailsAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffDailyAttendanceDto?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetByEmployeeAndDateAsync(employeeId, date);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeIdAsync(employeeId, from, to))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByDateAsync(date))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByStatusAsync(StaffAttendanceStatus status, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByStatusAsync(status, from, to))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetPendingVerificationAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetPendingVerificationAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetWithOpenExceptionsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetWithOpenExceptionsAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetWithOvertimeAsync(DateOnly from, DateOnly to, Guid? employeeId = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetWithOvertimeAsync(from, to, employeeId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetLateAttendancesAsync(DateOnly from, DateOnly to, Guid? employeeId = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetLateAttendancesAsync(from, to, employeeId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetRemoteWorkDaysAsync(DateOnly from, DateOnly to, Guid? employeeId = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetRemoteWorkDaysAsync(from, to, employeeId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByPayPeriodIdAsync(Guid payPeriodId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByPayPeriodIdAsync(payPeriodId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffDailyAttendanceSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // The summary DTO carries EmployeeName/EmployeeNumber, so Employee has to be loaded
        // or every row comes back with a blank name.
        var query = _repository.GetQueryable()
            .Include(r => r.Employee)
            .Where(r => r.TenantId == tenantId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.AttendanceDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffDailyAttendanceSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Tenant-wide filtered search. Filters compose with AND; a null or empty value leaves
    /// that dimension unfiltered, so an empty <paramref name="filter"/> behaves like
    /// <see cref="GetPagedAsync"/>.
    /// </summary>
    public async Task<PagedResult<StaffDailyAttendanceSummaryDto>> SearchAsync(
        StaffDailyAttendanceSearchDto filter, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var query = _repository.GetQueryable()
            .Include(r => r.Employee)
            .Where(r => r.TenantId == tenantId);

        if (filter.EmployeeId.HasValue)
            query = query.Where(r => r.EmployeeId == filter.EmployeeId.Value);

        // The attendance row has no org unit of its own; it is a property of the employee.
        if (filter.OrganizationUnitId.HasValue)
            query = query.Where(r => r.Employee.OrganizationUnitId == filter.OrganizationUnitId.Value);

        if (filter.LocationId.HasValue)
            query = query.Where(r => r.LocationId == filter.LocationId.Value);

        if (filter.WorkScheduleId.HasValue)
            query = query.Where(r => r.WorkScheduleId == filter.WorkScheduleId.Value);

        if (filter.PayPeriodId.HasValue)
            query = query.Where(r => r.PayPeriodId == filter.PayPeriodId.Value);

        if (filter.From.HasValue)
            query = query.Where(r => r.AttendanceDate >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(r => r.AttendanceDate <= filter.To.Value);

        if (filter.Statuses is { Count: > 0 })
            query = query.Where(r => filter.Statuses.Contains(r.Status));

        if (filter.IsLate.HasValue)
            query = query.Where(r => r.IsLate == filter.IsLate.Value);

        if (filter.IsEarlyDeparture.HasValue)
            query = query.Where(r => r.IsEarlyDeparture == filter.IsEarlyDeparture.Value);

        if (filter.IsOvertime.HasValue)
            query = query.Where(r => r.IsOvertime == filter.IsOvertime.Value);

        if (filter.IsRemoteWork.HasValue)
            query = query.Where(r => r.IsRemoteWork == filter.IsRemoteWork.Value);

        if (filter.HasException.HasValue)
            query = query.Where(r => r.HasException == filter.HasException.Value);

        if (filter.IsVerified.HasValue)
            query = query.Where(r => r.IsVerified == filter.IsVerified.Value);

        if (filter.RequiresVerification.HasValue)
            query = query.Where(r => r.RequiresVerification == filter.RequiresVerification.Value);

        if (filter.MinLateMinutes.HasValue)
            query = query.Where(r => r.LateMinutes >= filter.MinLateMinutes.Value);

        if (filter.MinOvertimeHours.HasValue)
            query = query.Where(r => r.OvertimeHours >= filter.MinOvertimeHours.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(r =>
                EF.Functions.Like(r.Employee.FirstName, $"%{term}%") ||
                EF.Functions.Like(r.Employee.LastName, $"%{term}%") ||
                EF.Functions.Like(r.Employee.EmployeeNumber, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(ct);

        // A secondary sort on employee keeps paging stable when the primary key ties, which
        // it always does for a date-sorted day of attendance.
        query = (filter.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "employee" => filter.SortDescending
                ? query.OrderByDescending(r => r.Employee.LastName).ThenByDescending(r => r.Employee.FirstName)
                : query.OrderBy(r => r.Employee.LastName).ThenBy(r => r.Employee.FirstName),
            "status" => filter.SortDescending
                ? query.OrderByDescending(r => r.Status).ThenByDescending(r => r.AttendanceDate)
                : query.OrderBy(r => r.Status).ThenBy(r => r.AttendanceDate),
            "workhours" => filter.SortDescending
                ? query.OrderByDescending(r => r.ActualWorkHours).ThenByDescending(r => r.AttendanceDate)
                : query.OrderBy(r => r.ActualWorkHours).ThenBy(r => r.AttendanceDate),
            "overtime" => filter.SortDescending
                ? query.OrderByDescending(r => r.OvertimeHours).ThenByDescending(r => r.AttendanceDate)
                : query.OrderBy(r => r.OvertimeHours).ThenBy(r => r.AttendanceDate),
            "lateminutes" => filter.SortDescending
                ? query.OrderByDescending(r => r.LateMinutes).ThenByDescending(r => r.AttendanceDate)
                : query.OrderBy(r => r.LateMinutes).ThenBy(r => r.AttendanceDate),
            _ => filter.SortDescending
                ? query.OrderByDescending(r => r.AttendanceDate).ThenBy(r => r.Employee.LastName)
                : query.OrderBy(r => r.AttendanceDate).ThenBy(r => r.Employee.LastName),
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffDailyAttendanceSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<StaffDailyAttendanceDto> CreateAsync(CreateStaffDailyAttendanceDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var existing = await _repository.GetByEmployeeAndDateAsync(dto.EmployeeId, dto.AttendanceDate);
        if (existing != null && existing.TenantId == current)
            throw new InvalidOperationException($"A daily attendance record already exists for employee {dto.EmployeeId} on {dto.AttendanceDate}.");

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Daily attendance created for employee {EmployeeId} on {Date}", dto.EmployeeId, dto.AttendanceDate);
        return entity.ToDto();
    }

    public async Task<StaffDailyAttendanceDto> UpdateAsync(UpdateStaffDailyAttendanceDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }

    public async Task<StaffDailyAttendanceDto> VerifyAsync(VerifyAttendanceDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.AttendanceId);

        entity.IsVerified = true;
        entity.VerifiedById = userId;
        entity.VerifiedDate = DateTime.UtcNow;
        entity.VerificationNotes = dto.VerificationNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Daily attendance {Id} verified by {UserId}", dto.AttendanceId, userId);
        return entity.ToDto();
    }

    public async Task<StaffDailyAttendanceDto> ApproveExceptionAsync(Guid attendanceId, string? notes, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(attendanceId);

        if (!entity.HasException)
            throw new InvalidOperationException("This attendance record has no pending exception.");

        entity.ExceptionApproved = true;
        entity.Notes = notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Exception approved on attendance {Id} by {UserId}", attendanceId, userId);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE LOG SERVICE
// ============================================================================

#region Staff Attendance Log Service

public class StaffAttendanceLogService : IStaffAttendanceLogService
{
    private readonly IStaffAttendanceLogRepository _repository;
    private readonly IStaffDailyAttendanceRepository _dailyRepository;
    private readonly IAttendanceLocationVerificationLogRepository _verificationLogRepository;
    private readonly IGeofenceVerificationService _geofenceVerification;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendanceLogService> _logger;

    public StaffAttendanceLogService(
        IStaffAttendanceLogRepository repository,
        IStaffDailyAttendanceRepository dailyRepository,
        IAttendanceLocationVerificationLogRepository verificationLogRepository,
        IGeofenceVerificationService geofenceVerification,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendanceLogService> logger)
    {
        _repository = repository;
        _dailyRepository = dailyRepository;
        _verificationLogRepository = verificationLogRepository;
        _geofenceVerification = geofenceVerification;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // An attendance log owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffAttendanceLog> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Attendance log '{id}' not found.");
        return entity;
    }

    public async Task<StaffAttendanceLogDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffAttendanceLogSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeIdAsync(employeeId, from, to))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceLogSummaryDto>> GetUnprocessedLogsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetUnprocessedLogsAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceLogSummaryDto>> GetByDeviceIdAsync(Guid deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByDeviceIdAsync(deviceId, from, to))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffAttendanceLogSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // EmployeeName is on the summary DTO, so Employee must be loaded.
        var query = _repository.GetQueryable().Include(l => l.Employee).Where(l => l.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.LogDateTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffAttendanceLogSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<StaffAttendanceLogDto> CreateAsync(CreateStaffAttendanceLogDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        if (dto.Latitude.HasValue && dto.Longitude.HasValue)
        {
            var verification = await _geofenceVerification.VerifyPunchAsync(
                dto.EmployeeId, current, dto.Latitude, dto.Longitude, ct);
            RejectIfRequired(verification);
        }

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Attendance log created for employee {EmployeeId} at {LogDateTime}", dto.EmployeeId, dto.LogDateTime);
        return entity.ToDto();
    }

    public async Task<StaffAttendancePunchResultDto> PunchAsync(
        StaffAttendancePunchDto dto,
        Guid tenantId,
        Guid employeeId,
        Guid userId,
        CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var verification = await _geofenceVerification.VerifyPunchAsync(
            employeeId, current, dto.Latitude, dto.Longitude, ct);
        RejectIfRequired(verification);

        var createDto = new CreateStaffAttendanceLogDto
        {
            EmployeeId = employeeId,
            LogDateTime = DateTime.UtcNow,
            LogType = dto.LogType,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Location = dto.Location,
        };

        var entity = createDto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Self-service punch ({LogType}) for employee {EmployeeId} at {LogDateTime}",
            dto.LogType,
            employeeId,
            entity.LogDateTime);

        Guid? dailyAttendanceId = null;
        var processedImmediately = false;

        if (dto.ProcessImmediately)
        {
            dailyAttendanceId = await ProcessLogInternalAsync(entity, userId, verification, ct);
            processedImmediately = true;
        }

        return new StaffAttendancePunchResultDto
        {
            Log = entity.ToDto(),
            Verification = ToSummaryDto(verification),
            DailyAttendanceId = dailyAttendanceId,
            ProcessedImmediately = processedImmediately,
        };
    }

    public async Task<Guid?> ProcessLogAsync(Guid logId, Guid userId, CancellationToken ct = default)
    {
        var log = await GetOwnedAsync(logId);

        if (log.IsProcessed)
            throw new InvalidOperationException("This log has already been processed.");

        var verification = await _geofenceVerification.VerifyPunchAsync(
            log.EmployeeId, log.TenantId, log.Latitude, log.Longitude, ct);
        RejectIfRequired(verification);

        return await ProcessLogInternalAsync(log, userId, verification, ct);
    }

    private async Task<Guid?> ProcessLogInternalAsync(
        StaffAttendanceLog log,
        Guid userId,
        GeofenceVerificationResult verification,
        CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (log.TenantId != tenantId)
            throw new ArgumentException($"Attendance log '{log.Id}' not found.");

        var logDate = DateOnly.FromDateTime(log.LogDateTime);
        var daily = await _dailyRepository.GetByEmployeeAndDateAsync(log.EmployeeId, logDate);

        if (daily != null && daily.TenantId != tenantId)
            daily = null;

        if (daily == null)
        {
            daily = new StaffDailyAttendance
            {
                Id = Guid.NewGuid(),
                TenantId = log.TenantId,
                EmployeeId = log.EmployeeId,
                AttendanceDate = logDate,
                ActualCheckInTime = log.LogType == AttendanceLogType.CheckIn ? (TimeSpan?)log.LogDateTime.TimeOfDay : null,
                ActualCheckOutTime = log.LogType == AttendanceLogType.CheckOut ? (TimeSpan?)log.LogDateTime.TimeOfDay : null,
                Status = StaffAttendanceStatus.Present,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId.ToString()
            };
            ApplyLocationToDaily(daily, log, verification);
            await _dailyRepository.AddAsync(daily);
        }
        else
        {
            if (log.LogType == AttendanceLogType.CheckIn && daily.ActualCheckInTime == null)
                daily.ActualCheckInTime = log.LogDateTime.TimeOfDay;
            else if (log.LogType == AttendanceLogType.CheckOut)
                daily.ActualCheckOutTime = log.LogDateTime.TimeOfDay;

            ApplyLocationToDaily(daily, log, verification);
            daily.UpdatedAt = DateTime.UtcNow;
            daily.UpdatedBy = userId.ToString();
            await _dailyRepository.UpdateAsync(daily);
        }

        await WriteVerificationLogAsync(log, verification, userId);

        log.IsProcessed = true;
        log.ProcessedDate = DateTime.UtcNow;
        log.AttendanceId = daily.Id;
        log.UpdatedAt = DateTime.UtcNow;
        log.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(log);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Log {LogId} processed into daily attendance {DailyId}", log.Id, daily.Id);
        return daily.Id;
    }

    private async Task WriteVerificationLogAsync(
        StaffAttendanceLog log,
        GeofenceVerificationResult verification,
        Guid userId)
    {
        if (!verification.HasConfiguredZone && !verification.HasGpsCoordinates)
            return;

        var verificationLog = new AttendanceLocationVerificationLog
        {
            Id = Guid.NewGuid(),
            TenantId = log.TenantId,
            AttendanceLogId = log.Id,
            EmployeeId = log.EmployeeId,
            VerificationDateTime = DateTime.UtcNow,
            Latitude = log.Latitude ?? 0,
            Longitude = log.Longitude ?? 0,
            GeofenceZoneId = verification.GeofenceZoneId,
            DistanceFromZoneMetres = verification.DistanceFromZoneMetres,
            Status = verification.Status,
            Notes = verification.Message,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };

        await _verificationLogRepository.AddAsync(verificationLog);
    }

    private static void ApplyLocationToDaily(
        StaffDailyAttendance daily,
        StaffAttendanceLog log,
        GeofenceVerificationResult verification)
    {
        if (log.LogType == AttendanceLogType.CheckIn)
        {
            if (log.Latitude.HasValue)
                daily.CheckInLatitude = log.Latitude;
            if (log.Longitude.HasValue)
                daily.CheckInLongitude = log.Longitude;
            if (!string.IsNullOrWhiteSpace(log.Location))
                daily.CheckInLocation = log.Location;

            if (verification.HasConfiguredZone || verification.HasGpsCoordinates)
            {
                daily.CheckInLocationStatus = verification.Status;
                if (verification.GeofenceZoneId.HasValue)
                    daily.CheckInGeofenceZoneId = verification.GeofenceZoneId;
            }
        }
        else if (log.LogType == AttendanceLogType.CheckOut)
        {
            if (log.Latitude.HasValue)
                daily.CheckOutLatitude = log.Latitude;
            if (log.Longitude.HasValue)
                daily.CheckOutLongitude = log.Longitude;
            if (!string.IsNullOrWhiteSpace(log.Location))
                daily.CheckOutLocation = log.Location;

            if (verification.HasConfiguredZone || verification.HasGpsCoordinates)
                daily.CheckOutLocationStatus = verification.Status;
        }
    }

    private static void RejectIfRequired(GeofenceVerificationResult verification)
    {
        if (verification.ShouldReject)
            throw new GeofenceVerificationRejectedException(
                verification.Message ?? "Check-in was rejected because you are outside the approved work zone.");
    }

    private static GeofenceVerificationSummaryDto ToSummaryDto(GeofenceVerificationResult verification)
        => new()
        {
            Status = verification.Status,
            GeofenceZoneId = verification.GeofenceZoneId,
            GeofenceZoneName = verification.GeofenceZoneName,
            DistanceFromZoneMetres = verification.DistanceFromZoneMetres,
            HasConfiguredZone = verification.HasConfiguredZone,
            HasGpsCoordinates = verification.HasGpsCoordinates,
            Message = verification.Message,
        };

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE REGULARIZATION SERVICE
// ============================================================================

#region Staff Attendance Regularization Service

public class StaffAttendanceRegularizationService : IStaffAttendanceRegularizationService
{
    /// <summary>Workflow entity type; must match the catalog entry and the status adapter.</summary>
    private const string EntityType = "StaffAttendanceRegularization";

    private readonly IStaffAttendanceRegularizationRepository _repository;
    private readonly IStaffDailyAttendanceRepository _dailyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendanceRegularizationService> _logger;

    public StaffAttendanceRegularizationService(
        IStaffAttendanceRegularizationRepository repository,
        IStaffDailyAttendanceRepository dailyRepository,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendanceRegularizationService> logger)
    {
        _repository = repository;
        _dailyRepository = dailyRepository;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// The workflow engine identifies approvers by ApplicationUser.Id, but every method on
    /// this service receives the caller's *Employee* id (that is what
    /// <c>AttendanceControllerBase</c> resolves from the token). The two are not
    /// interchangeable here: <c>StaffAttendanceRegularization.ApprovedById</c> is a real FK
    /// to <c>Employee</c>, unlike <c>LeaveRequest.ApprovedById</c> which is a bare Guid — so
    /// storing a user id in it is a foreign-key violation. Workflow calls therefore use this
    /// property, and the entity's own audit fields keep using the employee id.
    /// </summary>
    private Guid GetCurrentUserId() => _currentUserProvider.UserId;

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A regularization owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffAttendanceRegularization> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Regularization '{id}' not found.");
        return entity;
    }

    private async Task<StaffAttendanceRegularization> GetOwnedWithDetailsAsync(Guid id)
    {
        var entity = await _repository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Regularization '{id}' not found.");
        return entity;
    }

    public async Task<StaffAttendanceRegularizationDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedWithDetailsAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceRegularizationDto?> GetByRegularizationNumberAsync(string regularizationNumber, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetByRegularizationNumberAsync(regularizationNumber);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeIdAsync(employeeId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetByAttendanceIdAsync(Guid attendanceId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByAttendanceIdAsync(attendanceId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetByStatusAsync(AttendanceRegularizationStatus status, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetPendingApprovalAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffAttendanceRegularizationSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // EmployeeName is on the summary DTO, so Employee must be loaded.
        var query = _repository.GetQueryable().Include(r => r.Employee).Where(r => r.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffAttendanceRegularizationSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<StaffAttendanceRegularizationDto> CreateAsync(CreateStaffAttendanceRegularizationDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, userId);
        entity.RegularizationNumber = await GenerateRegularizationNumberAsync(current, ct);
        entity.Status = AttendanceRegularizationStatus.Pending;
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        // AttendanceRegularizationStatus has no Draft member, so there is no separate submit
        // step for the user to take — the approval workflow starts as soon as the request
        // exists. The adapter decides the resulting status; a definition that auto-approves
        // will land the row on Approved rather than Pending.
        await StartApprovalWorkflowAsync(entity, ct);

        _logger.LogInformation("Regularization {Number} created for employee {EmployeeId}", entity.RegularizationNumber, entity.EmployeeId);
        return entity.ToDto();
    }

    /// <summary>
    /// Starts the approval workflow for a newly created regularization. A missing or
    /// unpublished workflow definition must not block the request from being raised, so a
    /// failure here is logged and the row is left Pending for manual handling.
    /// </summary>
    private async Task StartApprovalWorkflowAsync(StaffAttendanceRegularization entity, CancellationToken ct)
    {
        try
        {
            var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, entity.Id);
            if (!workflowResult.ExecutionResult.Success)
            {
                _logger.LogWarning(
                    "Approval workflow did not start for regularization {Number}: {Message}",
                    entity.RegularizationNumber,
                    workflowResult.ExecutionResult.Message);
                return;
            }

            var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
            adapter.ApplySubmitOutcome(entity, workflowResult.Outcome, entity.EmployeeId);

            await _repository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start the approval workflow for regularization {Number}; it remains pending.",
                entity.RegularizationNumber);
        }
    }

    public async Task<StaffAttendanceRegularizationDto> UpdateAsync(UpdateStaffAttendanceRegularizationDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        if (entity.Status != AttendanceRegularizationStatus.Pending)
            throw new InvalidOperationException("Only pending regularizations can be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }

    /// <summary>
    /// Records an approval decision. The generic workflow engine owns the outcome — this
    /// method only relays the decision and lets
    /// <c>StaffAttendanceRegularizationWorkflowStatusAdapter</c> set the status, so a
    /// multi-step definition leaves the row Pending until the final step passes.
    /// </summary>
    public async Task<StaffAttendanceRegularizationDto> ApproveAsync(ApproveRegularizationDto dto, Guid userId, CancellationToken ct = default)
        => await ProcessDecisionAsync(dto.RegularizationId, "Approve", dto.ApprovalComments, userId, ct);

    public async Task<StaffAttendanceRegularizationDto> RejectAsync(RejectRegularizationDto dto, Guid userId, CancellationToken ct = default)
        => await ProcessDecisionAsync(dto.RegularizationId, "Reject", dto.RejectionReason, userId, ct);

    private async Task<StaffAttendanceRegularizationDto> ProcessDecisionAsync(
        Guid regularizationId,
        string action,
        string? comments,
        Guid employeeId,
        CancellationToken ct)
    {
        var entity = await GetOwnedAsync(regularizationId);

        if (entity.Status != AttendanceRegularizationStatus.Pending)
            throw new InvalidOperationException("Only pending regularizations can be decided.");

        var isReject = string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase);
        var decisionText = isReject && string.IsNullOrWhiteSpace(comments) ? "Rejected" : comments;

        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, entity.Id, currentUserId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            EntityType, entity.Id, currentUserId, action, decisionText);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the decision.");

        // The adapter is handed the *employee* id, not the user id — ApprovedById is an
        // Employee foreign key on this entity.
        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, employeeId, isReject ? decisionText : null);

        if (!isReject)
            entity.ApprovalComments = comments;

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = employeeId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Regularization {Number} decision '{Action}' processed by {UserId}; outcome {Outcome}",
            entity.RegularizationNumber, action, currentUserId, workflowResult.Outcome);

        return await ReadDetailAsync(entity.Id, ct) ?? entity.ToDto();
    }

    /// <summary>
    /// Re-reads a regularization with its navigations loaded, for returning after a write.
    ///
    /// The tracked instance cannot be used: <c>ApprovedById</c> is assigned *after* the
    /// entity was loaded, so its <c>ApprovedBy</c> navigation was never populated and the DTO
    /// would report a blank approver name. AsNoTracking sidesteps the identity map and gives
    /// a fresh graph.
    /// </summary>
    private async Task<StaffAttendanceRegularizationDto?> ReadDetailAsync(Guid id, CancellationToken ct)
    {
        var entity = await _repository.GetQueryable()
            .AsNoTracking()
            .Include(r => r.Employee)
            .Include(r => r.ApprovedBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        return entity?.ToDto();
    }

    public async Task<bool> ApplyAsync(Guid regularizationId, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(regularizationId);

        if (entity.Status != AttendanceRegularizationStatus.Approved)
            throw new InvalidOperationException("Only approved regularizations can be applied.");

        var daily = await _dailyRepository.GetByIdAsync(entity.AttendanceId);
        if (daily == null || daily.TenantId != entity.TenantId)
            throw new InvalidOperationException("The linked daily attendance record could not be found.");

        if (entity.RequestedCheckInTime.HasValue)
            daily.ActualCheckInTime = entity.RequestedCheckInTime;
        if (entity.RequestedCheckOutTime.HasValue)
            daily.ActualCheckOutTime = entity.RequestedCheckOutTime;

        daily.UpdatedAt = DateTime.UtcNow;
        daily.UpdatedBy = userId.ToString();

        entity.Status = AttendanceRegularizationStatus.Applied;
        // IsApplied/AppliedDate are what the DTO and the list screens read; without them a
        // record could sit at status Applied while still reporting isApplied = false.
        entity.IsApplied = true;
        entity.AppliedDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _dailyRepository.UpdateAsync(daily);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Regularization {Number} applied to attendance {AttendanceId}", entity.RegularizationNumber, daily.Id);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status == AttendanceRegularizationStatus.Applied)
            throw new InvalidOperationException("Applied regularizations cannot be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateRegularizationNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var count = await _repository.GetQueryable().CountAsync(r => r.TenantId == tenantId, ct);
        return $"REG-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}";
    }
}

#endregion

// ============================================================================
// STAFF MONTHLY ATTENDANCE SUMMARY SERVICE
// ============================================================================

#region Staff Monthly Attendance Summary Service

public class StaffMonthlyAttendanceSummaryService : IStaffMonthlyAttendanceSummaryService
{
    private readonly IStaffMonthlyAttendanceSummaryRepository _repository;
    private readonly IStaffDailyAttendanceRepository _dailyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffMonthlyAttendanceSummaryService> _logger;

    public StaffMonthlyAttendanceSummaryService(
        IStaffMonthlyAttendanceSummaryRepository repository,
        IStaffDailyAttendanceRepository dailyRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffMonthlyAttendanceSummaryService> logger)
    {
        _repository = repository;
        _dailyRepository = dailyRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A monthly summary owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffMonthlyAttendanceSummary> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Monthly summary '{id}' not found.");
        return entity;
    }

    public async Task<StaffMonthlyAttendanceSummaryDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffMonthlyAttendanceSummaryDto?> GetByEmployeeAndPeriodAsync(Guid employeeId, int year, int month, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetByEmployeeAndPeriodAsync(employeeId, year, month);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetByEmployeeAndYearAsync(Guid employeeId, int year, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeAndYearAsync(employeeId, year))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetByYearAndMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByYearAndMonthAsync(year, month))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetByPayPeriodIdAsync(Guid payPeriodId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByPayPeriodIdAsync(payPeriodId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetUnfinalizedAsync(int year, int month, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetUnfinalizedAsync(year, month))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffMonthlyAttendanceSummaryDto> RecalculateAsync(Guid employeeId, int year, int month, Guid userId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var days = (await _dailyRepository.GetByEmployeeIdAsync(employeeId, from, to))
            .Where(d => d.TenantId == tenantId)
            .ToList();

        var existing = await _repository.GetByEmployeeAndPeriodAsync(employeeId, year, month);
        if (existing != null && existing.TenantId != tenantId)
            existing = null;

        if (existing != null && existing.IsFinalized)
            throw new InvalidOperationException("A finalized monthly summary cannot be recalculated.");

        var presentCount = days.Count(d => d.Status == StaffAttendanceStatus.Present);
        var absentCount = days.Count(d => d.Status == StaffAttendanceStatus.Absent);
        var leaveDays = days.Count(d => d.Status == StaffAttendanceStatus.OnLeave);
        var totalOvertimeHours = days.Sum(d => d.OvertimeHours ?? 0);
        var lateDays = days.Count(d => d.IsLate);
        var totalLateMinutes = days.Sum(d => d.LateMinutes ?? 0);

        if (existing == null)
        {
            existing = new StaffMonthlyAttendanceSummary
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = employeeId,
                Year = year,
                Month = month,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId.ToString()
            };
        }

        existing.DaysPresent = presentCount;
        existing.DaysAbsent = absentCount;
        existing.DaysOnLeave = leaveDays;
        existing.TotalOvertimeHours = totalOvertimeHours;
        existing.NumberOfLateDays = lateDays;
        existing.TotalLateMinutes = totalLateMinutes;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId.ToString();

        if (existing.Id == Guid.Empty || await _repository.GetByIdAsync(existing.Id) is not { TenantId: var tid } || tid != tenantId)
            await _repository.AddAsync(existing);
        else
            await _repository.UpdateAsync(existing);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Monthly summary recalculated for employee {EmployeeId} {Year}/{Month}", employeeId, year, month);
        return existing.ToDto();
    }

    public async Task<StaffMonthlyAttendanceSummaryDto> FinalizeAsync(Guid summaryId, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(summaryId);

        if (entity.IsFinalized)
            throw new InvalidOperationException("This monthly summary is already finalized.");

        entity.IsFinalized = true;
        entity.FinalizedDate = DateTime.UtcNow;
        entity.FinalizedById = userId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Monthly summary {Id} finalized by {UserId}", summaryId, userId);
        return entity.ToDto();
    }

    public async Task<StaffMonthlyAttendanceSummaryDto> UpdateAsync(UpdateStaffMonthlyAttendanceSummaryDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        if (entity.IsFinalized)
            throw new InvalidOperationException("A finalized monthly summary cannot be edited directly. Use RecalculateAsync instead.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.IsFinalized)
            throw new InvalidOperationException("A finalized monthly summary cannot be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// STAFF BULK ATTENDANCE IMPORT SERVICE
// ============================================================================

#region Staff Bulk Attendance Import Service

public class StaffBulkAttendanceImportService : IStaffBulkAttendanceImportService
{
    private readonly IStaffBulkAttendanceImportRepository _repository;
    private readonly IStaffBulkAttendanceImportRowRepository _rowRepository;
    private readonly IStaffDailyAttendanceRepository _dailyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffBulkAttendanceImportService> _logger;

    public StaffBulkAttendanceImportService(
        IStaffBulkAttendanceImportRepository repository,
        IStaffBulkAttendanceImportRowRepository rowRepository,
        IStaffDailyAttendanceRepository dailyRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffBulkAttendanceImportService> logger)
    {
        _repository = repository;
        _rowRepository = rowRepository;
        _dailyRepository = dailyRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // An import batch owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffBulkAttendanceImport> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Import batch '{id}' not found.");
        return entity;
    }

    private async Task<StaffBulkAttendanceImport> GetOwnedWithRowsAsync(Guid id)
    {
        var entity = await _repository.GetWithRowsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Import batch '{id}' not found.");
        return entity;
    }

    public async Task<StaffBulkAttendanceImportDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedWithRowsAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffBulkAttendanceImportDto?> GetByImportReferenceAsync(string importReference, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetByImportReferenceAsync(importReference);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffBulkAttendanceImportSummaryDto>> GetByStatusAsync(AttendanceImportStatus status, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffBulkAttendanceImportSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // ImportedByName is on the summary DTO, so ImportedBy must be loaded.
        var query = _repository.GetQueryable().Include(i => i.ImportedBy).Where(i => i.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffBulkAttendanceImportSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<StaffBulkAttendanceImportRowDto>> GetRowsAsync(Guid importId, CancellationToken ct = default)
    {
        await GetOwnedAsync(importId);
        var tenantId = GetTenantId();
        var rows = (await _rowRepository.GetByImportIdAsync(importId))
            .Where(r => r.TenantId == tenantId);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffBulkAttendanceImportRowDto>> GetFailedRowsAsync(Guid importId, CancellationToken ct = default)
    {
        await GetOwnedAsync(importId);
        var tenantId = GetTenantId();
        var rows = (await _rowRepository.GetFailedRowsAsync(importId))
            .Where(r => r.TenantId == tenantId);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<StaffBulkAttendanceImportDto> InitiateAsync(CreateStaffBulkAttendanceImportDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, userId);
        entity.ImportReference = await GenerateImportReferenceAsync(current, ct);
        entity.Status = AttendanceImportStatus.Pending;
        entity.TotalRows = dto.Rows?.Count ?? 0;

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        if (dto.Rows?.Count > 0)
        {
            foreach (var rowDto in dto.Rows)
            {
                var row = new StaffBulkAttendanceImportRow
                {
                    Id = Guid.NewGuid(),
                    TenantId = current,
                    ImportId = entity.Id,
                    RowNumber = rowDto.RowNumber,
                    EmployeeId = rowDto.EmployeeId,
                    RawData = rowDto.RawData,
                    AttendanceDate = rowDto.AttendanceDate,
                    CheckInTime = rowDto.CheckInTime,
                    CheckOutTime = rowDto.CheckOutTime,
                    IsSuccess = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId.ToString()
                };
                await _rowRepository.AddAsync(row);
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Bulk import initiated: {Reference} with {Count} rows", entity.ImportReference, entity.TotalRows);

        var loaded = await _repository.GetWithRowsAsync(entity.Id);
        return loaded!.TenantId == current ? loaded.ToDto() : entity.ToDto();
    }

    public async Task<StaffBulkAttendanceImportDto> ProcessAsync(Guid importId, Guid userId, CancellationToken ct = default)
    {
        var import = await GetOwnedWithRowsAsync(importId);

        if (import.Status == AttendanceImportStatus.Completed)
            throw new InvalidOperationException("This import has already been completed.");

        import.Status = AttendanceImportStatus.Processing;

        int successCount = 0, failureCount = 0;

        foreach (var row in import.ImportRows.Where(r => r.TenantId == import.TenantId))
        {
            try
            {
                var attendanceDate = row.AttendanceDate ?? DateOnly.MinValue;
                var existing = await _dailyRepository.GetByEmployeeAndDateAsync(row.EmployeeId ?? Guid.Empty, attendanceDate);
                if (existing != null && existing.TenantId != import.TenantId)
                    existing = null;

                if (existing == null && row.EmployeeId.HasValue)
                {
                    var daily = new StaffDailyAttendance
                    {
                        Id = Guid.NewGuid(),
                        TenantId = import.TenantId,
                        EmployeeId = row.EmployeeId.Value,
                        AttendanceDate = attendanceDate,
                        ActualCheckInTime = row.CheckInTime?.ToTimeSpan(),
                        ActualCheckOutTime = row.CheckOutTime?.ToTimeSpan(),
                        Status = StaffAttendanceStatus.Present,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userId.ToString()
                    };
                    await _dailyRepository.AddAsync(daily);
                }

                row.IsSuccess = true;
                successCount++;
            }
            catch (Exception ex)
            {
                row.IsSuccess = false;
                row.ErrorMessage = ex.Message;
                failureCount++;
            }

            await _rowRepository.UpdateAsync(row);
        }

        import.SuccessCount = successCount;
        import.FailureCount = failureCount;
        import.Status = failureCount == 0 ? AttendanceImportStatus.Completed : AttendanceImportStatus.PartialSuccess;
        import.UpdatedAt = DateTime.UtcNow;
        import.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(import);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Import {Reference} processed: {Success} success, {Failure} failures", import.ImportReference, successCount, failureCount);
        return import.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status == AttendanceImportStatus.Processing)
            throw new InvalidOperationException("An import that is currently processing cannot be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateImportReferenceAsync(Guid tenantId, CancellationToken ct)
    {
        var count = await _repository.GetQueryable().CountAsync(i => i.TenantId == tenantId, ct);
        return $"ATT-IMP-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}";
    }
}

#endregion

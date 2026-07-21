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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendanceRecordService> _logger;

    public StaffAttendanceRecordService(
        IStaffAttendanceRecordRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendanceRecordService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffAttendanceRecordDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Attendance record '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffAttendanceRecordDto?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default)
    {
        var entity = await _repository.GetByEmployeeAndDateAsync(employeeId, date);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var entities = await _repository.GetByEmployeeIdAsync(employeeId, from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var entities = await _repository.GetByDateAsync(date);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var entities = await _repository.GetByDateRangeAsync(from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByStatusAsync(StaffAttendanceStatus status, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var entities = await _repository.GetByStatusAsync(status, from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffAttendanceRecordSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
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
        var existing = await _repository.GetByEmployeeAndDateAsync(dto.EmployeeId, dto.Date);
        if (existing != null)
            throw new InvalidOperationException($"An attendance record already exists for this employee on {dto.Date}.");

        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Attendance record created for employee {EmployeeId} on {Date}", dto.EmployeeId, dto.Date);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceRecordDto> UpdateAsync(UpdateStaffAttendanceRecordDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Attendance record '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Attendance record '{id}' not found.");

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDailyAttendanceService> _logger;

    public StaffDailyAttendanceService(
        IStaffDailyAttendanceRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<StaffDailyAttendanceService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffDailyAttendanceDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetWithFullDetailsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Daily attendance record '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffDailyAttendanceDto?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default)
    {
        var entity = await _repository.GetByEmployeeAndDateAsync(employeeId, date);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var entities = await _repository.GetByEmployeeIdAsync(employeeId, from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var entities = await _repository.GetByDateAsync(date);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByStatusAsync(StaffAttendanceStatus status, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var entities = await _repository.GetByStatusAsync(status, from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetPendingVerificationAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetPendingVerificationAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetWithOpenExceptionsAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetWithOpenExceptionsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetWithOvertimeAsync(DateOnly from, DateOnly to, Guid? employeeId = null, CancellationToken ct = default)
    {
        var entities = await _repository.GetWithOvertimeAsync(from, to, employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetLateAttendancesAsync(DateOnly from, DateOnly to, Guid? employeeId = null, CancellationToken ct = default)
    {
        var entities = await _repository.GetLateAttendancesAsync(from, to, employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetRemoteWorkDaysAsync(DateOnly from, DateOnly to, Guid? employeeId = null, CancellationToken ct = default)
    {
        var entities = await _repository.GetRemoteWorkDaysAsync(from, to, employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByPayPeriodIdAsync(Guid payPeriodId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByPayPeriodIdAsync(payPeriodId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffDailyAttendanceSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
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

    public async Task<StaffDailyAttendanceDto> CreateAsync(CreateStaffDailyAttendanceDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var existing = await _repository.GetByEmployeeAndDateAsync(dto.EmployeeId, dto.AttendanceDate);
        if (existing != null)
            throw new InvalidOperationException($"A daily attendance record already exists for employee {dto.EmployeeId} on {dto.AttendanceDate}.");

        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Daily attendance created for employee {EmployeeId} on {Date}", dto.EmployeeId, dto.AttendanceDate);
        return entity.ToDto();
    }

    public async Task<StaffDailyAttendanceDto> UpdateAsync(UpdateStaffDailyAttendanceDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Daily attendance '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }

    public async Task<StaffDailyAttendanceDto> VerifyAsync(VerifyAttendanceDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.AttendanceId);
        if (entity == null)
            throw new ArgumentException($"Daily attendance '{dto.AttendanceId}' not found.");

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
        var entity = await _repository.GetByIdAsync(attendanceId);
        if (entity == null)
            throw new ArgumentException($"Daily attendance '{attendanceId}' not found.");

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
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Daily attendance '{id}' not found.");

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendanceLogService> _logger;

    public StaffAttendanceLogService(
        IStaffAttendanceLogRepository repository,
        IStaffDailyAttendanceRepository dailyRepository,
        IAttendanceLocationVerificationLogRepository verificationLogRepository,
        IGeofenceVerificationService geofenceVerification,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendanceLogService> logger)
    {
        _repository = repository;
        _dailyRepository = dailyRepository;
        _verificationLogRepository = verificationLogRepository;
        _geofenceVerification = geofenceVerification;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffAttendanceLogDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Attendance log '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffAttendanceLogSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var entities = await _repository.GetByEmployeeIdAsync(employeeId, from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceLogSummaryDto>> GetUnprocessedLogsAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetUnprocessedLogsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceLogSummaryDto>> GetByDeviceIdAsync(Guid deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var entities = await _repository.GetByDeviceIdAsync(deviceId, from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffAttendanceLogSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
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
        if (dto.Latitude.HasValue && dto.Longitude.HasValue)
        {
            var verification = await _geofenceVerification.VerifyPunchAsync(
                dto.EmployeeId, tenantId, dto.Latitude, dto.Longitude, ct);
            RejectIfRequired(verification);
        }

        var entity = dto.ToEntity(tenantId, userId);
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
        var verification = await _geofenceVerification.VerifyPunchAsync(
            employeeId, tenantId, dto.Latitude, dto.Longitude, ct);
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

        var entity = createDto.ToEntity(tenantId, userId);
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
        var log = await _repository.GetByIdAsync(logId);
        if (log == null)
            throw new ArgumentException($"Attendance log '{logId}' not found.");

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
        var logDate = DateOnly.FromDateTime(log.LogDateTime);
        var daily = await _dailyRepository.GetByEmployeeAndDateAsync(log.EmployeeId, logDate);

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
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Attendance log '{id}' not found.");

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
    private readonly IStaffAttendanceRegularizationRepository _repository;
    private readonly IStaffDailyAttendanceRepository _dailyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendanceRegularizationService> _logger;

    public StaffAttendanceRegularizationService(
        IStaffAttendanceRegularizationRepository repository,
        IStaffDailyAttendanceRepository dailyRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendanceRegularizationService> logger)
    {
        _repository = repository;
        _dailyRepository = dailyRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffAttendanceRegularizationDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetWithFullDetailsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Regularization '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffAttendanceRegularizationDto?> GetByRegularizationNumberAsync(string regularizationNumber, CancellationToken ct = default)
    {
        var entity = await _repository.GetByRegularizationNumberAsync(regularizationNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetByAttendanceIdAsync(Guid attendanceId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByAttendanceIdAsync(attendanceId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetByStatusAsync(AttendanceRegularizationStatus status, CancellationToken ct = default)
    {
        var entities = await _repository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetPendingApprovalAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffAttendanceRegularizationSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
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
        var entity = dto.ToEntity(tenantId, userId);
        entity.RegularizationNumber = await GenerateRegularizationNumberAsync(ct);
        entity.Status = AttendanceRegularizationStatus.Pending;
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Regularization {Number} created for employee {EmployeeId}", entity.RegularizationNumber, entity.EmployeeId);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceRegularizationDto> UpdateAsync(UpdateStaffAttendanceRegularizationDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Regularization '{dto.Id}' not found.");

        if (entity.Status != AttendanceRegularizationStatus.Pending)
            throw new InvalidOperationException("Only pending regularizations can be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }

    public async Task<StaffAttendanceRegularizationDto> ApproveAsync(ApproveRegularizationDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.RegularizationId);
        if (entity == null)
            throw new ArgumentException($"Regularization '{dto.RegularizationId}' not found.");

        if (entity.Status != AttendanceRegularizationStatus.Pending)
            throw new InvalidOperationException("Only pending regularizations can be approved.");

        entity.Status = AttendanceRegularizationStatus.Approved;
        entity.ApprovedById = userId;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.ApprovalComments = dto.ApprovalComments;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Regularization {Number} approved by {UserId}", entity.RegularizationNumber, userId);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceRegularizationDto> RejectAsync(RejectRegularizationDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.RegularizationId);
        if (entity == null)
            throw new ArgumentException($"Regularization '{dto.RegularizationId}' not found.");

        if (entity.Status != AttendanceRegularizationStatus.Pending)
            throw new InvalidOperationException("Only pending regularizations can be rejected.");

        entity.Status = AttendanceRegularizationStatus.Rejected;
        entity.RejectedDate = DateTime.UtcNow;
        entity.RejectionReason = dto.RejectionReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Regularization {Number} rejected by {UserId}", entity.RegularizationNumber, userId);
        return entity.ToDto();
    }

    public async Task<bool> ApplyAsync(Guid regularizationId, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(regularizationId);
        if (entity == null)
            throw new ArgumentException($"Regularization '{regularizationId}' not found.");

        if (entity.Status != AttendanceRegularizationStatus.Approved)
            throw new InvalidOperationException("Only approved regularizations can be applied.");

        var daily = await _dailyRepository.GetByIdAsync(entity.AttendanceId);
        if (daily == null)
            throw new InvalidOperationException("The linked daily attendance record could not be found.");

        if (entity.RequestedCheckInTime.HasValue)
            daily.ActualCheckInTime = entity.RequestedCheckInTime;
        if (entity.RequestedCheckOutTime.HasValue)
            daily.ActualCheckOutTime = entity.RequestedCheckOutTime;

        daily.UpdatedAt = DateTime.UtcNow;
        daily.UpdatedBy = userId.ToString();

        entity.Status = AttendanceRegularizationStatus.Applied;
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
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Regularization '{id}' not found.");

        if (entity.Status == AttendanceRegularizationStatus.Applied)
            throw new InvalidOperationException("Applied regularizations cannot be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateRegularizationNumberAsync(CancellationToken ct)
    {
        var count = await _repository.GetQueryable().CountAsync(ct);
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffMonthlyAttendanceSummaryService> _logger;

    public StaffMonthlyAttendanceSummaryService(
        IStaffMonthlyAttendanceSummaryRepository repository,
        IStaffDailyAttendanceRepository dailyRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffMonthlyAttendanceSummaryService> logger)
    {
        _repository = repository;
        _dailyRepository = dailyRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffMonthlyAttendanceSummaryDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Monthly summary '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffMonthlyAttendanceSummaryDto?> GetByEmployeeAndPeriodAsync(Guid employeeId, int year, int month, CancellationToken ct = default)
    {
        var entity = await _repository.GetByEmployeeAndPeriodAsync(employeeId, year, month);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetByEmployeeAndYearAsync(Guid employeeId, int year, CancellationToken ct = default)
    {
        var entities = await _repository.GetByEmployeeAndYearAsync(employeeId, year);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetByYearAndMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var entities = await _repository.GetByYearAndMonthAsync(year, month);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetByPayPeriodIdAsync(Guid payPeriodId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByPayPeriodIdAsync(payPeriodId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetUnfinalizedAsync(int year, int month, CancellationToken ct = default)
    {
        var entities = await _repository.GetUnfinalizedAsync(year, month);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffMonthlyAttendanceSummaryDto> RecalculateAsync(Guid employeeId, int year, int month, Guid userId, CancellationToken ct = default)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var days = (await _dailyRepository.GetByEmployeeIdAsync(employeeId, from, to)).ToList();

        var existing = await _repository.GetByEmployeeAndPeriodAsync(employeeId, year, month);

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
                TenantId = days.FirstOrDefault()?.TenantId ?? Guid.Empty,
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

        if (existing.Id == Guid.Empty || await _repository.GetByIdAsync(existing.Id) == null)
            await _repository.AddAsync(existing);
        else
            await _repository.UpdateAsync(existing);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Monthly summary recalculated for employee {EmployeeId} {Year}/{Month}", employeeId, year, month);
        return existing.ToDto();
    }

    public async Task<StaffMonthlyAttendanceSummaryDto> FinalizeAsync(Guid summaryId, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(summaryId);
        if (entity == null)
            throw new ArgumentException($"Monthly summary '{summaryId}' not found.");

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
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Monthly summary '{dto.Id}' not found.");

        if (entity.IsFinalized)
            throw new InvalidOperationException("A finalized monthly summary cannot be edited directly. Use RecalculateAsync instead.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Monthly summary '{id}' not found.");

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffBulkAttendanceImportService> _logger;

    public StaffBulkAttendanceImportService(
        IStaffBulkAttendanceImportRepository repository,
        IStaffBulkAttendanceImportRowRepository rowRepository,
        IStaffDailyAttendanceRepository dailyRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffBulkAttendanceImportService> logger)
    {
        _repository = repository;
        _rowRepository = rowRepository;
        _dailyRepository = dailyRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffBulkAttendanceImportDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetWithRowsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Import batch '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffBulkAttendanceImportDto?> GetByImportReferenceAsync(string importReference, CancellationToken ct = default)
    {
        var entity = await _repository.GetByImportReferenceAsync(importReference);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffBulkAttendanceImportSummaryDto>> GetByStatusAsync(AttendanceImportStatus status, CancellationToken ct = default)
    {
        var entities = await _repository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffBulkAttendanceImportSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
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
        var rows = await _rowRepository.GetByImportIdAsync(importId);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffBulkAttendanceImportRowDto>> GetFailedRowsAsync(Guid importId, CancellationToken ct = default)
    {
        var rows = await _rowRepository.GetFailedRowsAsync(importId);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<StaffBulkAttendanceImportDto> InitiateAsync(CreateStaffBulkAttendanceImportDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        entity.ImportReference = await GenerateImportReferenceAsync(ct);
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
                    TenantId = tenantId,
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
        return loaded!.ToDto();
    }

    public async Task<StaffBulkAttendanceImportDto> ProcessAsync(Guid importId, Guid userId, CancellationToken ct = default)
    {
        var import = await _repository.GetWithRowsAsync(importId);
        if (import == null)
            throw new ArgumentException($"Import batch '{importId}' not found.");

        if (import.Status == AttendanceImportStatus.Completed)
            throw new InvalidOperationException("This import has already been completed.");

        import.Status = AttendanceImportStatus.Processing;

        int successCount = 0, failureCount = 0;

        foreach (var row in import.ImportRows)
        {
            try
            {
                var attendanceDate = row.AttendanceDate ?? DateOnly.MinValue;
                var existing = await _dailyRepository.GetByEmployeeAndDateAsync(row.EmployeeId ?? Guid.Empty, attendanceDate);
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
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Import batch '{id}' not found.");

        if (entity.Status == AttendanceImportStatus.Processing)
            throw new InvalidOperationException("An import that is currently processing cannot be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateImportReferenceAsync(CancellationToken ct)
    {
        var count = await _repository.GetQueryable().CountAsync(ct);
        return $"ATT-IMP-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}";
    }
}

#endregion

using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// GEOFENCE ZONE SERVICE
// ============================================================================

#region Geofence Zone Service

public class GeofenceZoneService : IGeofenceZoneService
{
    private readonly IGeofenceZoneRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GeofenceZoneService> _logger;

    public GeofenceZoneService(
        IGeofenceZoneRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<GeofenceZoneService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<GeofenceZoneDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Geofence zone '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<GeofenceZoneSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<GeofenceZoneSummaryDto>> GetActiveZonesAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetActiveZonesAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<GeofenceZoneSummaryDto>> GetByLocationIdAsync(Guid locationId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByLocationIdAsync(locationId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<GeofenceZoneSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(z => z.ZoneName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<GeofenceZoneSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<GeofenceZoneDto> CreateAsync(CreateGeofenceZoneDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        ValidateGeofenceDto(dto.Shape, dto.CentreLatitude, dto.CentreLongitude, dto.RadiusMetres, dto.PolygonCoordinatesJson);

        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Geofence zone created: {Name}", entity.ZoneName);
        return entity.ToDto();
    }

    public async Task<GeofenceZoneDto> UpdateAsync(UpdateGeofenceZoneDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Geofence zone '{dto.Id}' not found.");

        ValidateGeofenceDto(dto.Shape, dto.CentreLatitude, dto.CentreLongitude, dto.RadiusMetres, dto.PolygonCoordinatesJson);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Geofence zone '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private static void ValidateGeofenceDto(
        GeofenceShape shape,
        double? centreLatitude,
        double? centreLongitude,
        double? radiusMetres,
        string? polygonCoordinatesJson)
    {
        if (shape == GeofenceShape.Circle)
        {
            if (centreLatitude is null or < -90 or > 90)
                throw new ArgumentException("A valid centre latitude is required for a circle zone.");
            if (centreLongitude is null or < -180 or > 180)
                throw new ArgumentException("A valid centre longitude is required for a circle zone.");
            if (radiusMetres is null or < 1 or > 100000)
                throw new ArgumentException("Radius must be between 1 and 100,000 metres.");
            return;
        }

        if (shape == GeofenceShape.Polygon && string.IsNullOrWhiteSpace(polygonCoordinatesJson))
            throw new ArgumentException("Polygon coordinates are required for a polygon zone.");
    }
}

#endregion

// ============================================================================
// REMOTE WORK REQUEST SERVICE
// ============================================================================

#region Remote Work Request Service

public class RemoteWorkRequestService : IRemoteWorkRequestService
{
    private readonly IRemoteWorkRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoteWorkRequestService> _logger;

    public RemoteWorkRequestService(
        IRemoteWorkRequestRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<RemoteWorkRequestService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RemoteWorkRequestDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Remote work request '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<RemoteWorkRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken ct = default)
    {
        var entity = await _repository.GetByRequestNumberAsync(requestNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetByStatusAsync(RemoteWorkRequestStatus status, CancellationToken ct = default)
    {
        var entities = await _repository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetPendingApprovalAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var entities = await _repository.GetByDateRangeAsync(from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<RemoteWorkRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<RemoteWorkRequestSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<RemoteWorkRequestDto> CreateAsync(CreateRemoteWorkRequestDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        entity.RequestNumber = await GenerateRequestNumberAsync(ct);
        entity.Status = RemoteWorkRequestStatus.Pending;
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Remote work request {Number} created for employee {EmployeeId}", entity.RequestNumber, entity.EmployeeId);
        return entity.ToDto();
    }

    public async Task<RemoteWorkRequestDto> UpdateAsync(UpdateRemoteWorkRequestDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Remote work request '{dto.Id}' not found.");

        if (entity.Status != RemoteWorkRequestStatus.Pending)
            throw new InvalidOperationException("Only pending remote work requests can be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<RemoteWorkRequestDto> ApproveAsync(Guid requestId, string? comments, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(requestId);
        if (entity == null)
            throw new ArgumentException($"Remote work request '{requestId}' not found.");

        if (entity.Status != RemoteWorkRequestStatus.Pending)
            throw new InvalidOperationException("Only pending remote work requests can be approved.");

        entity.Status = RemoteWorkRequestStatus.Approved;
        entity.ApprovedById = userId;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.ApprovalComments = comments;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Remote work request {Number} approved by {UserId}", entity.RequestNumber, userId);
        return entity.ToDto();
    }

    public async Task<RemoteWorkRequestDto> RejectAsync(Guid requestId, string rejectionReason, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(requestId);
        if (entity == null)
            throw new ArgumentException($"Remote work request '{requestId}' not found.");

        if (entity.Status != RemoteWorkRequestStatus.Pending)
            throw new InvalidOperationException("Only pending remote work requests can be rejected.");

        entity.Status = RemoteWorkRequestStatus.Rejected;
        entity.RejectedDate = DateTime.UtcNow;
        entity.RejectionReason = rejectionReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Remote work request {Number} rejected by {UserId}", entity.RequestNumber, userId);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Remote work request '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateRequestNumberAsync(CancellationToken ct)
    {
        var count = await _repository.GetQueryable().CountAsync(ct);
        return $"RWR-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}";
    }
}

#endregion

// ============================================================================
// HOLIDAY CALENDAR SERVICE
// ============================================================================

#region Holiday Calendar Service

public class HolidayCalendarService : IHolidayCalendarService
{
    private readonly IHolidayCalendarRepository _repository;
    private readonly IPublicHolidayRepository _holidayRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HolidayCalendarService> _logger;

    public HolidayCalendarService(
        IHolidayCalendarRepository repository,
        IPublicHolidayRepository holidayRepository,
        IUnitOfWork unitOfWork,
        ILogger<HolidayCalendarService> logger)
    {
        _repository = repository;
        _holidayRepository = holidayRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<HolidayCalendarDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Holiday calendar '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<HolidayCalendarDto?> GetDefaultCalendarAsync(CancellationToken ct = default)
    {
        var entity = await _repository.GetDefaultCalendarAsync();
        return entity?.ToDto();
    }

    public async Task<IEnumerable<HolidayCalendarSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<HolidayCalendarSummaryDto>> GetActiveCalendarsAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetActiveCalendarsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<HolidayCalendarDto> GetWithHolidaysAsync(Guid id, int? year = null, CancellationToken ct = default)
    {
        var entity = await _repository.GetWithHolidaysAsync(id, year);
        if (entity == null)
            throw new ArgumentException($"Holiday calendar '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<PagedResult<HolidayCalendarSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.CalendarName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<HolidayCalendarSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<HolidayCalendarDto> CreateAsync(CreateHolidayCalendarDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Holiday calendar created: {Name}", entity.CalendarName);
        return entity.ToDto();
    }

    public async Task<HolidayCalendarDto> UpdateAsync(UpdateHolidayCalendarDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Holiday calendar '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Holiday calendar '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PublicHolidayDto> AddHolidayAsync(CreatePublicHolidayDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _holidayRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<IEnumerable<PublicHolidaySummaryDto>> GetHolidaysAsync(Guid calendarId, int? year = null, CancellationToken ct = default)
    {
        var entities = year.HasValue
            ? await _holidayRepository.GetByCalendarAndYearAsync(calendarId, year.Value)
            : await _holidayRepository.GetByCalendarIdAsync(calendarId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PublicHolidayDto> GetHolidayByIdAsync(Guid holidayId, CancellationToken ct = default)
    {
        var entity = await _holidayRepository.GetByIdAsync(holidayId);
        if (entity == null)
            throw new ArgumentException($"Public holiday '{holidayId}' not found.");
        return entity.ToDto();
    }

    public async Task<PublicHolidayDto> UpdateHolidayAsync(UpdatePublicHolidayDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _holidayRepository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Public holiday '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _holidayRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHolidayAsync(Guid holidayId, CancellationToken ct = default)
    {
        var entity = await _holidayRepository.GetByIdAsync(holidayId);
        if (entity == null)
            throw new ArgumentException($"Public holiday '{holidayId}' not found.");

        await _holidayRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// PAY PERIOD SERVICE
// ============================================================================

#region Pay Period Service

public class PayPeriodService : IPayPeriodService
{
    private readonly IPayPeriodRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PayPeriodService> _logger;

    public PayPeriodService(
        IPayPeriodRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<PayPeriodService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PayPeriodDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Pay period '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<PayPeriodDto?> GetCurrentOpenPeriodAsync(CancellationToken ct = default)
    {
        var entity = await _repository.GetCurrentOpenPeriodAsync();
        return entity?.ToDto();
    }

    public async Task<PayPeriodDto?> GetPeriodCoveringDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var entity = await _repository.GetPeriodCoveringDateAsync(date);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<PayPeriodSummaryDto>> GetByStatusAsync(PayPeriodStatus status, CancellationToken ct = default)
    {
        var entities = await _repository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<PayPeriodSummaryDto>> GetByTypeAsync(PayPeriodType type, CancellationToken ct = default)
    {
        var entities = await _repository.GetByTypeAsync(type);
        return entities.ToSummaryDtoList();
    }

    public async Task<PayPeriodDto> GetWithSummariesAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetWithSummariesAsync(id);
        if (entity == null)
            throw new ArgumentException($"Pay period '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<PagedResult<PayPeriodSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<PayPeriodSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PayPeriodDto> CreateAsync(CreatePayPeriodDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var overlapping = await _repository.GetPeriodCoveringDateAsync(dto.StartDate);
        if (overlapping != null)
            throw new InvalidOperationException($"An existing pay period already covers {dto.StartDate}.");

        var entity = dto.ToEntity(tenantId, userId);
        entity.Status = PayPeriodStatus.Open;
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Pay period created: {Name}", entity.PeriodName);
        return entity.ToDto();
    }

    public async Task<PayPeriodDto> UpdateAsync(UpdatePayPeriodDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Pay period '{dto.Id}' not found.");

        if (entity.Status == PayPeriodStatus.Closed)
            throw new InvalidOperationException("A closed pay period cannot be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<PayPeriodDto> CloseAsync(Guid periodId, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(periodId);
        if (entity == null)
            throw new ArgumentException($"Pay period '{periodId}' not found.");

        if (entity.Status == PayPeriodStatus.Closed)
            throw new InvalidOperationException("Pay period is already closed.");

        entity.Status = PayPeriodStatus.Closed;
        entity.ClosedDate = DateTime.UtcNow;
        entity.ClosedById = userId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Pay period {Name} closed by {UserId}", entity.PeriodName, userId);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Pay period '{id}' not found.");

        if (entity.Status == PayPeriodStatus.Closed)
            throw new InvalidOperationException("A closed pay period cannot be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE PAYROLL EXPORT SERVICE
// ============================================================================

#region Staff Attendance Payroll Export Service

public class StaffAttendancePayrollExportService : IStaffAttendancePayrollExportService
{
    private readonly IStaffAttendancePayrollExportRepository _repository;
    private readonly IPayPeriodRepository _payPeriodRepository;
    private readonly IStaffMonthlyAttendanceSummaryRepository _summaryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendancePayrollExportService> _logger;

    public StaffAttendancePayrollExportService(
        IStaffAttendancePayrollExportRepository repository,
        IPayPeriodRepository payPeriodRepository,
        IStaffMonthlyAttendanceSummaryRepository summaryRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendancePayrollExportService> logger)
    {
        _repository = repository;
        _payPeriodRepository = payPeriodRepository;
        _summaryRepository = summaryRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffAttendancePayrollExportDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Payroll export '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffAttendancePayrollExportDto?> GetByExportReferenceAsync(string exportReference, CancellationToken ct = default)
    {
        var entity = await _repository.GetByExportReferenceAsync(exportReference);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffAttendancePayrollExportSummaryDto>> GetByPayPeriodIdAsync(Guid payPeriodId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByPayPeriodIdAsync(payPeriodId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendancePayrollExportSummaryDto>> GetByStatusAsync(PayrollExportStatus status, CancellationToken ct = default)
    {
        var entities = await _repository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<StaffAttendancePayrollExportDto?> GetLatestSuccessfulExportForPeriodAsync(Guid payPeriodId, CancellationToken ct = default)
    {
        var entity = await _repository.GetLatestSuccessfulExportForPeriodAsync(payPeriodId);
        return entity?.ToDto();
    }

    public async Task<PagedResult<StaffAttendancePayrollExportSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.ExportDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffAttendancePayrollExportSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<StaffAttendancePayrollExportDto> ExportAsync(Guid payPeriodId, string? targetSystem, Guid userId, CancellationToken ct = default)
    {
        var period = await _payPeriodRepository.GetByIdAsync(payPeriodId);
        if (period == null)
            throw new ArgumentException($"Pay period '{payPeriodId}' not found.");

        if (period.Status != PayPeriodStatus.Closed)
            throw new InvalidOperationException("Only closed pay periods can be exported to payroll.");

        var summaries = await _summaryRepository.GetByPayPeriodIdAsync(payPeriodId);
        var summaryList = summaries.ToList();

        var export = new StaffAttendancePayrollExport
        {
            Id = Guid.NewGuid(),
            TenantId = period.TenantId,
            PayPeriodId = payPeriodId,
            ExportReference = await GenerateExportReferenceAsync(ct),
            ExportDate = DateTime.UtcNow,
            ExportedById = userId,
            TargetSystem = targetSystem ?? "Payroll",
            TotalRecords = summaryList.Count,
            Status = PayrollExportStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        };

        await _repository.AddAsync(export);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Payroll export {Reference} completed: {Count} records", export.ExportReference, export.TotalRecords);
        return export.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Payroll export '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateExportReferenceAsync(CancellationToken ct)
    {
        var count = await _repository.GetQueryable().CountAsync(ct);
        return $"PAY-EXP-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}";
    }
}

#endregion

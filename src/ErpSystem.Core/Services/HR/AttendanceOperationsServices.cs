using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Models.HR;
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GeofenceZoneService> _logger;

    public GeofenceZoneService(
        IGeofenceZoneRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<GeofenceZoneService> logger)
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

    // A geofence zone owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<GeofenceZone> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Geofence zone '{id}' not found.");
        return entity;
    }

    public async Task<GeofenceZoneDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<GeofenceZoneSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetAllAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<GeofenceZoneSummaryDto>> GetActiveZonesAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveZonesAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<GeofenceZoneSummaryDto>> GetByLocationIdAsync(Guid locationId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByLocationIdAsync(locationId)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<GeofenceZoneSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(z => z.TenantId == tenantId);
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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        ValidateGeofenceDto(dto.Shape, dto.CentreLatitude, dto.CentreLongitude, dto.RadiusMetres, dto.PolygonCoordinatesJson);

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Geofence zone created: {Name}", entity.ZoneName);
        return entity.ToDto();
    }

    public async Task<GeofenceZoneDto> UpdateAsync(UpdateGeofenceZoneDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        ValidateGeofenceDto(dto.Shape, dto.CentreLatitude, dto.CentreLongitude, dto.RadiusMetres, dto.PolygonCoordinatesJson);

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

        // A polygon that does not parse would be stored and then silently skipped at every punch
        // ("Unverified"), so it is refused here with the parser's reason.
        if (shape == GeofenceShape.Polygon && !GeofencePolygon.TryParse(polygonCoordinatesJson, out _, out var error))
            throw new ArgumentException(error ?? "Polygon coordinates are required for a polygon zone.");
    }
}

#endregion

// ============================================================================
// REMOTE WORK REQUEST SERVICE
// ============================================================================

#region Remote Work Request Service

public class RemoteWorkRequestService : IRemoteWorkRequestService
{
    /// <summary>Workflow entity type; must match the catalog entry and the status adapter.</summary>
    private const string EntityType = "RemoteWorkRequest";

    private readonly IRemoteWorkRequestRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoteWorkRequestService> _logger;

    public RemoteWorkRequestService(
        IRemoteWorkRequestRepository repository,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IUnitOfWork unitOfWork,
        ILogger<RemoteWorkRequestService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// The workflow engine identifies approvers by ApplicationUser.Id, while this service is
    /// handed the caller's *Employee* id by <c>AttendanceControllerBase</c>. Keep them
    /// apart: <c>RemoteWorkRequest.ApprovedById</c> is a foreign key to <c>Employee</c>, so
    /// writing a user id into it violates the constraint.
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

    // A remote work request owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<RemoteWorkRequest> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Remote work request '{id}' not found.");
        return entity;
    }

    public async Task<RemoteWorkRequestDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<RemoteWorkRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetByRequestNumberAsync(requestNumber);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeIdAsync(employeeId)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetByStatusAsync(RemoteWorkRequestStatus status, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetPendingApprovalAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RemoteWorkRequestSummaryDto>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByDateRangeAsync(from, to)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<RemoteWorkRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // Navigation names appear on the summary DTO, so they must be loaded.
        var query = _repository.GetQueryable().Include(r => r.Employee).Where(r => r.TenantId == tenantId);
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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, userId);
        entity.RequestNumber = await GenerateRequestNumberAsync(current, ct);
        entity.Status = RemoteWorkRequestStatus.Pending;
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        // RemoteWorkRequestStatus has no Draft member, so the request is submitted for
        // approval the moment it is created rather than on a separate action.
        await StartApprovalWorkflowAsync(entity, ct);

        _logger.LogInformation("Remote work request {Number} created for employee {EmployeeId}", entity.RequestNumber, entity.EmployeeId);
        return entity.ToDto();
    }

    /// <summary>
    /// Starts the approval workflow. A missing or unpublished definition must not stop an
    /// employee raising the request, so failures are logged and the row stays Pending.
    /// </summary>
    private async Task StartApprovalWorkflowAsync(RemoteWorkRequest entity, CancellationToken ct)
    {
        try
        {
            var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, entity.Id);
            if (!workflowResult.ExecutionResult.Success)
            {
                _logger.LogWarning(
                    "Approval workflow did not start for remote work request {Number}: {Message}",
                    entity.RequestNumber,
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
                "Failed to start the approval workflow for remote work request {Number}; it remains pending.",
                entity.RequestNumber);
        }
    }

    public async Task<RemoteWorkRequestDto> UpdateAsync(UpdateRemoteWorkRequestDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        if (entity.Status != RemoteWorkRequestStatus.Pending)
            throw new InvalidOperationException("Only pending remote work requests can be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    /// <summary>
    /// Relays an approval decision to the workflow engine. The engine owns the outcome and
    /// <c>RemoteWorkRequestWorkflowStatusAdapter</c> applies it, so a multi-step definition
    /// leaves the request Pending until its last step passes.
    /// </summary>
    public async Task<RemoteWorkRequestDto> ApproveAsync(Guid requestId, string? comments, Guid userId, CancellationToken ct = default)
        => await ProcessDecisionAsync(requestId, "Approve", comments, userId, ct);

    public async Task<RemoteWorkRequestDto> RejectAsync(Guid requestId, string rejectionReason, Guid userId, CancellationToken ct = default)
        => await ProcessDecisionAsync(requestId, "Reject", rejectionReason, userId, ct);

    private async Task<RemoteWorkRequestDto> ProcessDecisionAsync(
        Guid requestId,
        string action,
        string? comments,
        Guid employeeId,
        CancellationToken ct)
    {
        var entity = await GetOwnedAsync(requestId);

        if (entity.Status != RemoteWorkRequestStatus.Pending)
            throw new InvalidOperationException("Only pending remote work requests can be decided.");

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

        // ApprovedById is an Employee foreign key, so the adapter gets the employee id.
        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, employeeId, isReject ? decisionText : null);

        if (!isReject)
            entity.ApprovalComments = comments;

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = employeeId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Remote work request {Number} decision '{Action}' processed by {UserId}; outcome {Outcome}",
            entity.RequestNumber, action, currentUserId, workflowResult.Outcome);

        return await ReadDetailAsync(entity.Id, ct) ?? entity.ToDto();
    }

    /// <summary>
    /// Re-reads a remote work request with its navigations loaded, for returning after a
    /// write. The tracked instance would report a blank approver name, because the approver
    /// FK is assigned after the entity was loaded and its navigation was never populated.
    /// </summary>
    private async Task<RemoteWorkRequestDto?> ReadDetailAsync(Guid id, CancellationToken ct)
    {
        var entity = await _repository.GetQueryable()
            .AsNoTracking()
            .Include(r => r.Employee)
            .Include(r => r.ApprovedBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        return entity?.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    /// <remarks>
    /// ⚠ <b>This counted LIVE rows against an index that counts deleted ones.</b>
    /// <c>IX_RemoteWorkRequests_TenantId_RequestNumber</c> is unique with no <c>IsDeleted</c> filter while the delete is a soft delete,
    /// so removing one row dropped the count and the next create re-issued a number the removed
    /// row still holds — and every later create in that tenant failed with the generic handler's
    /// 500, naming neither the column nor the constraint.
    ///
    /// <para>Reads the highest number ever issued, deleted rows included, rather than counting: a
    /// count is also wrong the moment the sequence has a gap, and the maximum is the only value
    /// the unique index cares about. Same shape and same fix as the overtime, letter-request, SHE,
    /// movement, grievance, disciplinary, profile-change and travel-request generators.</para>
    ///
    /// <para><c>GetQueryableIncludingDeleted</c>, not <c>GetQueryable().IgnoreQueryFilters()</c> —
    /// a repository that applies its soft-delete filter as a plain <c>Where</c> is not affected by
    /// <c>IgnoreQueryFilters</c>.</para>
    ///
    /// <para>⚠ Still not atomic under concurrent creates. <c>INumberSequenceService</c> is the
    /// platform mechanism for that; moving these onto it needs each sequence seeded from the
    /// table's current maximum so it keeps issuing after the numbers already in the wild.</para>
    /// </remarks>
    private async Task<string> GenerateRequestNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var prefix = $"RWR-{DateTime.UtcNow:yyyyMMdd}-";

        var issued = await _repository
            .GetQueryableIncludingDeleted(r => r.TenantId == tenantId && r.RequestNumber.StartsWith(prefix))
            .Select(r => r.RequestNumber)
            .ToListAsync(ct);

        var highest = issued
            .Select(number => int.TryParse(number[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D5}";
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HolidayCalendarService> _logger;

    public HolidayCalendarService(
        IHolidayCalendarRepository repository,
        IPublicHolidayRepository holidayRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<HolidayCalendarService> logger)
    {
        _repository = repository;
        _holidayRepository = holidayRepository;
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

    // A holiday calendar owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<HolidayCalendar> GetOwnedCalendarAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Holiday calendar '{id}' not found.");
        return entity;
    }

    private async Task<PublicHoliday> GetOwnedHolidayAsync(Guid id)
    {
        var entity = await _holidayRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Public holiday '{id}' not found.");
        return entity;
    }

    public async Task<HolidayCalendarDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedCalendarAsync(id);
        return entity.ToDto();
    }

    public async Task<HolidayCalendarDto?> GetDefaultCalendarAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetDefaultCalendarAsync();
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<HolidayCalendarSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetAllAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<HolidayCalendarSummaryDto>> GetActiveCalendarsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveCalendarsAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<HolidayCalendarDto> GetWithHolidaysAsync(Guid id, int? year = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetWithHolidaysAsync(id, year);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Holiday calendar '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<PagedResult<HolidayCalendarSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // Navigation names appear on the summary DTO, so they must be loaded.
        var query = _repository.GetQueryable().Include(c => c.Country).Include(c => c.PublicHolidays).Where(c => c.TenantId == tenantId);
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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Holiday calendar created: {Name}", entity.CalendarName);
        return entity.ToDto();
    }

    public async Task<HolidayCalendarDto> UpdateAsync(UpdateHolidayCalendarDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedCalendarAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedCalendarAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PublicHolidayDto> AddHolidayAsync(CreatePublicHolidayDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedCalendarAsync(dto.HolidayCalendarId);

        var entity = dto.ToEntity(current, userId);
        await _holidayRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<IEnumerable<PublicHolidaySummaryDto>> GetHolidaysAsync(Guid calendarId, int? year = null, CancellationToken ct = default)
    {
        await GetOwnedCalendarAsync(calendarId);
        var tenantId = GetTenantId();
        var entities = year.HasValue
            ? await _holidayRepository.GetByCalendarAndYearAsync(calendarId, year.Value)
            : await _holidayRepository.GetByCalendarIdAsync(calendarId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<PublicHolidayDto> GetHolidayByIdAsync(Guid holidayId, CancellationToken ct = default)
    {
        var entity = await GetOwnedHolidayAsync(holidayId);
        return entity.ToDto();
    }

    public async Task<PublicHolidayDto> UpdateHolidayAsync(UpdatePublicHolidayDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedHolidayAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _holidayRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHolidayAsync(Guid holidayId, CancellationToken ct = default)
    {
        var entity = await GetOwnedHolidayAsync(holidayId);

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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PayPeriodService> _logger;

    public PayPeriodService(
        IPayPeriodRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PayPeriodService> logger)
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

    // A pay period owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<PayPeriod> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Pay period '{id}' not found.");
        return entity;
    }

    public async Task<PayPeriodDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<PayPeriodDto?> GetCurrentOpenPeriodAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetCurrentOpenPeriodAsync();
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<PayPeriodDto?> GetPeriodCoveringDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetPeriodCoveringDateAsync(date);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<PayPeriodSummaryDto>> GetByStatusAsync(PayPeriodStatus status, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<PayPeriodSummaryDto>> GetByTypeAsync(PayPeriodType type, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByTypeAsync(type)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PayPeriodDto> GetWithSummariesAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetWithSummariesAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Pay period '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<PagedResult<PayPeriodSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(p => p.TenantId == tenantId);
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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var overlapping = await _repository.GetPeriodCoveringDateAsync(dto.StartDate);
        if (overlapping != null && overlapping.TenantId == current)
            throw new InvalidOperationException($"An existing pay period already covers {dto.StartDate}.");

        var entity = dto.ToEntity(current, userId);
        entity.Status = PayPeriodStatus.Open;
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Pay period created: {Name}", entity.PeriodName);
        return entity.ToDto();
    }

    public async Task<PayPeriodDto> UpdateAsync(UpdatePayPeriodDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        if (entity.Status == PayPeriodStatus.Closed)
            throw new InvalidOperationException("A closed pay period cannot be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<PayPeriodDto> CloseAsync(Guid periodId, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(periodId);

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
        var entity = await GetOwnedAsync(id);

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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendancePayrollExportService> _logger;

    public StaffAttendancePayrollExportService(
        IStaffAttendancePayrollExportRepository repository,
        IPayPeriodRepository payPeriodRepository,
        IStaffMonthlyAttendanceSummaryRepository summaryRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendancePayrollExportService> logger)
    {
        _repository = repository;
        _payPeriodRepository = payPeriodRepository;
        _summaryRepository = summaryRepository;
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

    // A payroll export owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffAttendancePayrollExport> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Payroll export '{id}' not found.");
        return entity;
    }

    public async Task<StaffAttendancePayrollExportDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffAttendancePayrollExportDto?> GetByExportReferenceAsync(string exportReference, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetByExportReferenceAsync(exportReference);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffAttendancePayrollExportSummaryDto>> GetByPayPeriodIdAsync(Guid payPeriodId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByPayPeriodIdAsync(payPeriodId)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendancePayrollExportSummaryDto>> GetByStatusAsync(PayrollExportStatus status, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<StaffAttendancePayrollExportDto?> GetLatestSuccessfulExportForPeriodAsync(Guid payPeriodId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetLatestSuccessfulExportForPeriodAsync(payPeriodId);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<PagedResult<StaffAttendancePayrollExportSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // Navigation names appear on the summary DTO, so they must be loaded.
        var query = _repository.GetQueryable().Include(e => e.PayPeriod).Include(e => e.ExportedBy).Where(e => e.TenantId == tenantId);
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
        var tenantId = GetTenantId();
        var period = await _payPeriodRepository.GetByIdAsync(payPeriodId);
        if (period == null || period.TenantId != tenantId)
            throw new ArgumentException($"Pay period '{payPeriodId}' not found.");

        if (period.Status != PayPeriodStatus.Closed)
            throw new InvalidOperationException("Only closed pay periods can be exported to payroll.");

        var summaries = (await _summaryRepository.GetByPayPeriodIdAsync(payPeriodId))
            .Where(s => s.TenantId == tenantId);
        var summaryList = summaries.ToList();

        var export = new StaffAttendancePayrollExport
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PayPeriodId = payPeriodId,
            ExportReference = await GenerateExportReferenceAsync(tenantId, ct),
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
        var entity = await GetOwnedAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateExportReferenceAsync(Guid tenantId, CancellationToken ct)
    {
        var count = await _repository.GetQueryable().CountAsync(e => e.TenantId == tenantId, ct);
        return $"PAY-EXP-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}";
    }
}

#endregion

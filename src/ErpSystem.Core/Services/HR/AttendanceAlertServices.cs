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
// STAFF ATTENDANCE ALERT RULE SERVICE
// ============================================================================

#region Staff Attendance Alert Rule Service

public class StaffAttendanceAlertRuleService : IStaffAttendanceAlertRuleService
{
    private readonly IStaffAttendanceAlertRuleRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendanceAlertRuleService> _logger;

    public StaffAttendanceAlertRuleService(
        IStaffAttendanceAlertRuleRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendanceAlertRuleService> logger)
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

    // An alert rule owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffAttendanceAlertRule> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Alert rule '{id}' not found.");
        return entity;
    }

    public async Task<StaffAttendanceAlertRuleDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffAttendanceAlertRuleSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetAllAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceAlertRuleSummaryDto>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveRulesAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceAlertRuleSummaryDto>> GetByTypeAsync(AttendanceAlertTriggerType alertType, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByTriggerTypeAsync(alertType)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffAttendanceAlertRuleSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // Navigation names appear on the summary DTO, so they must be loaded.
        var query = _repository.GetQueryable().Include(r => r.Alerts).Where(r => r.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.RuleName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffAttendanceAlertRuleSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<StaffAttendanceAlertRuleDto> CreateAsync(CreateStaffAttendanceAlertRuleDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Alert rule created: {Name}", entity.RuleName);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceAlertRuleDto> UpdateAsync(UpdateStaffAttendanceAlertRuleDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> ToggleActiveAsync(Guid ruleId, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(ruleId);

        entity.IsActive = !entity.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Alert rule {Name} toggled to {IsActive}", entity.RuleName, entity.IsActive);
        return entity.IsActive;
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
// STAFF ATTENDANCE ALERT SERVICE
// ============================================================================

#region Staff Attendance Alert Service

public class StaffAttendanceAlertService : IStaffAttendanceAlertService
{
    private readonly IStaffAttendanceAlertRepository _repository;
    private readonly IStaffAttendanceAlertRuleRepository _ruleRepository;
    private readonly IStaffDailyAttendanceRepository _dailyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendanceAlertService> _logger;

    public StaffAttendanceAlertService(
        IStaffAttendanceAlertRepository repository,
        IStaffAttendanceAlertRuleRepository ruleRepository,
        IStaffDailyAttendanceRepository dailyRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendanceAlertService> logger)
    {
        _repository = repository;
        _ruleRepository = ruleRepository;
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

    // An attendance alert owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffAttendanceAlert> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Attendance alert '{id}' not found.");
        return entity;
    }

    public async Task<StaffAttendanceAlertDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeIdAsync(employeeId)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetByRuleIdAsync(Guid ruleId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByAlertRuleIdAsync(ruleId)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetUnacknowledgedAlertsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveUnacknowledgedAlertsAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetUnacknowledgedForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveAlertsForEmployeeAsync(employeeId)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetByAttendanceDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var entities = await _repository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.TriggeredDate >= start && a.TriggeredDate < end)
            .ToListAsync(ct);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetByTypeAsync(AttendanceAlertTriggerType alertType, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.TriggerType == alertType)
            .ToListAsync(ct);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetBySeverityAsync(AttendanceAlertSeverity severity, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetBySeverityAsync(severity)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffAttendanceAlertSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // Navigation names appear on the summary DTO, so they must be loaded.
        var query = _repository.GetQueryable().Include(a => a.AlertRule).Include(a => a.Employee).Where(a => a.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.TriggeredDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffAttendanceAlertSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<StaffAttendanceAlertDto>> EvaluateForAttendanceAsync(Guid dailyAttendanceId, Guid userId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var daily = await _dailyRepository.GetByIdAsync(dailyAttendanceId);
        if (daily == null || daily.TenantId != tenantId)
            throw new ArgumentException($"Daily attendance '{dailyAttendanceId}' not found.");

        var activeRules = (await _ruleRepository.GetActiveRulesAsync()).Where(r => r.TenantId == tenantId);
        var firedAlerts = new List<StaffAttendanceAlert>();

        foreach (var rule in activeRules)
        {
            bool shouldFire = rule.TriggerType switch
            {
                AttendanceAlertTriggerType.ChronicLateness => daily.IsLate && (daily.LateMinutes ?? 0) >= (int)rule.ThresholdValue,
                AttendanceAlertTriggerType.ExcessiveEarlyDeparture => daily.IsEarlyDeparture,
                AttendanceAlertTriggerType.ConsecutiveAbsences => daily.Status == StaffAttendanceStatus.Absent,
                AttendanceAlertTriggerType.UnauthorisedAbsence => daily.Status == StaffAttendanceStatus.Absent,
                AttendanceAlertTriggerType.MissingPunch => daily.ActualCheckInTime == null || daily.ActualCheckOutTime == null,
                AttendanceAlertTriggerType.OvertimeThresholdReached => (daily.OvertimeHours ?? 0) >= rule.ThresholdValue,
                _ => false
            };

            if (!shouldFire)
                continue;

            var alert = new StaffAttendanceAlert
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AlertRuleId = rule.Id,
                EmployeeId = daily.EmployeeId,
                TriggerType = rule.TriggerType,
                Severity = rule.Severity,
                TriggeredDate = DateTime.UtcNow,
                TriggerDescription = $"Rule '{rule.RuleName}' triggered for attendance on {daily.AttendanceDate}.",
                Status = AttendanceAlertStatus.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId.ToString()
            };

            await _repository.AddAsync(alert);
            firedAlerts.Add(alert);
        }

        if (firedAlerts.Count > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Evaluated attendance {DailyId}: {Count} alert(s) fired", dailyAttendanceId, firedAlerts.Count);
        return firedAlerts.Select(a => a.ToDto()).ToList();
    }

    public async Task<StaffAttendanceAlertDto> AcknowledgeAsync(Guid alertId, string? comments, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(alertId);

        if (entity.Status == AttendanceAlertStatus.Acknowledged)
            throw new InvalidOperationException("This alert has already been acknowledged.");

        entity.Status = AttendanceAlertStatus.Acknowledged;
        entity.AcknowledgedById = userId;
        entity.AcknowledgedDate = DateTime.UtcNow;
        entity.AcknowledgementNotes = comments;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Alert {Id} acknowledged by {UserId}", alertId, userId);
        return entity.ToDto();
    }

    public async Task<int> BulkAcknowledgeAsync(IEnumerable<Guid> alertIds, string? comments, Guid userId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        int count = 0;
        foreach (var id in alertIds)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null || entity.TenantId != tenantId || entity.Status == AttendanceAlertStatus.Acknowledged)
                continue;

            entity.Status = AttendanceAlertStatus.Acknowledged;
            entity.AcknowledgedById = userId;
            entity.AcknowledgedDate = DateTime.UtcNow;
            entity.AcknowledgementNotes = comments;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userId.ToString();

            await _repository.UpdateAsync(entity);
            count++;
        }

        if (count > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("{Count} alerts bulk-acknowledged by {UserId}", count, userId);
        return count;
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

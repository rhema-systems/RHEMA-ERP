using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// Administration of the goal-risk thresholds.
///
/// The evaluation pipeline reads them through <c>IGoalRiskSettingsProvider</c>, which falls back
/// to <see cref="GoalRiskSettingDefaults"/> when the tenant has no row. This service is the other
/// half: it is what puts a row there.
///
/// One active row per tenant is the invariant the provider relies on (it takes the first active
/// match), so a save deactivates every other active row rather than trusting there is only one.
/// </summary>
public sealed class GoalRiskSettingsService : IGoalRiskSettingsService
{
    private readonly IGenericRepository<GoalRiskSetting> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<GoalRiskSettingsService> _logger;

    public GoalRiskSettingsService(
        IGenericRepository<GoalRiskSetting> repository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<GoalRiskSettingsService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
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

    public async Task<GoalRiskSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var setting = await _repository.GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return setting is null ? Defaults() : ToDto(setting);
    }

    public async Task<GoalRiskSettingsDto> SaveAsync(
        UpdateGoalRiskSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.DaysRemainingThreshold < 1)
            throw new ArgumentException("The days-remaining threshold must be at least 1 day.");
        if (dto.MinimumProgressPercent is < 0 or > 100)
            throw new ArgumentException("The minimum progress percent must be between 0 and 100.");
        if (dto.ExpectedProgressTolerancePercent is < 0 or > 100)
            throw new ArgumentException("The expected-progress tolerance must be between 0 and 100.");

        var tenantId = GetTenantId();

        var active = await _repository.GetQueryable()
            .Where(s => s.TenantId == tenantId && s.IsActive && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        var target = active.FirstOrDefault();

        // Any extra active rows would make which thresholds apply a matter of query order.
        foreach (var stale in active.Skip(1))
        {
            stale.IsActive = false;
            await _repository.UpdateAsync(stale);
        }

        if (target is null)
        {
            target = new GoalRiskSetting { TenantId = tenantId, IsActive = true };
            Apply(dto, target);
            await _repository.AddAsync(target);
        }
        else
        {
            Apply(dto, target);
            await _repository.UpdateAsync(target);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Goal risk thresholds saved for tenant {TenantId}: {Days}d / {Min}% / {Tol}%",
            tenantId, target.DaysRemainingThreshold, target.MinimumProgressPercent,
            target.ExpectedProgressTolerancePercent);

        return ToDto(target);
    }

    public async Task<GoalRiskSettingsDto> ResetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var active = await _repository.GetQueryable()
            .Where(s => s.TenantId == tenantId && s.IsActive && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        // Deactivated rather than deleted — the provider only ever looks at active rows, and
        // keeping the history means an earlier threshold set can be read back if it is queried.
        foreach (var setting in active)
        {
            setting.IsActive = false;
            await _repository.UpdateAsync(setting);
        }

        if (active.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Goal risk thresholds reset to defaults for tenant {TenantId}", tenantId);
        }

        return Defaults();
    }

    private static void Apply(UpdateGoalRiskSettingsDto dto, GoalRiskSetting entity)
    {
        entity.DaysRemainingThreshold = dto.DaysRemainingThreshold;
        entity.MinimumProgressPercent = dto.MinimumProgressPercent;
        entity.ExpectedProgressTolerancePercent = dto.ExpectedProgressTolerancePercent;
        entity.IsActive = true;
    }

    private static GoalRiskSettingsDto Defaults() => new()
    {
        Id = null,
        IsConfigured = false,
        DaysRemainingThreshold = GoalRiskSettingDefaults.DaysRemainingThreshold,
        MinimumProgressPercent = GoalRiskSettingDefaults.MinimumProgressPercent,
        ExpectedProgressTolerancePercent = GoalRiskSettingDefaults.ExpectedProgressTolerancePercent,
    };

    private static GoalRiskSettingsDto ToDto(GoalRiskSetting entity) => new()
    {
        Id = entity.Id,
        IsConfigured = true,
        DaysRemainingThreshold = entity.DaysRemainingThreshold,
        MinimumProgressPercent = entity.MinimumProgressPercent,
        ExpectedProgressTolerancePercent = entity.ExpectedProgressTolerancePercent,
        UpdatedAt = entity.UpdatedAt,
        UpdatedBy = entity.UpdatedBy,
    };
}

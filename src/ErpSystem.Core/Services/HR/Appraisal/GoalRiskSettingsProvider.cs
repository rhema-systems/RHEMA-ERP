using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

// ─────────────────────────────────────────────────────────────────────────────
//  GoalRiskSettingsProvider
//
//  Single-responsibility: load the active GoalRiskSetting from the database.
//
//  ── Why not cache? ────────────────────────────────────────────────────────
//  Caching is intentionally omitted from this implementation.  Administrators
//  may change thresholds at any time; caching here would silently delay the
//  effect.  If caching is needed in the future, apply it via a decorator
//  (e.g., CachedGoalRiskSettingsProvider) rather than baking it in here —
//  this keeps the core implementation testable without cache invalidation logic.
//
//  ── AsNoTracking ──────────────────────────────────────────────────────────
//  This is a read-only path.  AsNoTracking() is always applied: the loaded
//  GoalRiskSetting is passed to IGoalRiskEvaluator and never mutated.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Production implementation of <see cref="IGoalRiskSettingsProvider"/>.
/// Queries the <c>GoalRiskSetting</c> table and returns the single active record.
/// </summary>
public sealed class GoalRiskSettingsProvider : IGoalRiskSettingsProvider
{
    private readonly IGenericRepository<GoalRiskSetting>    _repo;
    private readonly ICurrentUserProvider                   _currentUserProvider;
    private readonly ILogger<GoalRiskSettingsProvider>      _logger;

    public GoalRiskSettingsProvider(
        IGenericRepository<GoalRiskSetting> repo,
        ICurrentUserProvider                currentUserProvider,
        ILogger<GoalRiskSettingsProvider>   logger)
    {
        _repo   = repo;
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

    /// <inheritdoc />
    public async Task<GoalRiskSetting> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var setting = await _repo
            .GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (setting is null)
        {
            // Nothing seeds this table, so a tenant that has never opened the goal-risk settings
            // screen has no row. Throwing here took the whole manager workspace with it — five of
            // the Team Goals tabs and the org-wide at-risk report all load these settings, so an
            // unconfigured tenant got a 500 on every one of them. Fall back to the thresholds the
            // entity documents as defaults instead; HR can tune them from Administration → HR →
            // Performance → Goal Risk Thresholds, which is what persists a real row.
            _logger.LogWarning(
                "GoalRiskSettingsProvider: no active GoalRiskSetting for tenant {TenantId}; " +
                "falling back to the documented defaults ({Days}d / {Min}% / {Tol}%).",
                tenantId,
                GoalRiskSettingDefaults.DaysRemainingThreshold,
                GoalRiskSettingDefaults.MinimumProgressPercent,
                GoalRiskSettingDefaults.ExpectedProgressTolerancePercent);

            return GoalRiskSettingDefaults.Create(tenantId);
        }

        _logger.LogDebug(
            "GoalRiskSettingsProvider: loaded active settings " +
            "(DaysRemaining={Days}, MinProgress={Min}%, Tolerance={Tol}%)",
            setting.DaysRemainingThreshold,
            setting.MinimumProgressPercent,
            setting.ExpectedProgressTolerancePercent);

        return setting;
    }
}

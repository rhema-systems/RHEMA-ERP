using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Loads the current tenant's <see cref="CompanyHrPolicySettings"/>.
///
/// Implementations must:
///   • Query the database using AsNoTracking (read-only path).
///   • Return the single per-tenant record.
///   • Return a transient defaults instance (never throw / never null) when no row
///     exists yet, so a freshly-provisioned tenant keeps working before HR opens
///     the settings page.
///
/// Retirement / service-years math lives in the pure <see cref="Services.HR.HrPolicyCalculations"/>
/// helpers — load settings once, then compute per employee without re-hitting the DB.
/// </summary>
public interface ICompanyHrPolicyProvider
{
    /// <summary>
    /// Returns the tenant's settings, or a coded-defaults instance when none is persisted.
    /// </summary>
    Task<CompanyHrPolicySettings> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the named tenant's settings, or a coded-defaults instance when none is persisted.
    /// </summary>
    /// <remarks>
    /// ⚠ For callers with no authenticated tenant to read from — the scheduled reminder sweeps,
    /// which loop over every tenant with no HTTP context behind them. <see cref="GetAsync"/> resolves
    /// the tenant from the current user and throws when there is none, so a sweep that reached it
    /// died on every scheduled run while working perfectly from the run-now button.
    /// </remarks>
    Task<CompanyHrPolicySettings> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

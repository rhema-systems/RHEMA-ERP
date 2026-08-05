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
}

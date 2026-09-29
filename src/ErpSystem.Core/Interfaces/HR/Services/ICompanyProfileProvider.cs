using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Loads the current tenant's <see cref="CompanyProfile"/> (the legal-employer master record used on
/// generated documents and email letterheads).
///
/// Implementations must:
///   • Query read-only (AsNoTracking) and return the single per-tenant record.
///   • Never throw / never return null — when no row exists yet, return a transient instance whose
///     values are resolved from the <c>Tenant</c> record and configuration, so a freshly-provisioned
///     tenant still prints sensible company details before HR opens the profile page.
/// </summary>
public interface ICompanyProfileProvider
{
    Task<CompanyProfile> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The named tenant's profile — for a sender with no signed-in user to say whose it is (a background
    /// job, an anonymous careers request), which <see cref="GetAsync"/> cannot serve: it throws there.
    /// </summary>
    Task<CompanyProfile> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

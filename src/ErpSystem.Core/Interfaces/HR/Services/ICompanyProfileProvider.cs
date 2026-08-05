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
}

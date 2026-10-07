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

    /// <summary>
    /// The logo a letter or email carries (company-schedule final closure lane 4c, F-55): the uploaded logo in force,
    /// embedded as a data URI like the seal and the signature; else the tenant's own logo URL (<c>Tenant.LogoUrl</c>),
    /// whether or not a profile row exists; else null. The free-text <c>CompanyProfile.LogoUrl</c> is retired and not read.
    /// </summary>
    /// <remarks>
    /// Separate from the profile read on purpose: every templated email reads the profile for the company's name, and
    /// that must not load an image each time.
    /// </remarks>
    Task<string?> GetLogoAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

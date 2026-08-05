using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Production implementation of <see cref="ICompanyHrPolicyProvider"/>. Reads the
/// current tenant's <c>CompanyHrPolicySettings</c> with an explicit TenantId filter.
/// Read-only path: <c>AsNoTracking</c> throughout.
///
/// Unlike the goal-risk provider this <b>never throws</b> — a missing settings row is a
/// valid state for a new tenant, so we return a transient defaults instance whose
/// property initializers mirror the seeded defaults.
/// </summary>
public sealed class CompanyHrPolicyProvider : ICompanyHrPolicyProvider
{
    private readonly IGenericRepository<CompanyHrPolicySettings> _repo;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<CompanyHrPolicyProvider> _logger;

    public CompanyHrPolicyProvider(
        IGenericRepository<CompanyHrPolicySettings> repo,
        ICurrentUserProvider currentUser,
        ILogger<CompanyHrPolicyProvider> logger)
    {
        _repo = repo;
        _currentUser = currentUser;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this provider scopes reads to
    // the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <inheritdoc />
    public async Task<CompanyHrPolicySettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var settings = await _repo
            .GetQueryable()
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            _logger.LogDebug(
                "CompanyHrPolicyProvider: no settings row for tenant {TenantId}; using coded defaults.",
                tenantId);
            return new CompanyHrPolicySettings { TenantId = tenantId };
        }

        return settings;
    }
}

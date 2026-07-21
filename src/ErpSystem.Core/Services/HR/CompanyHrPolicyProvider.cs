using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Production implementation of <see cref="ICompanyHrPolicyProvider"/>. Reads the
/// current tenant's <c>CompanyHrPolicySettings</c> (tenant filtering is applied by the
/// global query filter). Read-only path: <c>AsNoTracking</c> throughout.
///
/// Unlike the goal-risk provider this <b>never throws</b> — a missing settings row is a
/// valid state for a new tenant, so we return a transient defaults instance whose
/// property initializers mirror the seeded defaults.
/// </summary>
public sealed class CompanyHrPolicyProvider : ICompanyHrPolicyProvider
{
    private readonly IGenericRepository<CompanyHrPolicySettings> _repo;
    private readonly ILogger<CompanyHrPolicyProvider> _logger;

    public CompanyHrPolicyProvider(
        IGenericRepository<CompanyHrPolicySettings> repo,
        ILogger<CompanyHrPolicyProvider> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CompanyHrPolicySettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _repo
            .GetQueryable()
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            _logger.LogDebug(
                "CompanyHrPolicyProvider: no settings row for current tenant; using coded defaults.");
            return new CompanyHrPolicySettings();
        }

        return settings;
    }
}

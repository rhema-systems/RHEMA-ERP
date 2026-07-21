using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Production implementation of <see cref="ICompanyProfileProvider"/>. Returns the current tenant's
/// saved <see cref="CompanyProfile"/>, or — when none is persisted yet — a transient instance resolved
/// from the <see cref="Tenant"/> record and configuration (<c>Company:*</c> keys), so documents and
/// emails still render sensible company details before HR fills in the profile. Never throws / null.
/// </summary>
public sealed class CompanyProfileProvider : ICompanyProfileProvider
{
    private readonly IGenericRepository<CompanyProfile> _repo;
    private readonly IGenericRepository<Tenant> _tenantRepo;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CompanyProfileProvider> _logger;

    public CompanyProfileProvider(
        IGenericRepository<CompanyProfile> repo,
        IGenericRepository<Tenant> tenantRepo,
        ICurrentUserProvider currentUser,
        IConfiguration configuration,
        ILogger<CompanyProfileProvider> logger)
    {
        _repo = repo;
        _tenantRepo = tenantRepo;
        _currentUser = currentUser;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CompanyProfile> GetAsync(CancellationToken cancellationToken = default)
    {
        var profile = await _repo
            .GetQueryable()
            .Include(p => p.Country)
            .Include(p => p.CountryOfIncorporation)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is not null)
            return profile;

        _logger.LogDebug(
            "CompanyProfileProvider: no profile row for current tenant; resolving defaults from Tenant + config.");

        // The Tenant table is not tenant-filtered (it *is* the tenant); fetch the current one directly.
        var tenant = await _tenantRepo
            .GetQueryable()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId, cancellationToken);

        return new CompanyProfile
        {
            TenantId              = _currentUser.TenantId,
            LegalName             = tenant?.Name
                                    ?? _configuration["Company:Name"]
                                    ?? _configuration["ApplicationName"]
                                    ?? "Our Company",
            RegisteredAddress     = tenant?.Address
                                    ?? _configuration["Company:RegisteredAddress"]
                                    ?? _configuration["Company:Address"],
            PhonePrimary          = tenant?.ContactPhone,
            HrEmail               = tenant?.ContactEmail ?? _configuration["Company:HrEmail"],
            Website               = tenant?.Domain,
            LogoUrl               = tenant?.LogoUrl,
            DefaultSignatoryName  = _configuration["Company:HrSignatoryName"],
            DefaultSignatoryTitle = _configuration["Company:HrSignatoryTitle"] ?? "Head of Human Resources",
            OfferAcceptanceInstructions = _configuration["Company:OfferAcceptanceInstructions"],
        };
    }
}

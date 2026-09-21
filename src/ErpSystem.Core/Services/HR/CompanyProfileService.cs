using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Manages the tenant's single <see cref="CompanyProfile"/> record. Read returns the saved row or
/// Tenant/config-resolved defaults (via <see cref="ICompanyProfileProvider"/>); update performs a
/// get-or-create upsert scoped to the current tenant.
/// </summary>
public class CompanyProfileService : ICompanyProfileService
{
    private readonly IGenericRepository<CompanyProfile> _repository;
    private readonly ICompanyProfileProvider _provider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<CompanyProfileService> _logger;

    // Shared reference data. Resolves the registered address's area into the Region and City text
    // the profile prints, so the two cannot disagree.
    private readonly ErpSystem.Core.Services.Reference.IGeographyService _geography;

    public CompanyProfileService(
        IGenericRepository<CompanyProfile> repository,
        ICompanyProfileProvider provider,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ErpSystem.Core.Services.Reference.IGeographyService geography,
        ILogger<CompanyProfileService> logger)
    {
        _repository = repository;
        _provider = provider;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _geography = geography;
        _logger = logger;
    }

    /// <summary>
    /// Rewrites <c>Region</c> and <c>City</c> from the profile's administrative area. A null area
    /// leaves both exactly as they were.
    /// </summary>
    /// <remarks>
    /// The rule itself lives in <see cref="ErpSystem.Core.Services.Reference.GeoAddressSnapshot"/> since round 2 lane D2 — there were
    /// four private copies of it and they had already drifted.
    /// </remarks>
    private Task ApplyGeoAreaSnapshotAsync(CompanyProfile entity, CancellationToken ct)
        => ErpSystem.Core.Services.Reference.GeoAddressSnapshot.ApplyAsync(
            _geography, _logger, entity.GeoAreaId,
            r => entity.Region = r, c => entity.City = c,
            "company profile", entity.Id, ct);

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    public async Task<CompanyProfileDto> GetAsync(CancellationToken cancellationToken = default)
    {
        // Provider returns the saved row (tenant-filtered) or Tenant/config-resolved defaults.
        var profile = await _provider.GetAsync(cancellationToken);
        return profile.ToDto();
    }

    public async Task<CompanyProfileDto> UpdateAsync(UpdateCompanyProfileDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.LegalName))
            throw new InvalidOperationException("Legal name is required.");

        var tenantId = GetTenantId();
        var entity = await _repository.GetQueryable()
            .Where(p => !p.IsDeleted && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new CompanyProfile { TenantId = tenantId };
            entity.ApplyUpdate(dto);
            await ApplyGeoAreaSnapshotAsync(entity, cancellationToken); // ⚠ after Apply — the tree wins
            entity.StampCreated(_currentUser);
            await _repository.AddAsync(entity);
            _logger.LogInformation("Company profile created for tenant {TenantId}", entity.TenantId);
        }
        else
        {
            entity.ApplyUpdate(dto);
            await ApplyGeoAreaSnapshotAsync(entity, cancellationToken); // ⚠ after Apply — the tree wins
            entity.StampUpdated(_currentUser);
            await _repository.UpdateAsync(entity);
            _logger.LogInformation("Company profile updated for tenant {TenantId}", entity.TenantId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read through the provider rather than mapping the entity we just wrote. `CompanyProfileDto`
        // carries `CountryName` and `CountryOfIncorporationName`, both read off navigations — and on
        // this path those navigations are either absent (a first save creates the entity in memory) or
        // stale (a save that CHANGES the country still holds the old one). Either way the caller gets a
        // country id with a blank or wrong name beside it, which is exactly the shape the screen renders.
        return await GetAsync(cancellationToken);
    }
}

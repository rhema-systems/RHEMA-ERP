using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Reference;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Who points at the administrative-geography tree.
//
//  ⚠ EVERY entity that gains a GeoAreaId needs a probe here, and registering it in
//  ServiceCollectionExtensions is not optional. Geography deletes are SOFT, so the foreign key is
//  never consulted — without a probe an area someone still uses deletes cleanly, vanishes from
//  every read, and takes the address with it while leaving a dangling id. That is not a
//  hypothetical: it is how a seeded community and an employee's address were lost on 2026-09-03.
//
//  ⚠ A GeoAreaId belongs on a record whose address is a property of a PLACE — a site, the company's
//  seat, a hospital. It does NOT belong on a record that merely mentions a city: a hotel booking, a
//  per-diem rate, a travel alert. Those are usually foreign cities the scheme cannot hold, and
//  per-diem-by-area is a rate-model change, not an address. See docs/GEOGRAPHY-REFERENCE-DESIGN.md.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The company's own sites — offices, depots, clock-in stations.</summary>
public sealed class LocationGeoAreaConsumer : IGeoAreaConsumer
{
    private readonly IUnitOfWork _unitOfWork;
    public LocationGeoAreaConsumer(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string ResourceName => "sites";
    public string ResourceNameSingular => "site";

    public Task<int> CountUsagesAsync(Guid geoAreaId, Guid tenantId, CancellationToken ct = default)
        => _unitOfWork.Repository<Location>().GetQueryable()
            .CountAsync(l => l.TenantId == tenantId && !l.IsDeleted && l.GeoAreaId == geoAreaId, ct);
}

/// <summary>The company's own registered address. At most one row, but it still counts.</summary>
public sealed class CompanyProfileGeoAreaConsumer : IGeoAreaConsumer
{
    private readonly IUnitOfWork _unitOfWork;
    public CompanyProfileGeoAreaConsumer(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string ResourceName => "company profiles";
    public string ResourceNameSingular => "company profile";

    public Task<int> CountUsagesAsync(Guid geoAreaId, Guid tenantId, CancellationToken ct = default)
        => _unitOfWork.Repository<CompanyProfile>().GetQueryable()
            .CountAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.GeoAreaId == geoAreaId, ct);
}

/// <summary>Hospitals and clinics on the medical register.</summary>
public sealed class HealthcareFacilityGeoAreaConsumer : IGeoAreaConsumer
{
    private readonly IUnitOfWork _unitOfWork;
    public HealthcareFacilityGeoAreaConsumer(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string ResourceName => "healthcare facilities";
    public string ResourceNameSingular => "healthcare facility";

    public Task<int> CountUsagesAsync(Guid geoAreaId, Guid tenantId, CancellationToken ct = default)
        => _unitOfWork.Repository<HealthcareFacility>().GetQueryable()
            .CountAsync(f => f.TenantId == tenantId && !f.IsDeleted && f.GeoAreaId == geoAreaId, ct);
}

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

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Round 2, lane D2 — the employee's address-bearing sub-records (register rows E-3, E-6).
//
//  ⚠ Four probes for four columns, each registered in ServiceCollectionExtensions. The employee's
//  OWN address already had one; these are the addresses hanging off them — a second home, a next of
//  kin, a guarantor, a previous employer — and without a probe each they would be exactly as
//  exposed as the seeded community that vanished on 2026-09-03.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>An employee's further addresses — a postal address, a temporary one, a second home.</summary>
public sealed class EmployeeContactGeoAreaConsumer : IGeoAreaConsumer
{
    private readonly IUnitOfWork _unitOfWork;
    public EmployeeContactGeoAreaConsumer(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string ResourceName => "employee addresses";
    public string ResourceNameSingular => "employee address";

    public Task<int> CountUsagesAsync(Guid geoAreaId, Guid tenantId, CancellationToken ct = default)
        => _unitOfWork.Repository<EmployeeContact>().GetQueryable()
            .CountAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.GeoAreaId == geoAreaId, ct);
}

/// <summary>Where an employee's next of kin or emergency contact lives.</summary>
public sealed class EmployeeEmergencyContactGeoAreaConsumer : IGeoAreaConsumer
{
    private readonly IUnitOfWork _unitOfWork;
    public EmployeeEmergencyContactGeoAreaConsumer(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string ResourceName => "emergency contacts";
    public string ResourceNameSingular => "emergency contact";

    public Task<int> CountUsagesAsync(Guid geoAreaId, Guid tenantId, CancellationToken ct = default)
        => _unitOfWork.Repository<EmployeeEmergencyContact>().GetQueryable()
            .CountAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.GeoAreaId == geoAreaId, ct);
}

/// <summary>Where a guarantor lives — the address a surety is actually served at.</summary>
public sealed class EmployeeGuarantorGeoAreaConsumer : IGeoAreaConsumer
{
    private readonly IUnitOfWork _unitOfWork;
    public EmployeeGuarantorGeoAreaConsumer(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string ResourceName => "guarantors";
    public string ResourceNameSingular => "guarantor";

    public Task<int> CountUsagesAsync(Guid geoAreaId, Guid tenantId, CancellationToken ct = default)
        => _unitOfWork.Repository<EmployeeGuarantor>().GetQueryable()
            .CountAsync(g => g.TenantId == tenantId && !g.IsDeleted && g.GeoAreaId == geoAreaId, ct);
}

/// <summary>
/// Where a previous employer is.
/// </summary>
/// <remarks>
/// ⚠ This one is a PLACE, which is why it earns a <c>GeoAreaId</c> at all: a company sits somewhere
/// and someone may have to write to it for a reference. It is not the "merely mentions a city" case
/// the file header warns about.
/// </remarks>
public sealed class EmployeeWorkHistoryGeoAreaConsumer : IGeoAreaConsumer
{
    private readonly IUnitOfWork _unitOfWork;
    public EmployeeWorkHistoryGeoAreaConsumer(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string ResourceName => "previous employers";
    public string ResourceNameSingular => "previous employer";

    public Task<int> CountUsagesAsync(Guid geoAreaId, Guid tenantId, CancellationToken ct = default)
        => _unitOfWork.Repository<EmployeeWorkHistory>().GetQueryable()
            .CountAsync(w => w.TenantId == tenantId && !w.IsDeleted && w.GeoAreaId == geoAreaId, ct);
}

using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Reference;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// HR's answer to "does anyone live here?" — the employee register's stake in the shared
/// administrative-geography tree.
/// </summary>
/// <remarks>
/// Registered as an <see cref="IGeoAreaConsumer"/> so the geography service can refuse to delete an
/// area without HR having to be known to it. HR is the first consumer; phase 4 adds the same shape
/// for Location, CompanyProfile, the medical facilities and travel destinations.
/// </remarks>
public sealed class EmployeeGeoAreaConsumer : IGeoAreaConsumer
{
    private readonly IUnitOfWork _unitOfWork;

    public EmployeeGeoAreaConsumer(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string ResourceName => "employees";
    public string ResourceNameSingular => "employee";

    public Task<int> CountUsagesAsync(Guid geoAreaId, Guid tenantId, CancellationToken ct = default)
        => _unitOfWork.Repository<Employee>().GetQueryable()
            .CountAsync(e => e.TenantId == tenantId && !e.IsDeleted && e.GeoAreaId == geoAreaId, ct);
}

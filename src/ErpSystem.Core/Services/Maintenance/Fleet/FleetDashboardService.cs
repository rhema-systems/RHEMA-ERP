using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetDashboardService : IFleetDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetDashboardService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<FleetDashboardSummaryDto> GetSummaryAsync(Guid? vehicleAssetId = null, DateTime? asAtUtc = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var now = asAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var vehiclesQ = _unitOfWork.Repository<MaintenanceAsset>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted)
            .Include(a => a.AssetCategory)
            .Where(a => a.AssetCategory != null && a.AssetCategory.AssetType == "Vehicle");

        var activeVehicles = await vehiclesQ.CountAsync();

        var tripsQ = _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted);
        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            tripsQ = tripsQ.Where(t => t.VehicleAssetId == vehicleAssetId.Value);

        var tripsThisMonth = await tripsQ.CountAsync(t => t.CreatedAt >= monthStart);

        var defectsQ = _unitOfWork.Repository<FleetDefect>()
            .GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted);
        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            defectsQ = defectsQ.Where(d => d.VehicleAssetId == vehicleAssetId.Value);

        var openDefects = await defectsQ.CountAsync(d => d.Status == "Open" || d.Status == "InProgress");

        var today = now.Date;
        var settings = await _unitOfWork.Repository<MaintenanceSettings>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);
        var dueSoonDays = settings?.FleetComplianceDueSoonDays ?? 7;
        var dueSoonCutoff = today.AddDays(Math.Max(0, dueSoonDays));

        var complianceQ = _unitOfWork.Repository<FleetComplianceItem>()
            .GetQueryable(c => c.TenantId == tenantId && !c.IsDeleted && c.IsCritical);
        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            complianceQ = complianceQ.Where(c => c.VehicleAssetId == vehicleAssetId.Value);

        var dueSoon = await complianceQ.CountAsync(c => c.ExpiryDate.Date >= today && c.ExpiryDate.Date <= dueSoonCutoff);
        var overdue = await complianceQ.CountAsync(c => c.ExpiryDate.Date < today);

        var fuelQ = _unitOfWork.Repository<FleetFuelTransaction>()
            .GetQueryable(f => f.TenantId == tenantId && !f.IsDeleted);
        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            fuelQ = fuelQ.Where(f => f.VehicleAssetId == vehicleAssetId.Value);

        var fuelCostThisMonth = await fuelQ
            .Where(f => f.FuelledAt >= monthStart)
            .SumAsync(f => (decimal?)(f.TotalCost ?? 0)) ?? 0m;

        var externalRepairCostThisMonth = await _unitOfWork.Repository<FleetCostEntry>()
            .GetQueryable(c => c.TenantId == tenantId && !c.IsDeleted && c.CostType == "ExternalRepair" && c.CostDateUtc >= monthStart)
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;

        // Simple KPIs: average km per liter and average fuel cost per km over completed trips this month.
        decimal? avgKmPerLiter = null;
        decimal? avgFuelCostPerKm = null;

        var completedTrips = await tripsQ
            .Where(t => t.Status == FleetTripStatuses.Completed && t.CompletedAt.HasValue && t.CompletedAt.Value >= monthStart)
            .Select(t => new { t.StartMileage, t.EndMileage, t.VehicleAssetId })
            .ToListAsync();

        var km = completedTrips
            .Where(t => t.StartMileage.HasValue && t.EndMileage.HasValue && t.EndMileage.Value >= t.StartMileage.Value)
            .Sum(t => (decimal)(t.EndMileage!.Value - t.StartMileage!.Value));

        var liters = await fuelQ
            .Where(f => f.FuelledAt >= monthStart)
            .SumAsync(f => (decimal?)f.Quantity) ?? 0m;

        if (liters > 0 && km > 0) avgKmPerLiter = km / liters;
        if (fuelCostThisMonth > 0 && km > 0) avgFuelCostPerKm = fuelCostThisMonth / km;

        return new FleetDashboardSummaryDto
        {
            ActiveVehicles = activeVehicles,
            TripsThisMonth = tripsThisMonth,
            OpenDefects = openDefects,
            ComplianceDueSoon = dueSoon,
            ComplianceOverdue = overdue,
            FuelCostThisMonth = fuelCostThisMonth,
            ExternalRepairCostThisMonth = externalRepairCostThisMonth,
            AverageFuelCostPerKm = avgFuelCostPerKm,
            AverageKmPerLiter = avgKmPerLiter
        };
    }
}


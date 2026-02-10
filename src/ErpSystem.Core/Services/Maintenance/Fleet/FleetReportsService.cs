using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetReportsService : IFleetReportsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetReportsService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<FleetCostSummaryDto> GetCostSummaryAsync(DateTime? fromUtc = null, DateTime? toUtc = null, int top = 10, Guid? vehicleAssetId = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;

        var from = (fromUtc ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)).ToUniversalTime();
        var to = (toUtc ?? now).ToUniversalTime();
        if (to < from) (from, to) = (to, from);

        if (top <= 0) top = 10;
        if (top > 200) top = 200;

        var costsQ = _unitOfWork.Repository<FleetCostEntry>()
            .GetQueryable(c => c.TenantId == tenantId && !c.IsDeleted && c.CostDateUtc >= from && c.CostDateUtc <= to);

        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            costsQ = costsQ.Where(c => c.VehicleAssetId == vehicleAssetId.Value);

        var utilQ = _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t =>
                t.TenantId == tenantId &&
                !t.IsDeleted &&
                t.Status == FleetTripStatuses.Completed &&
                t.CompletedAt.HasValue &&
                t.CompletedAt.Value >= from &&
                t.CompletedAt.Value <= to);

        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            utilQ = utilQ.Where(t => t.VehicleAssetId == vehicleAssetId.Value);

        var utilRows = await utilQ
            .GroupBy(t => t.VehicleAssetId)
            .Select(g => new
            {
                VehicleAssetId = g.Key,
                CompletedTrips = g.Count(),
                TotalKm = g.Sum(t =>
                    t.StartMileage.HasValue &&
                    t.EndMileage.HasValue &&
                    t.EndMileage.Value >= t.StartMileage.Value
                        ? (t.EndMileage.Value - t.StartMileage.Value)
                        : 0d),
                TotalHours = g.Sum(t =>
                    t.StartOperatingHours.HasValue &&
                    t.EndOperatingHours.HasValue &&
                    t.EndOperatingHours.Value >= t.StartOperatingHours.Value
                        ? (t.EndOperatingHours.Value - t.StartOperatingHours.Value)
                        : 0d)
            })
            .ToListAsync();

        var utilByVehicle = utilRows.ToDictionary(
            x => x.VehicleAssetId,
            x => new
            {
                x.CompletedTrips,
                TotalKm = (decimal)Math.Round(x.TotalKm, 2, MidpointRounding.AwayFromZero),
                TotalHours = (decimal)Math.Round(x.TotalHours, 2, MidpointRounding.AwayFromZero)
            });

        var assetsQ = _unitOfWork.Repository<MaintenanceAsset>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted);

        var joined = from c in costsQ
                     join a in assetsQ on c.VehicleAssetId equals a.Id
                     select new { c.VehicleAssetId, VehicleName = a.Name, c.CostType, c.Amount };

        var rowsRaw = await joined
            .GroupBy(x => new { x.VehicleAssetId, x.VehicleName })
            .Select(g => new
            {
                g.Key.VehicleAssetId,
                g.Key.VehicleName,
                EntryCount = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
                FuelAmount = g.Where(x => x.CostType == "Fuel").Sum(x => (decimal?)x.Amount) ?? 0m,
                ExternalRepairAmount = g.Where(x => x.CostType == "ExternalRepair").Sum(x => (decimal?)x.Amount) ?? 0m,
                InternalMaintenanceAmount = g.Where(x => x.CostType == "InternalMaintenance").Sum(x => (decimal?)x.Amount) ?? 0m,
                OtherAmount = g.Where(x => x.CostType != "Fuel" && x.CostType != "ExternalRepair" && x.CostType != "InternalMaintenance")
                    .Sum(x => (decimal?)x.Amount) ?? 0m
            })
            .OrderByDescending(x => x.TotalAmount)
            .Take(top)
            .ToListAsync();

        var rows = rowsRaw.Select(r => new FleetCostSummaryRowDto
        {
            VehicleAssetId = r.VehicleAssetId,
            VehicleName = r.VehicleName,
            EntryCount = r.EntryCount,
            TotalAmount = Math.Round(r.TotalAmount, 2, MidpointRounding.AwayFromZero),
            FuelAmount = Math.Round(r.FuelAmount, 2, MidpointRounding.AwayFromZero),
            ExternalRepairAmount = Math.Round(r.ExternalRepairAmount, 2, MidpointRounding.AwayFromZero),
            InternalMaintenanceAmount = Math.Round(r.InternalMaintenanceAmount, 2, MidpointRounding.AwayFromZero),
            OtherAmount = Math.Round(r.OtherAmount, 2, MidpointRounding.AwayFromZero),
            CompletedTrips = utilByVehicle.TryGetValue(r.VehicleAssetId, out var u) ? u.CompletedTrips : 0,
            TotalKm = utilByVehicle.TryGetValue(r.VehicleAssetId, out var u2) ? u2.TotalKm : 0m,
            TotalHours = utilByVehicle.TryGetValue(r.VehicleAssetId, out var u3) ? u3.TotalHours : 0m,
            CostPerKm = utilByVehicle.TryGetValue(r.VehicleAssetId, out var u4) && u4.TotalKm > 0
                ? Math.Round(Math.Round(r.TotalAmount, 2, MidpointRounding.AwayFromZero) / u4.TotalKm, 4, MidpointRounding.AwayFromZero)
                : null,
            CostPerHour = utilByVehicle.TryGetValue(r.VehicleAssetId, out var u5) && u5.TotalHours > 0
                ? Math.Round(Math.Round(r.TotalAmount, 2, MidpointRounding.AwayFromZero) / u5.TotalHours, 4, MidpointRounding.AwayFromZero)
                : null
        }).ToList();

        var totalAmount = rows.Sum(r => r.TotalAmount);

        return new FleetCostSummaryDto
        {
            FromUtc = from,
            ToUtc = to,
            TotalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero),
            Rows = rows
        };
    }

    public async Task<FleetUtilizationSummaryDto> GetUtilizationSummaryAsync(DateTime? fromUtc = null, DateTime? toUtc = null, int top = 10, Guid? vehicleAssetId = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var now = DateTime.UtcNow;

        var from = (fromUtc ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)).ToUniversalTime();
        var to = (toUtc ?? now).ToUniversalTime();
        if (to < from) (from, to) = (to, from);

        if (top <= 0) top = 10;
        if (top > 200) top = 200;

        var tripsQ = _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t =>
                t.TenantId == tenantId &&
                !t.IsDeleted &&
                t.Status == FleetTripStatuses.Completed &&
                t.CompletedAt.HasValue &&
                t.CompletedAt.Value >= from &&
                t.CompletedAt.Value <= to);

        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            tripsQ = tripsQ.Where(t => t.VehicleAssetId == vehicleAssetId.Value);

        var assetsQ = _unitOfWork.Repository<MaintenanceAsset>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted);

        var joined = from t in tripsQ
                     join a in assetsQ on t.VehicleAssetId equals a.Id
                     select new
                     {
                         t.VehicleAssetId,
                         VehicleName = a.Name,
                         t.StartMileage,
                         t.EndMileage,
                         t.StartOperatingHours,
                         t.EndOperatingHours
                     };

        var rowsRaw = await joined
            .GroupBy(x => new { x.VehicleAssetId, x.VehicleName })
            .Select(g => new
            {
                g.Key.VehicleAssetId,
                g.Key.VehicleName,
                CompletedTrips = g.Count(),
                TotalKm = g.Sum(x =>
                    x.StartMileage.HasValue &&
                    x.EndMileage.HasValue &&
                    x.EndMileage.Value >= x.StartMileage.Value
                        ? (x.EndMileage.Value - x.StartMileage.Value)
                        : 0d),
                TotalHours = g.Sum(x =>
                    x.StartOperatingHours.HasValue &&
                    x.EndOperatingHours.HasValue &&
                    x.EndOperatingHours.Value >= x.StartOperatingHours.Value
                        ? (x.EndOperatingHours.Value - x.StartOperatingHours.Value)
                        : 0d)
            })
            .OrderByDescending(x => x.TotalKm + x.TotalHours)
            .Take(top)
            .ToListAsync();

        var rows = rowsRaw.Select(r =>
        {
            var totalKm = (decimal)Math.Round(r.TotalKm, 2, MidpointRounding.AwayFromZero);
            var totalHours = (decimal)Math.Round(r.TotalHours, 2, MidpointRounding.AwayFromZero);
            decimal? avgKm = r.CompletedTrips > 0 ? Math.Round(totalKm / r.CompletedTrips, 2, MidpointRounding.AwayFromZero) : null;
            decimal? avgHours = r.CompletedTrips > 0 ? Math.Round(totalHours / r.CompletedTrips, 2, MidpointRounding.AwayFromZero) : null;

            return new FleetUtilizationRowDto
            {
                VehicleAssetId = r.VehicleAssetId,
                VehicleName = r.VehicleName,
                CompletedTrips = r.CompletedTrips,
                TotalKm = totalKm,
                TotalHours = totalHours,
                AverageKmPerTrip = avgKm,
                AverageHoursPerTrip = avgHours
            };
        }).ToList();

        var completedTrips = rows.Sum(r => r.CompletedTrips);
        var totalKmAll = rows.Sum(r => r.TotalKm);
        var totalHoursAll = rows.Sum(r => r.TotalHours);

        return new FleetUtilizationSummaryDto
        {
            FromUtc = from,
            ToUtc = to,
            CompletedTrips = completedTrips,
            TotalKm = Math.Round(totalKmAll, 2, MidpointRounding.AwayFromZero),
            TotalHours = Math.Round(totalHoursAll, 2, MidpointRounding.AwayFromZero),
            Rows = rows
        };
    }
}

using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetCostService : IFleetCostService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetCostService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PagedResult<FleetCostEntryDto>> GetPagedAsync(Guid vehicleAssetId, int page, int pageSize, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        if (vehicleAssetId == Guid.Empty) return new PagedResult<FleetCostEntryDto> { Items = new List<FleetCostEntryDto>(), TotalCount = 0, Page = 1, PageSize = pageSize };
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetCostEntry>();

        IQueryable<FleetCostEntry> q = repo
            .GetQueryable(c => c.TenantId == tenantId && c.VehicleAssetId == vehicleAssetId && !c.IsDeleted)
            .Include(c => c.VehicleAsset);

        if (fromUtc.HasValue) q = q.Where(c => c.CostDateUtc >= fromUtc.Value.ToUniversalTime());
        if (toUtc.HasValue) q = q.Where(c => c.CostDateUtc <= toUtc.Value.ToUniversalTime());

        var total = await q.CountAsync();

        var items = await q.OrderByDescending(c => c.CostDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new FleetCostEntryDto
            {
                Id = c.Id,
                VehicleAssetId = c.VehicleAssetId,
                VehicleName = c.VehicleAsset.Name,
                CostDateUtc = c.CostDateUtc,
                CostType = c.CostType,
                Source = c.Source,
                Amount = c.Amount,
                CurrencyCode = c.CurrencyCode,
                Notes = c.Notes,
                FleetTripId = c.FleetTripId,
                WorkOrderId = c.WorkOrderId,
                FleetExternalRepairId = c.FleetExternalRepairId,
                FleetFuelTransactionId = c.FleetFuelTransactionId,
                FleetIncidentId = c.FleetIncidentId
            })
            .ToListAsync();

        return new PagedResult<FleetCostEntryDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<FleetCostEntryDto>> GetPagedForTripAsync(Guid fleetTripId, int page, int pageSize, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        if (fleetTripId == Guid.Empty) return new PagedResult<FleetCostEntryDto> { Items = new List<FleetCostEntryDto>(), TotalCount = 0, Page = 1, PageSize = pageSize };
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetCostEntry>();

        IQueryable<FleetCostEntry> q = repo
            .GetQueryable(c => c.TenantId == tenantId && c.FleetTripId == fleetTripId && !c.IsDeleted)
            .Include(c => c.VehicleAsset);

        if (fromUtc.HasValue) q = q.Where(c => c.CostDateUtc >= fromUtc.Value.ToUniversalTime());
        if (toUtc.HasValue) q = q.Where(c => c.CostDateUtc <= toUtc.Value.ToUniversalTime());

        var total = await q.CountAsync();

        var items = await q.OrderByDescending(c => c.CostDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new FleetCostEntryDto
            {
                Id = c.Id,
                VehicleAssetId = c.VehicleAssetId,
                VehicleName = c.VehicleAsset.Name,
                CostDateUtc = c.CostDateUtc,
                CostType = c.CostType,
                Source = c.Source,
                Amount = c.Amount,
                CurrencyCode = c.CurrencyCode,
                Notes = c.Notes,
                FleetTripId = c.FleetTripId,
                WorkOrderId = c.WorkOrderId,
                FleetExternalRepairId = c.FleetExternalRepairId,
                FleetFuelTransactionId = c.FleetFuelTransactionId,
                FleetIncidentId = c.FleetIncidentId
            })
            .ToListAsync();

        return new PagedResult<FleetCostEntryDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<FleetCostEntryDto> CreateAsync(CreateFleetCostEntryDto dto)
    {
        dto ??= new CreateFleetCostEntryDto();
        var hasTrip = dto.FleetTripId.HasValue && dto.FleetTripId.Value != Guid.Empty;
        var hasVehicle = dto.VehicleAssetId.HasValue && dto.VehicleAssetId.Value != Guid.Empty;
        if (!hasTrip && !hasVehicle) throw new ArgumentException("FleetTripId (preferred) or VehicleAssetId is required.");
        if (dto.Amount <= 0) throw new ArgumentException("Amount must be greater than 0.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        Guid vehicleAssetId;
        Guid? fleetTripId = null;

        if (hasTrip)
        {
            fleetTripId = dto.FleetTripId!.Value;
            var trip = await _unitOfWork.Repository<FleetTrip>()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == fleetTripId.Value && !t.IsDeleted);

            if (trip == null)
            {
                throw new ArgumentException("FleetTripId is invalid.");
            }

            vehicleAssetId = trip.VehicleAssetId;

            if (hasVehicle && dto.VehicleAssetId!.Value != vehicleAssetId)
            {
                throw new ArgumentException("VehicleAssetId does not match the selected FleetTripId.");
            }
        }
        else
        {
            vehicleAssetId = dto.VehicleAssetId!.Value;
        }

        var entity = new FleetCostEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = vehicleAssetId,
            FleetTripId = fleetTripId,
            CostDateUtc = (dto.CostDateUtc ?? now).ToUniversalTime(),
            CostType = string.IsNullOrWhiteSpace(dto.CostType) ? "Other" : dto.CostType.Trim(),
            Source = NormalizeSource(dto.Source),
            Amount = dto.Amount,
            CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? null : dto.CurrencyCode.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedAt = now,
            CreatedById = userId
        };

        await _unitOfWork.Repository<FleetCostEntry>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>().FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == vehicleAssetId && !a.IsDeleted);
        return new FleetCostEntryDto
        {
            Id = entity.Id,
            VehicleAssetId = entity.VehicleAssetId,
            VehicleName = vehicle?.Name ?? string.Empty,
            CostDateUtc = entity.CostDateUtc,
            CostType = entity.CostType,
            Source = entity.Source,
            Amount = entity.Amount,
            CurrencyCode = entity.CurrencyCode,
            Notes = entity.Notes,
            FleetTripId = entity.FleetTripId
        };
    }

    private static string NormalizeSource(string? source)
    {
        var s = (source ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(s)) return "Manual";

        // Whitelist to keep reporting/filters consistent.
        return s switch
        {
            "Manual" => "Manual",
            "TripExpense" => "TripExpense",
            _ => throw new ArgumentException("Invalid Source. Allowed values: Manual, TripExpense.")
        };
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty) return false;

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetCostEntry>();
        var entity = await repo.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id && !c.IsDeleted);
        if (entity == null) return false;

        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.DeletedBy = userId.ToString();
        entity.UpdatedAt = now;
        entity.LastModifiedById = userId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}

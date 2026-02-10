using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public class FleetFuelService : IFleetFuelService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetFuelService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PagedResult<FleetFuelTransactionDto>> GetFuelTransactionsPagedAsync(Guid vehicleAssetId, int page, int pageSize)
    {
        if (vehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetFuelTransaction>();

        var q = repo.GetQueryable(t => t.TenantId == tenantId && t.VehicleAssetId == vehicleAssetId)
            .Include(t => t.VehicleAsset);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(t => t.FuelledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new FleetFuelTransactionDto
            {
                Id = t.Id,
                VehicleAssetId = t.VehicleAssetId,
                VehicleName = t.VehicleAsset != null ? t.VehicleAsset.Name : string.Empty,
                FleetTripId = t.FleetTripId,
                FuelledAt = t.FuelledAt,
                Quantity = t.Quantity,
                Unit = t.Unit,
                UnitCost = t.UnitCost,
                TotalCost = t.TotalCost,
                MileageAtFuel = t.MileageAtFuel,
                OperatingHoursAtFuel = t.OperatingHoursAtFuel,
                VendorName = t.VendorName,
                ReceiptReference = t.ReceiptReference,
                Notes = t.Notes,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<FleetFuelTransactionDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FleetFuelTransactionDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetFuelTransaction>();
        var t = await repo.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, x => x.VehicleAsset);
        if (t == null) return null;

        return new FleetFuelTransactionDto
        {
            Id = t.Id,
            VehicleAssetId = t.VehicleAssetId,
            VehicleName = t.VehicleAsset != null ? t.VehicleAsset.Name : string.Empty,
            FleetTripId = t.FleetTripId,
            FuelledAt = t.FuelledAt,
            Quantity = t.Quantity,
            Unit = t.Unit,
            UnitCost = t.UnitCost,
            TotalCost = t.TotalCost,
            MileageAtFuel = t.MileageAtFuel,
            OperatingHoursAtFuel = t.OperatingHoursAtFuel,
            VendorName = t.VendorName,
            ReceiptReference = t.ReceiptReference,
            Notes = t.Notes,
            CreatedAt = t.CreatedAt
        };
    }

    public async Task<FleetFuelTransactionDto> CreateAsync(CreateFleetFuelTransactionDto dto)
    {
        dto ??= new CreateFleetFuelTransactionDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (dto.Quantity <= 0) throw new ArgumentException("Quantity must be greater than 0.");

        var tenantId = _currentUserProvider.TenantId;
        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.Id == dto.VehicleAssetId && a.TenantId == tenantId, a => a.AssetCategory);

        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selected asset is not a vehicle.");

        if (dto.FleetTripId.HasValue && dto.FleetTripId.Value != Guid.Empty)
        {
            var trip = await _unitOfWork.Repository<FleetTrip>()
                .FirstOrDefaultAsync(t => t.Id == dto.FleetTripId.Value && t.TenantId == tenantId);

            if (trip == null)
                throw new ArgumentException("Fleet trip not found.");
        }

        var total = dto.UnitCost.HasValue ? Math.Round(dto.Quantity * dto.UnitCost.Value, 2, MidpointRounding.AwayFromZero) : (decimal?)null;

        var repo = _unitOfWork.Repository<FleetFuelTransaction>();
        var entity = new FleetFuelTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            FleetTripId = dto.FleetTripId,
            FuelledAt = dto.FuelledAt.ToUniversalTime(),
            Quantity = Math.Round(dto.Quantity, 2, MidpointRounding.AwayFromZero),
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "L" : dto.Unit.Trim(),
            UnitCost = dto.UnitCost.HasValue ? Math.Round(dto.UnitCost.Value, 4, MidpointRounding.AwayFromZero) : null,
            TotalCost = total,
            MileageAtFuel = dto.MileageAtFuel,
            OperatingHoursAtFuel = dto.OperatingHoursAtFuel,
            VendorName = string.IsNullOrWhiteSpace(dto.VendorName) ? null : dto.VendorName.Trim(),
            ReceiptReference = string.IsNullOrWhiteSpace(dto.ReceiptReference) ? null : dto.ReceiptReference.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserProvider.UserId
        };

        await repo.AddAsync(entity);

        // Auto-add cost entry (best-effort).
        if (entity.TotalCost.HasValue && entity.TotalCost.Value > 0)
        {
            await _unitOfWork.Repository<FleetCostEntry>().AddAsync(new FleetCostEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                VehicleAssetId = entity.VehicleAssetId,
                FleetTripId = entity.FleetTripId,
                FleetFuelTransactionId = entity.Id,
                CostDateUtc = entity.FuelledAt,
                CostType = "Fuel",
                Source = "FuelTransaction",
                Amount = entity.TotalCost.Value,
                CurrencyCode = null,
                Notes = "Fuel transaction",
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUserProvider.UserId
            });
        }

        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty) return false;
        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetFuelTransaction>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}


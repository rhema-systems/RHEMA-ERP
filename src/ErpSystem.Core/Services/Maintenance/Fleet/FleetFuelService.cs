using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services.Maintenance;
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
        var currencyCode = await MaintenanceCurrencyResolver.ResolveBaseCurrencyCodeAsync(_unitOfWork, tenantId);
        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.Id == dto.VehicleAssetId && a.TenantId == tenantId, a => a.AssetCategory);

        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selected asset is not a vehicle.");

        if (!dto.FleetTripId.HasValue || dto.FleetTripId.Value == Guid.Empty)
            throw new ArgumentException("FleetTripId is required. Fuel must be captured from a trip.");

        var trip = await _unitOfWork.Repository<FleetTrip>()
            .FirstOrDefaultAsync(t => t.Id == dto.FleetTripId.Value && t.TenantId == tenantId);

        if (trip == null)
            throw new ArgumentException("Fleet trip not found.");

        if (trip.VehicleAssetId != dto.VehicleAssetId)
            throw new ArgumentException("VehicleAssetId does not match the selected FleetTripId.");

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
                CurrencyCode = currencyCode,
                Notes = "Fuel transaction",
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUserProvider.UserId
            });
        }

        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetFuelTransactionDto> UpdateAsync(Guid id, UpdateFleetFuelTransactionDto dto)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id is required.");
        dto ??= new UpdateFleetFuelTransactionDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (dto.Quantity <= 0) throw new ArgumentException("Quantity must be greater than 0.");

        var tenantId = _currentUserProvider.TenantId;
        var currencyCode = await MaintenanceCurrencyResolver.ResolveBaseCurrencyCodeAsync(_unitOfWork, tenantId);

        var repo = _unitOfWork.Repository<FleetFuelTransaction>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
        if (entity == null) throw new InvalidOperationException("Fuel transaction not found.");

        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.Id == dto.VehicleAssetId && a.TenantId == tenantId, a => a.AssetCategory);

        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selected asset is not a vehicle.");

        if (!dto.FleetTripId.HasValue || dto.FleetTripId.Value == Guid.Empty)
            throw new ArgumentException("FleetTripId is required. Fuel must be captured from a trip.");

        var trip = await _unitOfWork.Repository<FleetTrip>()
            .FirstOrDefaultAsync(t => t.Id == dto.FleetTripId.Value && t.TenantId == tenantId);

        if (trip == null)
            throw new ArgumentException("Fleet trip not found.");

        if (trip.VehicleAssetId != dto.VehicleAssetId)
            throw new ArgumentException("VehicleAssetId does not match the selected FleetTripId.");

        var total = dto.UnitCost.HasValue ? Math.Round(dto.Quantity * dto.UnitCost.Value, 2, MidpointRounding.AwayFromZero) : (decimal?)null;

        entity.VehicleAssetId = dto.VehicleAssetId;
        entity.FleetTripId = dto.FleetTripId;
        entity.FuelledAt = dto.FuelledAt.ToUniversalTime();
        entity.Quantity = Math.Round(dto.Quantity, 2, MidpointRounding.AwayFromZero);
        entity.Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "L" : dto.Unit.Trim();
        entity.UnitCost = dto.UnitCost.HasValue ? Math.Round(dto.UnitCost.Value, 4, MidpointRounding.AwayFromZero) : null;
        entity.TotalCost = total;
        entity.MileageAtFuel = dto.MileageAtFuel;
        entity.OperatingHoursAtFuel = dto.OperatingHoursAtFuel;
        entity.VendorName = string.IsNullOrWhiteSpace(dto.VendorName) ? null : dto.VendorName.Trim();
        entity.ReceiptReference = string.IsNullOrWhiteSpace(dto.ReceiptReference) ? null : dto.ReceiptReference.Trim();
        entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.LastModifiedById = _currentUserProvider.UserId;

        var costRepo = _unitOfWork.Repository<FleetCostEntry>();
        var existingCost = await costRepo.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.FleetFuelTransactionId == entity.Id);

        if (entity.TotalCost.HasValue && entity.TotalCost.Value > 0)
        {
            if (existingCost == null)
            {
                await costRepo.AddAsync(new FleetCostEntry
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
                    CurrencyCode = currencyCode,
                    Notes = "Fuel transaction",
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = _currentUserProvider.UserId
                });
            }
            else
            {
                existingCost.VehicleAssetId = entity.VehicleAssetId;
                existingCost.FleetTripId = entity.FleetTripId;
                existingCost.CostDateUtc = entity.FuelledAt;
                existingCost.CostType = "Fuel";
                existingCost.Source = "FuelTransaction";
                existingCost.Amount = entity.TotalCost.Value;
                existingCost.CurrencyCode = string.IsNullOrWhiteSpace(existingCost.CurrencyCode) ? currencyCode : existingCost.CurrencyCode;
                existingCost.UpdatedAt = DateTime.UtcNow;
                existingCost.LastModifiedById = _currentUserProvider.UserId;
            }
        }
        else if (existingCost != null)
        {
            await costRepo.DeleteAsync(existingCost);
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

        var costRepo = _unitOfWork.Repository<FleetCostEntry>();
        var existingCost = await costRepo.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.FleetFuelTransactionId == entity.Id);
        if (existingCost != null)
        {
            await costRepo.DeleteAsync(existingCost);
        }

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}


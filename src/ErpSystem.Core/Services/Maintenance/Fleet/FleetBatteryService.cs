using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetBatteryService : IFleetBatteryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetBatteryService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PagedResult<FleetBatteryDto>> GetPagedAsync(Guid vehicleAssetId, int page, int pageSize)
    {
        if (vehicleAssetId == Guid.Empty) return new PagedResult<FleetBatteryDto> { Items = new List<FleetBatteryDto>(), TotalCount = 0, Page = 1, PageSize = pageSize };
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetBattery>();

        var q = repo.GetQueryable(b => b.TenantId == tenantId && b.VehicleAssetId == vehicleAssetId && !b.IsDeleted)
            .Include(b => b.VehicleAsset);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(b => b.InstalledAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new FleetBatteryDto
            {
                Id = b.Id,
                VehicleAssetId = b.VehicleAssetId,
                VehicleName = b.VehicleAsset.Name,
                SerialNumber = b.SerialNumber,
                Brand = b.Brand,
                Spec = b.Spec,
                Position = b.Position,
                InstalledAtUtc = b.InstalledAtUtc,
                RemovedAtUtc = b.RemovedAtUtc,
                Status = b.Status,
                Notes = b.Notes
            })
            .ToListAsync();

        return new PagedResult<FleetBatteryDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<FleetBatteryDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetBattery>();
        var b = await repo.GetQueryable(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted)
            .Include(x => x.VehicleAsset)
            .FirstOrDefaultAsync();

        if (b == null) return null;

        return new FleetBatteryDto
        {
            Id = b.Id,
            VehicleAssetId = b.VehicleAssetId,
            VehicleName = b.VehicleAsset.Name,
            SerialNumber = b.SerialNumber,
            Brand = b.Brand,
            Spec = b.Spec,
            Position = b.Position,
            InstalledAtUtc = b.InstalledAtUtc,
            RemovedAtUtc = b.RemovedAtUtc,
            Status = b.Status,
            Notes = b.Notes
        };
    }

    public async Task<FleetBatteryDto> CreateAsync(CreateFleetBatteryDto dto)
    {
        dto ??= new CreateFleetBatteryDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (string.IsNullOrWhiteSpace(dto.SerialNumber)) throw new ArgumentException("SerialNumber is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var entity = new FleetBattery
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            SerialNumber = dto.SerialNumber.Trim(),
            Brand = string.IsNullOrWhiteSpace(dto.Brand) ? null : dto.Brand.Trim(),
            Spec = string.IsNullOrWhiteSpace(dto.Spec) ? null : dto.Spec.Trim(),
            Position = string.IsNullOrWhiteSpace(dto.Position) ? null : dto.Position.Trim(),
            InstalledAtUtc = (dto.InstalledAtUtc ?? now).ToUniversalTime(),
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "Installed" : dto.Status.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedAt = now,
            CreatedById = userId
        };

        await _unitOfWork.Repository<FleetBattery>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetBatteryDto> UpdateAsync(Guid id, CreateFleetBatteryDto dto)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id is required.");
        dto ??= new CreateFleetBatteryDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetBattery>();
        var entity = await repo.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id && !b.IsDeleted)
            ?? throw new ArgumentException("Battery not found.");

        if (dto.VehicleAssetId != Guid.Empty) entity.VehicleAssetId = dto.VehicleAssetId;
        if (!string.IsNullOrWhiteSpace(dto.SerialNumber)) entity.SerialNumber = dto.SerialNumber.Trim();
        entity.Brand = string.IsNullOrWhiteSpace(dto.Brand) ? null : dto.Brand.Trim();
        entity.Spec = string.IsNullOrWhiteSpace(dto.Spec) ? null : dto.Spec.Trim();
        entity.Position = string.IsNullOrWhiteSpace(dto.Position) ? null : dto.Position.Trim();
        if (dto.InstalledAtUtc.HasValue) entity.InstalledAtUtc = dto.InstalledAtUtc.Value.ToUniversalTime();
        entity.Status = string.IsNullOrWhiteSpace(dto.Status) ? entity.Status : dto.Status.Trim();
        entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        entity.UpdatedAt = now;
        entity.LastModifiedById = userId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty) return false;
        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetBattery>();
        var entity = await repo.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id && !b.IsDeleted);
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


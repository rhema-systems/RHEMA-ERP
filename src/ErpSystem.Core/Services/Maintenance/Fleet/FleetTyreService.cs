using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetTyreService : IFleetTyreService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetTyreService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PagedResult<FleetTyreDto>> GetPagedAsync(Guid vehicleAssetId, int page, int pageSize)
    {
        if (vehicleAssetId == Guid.Empty) return new PagedResult<FleetTyreDto> { Items = new List<FleetTyreDto>(), TotalCount = 0, Page = 1, PageSize = pageSize };
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetTyre>();

        var q = repo.GetQueryable(t => t.TenantId == tenantId && t.VehicleAssetId == vehicleAssetId && !t.IsDeleted)
            .Include(t => t.VehicleAsset);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(t => t.InstalledAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new FleetTyreDto
            {
                Id = t.Id,
                VehicleAssetId = t.VehicleAssetId,
                VehicleName = t.VehicleAsset.Name,
                SerialNumber = t.SerialNumber,
                Brand = t.Brand,
                Size = t.Size,
                Position = t.Position,
                TreadDepthMm = t.TreadDepthMm,
                InstalledAtUtc = t.InstalledAtUtc,
                RemovedAtUtc = t.RemovedAtUtc,
                Status = t.Status,
                Notes = t.Notes
            })
            .ToListAsync();

        return new PagedResult<FleetTyreDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<FleetTyreDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetTyre>();
        var t = await repo.GetQueryable(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted)
            .Include(x => x.VehicleAsset)
            .FirstOrDefaultAsync();

        if (t == null) return null;

        return new FleetTyreDto
        {
            Id = t.Id,
            VehicleAssetId = t.VehicleAssetId,
            VehicleName = t.VehicleAsset.Name,
            SerialNumber = t.SerialNumber,
            Brand = t.Brand,
            Size = t.Size,
            Position = t.Position,
            TreadDepthMm = t.TreadDepthMm,
            InstalledAtUtc = t.InstalledAtUtc,
            RemovedAtUtc = t.RemovedAtUtc,
            Status = t.Status,
            Notes = t.Notes
        };
    }

    public async Task<FleetTyreDto> CreateAsync(CreateFleetTyreDto dto)
    {
        dto ??= new CreateFleetTyreDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (string.IsNullOrWhiteSpace(dto.SerialNumber)) throw new ArgumentException("SerialNumber is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var entity = new FleetTyre
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            SerialNumber = dto.SerialNumber.Trim(),
            Brand = string.IsNullOrWhiteSpace(dto.Brand) ? null : dto.Brand.Trim(),
            Size = string.IsNullOrWhiteSpace(dto.Size) ? null : dto.Size.Trim(),
            Position = string.IsNullOrWhiteSpace(dto.Position) ? null : dto.Position.Trim(),
            TreadDepthMm = dto.TreadDepthMm,
            InstalledAtUtc = (dto.InstalledAtUtc ?? now).ToUniversalTime(),
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "Installed" : dto.Status.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedAt = now,
            CreatedById = userId
        };

        await _unitOfWork.Repository<FleetTyre>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetTyreDto> UpdateAsync(Guid id, CreateFleetTyreDto dto)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id is required.");
        dto ??= new CreateFleetTyreDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetTyre>();
        var entity = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted)
            ?? throw new ArgumentException("Tyre not found.");

        if (dto.VehicleAssetId != Guid.Empty) entity.VehicleAssetId = dto.VehicleAssetId;
        if (!string.IsNullOrWhiteSpace(dto.SerialNumber)) entity.SerialNumber = dto.SerialNumber.Trim();
        entity.Brand = string.IsNullOrWhiteSpace(dto.Brand) ? null : dto.Brand.Trim();
        entity.Size = string.IsNullOrWhiteSpace(dto.Size) ? null : dto.Size.Trim();
        entity.Position = string.IsNullOrWhiteSpace(dto.Position) ? null : dto.Position.Trim();
        entity.TreadDepthMm = dto.TreadDepthMm;
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

        var repo = _unitOfWork.Repository<FleetTyre>();
        var entity = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted);
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


using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services.Maintenance;
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

    public async Task<IReadOnlyList<FleetTyreEventDto>> GetEventsAsync(Guid fleetTyreId)
    {
        if (fleetTyreId == Guid.Empty) return Array.Empty<FleetTyreEventDto>();

        var tenantId = _currentUserProvider.TenantId;
        var baseCurrencyCode = await MaintenanceCurrencyResolver.ResolveBaseCurrencyCodeAsync(_unitOfWork, tenantId);
        var repo = _unitOfWork.Repository<FleetTyreEvent>();

        var items = await repo.GetQueryable(e => e.TenantId == tenantId && e.FleetTyreId == fleetTyreId && !e.IsDeleted)
            .OrderByDescending(e => e.EventAtUtc)
            .Select(e => new FleetTyreEventDto
            {
                Id = e.Id,
                FleetTyreId = e.FleetTyreId,
                VehicleAssetId = e.VehicleAssetId,
                EventAtUtc = e.EventAtUtc,
                EventType = e.EventType,
                FromPosition = e.FromPosition,
                ToPosition = e.ToPosition,
                FromStatus = e.FromStatus,
                ToStatus = e.ToStatus,
                TreadDepthMm = e.TreadDepthMm,
                CostAmount = e.CostAmount,
                CurrencyCode = e.CurrencyCode == null || e.CurrencyCode == string.Empty ? baseCurrencyCode : e.CurrencyCode,
                Notes = e.Notes,
                CreatedAt = e.CreatedAt,
                CreatedByUserId = e.CreatedById,
            })
            .ToListAsync();

        return items;
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
        var currencyCode = await MaintenanceCurrencyResolver.ResolveCurrencyCodeAsync(_unitOfWork, tenantId, dto.CurrencyCode);

        var status = string.IsNullOrWhiteSpace(dto.Status) ? "Installed" : dto.Status.Trim();
        var position = string.IsNullOrWhiteSpace(dto.Position) ? null : dto.Position.Trim();

        if (string.Equals(status, "Installed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(position))
        {
            await EnsureNoOtherInstalledAtPositionAsync(dto.VehicleAssetId, position, excludeTyreId: null);
        }

        var entity = new FleetTyre
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            SerialNumber = dto.SerialNumber.Trim(),
            Brand = string.IsNullOrWhiteSpace(dto.Brand) ? null : dto.Brand.Trim(),
            Size = string.IsNullOrWhiteSpace(dto.Size) ? null : dto.Size.Trim(),
            Position = position,
            TreadDepthMm = dto.TreadDepthMm,
            InstalledAtUtc = (dto.InstalledAtUtc ?? now).ToUniversalTime(),
            Status = status,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedAt = now,
            CreatedById = userId
        };

        await _unitOfWork.Repository<FleetTyre>().AddAsync(entity);

        var ev = new FleetTyreEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FleetTyreId = entity.Id,
            VehicleAssetId = entity.VehicleAssetId,
            EventAtUtc = now,
            EventType = string.Equals(status, "Installed", StringComparison.OrdinalIgnoreCase) ? "Installed" : "Created",
            FromPosition = null,
            ToPosition = entity.Position,
            FromStatus = null,
            ToStatus = entity.Status,
            TreadDepthMm = entity.TreadDepthMm,
            CostAmount = (dto.CostAmount.HasValue && dto.CostAmount.Value > 0) ? Math.Round(dto.CostAmount.Value, 2, MidpointRounding.AwayFromZero) : null,
            CurrencyCode = currencyCode,
            Notes = entity.Notes,
            CreatedAt = now,
            CreatedById = userId,
        };

        await _unitOfWork.Repository<FleetTyreEvent>().AddAsync(ev);

        await UpsertTyreCostEntryAsync(entity.VehicleAssetId, ev, userId, now);

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
        var currencyCode = await MaintenanceCurrencyResolver.ResolveCurrencyCodeAsync(_unitOfWork, tenantId, dto.CurrencyCode);

        var repo = _unitOfWork.Repository<FleetTyre>();
        var entity = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted)
            ?? throw new ArgumentException("Tyre not found.");

        var fromPosition = entity.Position;
        var fromStatus = entity.Status;
        var fromTread = entity.TreadDepthMm;

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

        if (string.Equals(entity.Status, "Installed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(entity.Position))
        {
            await EnsureNoOtherInstalledAtPositionAsync(entity.VehicleAssetId, entity.Position, excludeTyreId: entity.Id);
        }

        if (string.Equals(entity.Status, "Removed", StringComparison.OrdinalIgnoreCase) || string.Equals(entity.Status, "Disposed", StringComparison.OrdinalIgnoreCase))
        {
            entity.RemovedAtUtc ??= now;
        }
        else if (string.Equals(entity.Status, "Installed", StringComparison.OrdinalIgnoreCase))
        {
            entity.RemovedAtUtc = null;
        }

        await repo.UpdateAsync(entity);

        var hasCost = dto.CostAmount.HasValue && dto.CostAmount.Value > 0;
        if (!string.Equals(fromPosition, entity.Position, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(fromStatus, entity.Status, StringComparison.OrdinalIgnoreCase) ||
            fromTread != entity.TreadDepthMm ||
            hasCost)
        {
            var ev = new FleetTyreEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FleetTyreId = entity.Id,
                VehicleAssetId = entity.VehicleAssetId,
                EventAtUtc = now,
                EventType = hasCost && string.Equals(fromPosition, entity.Position, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(fromStatus, entity.Status, StringComparison.OrdinalIgnoreCase) &&
                            fromTread == entity.TreadDepthMm
                    ? "CostRecorded"
                    : ResolveEventType(fromPosition, entity.Position, fromStatus, entity.Status),
                FromPosition = fromPosition,
                ToPosition = entity.Position,
                FromStatus = fromStatus,
                ToStatus = entity.Status,
                TreadDepthMm = entity.TreadDepthMm,
                CostAmount = hasCost ? Math.Round(dto.CostAmount!.Value, 2, MidpointRounding.AwayFromZero) : null,
                CurrencyCode = currencyCode,
                Notes = entity.Notes,
                CreatedAt = now,
                CreatedById = userId,
            };

            await _unitOfWork.Repository<FleetTyreEvent>().AddAsync(ev);
            await UpsertTyreCostEntryAsync(entity.VehicleAssetId, ev, userId, now);
        }

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

    private async Task EnsureNoOtherInstalledAtPositionAsync(Guid vehicleAssetId, string position, Guid? excludeTyreId)
    {
        var tenantId = _currentUserProvider.TenantId;

        var q = _unitOfWork.Repository<FleetTyre>()
            .GetQueryable(t =>
                t.TenantId == tenantId &&
                t.VehicleAssetId == vehicleAssetId &&
                !t.IsDeleted &&
                t.Status == "Installed" &&
                t.Position != null &&
                t.Position == position);

        if (excludeTyreId.HasValue && excludeTyreId.Value != Guid.Empty)
        {
            q = q.Where(t => t.Id != excludeTyreId.Value);
        }

        var exists = await q.AnyAsync();
        if (exists)
            throw new InvalidOperationException($"Position '{position}' already has an installed tyre for this vehicle.");
    }

    private static string ResolveEventType(string? fromPosition, string? toPosition, string fromStatus, string toStatus)
    {
        if (!string.Equals(fromStatus, toStatus, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(toStatus, "Installed", StringComparison.OrdinalIgnoreCase)) return "Installed";
            if (string.Equals(toStatus, "Removed", StringComparison.OrdinalIgnoreCase)) return "Removed";
            if (string.Equals(toStatus, "Disposed", StringComparison.OrdinalIgnoreCase)) return "Disposed";
            return "StatusChanged";
        }

        if (!string.Equals(fromPosition, toPosition, StringComparison.OrdinalIgnoreCase))
        {
            return "Rotated";
        }

        return "Updated";
    }

    private async Task UpsertTyreCostEntryAsync(Guid vehicleAssetId, FleetTyreEvent ev, Guid userId, DateTime nowUtc)
    {
        if (ev.CostAmount == null || ev.CostAmount.Value <= 0) return;

        var repo = _unitOfWork.Repository<FleetCostEntry>();
        var existing = await repo.FirstOrDefaultAsync(c =>
            c.TenantId == ev.TenantId &&
            c.FleetTyreEventId == ev.Id &&
            !c.IsDeleted);

        if (existing == null)
        {
            await repo.AddAsync(new FleetCostEntry
            {
                Id = Guid.NewGuid(),
                TenantId = ev.TenantId,
                VehicleAssetId = vehicleAssetId,
                FleetTyreEventId = ev.Id,
                CostDateUtc = ev.EventAtUtc,
                CostType = "Tyre",
                Source = "TyreEvent",
                Amount = ev.CostAmount.Value,
                CurrencyCode = ev.CurrencyCode,
                Notes = $"Tyre {ev.EventType}: {ev.FleetTyreId}",
                CreatedAt = nowUtc,
                CreatedById = userId
            });
        }
        else
        {
            existing.VehicleAssetId = vehicleAssetId;
            existing.CostDateUtc = ev.EventAtUtc;
            existing.CostType = "Tyre";
            existing.Amount = ev.CostAmount.Value;
            existing.CurrencyCode = ev.CurrencyCode;
            existing.Notes = $"Tyre {ev.EventType}: {ev.FleetTyreId}";
            existing.Source = "TyreEvent";
            existing.UpdatedAt = nowUtc;
            existing.LastModifiedById = userId;
            await repo.UpdateAsync(existing);
        }
    }
}


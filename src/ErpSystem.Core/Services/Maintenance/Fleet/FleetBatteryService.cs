using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services.Maintenance;
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

    public async Task<IReadOnlyList<FleetBatteryEventDto>> GetEventsAsync(Guid fleetBatteryId)
    {
        if (fleetBatteryId == Guid.Empty) return Array.Empty<FleetBatteryEventDto>();

        var tenantId = _currentUserProvider.TenantId;
        var baseCurrencyCode = await MaintenanceCurrencyResolver.ResolveBaseCurrencyCodeAsync(_unitOfWork, tenantId);
        var repo = _unitOfWork.Repository<FleetBatteryEvent>();

        var items = await repo.GetQueryable(e => e.TenantId == tenantId && e.FleetBatteryId == fleetBatteryId && !e.IsDeleted)
            .OrderByDescending(e => e.EventAtUtc)
            .Select(e => new FleetBatteryEventDto
            {
                Id = e.Id,
                FleetBatteryId = e.FleetBatteryId,
                VehicleAssetId = e.VehicleAssetId,
                EventAtUtc = e.EventAtUtc,
                EventType = e.EventType,
                FromPosition = e.FromPosition,
                ToPosition = e.ToPosition,
                FromStatus = e.FromStatus,
                ToStatus = e.ToStatus,
                CostAmount = e.CostAmount,
                CurrencyCode = e.CurrencyCode == null || e.CurrencyCode == string.Empty ? baseCurrencyCode : e.CurrencyCode,
                Notes = e.Notes,
                CreatedAt = e.CreatedAt,
                CreatedByUserId = e.CreatedById,
            })
            .ToListAsync();

        return items;
    }

    public async Task<FleetBatteryKpisDto> GetKpisAsync(Guid vehicleAssetId, DateTime? asAtUtc = null)
    {
        if (vehicleAssetId == Guid.Empty)
            return new FleetBatteryKpisDto { VehicleAssetId = vehicleAssetId };

        var tenantId = _currentUserProvider.TenantId;
        var now = asAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;

        var q = _unitOfWork.Repository<FleetBattery>()
            .GetQueryable(b => b.TenantId == tenantId && b.VehicleAssetId == vehicleAssetId && !b.IsDeleted);

        var rows = await q.Select(b => new { b.Status, b.InstalledAtUtc }).ToListAsync();

        var installed = rows.Count(r => string.Equals(r.Status, "Installed", StringComparison.OrdinalIgnoreCase));
        var inStock = rows.Count(r => string.Equals(r.Status, "InStock", StringComparison.OrdinalIgnoreCase));
        var removed = rows.Count(r => string.Equals(r.Status, "Removed", StringComparison.OrdinalIgnoreCase));
        var disposed = rows.Count(r => string.Equals(r.Status, "Disposed", StringComparison.OrdinalIgnoreCase));

        double? avgAgeDays = null;
        var installedAges = rows
            .Where(r => string.Equals(r.Status, "Installed", StringComparison.OrdinalIgnoreCase))
            .Select(r => (now - r.InstalledAtUtc.ToUniversalTime()).TotalDays)
            .Where(d => d >= 0)
            .ToList();

        if (installedAges.Count > 0) avgAgeDays = installedAges.Average();

        DateTime? latestInstalled = null;
        var latest = rows
            .Where(r => string.Equals(r.Status, "Installed", StringComparison.OrdinalIgnoreCase))
            .Select(r => (DateTime?)r.InstalledAtUtc)
            .Max();
        if (latest.HasValue) latestInstalled = latest.Value;

        return new FleetBatteryKpisDto
        {
            VehicleAssetId = vehicleAssetId,
            Total = rows.Count,
            Installed = installed,
            InStock = inStock,
            Removed = removed,
            Disposed = disposed,
            AverageInstalledAgeDays = avgAgeDays,
            LatestInstalledAtUtc = latestInstalled
        };
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
        var currencyCode = await MaintenanceCurrencyResolver.ResolveCurrencyCodeAsync(_unitOfWork, tenantId, dto.CurrencyCode);

        var status = string.IsNullOrWhiteSpace(dto.Status) ? "Installed" : dto.Status.Trim();
        var position = string.IsNullOrWhiteSpace(dto.Position) ? null : dto.Position.Trim();

        if (string.Equals(status, "Installed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(position))
        {
            await EnsureNoOtherInstalledAtPositionAsync(dto.VehicleAssetId, position, excludeBatteryId: null);
        }

        var entity = new FleetBattery
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            SerialNumber = dto.SerialNumber.Trim(),
            Brand = string.IsNullOrWhiteSpace(dto.Brand) ? null : dto.Brand.Trim(),
            Spec = string.IsNullOrWhiteSpace(dto.Spec) ? null : dto.Spec.Trim(),
            Position = position,
            InstalledAtUtc = (dto.InstalledAtUtc ?? now).ToUniversalTime(),
            Status = status,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedAt = now,
            CreatedById = userId
        };

        await _unitOfWork.Repository<FleetBattery>().AddAsync(entity);

        var ev = new FleetBatteryEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FleetBatteryId = entity.Id,
            VehicleAssetId = entity.VehicleAssetId,
            EventAtUtc = now,
            EventType = string.Equals(status, "Installed", StringComparison.OrdinalIgnoreCase) ? "Installed" : "Created",
            FromPosition = null,
            ToPosition = entity.Position,
            FromStatus = null,
            ToStatus = entity.Status,
            CostAmount = (dto.CostAmount.HasValue && dto.CostAmount.Value > 0) ? Math.Round(dto.CostAmount.Value, 2, MidpointRounding.AwayFromZero) : null,
            CurrencyCode = currencyCode,
            Notes = entity.Notes,
            CreatedAt = now,
            CreatedById = userId,
        };

        await _unitOfWork.Repository<FleetBatteryEvent>().AddAsync(ev);
        await UpsertBatteryCostEntryAsync(entity.VehicleAssetId, ev, userId, now);

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
        var currencyCode = await MaintenanceCurrencyResolver.ResolveCurrencyCodeAsync(_unitOfWork, tenantId, dto.CurrencyCode);

        var repo = _unitOfWork.Repository<FleetBattery>();
        var entity = await repo.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id && !b.IsDeleted)
            ?? throw new ArgumentException("Battery not found.");

        var fromPosition = entity.Position;
        var fromStatus = entity.Status;

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

        if (string.Equals(entity.Status, "Installed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(entity.Position))
        {
            await EnsureNoOtherInstalledAtPositionAsync(entity.VehicleAssetId, entity.Position, excludeBatteryId: entity.Id);
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
            hasCost)
        {
            var ev = new FleetBatteryEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FleetBatteryId = entity.Id,
                VehicleAssetId = entity.VehicleAssetId,
                EventAtUtc = now,
                EventType = hasCost && string.Equals(fromPosition, entity.Position, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(fromStatus, entity.Status, StringComparison.OrdinalIgnoreCase)
                    ? "CostRecorded"
                    : ResolveEventType(fromPosition, entity.Position, fromStatus, entity.Status),
                FromPosition = fromPosition,
                ToPosition = entity.Position,
                FromStatus = fromStatus,
                ToStatus = entity.Status,
                CostAmount = hasCost ? Math.Round(dto.CostAmount!.Value, 2, MidpointRounding.AwayFromZero) : null,
                CurrencyCode = currencyCode,
                Notes = entity.Notes,
                CreatedAt = now,
                CreatedById = userId,
            };

            await _unitOfWork.Repository<FleetBatteryEvent>().AddAsync(ev);
            await UpsertBatteryCostEntryAsync(entity.VehicleAssetId, ev, userId, now);
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

    private async Task EnsureNoOtherInstalledAtPositionAsync(Guid vehicleAssetId, string position, Guid? excludeBatteryId)
    {
        var tenantId = _currentUserProvider.TenantId;

        var q = _unitOfWork.Repository<FleetBattery>()
            .GetQueryable(b =>
                b.TenantId == tenantId &&
                b.VehicleAssetId == vehicleAssetId &&
                !b.IsDeleted &&
                b.Status == "Installed" &&
                b.Position != null &&
                b.Position == position);

        if (excludeBatteryId.HasValue && excludeBatteryId.Value != Guid.Empty)
        {
            q = q.Where(b => b.Id != excludeBatteryId.Value);
        }

        var exists = await q.AnyAsync();
        if (exists)
            throw new InvalidOperationException($"Position '{position}' already has an installed battery for this vehicle.");
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
            return "Moved";
        }

        return "Updated";
    }

    private async Task UpsertBatteryCostEntryAsync(Guid vehicleAssetId, FleetBatteryEvent ev, Guid userId, DateTime nowUtc)
    {
        if (ev.CostAmount == null || ev.CostAmount.Value <= 0) return;

        var repo = _unitOfWork.Repository<FleetCostEntry>();
        var existing = await repo.FirstOrDefaultAsync(c =>
            c.TenantId == ev.TenantId &&
            c.FleetBatteryEventId == ev.Id &&
            !c.IsDeleted);

        if (existing == null)
        {
            await repo.AddAsync(new FleetCostEntry
            {
                Id = Guid.NewGuid(),
                TenantId = ev.TenantId,
                VehicleAssetId = vehicleAssetId,
                FleetBatteryEventId = ev.Id,
                CostDateUtc = ev.EventAtUtc,
                CostType = "Battery",
                Source = "BatteryEvent",
                Amount = ev.CostAmount.Value,
                CurrencyCode = ev.CurrencyCode,
                Notes = $"Battery {ev.EventType}: {ev.FleetBatteryId}",
                CreatedAt = nowUtc,
                CreatedById = userId
            });
        }
        else
        {
            existing.VehicleAssetId = vehicleAssetId;
            existing.CostDateUtc = ev.EventAtUtc;
            existing.CostType = "Battery";
            existing.Amount = ev.CostAmount.Value;
            existing.CurrencyCode = ev.CurrencyCode;
            existing.Notes = $"Battery {ev.EventType}: {ev.FleetBatteryId}";
            existing.Source = "BatteryEvent";
            existing.UpdatedAt = nowUtc;
            existing.LastModifiedById = userId;
            await repo.UpdateAsync(existing);
        }
    }
}


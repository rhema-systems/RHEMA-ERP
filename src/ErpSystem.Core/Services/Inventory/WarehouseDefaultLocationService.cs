using System.Data;
using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Assigns previously unlocated balances to the configured bin. This does not receive,
/// issue or transfer stock: item and warehouse quantities and values are unchanged.
/// </summary>
public sealed class WarehouseDefaultLocationService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUser)
    : IWarehouseDefaultLocationService
{
    private Guid TenantId => currentUser.TenantId != Guid.Empty ? currentUser.TenantId
        : throw new UnauthorizedAccessException("A tenant is required for default-bin assignment.");

    public Task<WarehouseLocation> GetOrCreateAsync(Guid warehouseId, Guid actorId, CancellationToken cancellationToken = default) =>
        InWarehouseTransactionAsync(warehouseId, actorId, () => ResolveAsync(warehouseId, actorId, cancellationToken), cancellationToken);

    public Task<WarehouseLocation> EnsureItemAssignmentAsync(Guid warehouseId, Guid inventoryItemId, Guid actorId,
        CancellationToken cancellationToken = default) => InWarehouseTransactionAsync(warehouseId, actorId, async () =>
    {
        var location = await ResolveAsync(warehouseId, actorId, cancellationToken);
        var item = await unitOfWork.Repository<InventoryItem>().GetQueryable(x => x.Id == inventoryItemId &&
            x.TenantId == TenantId && !x.IsDeleted).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The inventory item is not in the current tenant.");
        var warehouseQuantity = await unitOfWork.Repository<WarehouseQuantity>().GetQueryable(x =>
            x.TenantId == TenantId && x.InventoryItemId == inventoryItemId && x.WarehouseId == warehouseId && !x.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Assign the item to the warehouse before assigning its default bin.");
        var locations = await unitOfWork.Repository<WarehouseLocation>().GetQueryable(x =>
                x.TenantId == TenantId && !x.IsDeleted &&
                ((!x.IsConsignmentBin && x.WarehouseId == warehouseId) ||
                 (x.IsConsignmentBin && x.ConsignmentWarehouseId == warehouseId)))
            .Select(x => x.Id).ToListAsync(cancellationToken);
        var balances = await unitOfWork.Repository<InventoryLocation>().GetQueryable(x => x.TenantId == TenantId &&
            x.InventoryItemId == inventoryItemId && locations.Contains(x.LocationId) && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var residual = warehouseQuantity.CurrentStock - balances.Sum(x => x.Quantity);
        var allocatedResidual = warehouseQuantity.AllocatedStock - balances.Sum(x => x.AllocatedQuantity);
        if (residual < 0 || allocatedResidual < 0 || allocatedResidual > residual)
            throw new InvalidOperationException($"The warehouse and location balances for {item.ItemCode} do not reconcile. Review the balances before assigning the default bin.");
        var target = balances.SingleOrDefault(x => x.LocationId == location.Id);
        if (target == null && residual == 0 && allocatedResidual == 0 && balances.Count > 0) return location;
        var before = new { WarehouseQuantity = warehouseQuantity.CurrentStock, WarehouseAllocated = warehouseQuantity.AllocatedStock,
            LocatedQuantity = balances.Sum(x => x.Quantity), TargetQuantity = target?.Quantity ?? 0, TargetAllocated = target?.AllocatedQuantity ?? 0 };
        var wasNew = target == null;
        if (residual > 0)
            await RelocateValuationAsync(warehouseId, item, location, residual, actorId, cancellationToken);
        if (target == null)
        {
            target = new InventoryLocation { TenantId = TenantId, InventoryItemId = inventoryItemId,
                LocationId = location.Id, CreatedById = actorId, AverageCost = warehouseQuantity.AverageCost };
            await unitOfWork.Repository<InventoryLocation>().AddAsync(target);
        }
        target.Quantity += residual;
        target.AllocatedQuantity += allocatedResidual;
        target.AvailableQuantity = target.Quantity - target.AllocatedQuantity;
        target.LastModifiedById = actorId;
        target.UpdatedAt = DateTime.UtcNow;
        if (wasNew || residual != 0 || allocatedResidual != 0)
            await AuditAsync("Inventory.DefaultBinAssigned", actorId, inventoryItemId, before,
                new { WarehouseId = warehouseId, InventoryItemId = inventoryItemId, LocationId = location.Id, location.LocationCode,
                    AssignedQuantity = residual, AssignedAllocated = allocatedResidual, target.Quantity, target.AvailableQuantity,
                    WarehouseQuantityUnchanged = warehouseQuantity.CurrentStock, NoStockMovementOrFinancePosting = true });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return location;
    }, cancellationToken);

    public async Task SetDefaultAsync(WarehouseLocation location, Guid actorId, CancellationToken cancellationToken = default)
    {
        await InWarehouseTransactionAsync(location.WarehouseId, actorId, async () =>
        {
            await WarehouseAsync(location.WarehouseId, cancellationToken);
            if (location.TenantId != TenantId || !IsEligible(location))
                throw new InvalidOperationException("The default must be an active normal Bin in this warehouse, not a consignment or special-purpose location.");
            var others = await unitOfWork.Repository<WarehouseLocation>().GetQueryable(x =>
                x.TenantId == TenantId && x.WarehouseId == location.WarehouseId && x.IsDefault && !x.IsDeleted && x.Id != location.Id)
                .ToListAsync(cancellationToken);
            var oldDefaultIds = others.Select(x => x.Id).Concat(location.IsDefault ? [location.Id] : []).ToList();
            // Flush the old default first: the filtered unique index forbids an intermediate pair.
            location.IsDefault = false;
            foreach (var other in others) { other.IsDefault = false; other.LastModifiedById = actorId; other.UpdatedAt = DateTime.UtcNow; }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            location.IsDefault = true;
            location.LastModifiedById = actorId;
            location.UpdatedAt = DateTime.UtcNow;
            if (oldDefaultIds.Count != 1 || oldDefaultIds[0] != location.Id)
                await AuditAsync("Warehouse.DefaultBinChanged", actorId, location.WarehouseId,
                    new { DefaultLocationIds = oldDefaultIds }, new { DefaultLocationId = location.Id, location.LocationCode, ExistingStockUnchanged = true });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return location;
        }, cancellationToken);
    }

    public static bool IsEligible(WarehouseLocation location) => location.IsActive && !location.IsDeleted &&
        string.Equals(location.LocationType, "Bin", StringComparison.OrdinalIgnoreCase) &&
        !location.IsConsignmentBin && !location.ConsignmentWarehouseId.HasValue &&
        !location.IsQuarantineLocation && !location.IsInspectionLocation && !location.IsInTransitLocation &&
        !location.IsShippingLocation && !location.IsStagingLocation && !location.IsReturnLocation && !location.IsDamageLocation;

    private async Task<WarehouseLocation> ResolveAsync(Guid warehouseId, Guid actorId, CancellationToken cancellationToken)
    {
        var warehouse = await WarehouseAsync(warehouseId, cancellationToken);
        var locations = await unitOfWork.Repository<WarehouseLocation>().GetQueryable(x =>
            x.TenantId == TenantId && x.WarehouseId == warehouseId && !x.IsDeleted).ToListAsync(cancellationToken);
        var current = locations.SingleOrDefault(x => x.IsDefault);
        if (current != null)
        {
            if (!IsEligible(current)) throw new InvalidOperationException("The warehouse default bin is inactive or unsuitable. Choose an active normal bin in warehouse setup.");
            return current;
        }
        var eligible = locations.Where(IsEligible).ToList();
        var chosen = eligible.Count == 1 ? eligible[0] : new WarehouseLocation
        {
            TenantId = TenantId, WarehouseId = warehouseId,
            LocationCode = locations.Any(x => string.Equals(x.LocationCode, "DEFAULT", StringComparison.OrdinalIgnoreCase))
                ? $"DEFAULT-{Guid.NewGuid():N}"[..16] : "DEFAULT", Name = "Default bin", LocationType = "Bin",
            IsActive = true, IsPickingLocation = true, IsReceivingLocation = true, CreatedById = actorId,
            Description = $"Default storage bin for {warehouse.Name}."
        };
        if (eligible.Count != 1) await unitOfWork.Repository<WarehouseLocation>().AddAsync(chosen);
        chosen.IsDefault = true;
        chosen.LastModifiedById = actorId;
        chosen.UpdatedAt = DateTime.UtcNow;
        await AuditAsync("Warehouse.DefaultBinConfigured", actorId, warehouseId, null,
            new { DefaultLocationId = chosen.Id, chosen.LocationCode, Created = eligible.Count != 1, ExistingStockUnchanged = true });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return chosen;
    }

    private async Task<Warehouse> WarehouseAsync(Guid warehouseId, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<Warehouse>().GetQueryable(x => x.Id == warehouseId && x.TenantId == TenantId &&
            !x.IsDeleted && x.IsActive).SingleOrDefaultAsync(cancellationToken)
        ?? throw new InvalidOperationException("Select an active warehouse in the current tenant.");

    private async Task RelocateValuationAsync(Guid warehouseId, InventoryItem item, WarehouseLocation location,
        decimal residual, Guid actorId, CancellationToken cancellationToken)
    {
        var balances = await unitOfWork.Repository<InventoryBalance>().GetQueryable(x => x.TenantId == TenantId &&
            x.WarehouseId == warehouseId && x.InventoryItemId == item.Id && !x.IsDeleted &&
            (!x.LocationId.HasValue || x.LocationId == location.Id)).ToListAsync(cancellationToken);
        var source = balances.SingleOrDefault(x => !x.LocationId.HasValue);
        var target = balances.SingleOrDefault(x => x.LocationId == location.Id);
        var layers = await unitOfWork.Repository<InventoryLayer>().GetQueryable(x => x.TenantId == TenantId &&
            x.WarehouseId == warehouseId && x.InventoryItemId == item.Id && !x.IsDeleted && x.IsActive &&
            !x.LocationId.HasValue && !x.IsFullyConsumed && x.RemainingQuantity > 0).ToListAsync(cancellationToken);
        if ((source != null && source.QuantityOnHand != residual) ||
            (layers.Count > 0 && layers.Sum(x => x.RemainingQuantity) != residual))
            throw new InvalidOperationException($"The unlocated valuation balance for {item.ItemCode} does not match its unlocated warehouse stock. Reconcile its valuation before assigning the default bin; no stock quantities have changed.");
        var originalSource = source == null ? null : new { source.Id, source.LocationId, source.QuantityOnHand, source.TotalValue, source.QuantityAllocated };
        // Legacy operational balances may have no valuation record at all. Do not invent
        // financial value merely to supply a bin; keep that separate valuation gap visible.
        if (source != null && target == null) source.LocationId = location.Id;
        else if (source != null && target != null)
        {
            target.QuantityOnHand += source.QuantityOnHand;
            target.QuantityAllocated += source.QuantityAllocated;
            target.QuantityAvailable = target.QuantityOnHand - target.QuantityAllocated;
            target.QuantityOnOrder += source.QuantityOnOrder;
            target.TotalValue += source.TotalValue;
            target.AverageUnitCost = target.QuantityOnHand == 0 ? 0 : target.TotalValue / target.QuantityOnHand;
            target.LastModifiedById = actorId; target.UpdatedAt = DateTime.UtcNow;
            source.QuantityOnHand = 0; source.QuantityAllocated = 0; source.QuantityAvailable = 0;
            source.QuantityOnOrder = 0; source.TotalValue = 0; source.AverageUnitCost = 0;
        }
        if (source != null) { source.LastModifiedById = actorId; source.UpdatedAt = DateTime.UtcNow; }
        foreach (var layer in layers) { layer.LocationId = location.Id; layer.LastModifiedById = actorId; layer.UpdatedAt = DateTime.UtcNow; }
        if (source != null || layers.Count > 0)
            await AuditAsync("Inventory.DefaultBinValuationMapped", actorId, item.Id, originalSource,
                new { WarehouseId = warehouseId, LocationId = location.Id, SourceBalanceId = source?.Id,
                    TargetBalanceId = target?.Id ?? source?.Id, LayerIds = layers.Select(x => x.Id),
                    QuantityAndValueUnchanged = true, NoFinancePosting = true });
    }

    private Task<AuditLog> AuditAsync(string action, Guid actorId, Guid resourceId, object? before, object after) =>
        unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = TenantId, UserId = actorId, Username = currentUser.Username ?? actorId.ToString(),
            Action = action, Resource = "WarehouseDefaultLocation", ResourceId = resourceId.ToString(),
            OldValues = before == null ? null : JsonSerializer.Serialize(before), NewValues = JsonSerializer.Serialize(after),
            IpAddress = "application", Timestamp = DateTime.UtcNow
        });

    private async Task<T> InWarehouseTransactionAsync<T>(Guid warehouseId, Guid actorId, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || actorId != currentUser.UserId)
            throw new UnauthorizedAccessException("Default-bin changes must use the current authenticated actor.");
        if (unitOfWork.HasActiveTransaction)
        {
            await unitOfWork.AcquireTransactionLockAsync($"warehouse-default:{TenantId:N}:{warehouseId:N}", cancellationToken);
            return await action();
        }
        return await unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await unitOfWork.AcquireTransactionLockAsync($"warehouse-default:{TenantId:N}:{warehouseId:N}", cancellationToken);
                var result = await action();
                await unitOfWork.CommitAsync(cancellationToken);
                return result;
            }
            catch { await unitOfWork.RollbackAsync(cancellationToken); throw; }
        }, cancellationToken);
    }
}

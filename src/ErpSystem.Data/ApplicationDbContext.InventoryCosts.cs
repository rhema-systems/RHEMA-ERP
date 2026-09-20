using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ErpSystem.Data;

public partial class ApplicationDbContext
{
    private bool HasInventoryCostProjectionChanges()
    {
        ChangeTracker.DetectChanges();
        return ChangeTracker.Entries<InventoryBalance>().Any(IsValuationBalanceChange);
    }

    private static bool IsValuationBalanceChange(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<InventoryBalance> entry)
        => entry.State is EntityState.Added or EntityState.Deleted || entry.State == EntityState.Modified &&
            (entry.Property(value => value.QuantityOnHand).OriginalValue != entry.Entity.QuantityOnHand ||
             entry.Property(value => value.TotalValue).OriginalValue != entry.Entity.TotalValue ||
             entry.Property(value => value.IsDeleted).OriginalValue != entry.Entity.IsDeleted);

    /// <summary>
    /// Current display averages are projections of the valuation balances, not a
    /// second valuation ledger. Run before the same save that writes those balances
    /// so a receipt, issue, return, adjustment or value-only landed cost cannot leave
    /// an older item/warehouse/bin average behind. Never writes quantities or history.
    /// </summary>
    private async Task SynchronizeInventoryAverageCostsAsync(bool asynchronous, CancellationToken token)
    {
        ChangeTracker.DetectChanges();
        var changedBalances = ChangeTracker.Entries<InventoryBalance>()
            .Where(IsValuationBalanceChange)
            .ToList();
        if (changedBalances.Count == 0) return;

        foreach (var tenantGroup in changedBalances.GroupBy(entry => entry.Entity.TenantId).OrderBy(group => group.Key))
        {
            var tenantId = tenantGroup.Key;
            if (tenantId == Guid.Empty || (_tenantId.HasValue && _tenantId.Value != tenantId))
                throw new InvalidOperationException("Inventory valuation must belong to the current tenant.");
            var itemIds = tenantGroup.Select(entry => entry.Entity.InventoryItemId).Distinct().ToArray();
            if (Database.IsSqlServer())
            {
                // Serialize the read/derive/write across warehouses for one item.
                // This joins the existing stock-owner transaction. SaveChanges owns
                // a short transaction only when the caller did not start one.
                foreach (var itemId in itemIds.OrderBy(value => value))
                {
                    FormattableString sql = $"DECLARE @lockedItem uniqueidentifier; SELECT @lockedItem=Id FROM dbo.InventoryItems WITH(UPDLOCK,HOLDLOCK) WHERE TenantId={tenantId} AND Id={itemId};";
                    if (asynchronous) await Database.ExecuteSqlInterpolatedAsync(sql, token);
                    else Database.ExecuteSqlInterpolated(sql);
                }
            }
            // Database reads alone miss Added rows and return the previous value of
            // Modified rows. Overlay the tracker exactly once by primary key.
            var persisted = await ReadCostRowsAsync(Set<InventoryBalance>().IgnoreQueryFilters().AsNoTracking()
                .Where(value => value.TenantId == tenantId && itemIds.Contains(value.InventoryItemId)), asynchronous, token);
            var balancesById = persisted.ToDictionary(value => value.Id);
            foreach (var entry in ChangeTracker.Entries<InventoryBalance>().Where(entry =>
                         entry.Entity.TenantId == tenantId && itemIds.Contains(entry.Entity.InventoryItemId) &&
                         entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                balancesById.Remove(entry.Entity.Id);
                if (entry.State != EntityState.Deleted && !entry.Entity.IsDeleted)
                    balancesById.Add(entry.Entity.Id, entry.Entity);
            }
            var balances = balancesById.Values.Where(value => !value.IsDeleted).ToList();
            var transit = await ReadInventoryTransitCostsAsync(tenantId, itemIds, asynchronous, token);
            var warehouseIds = balances.Select(value => value.WarehouseId)
                .Concat(tenantGroup.Select(entry => entry.Entity.WarehouseId))
                .Concat(transit.Select(value => value.WarehouseId)).Distinct().ToArray();
            var warehouses = await ReadCostRowsAsync(Set<Warehouse>().IgnoreQueryFilters().AsNoTracking()
                .Where(value => value.TenantId == tenantId && warehouseIds.Contains(value.Id)), asynchronous, token);
            var warehouseOwners = MergeCostOwners(warehouses, tenantId).ToDictionary(value => value.Id);
            if (transit.Any(value => !warehouseOwners.ContainsKey(value.WarehouseId)))
                throw new InvalidOperationException("Transfer carrying value has missing warehouse ownership; current costs were not changed.");
            var items = MergeCostOwners(await ReadCostRowsAsync(Set<InventoryItem>().IgnoreQueryFilters()
                .Where(value => value.TenantId == tenantId && itemIds.Contains(value.Id)), asynchronous, token), tenantId)
                .Where(value => itemIds.Contains(value.Id) && !value.IsDeleted).ToList();
            var warehouseQuantities = MergeCostOwners(await ReadCostRowsAsync(Set<WarehouseQuantity>().IgnoreQueryFilters()
                .Where(value => value.TenantId == tenantId && itemIds.Contains(value.InventoryItemId)), asynchronous, token), tenantId)
                .Where(value => itemIds.Contains(value.InventoryItemId) && !value.IsDeleted).ToList();
            var locations = MergeCostOwners(await ReadCostRowsAsync(Set<InventoryLocation>().IgnoreQueryFilters()
                .Where(value => value.TenantId == tenantId && itemIds.Contains(value.InventoryItemId)), asynchronous, token), tenantId)
                .Where(value => itemIds.Contains(value.InventoryItemId) && !value.IsDeleted).ToList();

            foreach (var itemId in itemIds)
            {
                var itemBalances = balances.Where(value => value.InventoryItemId == itemId).ToList();
                foreach (var scope in itemBalances.GroupBy(value => value.WarehouseId))
                {
                    // Do not guess whether an old unlocated row is a duplicate
                    // warehouse total or additional stock. That needs a reviewed
                    // reconciliation, not silent double-counting during posting.
                    if (scope.Any(value => value.LocationId.HasValue) && scope.Any(value =>
                            !value.LocationId.HasValue && (value.QuantityOnHand != 0 || value.TotalValue != 0)))
                        throw new InvalidOperationException(
                            "Inventory valuation contains both exact-bin and nonzero unlocated balances. Reconcile that warehouse before posting; no stock or historical costs were changed.");
                }
                foreach (var changed in tenantGroup.Where(entry => entry.Entity.InventoryItemId == itemId &&
                             entry.State != EntityState.Deleted && !entry.Entity.IsDeleted))
                    changed.Entity.AverageUnitCost = CurrentAverage(new[] { changed.Entity });
                foreach (var owner in warehouseQuantities.Where(value => value.InventoryItemId == itemId))
                {
                    var scope = itemBalances.Where(value => value.WarehouseId == owner.WarehouseId).ToList();
                    // Do not reset unrelated legacy assignments without a valuation
                    // balance. A deleted last balance, however, has a known zero value.
                    if (scope.Count != 0 || tenantGroup.Any(entry => entry.Entity.InventoryItemId == itemId &&
                            entry.Entity.WarehouseId == owner.WarehouseId))
                        owner.AverageCost = CurrentAverage(scope, typeof(WarehouseQuantity));
                }
                foreach (var owner in locations.Where(value => value.InventoryItemId == itemId))
                {
                    var scope = itemBalances.Where(value => value.LocationId == owner.LocationId).ToList();
                    if (scope.Count != 0 || tenantGroup.Any(entry => entry.Entity.InventoryItemId == itemId &&
                            entry.Entity.LocationId == owner.LocationId))
                        owner.AverageCost = CurrentAverage(scope, typeof(InventoryLocation));
                }
                var item = items.SingleOrDefault(value => value.Id == itemId);
                // Missing warehouse metadata cannot be interpreted as owned stock.
                // Production foreign keys normally make this impossible; retain the
                // current item projection until ownership is known for every scope.
                if (item != null && itemBalances.All(value => warehouseOwners.ContainsKey(value.WarehouseId)))
                {
                    var ownedTransit = transit.Where(value => value.ItemId == itemId &&
                        warehouseOwners.TryGetValue(value.WarehouseId, out var warehouse) && !warehouse.IsConsignmentWarehouse).ToList();
                    item.AverageCost = CurrentAverage(itemBalances.Where(value =>
                        !warehouseOwners[value.WarehouseId].IsConsignmentWarehouse), typeof(InventoryItem),
                        ownedTransit.Sum(value => value.Quantity), ownedTransit.Sum(value => value.Value));
                }
            }
        }
    }

    private IEnumerable<T> MergeCostOwners<T>(IEnumerable<T> persisted, Guid tenantId) where T : TenantEntity
    {
        var owners = persisted.ToDictionary(value => value.Id);
        foreach (var entry in ChangeTracker.Entries<T>().Where(entry => entry.Entity.TenantId == tenantId &&
                     entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            owners.Remove(entry.Entity.Id);
            if (entry.State != EntityState.Deleted) owners[entry.Entity.Id] = entry.Entity;
        }
        return owners.Values;
    }

    private decimal CurrentAverage(IEnumerable<InventoryBalance> balances, Type? ownerType = null,
        decimal transitQuantity = 0, decimal transitValue = 0)
    {
        // Use the effective EF mapping, not the overridden entity annotations.
        // ConfigureDecimalPrecision maps live value/average columns to scale 2
        // and quantities to scale 4. Derive from values SQL will actually persist.
        var rows = balances.ToList();
        var quantityScale = CostColumnScale(typeof(InventoryBalance), nameof(InventoryBalance.QuantityOnHand));
        var valueScale = CostColumnScale(typeof(InventoryBalance), nameof(InventoryBalance.TotalValue));
        var averageScale = CostColumnScale(ownerType ?? typeof(InventoryBalance),
            ownerType == null ? nameof(InventoryBalance.AverageUnitCost) : nameof(InventoryItem.AverageCost));
        var quantity = transitQuantity + rows.Sum(value => decimal.Round(value.QuantityOnHand, quantityScale, MidpointRounding.AwayFromZero));
        var totalValue = transitValue + rows.Sum(value => decimal.Round(value.TotalValue, valueScale, MidpointRounding.AwayFromZero));
        return quantity > 0 ? decimal.Round(totalValue / quantity, averageScale, MidpointRounding.AwayFromZero) : 0;
    }

    private int CostColumnScale(Type ownerType, string propertyName)
    {
        // Runtime models may trim relational annotations, and GetColumnType()
        // asks the provider for a RelationalTypeMapping (invalid for InMemory).
        // The configured design model retains the explicit precision contract
        // without requiring any provider-specific mapping or database access.
        var property = this.GetService<IDesignTimeModel>().Model.FindEntityType(ownerType)?.FindProperty(propertyName)
            ?? throw new InvalidOperationException("Inventory cost projection mapping was not found.");
        var configuredType = property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value as string;
        var match = System.Text.RegularExpressions.Regex.Match(configuredType ?? string.Empty,
            @"\(\s*\d+\s*,\s*(\d+)\s*\)");
        if (match.Success) return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        return property.GetScale() ?? throw new InvalidOperationException("Inventory cost projection requires an explicit decimal scale.");
    }

    private static Task<List<T>> ReadCostRowsAsync<T>(IQueryable<T> query, bool asynchronous, CancellationToken token)
        => asynchronous ? query.ToListAsync(token) : Task.FromResult(query.ToList());
}

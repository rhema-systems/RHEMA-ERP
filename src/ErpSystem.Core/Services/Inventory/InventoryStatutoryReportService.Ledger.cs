using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public sealed partial class InventoryStatutoryReportService
{
    private async Task<ReportResultDto> ExecuteLedgerAsync(
        InventorySystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var asOf = DateTime.UtcNow;
        // Historical item/warehouse descriptions remain visible after a master is retired.
        // Tenant and posted-ledger restrictions are explicit after disabling navigation filters.
        var query = Query<InventoryMovement>().IgnoreQueryFilters().Where(m =>
            m.TenantId == _currentUser.TenantId && !m.IsDeleted && m.IsPosted &&
            m.InventoryItem.TenantId == _currentUser.TenantId && m.Warehouse.TenantId == _currentUser.TenantId &&
            (!m.PostedAt.HasValue || m.PostedAt <= asOf));
        if (filters.EndExclusiveUtc.HasValue) query = query.Where(m => m.MovementDate < filters.EndExclusiveUtc.Value);
        if (filters.WarehouseId.HasValue) query = query.Where(m => m.WarehouseId == filters.WarehouseId.Value);
        if (filters.LocationId.HasValue) query = query.Where(m => m.LocationId == filters.LocationId.Value);
        if (filters.InventoryItemId.HasValue) query = query.Where(m => m.InventoryItemId == filters.InventoryItemId.Value);
        if (!string.IsNullOrWhiteSpace(filters.ItemCode)) query = query.Where(m => m.InventoryItem.ItemCode == filters.ItemCode);

        var scopes = await query.Select(m => new InventoryScope(m.WarehouseId, m.LocationId)).Distinct().ToListAsync(cancellationToken);
        var allowed = await ReadableScopesAsync(scopes, definition.Code, cancellationToken);
        if (scopes.Count > 0 && allowed.Count == 0)
            throw new UnauthorizedAccessException("The current actor has no assigned inventory scope for this ledger.");
        query = query.Where(BuildScopePredicate<InventoryMovement>(allowed));

        var balances = new Dictionary<LedgerBalanceKey, decimal>();
        if (filters.StartUtc.HasValue)
        {
            var opening = await query.Where(m => m.MovementDate < filters.StartUtc.Value)
                .GroupBy(m => new { m.InventoryItemId, m.WarehouseId, m.LocationId })
                .Select(group => new
                {
                    group.Key.InventoryItemId, group.Key.WarehouseId, group.Key.LocationId,
                    Balance = group.Sum(m => m.Direction == MovementDirection.Out ? -m.Quantity : m.Quantity)
                }).ToListAsync(cancellationToken);
            foreach (var item in opening)
                balances[new(item.InventoryItemId, item.WarehouseId, item.LocationId)] = item.Balance;
            query = query.Where(m => m.MovementDate >= filters.StartUtc.Value);
        }

        // Type filtering and pagination happen after balance calculation, never before it.
        var movements = await query.Include(m => m.InventoryItem).Include(m => m.Warehouse)
            .Include(m => m.Location).Include(m => m.PostedBy).Include(m => m.CreatedBy)
            .OrderBy(m => m.MovementDate).ThenBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Take(20001).ToListAsync(cancellationToken);
        if (movements.Count > 20000)
            throw new InvalidOperationException("The inventory ledger exceeds 20,000 movements. Narrow the date range, item or warehouse; balances will still include earlier movements.");

        InventoryMovementType? selectedType = null;
        if (!string.IsNullOrWhiteSpace(filters.MovementType))
        {
            if (!Enum.TryParse<InventoryMovementType>(filters.MovementType, true, out var parsed) || !Enum.IsDefined(parsed))
                throw new InvalidOperationException("Choose a valid posted inventory transaction type.");
            selectedType = parsed;
        }

        var rows = new List<Dictionary<string, object>>();
        foreach (var movement in movements)
        {
            var key = new LedgerBalanceKey(movement.InventoryItemId, movement.WarehouseId, movement.LocationId);
            var before = balances.GetValueOrDefault(key);
            var quantityIn = movement.Direction == MovementDirection.In ? movement.Quantity : 0m;
            var quantityOut = movement.Direction == MovementDirection.Out ? movement.Quantity : 0m;
            var after = before + quantityIn - quantityOut;
            balances[key] = after;
            if (selectedType.HasValue && movement.MovementType != selectedType.Value) continue;
            var actor = movement.PostedBy ?? movement.CreatedBy;
            rows.Add(Row(
                ("MovementDate", movement.MovementDate), ("MovementNumber", movement.MovementNumber),
                ("ItemCode", movement.InventoryItem.ItemCode), ("ItemName", movement.InventoryItem.Name),
                ("UnitOfMeasure", movement.InventoryItem.UnitOfMeasure), ("WarehouseCode", movement.Warehouse.Code),
                ("WarehouseName", movement.Warehouse.Name), ("LocationCode", movement.Location?.LocationCode),
                ("MovementType", movement.MovementType.ToString()), ("ReferenceType", movement.ReferenceType.ToString()),
                ("ReferenceNumber", movement.ReferenceNumber), ("QuantityIn", quantityIn), ("QuantityOut", quantityOut),
                ("BalanceBefore", before), ("BalanceAfter", after), ("UnitCost", movement.UnitCost),
                ("TotalValue", movement.Direction == MovementDirection.Out ? -movement.TotalValue : movement.TotalValue),
                ("PostedBy", actor == null ? string.Empty : (actor.FirstName + " " + actor.LastName).Trim())));
        }
        return PageRows(rows, definition, request, filters, asOf);
    }

    private sealed record LedgerBalanceKey(Guid InventoryItemId, Guid WarehouseId, Guid? LocationId);
}

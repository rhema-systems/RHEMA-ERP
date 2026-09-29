using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class InventoryValuationService
{
    public async Task<IReadOnlyDictionary<Guid, decimal>> PreviewIssueCostsAsync(
        IReadOnlyList<InventoryIssueCostPreviewLineDto> lines, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Any(x => x.LineId == Guid.Empty || x.Quantity <= 0) || lines.Select(x => x.LineId).Distinct().Count() != lines.Count)
            throw new InvalidOperationException("Issue preview requires unique line identifiers and positive quantities.");
        var tenantId = _currentUserProvider.TenantId;
        var bins = new Dictionary<(Guid Item, Guid Warehouse, Guid? Location), IssuePreviewBin>();
        var result = new Dictionary<Guid, decimal>();
        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = (line.InventoryItemId, line.WarehouseId, line.LocationId);
            if (!bins.TryGetValue(key, out var bin))
            {
                var warehouse = await _unitOfWork.Repository<Warehouse>().GetQueryable(x => x.TenantId == tenantId &&
                    x.Id == line.WarehouseId && !x.IsDeleted && x.IsActive).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("The issue warehouse is unavailable in this tenant.");
                if (InventoryTransitProtection.IsProtected(warehouse)) throw new InvalidOperationException(InventoryTransitProtection.Message);
                if (line.LocationId.HasValue)
                {
                    var location = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(x => x.TenantId == tenantId &&
                        x.Id == line.LocationId && !x.IsDeleted && x.IsActive).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                        ?? throw new InvalidOperationException("The issue location is unavailable in this tenant.");
                    if (location.InventoryWarehouseId != line.WarehouseId)
                        throw new InvalidOperationException("The issue location does not belong to the selected stock warehouse.");
                    await InventoryTransitProtection.EnsureOrdinaryStockScopeAsync(_unitOfWork, tenantId,
                        line.WarehouseId, line.LocationId, cancellationToken);
                }
                var item = await _unitOfWork.Repository<InventoryItem>().GetQueryable(x => x.TenantId == tenantId &&
                    x.Id == line.InventoryItemId && !x.IsDeleted).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("The issue item is unavailable in this tenant.");
                if (item.ValuationMethod is not (ValuationMethod.FIFO or ValuationMethod.WeightedAverage or ValuationMethod.StandardCost))
                    throw new InvalidOperationException($"Issue valuation is not supported for {item.ValuationMethod}.");
                var balance = await _unitOfWork.Repository<InventoryBalance>().GetQueryable(x => x.TenantId == tenantId &&
                    x.InventoryItemId == line.InventoryItemId && x.WarehouseId == line.WarehouseId &&
                    x.LocationId == line.LocationId && !x.IsDeleted).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("There is no stock balance in the selected location.");
                bin = new IssuePreviewBin { Method = item.ValuationMethod, StandardCost = item.StandardCost,
                    Quantity = balance.QuantityOnHand, Value = balance.TotalValue };
                if (item.ValuationMethod == ValuationMethod.FIFO)
                {
                    var layers = await _unitOfWork.Repository<InventoryLayer>().GetQueryable(x => x.TenantId == tenantId &&
                        x.InventoryItemId == line.InventoryItemId && x.WarehouseId == line.WarehouseId && x.LocationId == line.LocationId &&
                        !x.IsDeleted && x.IsActive && !x.IsFullyConsumed && x.RemainingQuantity > 0).AsNoTracking()
                        .OrderBy(x => x.LayerDate).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
                    bin.Layers.AddRange(layers.Select(x => new IssuePreviewLayer {
                        Quantity = x.RemainingQuantity, Value = x.RemainingValue, UnitCost = x.UnitCost, LotNumber = x.LotNumber }));
                }
                bins.Add(key, bin);
            }
            if (bin.Quantity < line.Quantity) throw new InvalidOperationException("Insufficient stock in the selected issue location.");
            // Serial availability is governed by tracking. FIFO costs the selected lot's
            // layers; serials are not a separate cost-layer dimension in this ledger.
            decimal cost;
            if (bin.Method == ValuationMethod.FIFO)
            {
                var selectedLot = line.LotNumber?.Trim();
                var layers = string.IsNullOrWhiteSpace(selectedLot) ? bin.Layers
                    : bin.Layers.Where(x => string.Equals(x.LotNumber, selectedLot, StringComparison.OrdinalIgnoreCase)).ToList();
                if (layers.Sum(x => x.Quantity) < line.Quantity)
                    throw new InvalidOperationException("Insufficient inventory layers in the selected stock location.");
                var remaining = line.Quantity; cost = 0;
                foreach (var layer in layers)
                {
                    if (remaining == 0) break;
                    var take = Math.Min(remaining, layer.Quantity);
                    var value = take == layer.Quantity ? layer.Value
                        : Math.Min(layer.Value, decimal.Round(take * layer.UnitCost, 2, MidpointRounding.AwayFromZero));
                    layer.Quantity -= take; layer.Value -= value; remaining -= take; cost += value;
                }
            }
            else if (bin.Method == ValuationMethod.WeightedAverage)
                cost = line.Quantity == bin.Quantity ? bin.Value
                    : decimal.Round(line.Quantity * (bin.Value / bin.Quantity), 2, MidpointRounding.AwayFromZero);
            else cost = line.Quantity * bin.StandardCost;
            bin.Quantity -= line.Quantity; bin.Value -= cost;
            result.Add(line.LineId, cost);
        }
        return result;
    }

    private sealed class IssuePreviewBin
    {
        public ValuationMethod Method { get; init; }
        public decimal StandardCost { get; init; }
        public decimal Quantity { get; set; }
        public decimal Value { get; set; }
        public List<IssuePreviewLayer> Layers { get; } = [];
    }
    private sealed class IssuePreviewLayer
    {
        public decimal Quantity { get; set; }
        public decimal Value { get; set; }
        public decimal UnitCost { get; init; }
        public string? LotNumber { get; init; }
    }
}

using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IConsignmentSettlementService
{
    Task TryCreateFromStockMovementAsync(StockMovement movement);
}


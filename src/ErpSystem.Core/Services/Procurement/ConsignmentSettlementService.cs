using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class ConsignmentSettlementService : IConsignmentSettlementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ConsignmentSettlementService> _logger;

    public ConsignmentSettlementService(
        IUnitOfWork unitOfWork,
        IWarehouseRepository warehouseRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<ConsignmentSettlementService> logger)
    {
        _unitOfWork = unitOfWork;
        _warehouseRepository = warehouseRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task TryCreateFromStockMovementAsync(StockMovement movement)
    {
        if (movement == null)
        {
            return;
        }

        if (!movement.WarehouseId.HasValue || movement.WarehouseId.Value == Guid.Empty)
        {
            return;
        }

        if (movement.Quantity >= 0)
        {
            return;
        }

        var tenantId = movement.TenantId != Guid.Empty ? movement.TenantId : _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning("Cannot create consignment settlement: missing tenant context for movement {MovementId}", movement.Id);
            return;
        }

        var warehouse = await _warehouseRepository.GetByIdAsync(movement.WarehouseId.Value);
        if (warehouse?.IsConsignmentWarehouse != true)
        {
            return;
        }

        var settlementRepo = _unitOfWork.Repository<ConsignmentSettlement>();
        var alreadyCreated = await settlementRepo.ExistsAsync(s => s.TenantId == tenantId && s.StockMovementId == movement.Id);
        if (alreadyCreated)
        {
            return;
        }

        var qty = Math.Abs(movement.Quantity);
        var unitCost = movement.UnitCost;
        var totalValue = movement.TotalValue != 0 ? Math.Abs(movement.TotalValue) : qty * unitCost;

        await settlementRepo.AddAsync(new ConsignmentSettlement
        {
            TenantId = tenantId,
            StockMovementId = movement.Id,
            InventoryItemId = movement.InventoryItemId,
            WarehouseId = movement.WarehouseId.Value,
            LocationId = movement.LocationId,
            MovementType = movement.MovementType,
            Quantity = qty,
            UnitCost = unitCost,
            TotalValue = totalValue,
            ConsumedAt = movement.MovementDate,
            ReferenceType = movement.ReferenceType,
            ReferenceNumber = movement.ReferenceNumber,
            ReferenceId = movement.ReferenceId,
            Notes = movement.Notes
        });

        _logger.LogInformation(
            "Created consignment settlement for movement {MovementId} (Warehouse {WarehouseId}, Item {ItemId}, Qty {Qty})",
            movement.Id, movement.WarehouseId.Value, movement.InventoryItemId, qty);
    }
}

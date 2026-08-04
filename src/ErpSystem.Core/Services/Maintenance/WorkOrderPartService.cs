using System.Data;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

public class WorkOrderPartService : IWorkOrderPartService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryNegativeStockControlService _negativeStockControls;
    private readonly ILogger<WorkOrderPartService> _logger;

    public WorkOrderPartService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryNegativeStockControlService negativeStockControls,
        ILogger<WorkOrderPartService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _warehouseRepository = warehouseRepository;
        _negativeStockControls = negativeStockControls;
        _logger = logger;
    }

    public async Task<WorkOrderPartDto> AddPartAsync(CreateWorkOrderPartDto createDto)
    {
        var tenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("TenantId is required");
        var userIdString = _currentUserService.UserId ?? throw new InvalidOperationException("UserId is required");
        var userId = Guid.Parse(userIdString);

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(createDto.WarehouseId)
                ?? throw new InvalidOperationException($"Warehouse {createDto.WarehouseId} not found");
            var partId = Guid.NewGuid();
            var decreaseAuthorization = await _negativeStockControls.PrepareDecreaseAsync(new InventoryStockDecreaseRequest
            {
                InventoryItemId = createDto.InventoryItemId,
                WarehouseId = createDto.WarehouseId,
                LocationId = createDto.WarehouseLocationId,
                Quantity = createDto.QuantityRequired,
                ReferenceType = "MaintenanceWorkOrderPartReservation",
                ReferenceNumber = $"WO-{createDto.WorkOrderId:N}",
                ReferenceId = createDto.WorkOrderId,
                ReferenceLineId = partId,
                NegativeStockOverrideId = createDto.NegativeStockOverrideId,
                DecreaseCurrentStock = false,
                CheckInventoryItemBalance = false,
                CorrelationId = $"work-order:{createDto.WorkOrderId:N}:part:{partId:N}:reserve"
            });
            var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
                createDto.WarehouseId, createDto.InventoryItemId) ?? throw new InvalidOperationException(
                    $"Item {createDto.InventoryItemId} not found in warehouse {warehouse.Name}");
            if (warehouseQuantity.AvailableStock < createDto.QuantityRequired &&
                !decreaseAuthorization.EmergencyOverrideApplied)
                throw new InvalidOperationException(
                    $"Insufficient stock in {warehouse.Name} for {warehouseQuantity.InventoryItem.ItemCode}. " +
                    $"Available: {warehouseQuantity.AvailableStock}, Required: {createDto.QuantityRequired}");

            var part = new WorkOrderPart
            {
                Id = partId,
                TenantId = tenantId,
                WorkOrderId = createDto.WorkOrderId,
                InventoryItemId = createDto.InventoryItemId,
                ItemCode = warehouseQuantity.InventoryItem.ItemCode,
                ItemName = warehouseQuantity.InventoryItem.Name,
                QuantityRequired = createDto.QuantityRequired,
                QuantityAllocated = createDto.QuantityRequired,
                QuantityUsed = 0,
                QuantityReturned = 0,
                UnitCost = createDto.UnitCost,
                TotalCost = createDto.QuantityRequired * createDto.UnitCost,
                Status = "Allocated",
                Notes = createDto.Notes,
                SerialNumber = createDto.SerialNumber,
                LotNumber = createDto.LotNumber,
                WarehouseLocationId = createDto.WarehouseLocationId,
                AllocatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            // Update warehouse quantity stock levels
            warehouseQuantity.AvailableStock -= createDto.QuantityRequired;
            warehouseQuantity.AllocatedStock += createDto.QuantityRequired;
            warehouseQuantity.LastMovementDate = DateTime.UtcNow;
            warehouseQuantity.UpdatedAt = DateTime.UtcNow;

            // Save part and update warehouse quantity
            var partRepo = _unitOfWork.Repository<WorkOrderPart>();
            await partRepo.AddAsync(part);
            await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);

            // Always create inventory allocation for tracking and consume functionality
            // WarehouseId is required, LocationId is optional (for specific warehouse location)
            var allocation = new InventoryAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                InventoryItemId = createDto.InventoryItemId,
                WarehouseId = createDto.WarehouseId,
                LocationId = createDto.WarehouseLocationId, // Nullable - use if provided
                AllocationType = "WorkOrder",
                ReferenceNumber = $"WO-{createDto.WorkOrderId}",
                ReferenceId = createDto.WorkOrderId,
                AllocatedQuantity = createDto.QuantityRequired,
                ConsumedQuantity = 0,
                RemainingQuantity = createDto.QuantityRequired,
                AllocationDate = DateTime.UtcNow,
                Status = "Active",
                SerialNumber = createDto.SerialNumber,
                LotNumber = createDto.LotNumber,
                Notes = createDto.Notes,
                AllocatedById = userId,
                CreatedAt = DateTime.UtcNow
            };

            var allocationRepo = _unitOfWork.Repository<InventoryAllocation>();
            await allocationRepo.AddAsync(allocation);

            part.AllocationId = allocation.Id;
            await partRepo.UpdateAsync(part);

            if (decreaseAuthorization.EmergencyOverrideApplied)
            {
                await _unitOfWork.SaveChangesAsync();
                await _negativeStockControls.ClearMutationContextAsync();
            }

            if (ownsTransaction) await _unitOfWork.CommitAsync();

            _logger.LogInformation(
                "Allocated {Quantity} units of {ItemCode} from warehouse {Warehouse} for work order {WorkOrderId}",
                createDto.QuantityRequired, warehouseQuantity.InventoryItem.ItemCode,
                warehouse.Name, createDto.WorkOrderId);

            return MapToDto(part);
        }
        catch (Exception ex)
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            _logger.LogError(ex, "Error allocating part for work order {WorkOrderId}", createDto.WorkOrderId);
            throw;
        }
    }

    public async Task<WorkOrderPartDto> UpdatePartAsync(Guid id, UpdateWorkOrderPartDto updateDto)
    {
        var partRepo = _unitOfWork.Repository<WorkOrderPart>();
        var part = await partRepo.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Part with ID {id} not found");
        var oldQuantityUsed = part.QuantityUsed;
        var newQuantityUsed = updateDto.QuantityUsed;

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Update part details
                part.QuantityRequired = updateDto.QuantityRequired;
                part.QuantityUsed = newQuantityUsed;
                part.QuantityReturned = updateDto.QuantityReturned;
                part.UnitCost = updateDto.UnitCost;
                part.TotalCost = updateDto.QuantityRequired * updateDto.UnitCost;
                part.Status = updateDto.Status;
                part.Notes = updateDto.Notes;
                part.SerialNumber = updateDto.SerialNumber;
                part.LotNumber = updateDto.LotNumber;
                part.WarehouseLocationId = updateDto.WarehouseLocationId;
                part.UpdatedAt = DateTime.UtcNow;

                // If quantity used increased, update inventory
                if (newQuantityUsed > oldQuantityUsed && part.AllocationId.HasValue)
                {
                    var quantityConsumed = newQuantityUsed - oldQuantityUsed;
                    await UpdateInventoryConsumptionAsync(part, quantityConsumed, updateDto.NegativeStockOverrideId);
                }

                await partRepo.UpdateAsync(part);
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                throw; // Let ExecuteInStrategyAsync handle retries
            }
        });

        return MapToDto(part);
    }

    public async Task DeletePartAsync(Guid id)
    {
        var partRepo = _unitOfWork.Repository<WorkOrderPart>();
        var part = await partRepo.GetByIdAsync(id);
        if (part == null)
        {
            return;
        }

        // Check if part has been used - if so, don't delete (keep for history)
        if (part.QuantityUsed > 0 || part.Status == "Used" || part.Status == "Returned")
        {
            _logger.LogWarning("Cannot delete part {PartId} - it has been used or returned. Status: {Status}", id, part.Status);
            throw new InvalidOperationException("Cannot delete parts that have been used or returned. This data is needed for work order history.");
        }

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Release allocation if exists
                if (part.AllocationId.HasValue)
                {
                    await ReleaseAllocationAsync(part);
                }

                // Hard delete - only allowed for unused parts (Draft/planning phase)
                await partRepo.HardDeleteAsync(part);
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                throw; // Let ExecuteInStrategyAsync handle retries
            }
        });
    }

    public async Task<IEnumerable<WorkOrderPartDto>> GetPartsByWorkOrderAsync(Guid workOrderId)
    {
        var partRepo = _unitOfWork.Repository<WorkOrderPart>();
        var parts = await partRepo.GetAllAsync();
        var workOrderParts = parts.Where(p => p.WorkOrderId == workOrderId);
        return workOrderParts.Select(MapToDto);
    }

    public async Task<WorkOrderPartDto> UpdatePartStatusAsync(Guid id, string status, int? quantityUsed = null)
    {
        var partRepo = _unitOfWork.Repository<WorkOrderPart>();
        var part = await partRepo.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Part with ID {id} not found");
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var oldQuantityUsed = part.QuantityUsed;

                part.Status = status;
                if (quantityUsed.HasValue)
                {
                    part.QuantityUsed = quantityUsed.Value;

                    // Update inventory consumption if quantity increased
                    if (quantityUsed.Value > oldQuantityUsed && part.AllocationId.HasValue)
                    {
                        var quantityConsumed = quantityUsed.Value - oldQuantityUsed;
                        await UpdateInventoryConsumptionAsync(part, quantityConsumed, null);
                    }
                }

                if (status == "Returned" && part.AllocationId.HasValue)
                {
                    await ReleaseAllocationAsync(part);
                }

                part.UpdatedAt = DateTime.UtcNow;

                await partRepo.UpdateAsync(part);
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                throw; // Let ExecuteInStrategyAsync handle retries
            }
        });

        return MapToDto(part);
    }

    public async Task<decimal> GetTotalPartsCostAsync(Guid workOrderId)
    {
        var partRepo = _unitOfWork.Repository<WorkOrderPart>();
        var parts = await partRepo.GetAllAsync();
        return parts.Where(p => p.WorkOrderId == workOrderId).Sum(p => p.TotalCost);
    }

    public async Task<IEnumerable<WorkOrderPartDto>> GetPartsRequiringOrderAsync()
    {
        // This would typically query parts where quantity required > available
        // For now, return empty list
        return Enumerable.Empty<WorkOrderPartDto>();
    }

    public async Task<WorkOrderPartDto> ReturnUnusedPartsAsync(Guid partId)
    {
        var partRepo = _unitOfWork.Repository<WorkOrderPart>();
        var part = await partRepo.GetByIdAsync(partId) ?? throw new KeyNotFoundException($"Part with ID {partId} not found");
        if (!part.AllocationId.HasValue)
        {
            throw new InvalidOperationException("Part has no allocation to return");
        }

        // Prevent returning multiple times
        if (part.Status == "Returned" || part.QuantityReturned > 0)
        {
            throw new InvalidOperationException("Parts have already been returned");
        }

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var allocationRepo = _unitOfWork.Repository<InventoryAllocation>();
                var allocation = await allocationRepo.GetByIdAsync(part.AllocationId.Value) ?? throw new InvalidOperationException("Allocation not found");

                // Calculate unused quantity (allocated - used)
                var unusedQuantity = part.QuantityAllocated - part.QuantityUsed;

                if (unusedQuantity <= 0)
                {
                    _logger.LogInformation("No unused parts to return for part {PartId}", partId);
                    await _unitOfWork.CommitAsync();
                    return;
                }

                // Get warehouse quantity using WarehouseId from allocation
                var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
                    allocation.WarehouseId, part.InventoryItemId) ?? throw new InvalidOperationException("Warehouse quantity not found");

                // Return unused quantity back to available stock
                warehouseQuantity.AvailableStock += unusedQuantity;
                warehouseQuantity.AllocatedStock -= unusedQuantity;
                warehouseQuantity.LastMovementDate = DateTime.UtcNow;
                warehouseQuantity.UpdatedAt = DateTime.UtcNow;

                // Update allocation
                allocation.RemainingQuantity = 0;
                allocation.Status = allocation.ConsumedQuantity > 0 ? "PartiallyUsed" : "Returned";
                allocation.UpdatedAt = DateTime.UtcNow;

                // Update part
                part.QuantityReturned = unusedQuantity;
                part.Status = "Returned";
                part.UpdatedAt = DateTime.UtcNow;

                await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);
                await allocationRepo.UpdateAsync(allocation);
                await partRepo.UpdateAsync(part);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation(
                    "Returned {Quantity} unused units of {ItemCode} to warehouse for part {PartId}",
                    unusedQuantity, warehouseQuantity.InventoryItem.ItemCode, partId);
            }
            catch
            {
                throw; // Let ExecuteInStrategyAsync handle retries
            }
        });

        return MapToDto(part);
    }

    private async Task UpdateInventoryConsumptionAsync(WorkOrderPart part, decimal quantityConsumed, Guid? negativeStockOverrideId)
    {
        if (part.AllocationId.HasValue)
        {
            // Has InventoryAllocation - update it along with warehouse quantities
            var allocationRepo = _unitOfWork.Repository<InventoryAllocation>();
            var allocation = await allocationRepo.GetByIdAsync(part.AllocationId.Value);
            if (allocation == null)
            {
                _logger.LogWarning("Allocation {AllocationId} not found for part {PartId}", part.AllocationId, part.Id);
                return;
            }

            var decreaseAuthorization = await _negativeStockControls.PrepareDecreaseAsync(new InventoryStockDecreaseRequest
            {
                InventoryItemId = part.InventoryItemId,
                WarehouseId = allocation.WarehouseId,
                LocationId = allocation.LocationId,
                Quantity = quantityConsumed,
                ReferenceType = "MaintenanceWorkOrderPartConsumption",
                ReferenceNumber = $"WO-{part.WorkOrderId:N}",
                ReferenceId = part.WorkOrderId,
                ReferenceLineId = part.Id,
                NegativeStockOverrideId = negativeStockOverrideId,
                DecreaseAvailableStock = false,
                CheckInventoryItemBalance = false,
                CorrelationId = $"work-order:{part.WorkOrderId:N}:part:{part.Id:N}:consume"
            });
            // Reload after the shared stock lock.
            var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
                allocation.WarehouseId, part.InventoryItemId);

            if (warehouseQuantity == null)
            {
                _logger.LogWarning("Warehouse quantity not found for item {ItemId}", part.InventoryItemId);
                return;
            }

            // Update allocation
            allocation.ConsumedQuantity += quantityConsumed;
            allocation.RemainingQuantity -= quantityConsumed;
            allocation.UpdatedAt = DateTime.UtcNow;

            if (allocation.RemainingQuantity <= 0)
            {
                allocation.Status = "Used";
            }

            // Update warehouse quantity - reduce current stock and allocated stock
            warehouseQuantity.CurrentStock -= quantityConsumed;
            warehouseQuantity.AllocatedStock -= quantityConsumed;
            warehouseQuantity.LastMovementDate = DateTime.UtcNow;
            warehouseQuantity.UpdatedAt = DateTime.UtcNow;

            await allocationRepo.UpdateAsync(allocation);
            await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);

            if (decreaseAuthorization.EmergencyOverrideApplied)
            {
                await _unitOfWork.SaveChangesAsync();
                await _negativeStockControls.ClearMutationContextAsync();
            }

            part.UsedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Consumed {Quantity} units of {ItemCode} from allocation {AllocationId}",
                quantityConsumed, warehouseQuantity.InventoryItem.ItemCode, allocation.Id);
        }
        else
        {
            // No InventoryAllocation - update warehouse quantities directly
            // Find warehouse with allocated stock for this item
            var warehouseQuantities = await _warehouseQuantityRepository.GetByInventoryItemIdAsync(part.InventoryItemId);
            var warehouseQuantity = warehouseQuantities.FirstOrDefault(wq => wq.AllocatedStock >= quantityConsumed);

            if (warehouseQuantity == null)
            {
                _logger.LogWarning("No warehouse found with sufficient allocated stock for item {ItemId}", part.InventoryItemId);
                return;
            }

            var decreaseAuthorization = await _negativeStockControls.PrepareDecreaseAsync(new InventoryStockDecreaseRequest
            {
                InventoryItemId = part.InventoryItemId,
                WarehouseId = warehouseQuantity.WarehouseId,
                Quantity = quantityConsumed,
                ReferenceType = "MaintenanceWorkOrderPartConsumption",
                ReferenceNumber = $"WO-{part.WorkOrderId:N}",
                ReferenceId = part.WorkOrderId,
                ReferenceLineId = part.Id,
                NegativeStockOverrideId = negativeStockOverrideId,
                DecreaseAvailableStock = false,
                CheckInventoryItemBalance = false,
                CorrelationId = $"work-order:{part.WorkOrderId:N}:part:{part.Id:N}:consume"
            });
            warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
                warehouseQuantity.WarehouseId, part.InventoryItemId) ?? throw new InvalidOperationException("Warehouse quantity disappeared after stock locking.");

            // Update warehouse quantity - reduce current stock and allocated stock
            warehouseQuantity.CurrentStock -= quantityConsumed;
            warehouseQuantity.AllocatedStock -= quantityConsumed;
            warehouseQuantity.LastMovementDate = DateTime.UtcNow;
            warehouseQuantity.UpdatedAt = DateTime.UtcNow;

            await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);

            if (decreaseAuthorization.EmergencyOverrideApplied)
            {
                await _unitOfWork.SaveChangesAsync();
                await _negativeStockControls.ClearMutationContextAsync();
            }

            part.UsedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Consumed {Quantity} units of {ItemCode} from warehouse (no allocation)",
                quantityConsumed, warehouseQuantity.InventoryItem.ItemCode);
        }
    }

    private async Task ReleaseAllocationAsync(WorkOrderPart part)
    {
        if (!part.AllocationId.HasValue)
        {
            return;
        }

        var allocationRepo = _unitOfWork.Repository<InventoryAllocation>();
        var allocation = await allocationRepo.GetByIdAsync(part.AllocationId.Value);
        if (allocation == null)
        {
            return;
        }

        // Get warehouse quantity using WarehouseId from allocation
        var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
            allocation.WarehouseId, part.InventoryItemId);
        if (warehouseQuantity == null)
        {
            return;
        }

        // Calculate quantity to release (allocated - consumed)
        var quantityToRelease = allocation.RemainingQuantity;

        if (quantityToRelease > 0)
        {
            // Return allocated quantity back to available stock
            warehouseQuantity.AvailableStock += quantityToRelease;
            warehouseQuantity.AllocatedStock -= quantityToRelease;
            warehouseQuantity.LastMovementDate = DateTime.UtcNow;
            warehouseQuantity.UpdatedAt = DateTime.UtcNow;

            await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);

            _logger.LogInformation(
                "Released {Quantity} units of {ItemCode} from allocation {AllocationId}",
                quantityToRelease, warehouseQuantity.InventoryItem.ItemCode, allocation.Id);
        }

        // Update allocation status
        allocation.Status = "Cancelled";
        allocation.UpdatedAt = DateTime.UtcNow;
        await allocationRepo.UpdateAsync(allocation);
    }

    public async Task<IEnumerable<WorkOrderPartDto>> AddPartsBulkAsync(IEnumerable<CreateWorkOrderPartDto> createDtos)
    {
        var tenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("TenantId is required");
        var userIdString = _currentUserService.UserId ?? throw new InvalidOperationException("UserId is required");
        var userId = Guid.Parse(userIdString);

        var createdParts = new List<WorkOrderPartDto>();

        // Use execution strategy to handle retries with transaction
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var partRepo = _unitOfWork.Repository<WorkOrderPart>();
                var allocationRepo = _unitOfWork.Repository<InventoryAllocation>();

                foreach (var createDto in createDtos)
                {
                    // Verify warehouse exists
                    var warehouse = await _warehouseRepository.GetByIdAsync(createDto.WarehouseId) ?? throw new InvalidOperationException($"Warehouse {createDto.WarehouseId} not found");

                    // Check warehouse-level inventory availability
                    var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
                        createDto.WarehouseId, createDto.InventoryItemId) ?? throw new InvalidOperationException(
                            $"Item {createDto.InventoryItemId} not found in warehouse {warehouse.Name}");
                    if (warehouseQuantity.AvailableStock < createDto.QuantityRequired)
                    {
                        _logger.LogWarning(
                            "Insufficient stock in warehouse {Warehouse} for item {ItemCode}. Required: {Required}, Available: {Available}",
                            warehouse.Name, warehouseQuantity.InventoryItem.ItemCode,
                            createDto.QuantityRequired, warehouseQuantity.AvailableStock);
                        throw new InvalidOperationException(
                            $"Insufficient stock in {warehouse.Name} for {warehouseQuantity.InventoryItem.ItemCode}. " +
                            $"Available: {warehouseQuantity.AvailableStock}, Required: {createDto.QuantityRequired}");
                    }

                    // Create inventory allocation first
                    var allocationId = Guid.NewGuid();
                    var allocation = new InventoryAllocation
                    {
                        Id = allocationId,
                        TenantId = tenantId,
                        InventoryItemId = createDto.InventoryItemId,
                        WarehouseId = createDto.WarehouseId,
                        LocationId = createDto.WarehouseLocationId, // Nullable - use if provided
                        AllocationType = "WorkOrder",
                        ReferenceNumber = $"WO-{createDto.WorkOrderId}",
                        ReferenceId = createDto.WorkOrderId,
                        AllocatedQuantity = createDto.QuantityRequired,
                        ConsumedQuantity = 0,
                        RemainingQuantity = createDto.QuantityRequired,
                        AllocationDate = DateTime.UtcNow,
                        Status = "Active",
                        SerialNumber = createDto.SerialNumber,
                        LotNumber = createDto.LotNumber,
                        Notes = createDto.Notes,
                        AllocatedById = userId,
                        CreatedAt = DateTime.UtcNow
                    };

                    var part = new WorkOrderPart
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        WorkOrderId = createDto.WorkOrderId,
                        InventoryItemId = createDto.InventoryItemId,
                        ItemCode = warehouseQuantity.InventoryItem.ItemCode,
                        ItemName = warehouseQuantity.InventoryItem.Name,
                        QuantityRequired = createDto.QuantityRequired,
                        QuantityAllocated = createDto.QuantityRequired,
                        QuantityUsed = 0,
                        QuantityReturned = 0,
                        UnitCost = createDto.UnitCost,
                        TotalCost = createDto.QuantityRequired * createDto.UnitCost,
                        Status = "Allocated",
                        Notes = createDto.Notes,
                        SerialNumber = createDto.SerialNumber,
                        LotNumber = createDto.LotNumber,
                        WarehouseLocationId = createDto.WarehouseLocationId,
                        Allocation = allocation, // Use navigation property instead of FK
                        AllocatedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Update warehouse quantity stock levels
                    warehouseQuantity.AvailableStock -= createDto.QuantityRequired;
                    warehouseQuantity.AllocatedStock += createDto.QuantityRequired;
                    warehouseQuantity.LastMovementDate = DateTime.UtcNow;
                    warehouseQuantity.UpdatedAt = DateTime.UtcNow;

                    // Add both allocation and part - EF will handle the FK relationship order
                    await allocationRepo.AddAsync(allocation);
                    await partRepo.AddAsync(part);
                    await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);

                    createdParts.Add(MapToDto(part));

                    _logger.LogInformation(
                        "Allocated {Quantity} units of {ItemCode} from warehouse {Warehouse} for work order {WorkOrderId}",
                        createDto.QuantityRequired, warehouseQuantity.InventoryItem.ItemCode,
                        warehouse.Name, createDto.WorkOrderId);
                }

                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                // ExecuteInStrategyAsync handles rollback automatically
                _logger.LogError(ex, "Error allocating parts in bulk");
                throw;
            }
        });

        return createdParts;
    }

    public async Task DeletePartsBulkAsync(IEnumerable<Guid> ids)
    {
        // Use execution strategy to handle retries with transaction
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var partRepo = _unitOfWork.Repository<WorkOrderPart>();

                foreach (var id in ids)
                {
                    var part = await partRepo.GetByIdAsync(id);
                    if (part == null)
                    {
                        continue;
                    }

                    // Release allocation if exists
                    if (part.AllocationId.HasValue)
                    {
                        await ReleaseAllocationAsync(part);
                    }

                    // Hard delete - permanently remove from database
                    await partRepo.HardDeleteAsync(part);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Deleted {Count} parts in bulk", ids.Count());
            }
            catch (Exception ex)
            {
                // ExecuteInStrategyAsync handles rollback automatically
                _logger.LogError(ex, "Error deleting parts in bulk");
                throw;
            }
        });
    }

    private static WorkOrderPartDto MapToDto(WorkOrderPart part)
    {
        return new WorkOrderPartDto
        {
            Id = part.Id,
            WorkOrderId = part.WorkOrderId,
            InventoryItemId = part.InventoryItemId,
            ItemCode = part.ItemCode ?? part.InventoryItem?.ItemCode ?? string.Empty,
            ItemName = part.ItemName ?? part.InventoryItem?.Name ?? string.Empty,
            Description = part.InventoryItem?.Description,
            QuantityRequired = part.QuantityRequired,
            QuantityAllocated = part.QuantityAllocated,
            QuantityUsed = part.QuantityUsed,
            QuantityReturned = part.QuantityReturned,
            UnitCost = part.UnitCost,
            TotalCost = part.TotalCost,
            SerialNumber = part.SerialNumber,
            LotNumber = part.LotNumber,
            WarehouseLocationCode = part.WarehouseLocation?.LocationCode,
            WarehouseLocationName = part.WarehouseLocation?.Name,
            Status = part.Status,
            AllocationId = part.AllocationId,
            AllocatedAt = part.AllocatedAt,
            PickedAt = part.PickedAt,
            UsedAt = part.UsedAt,
            Notes = part.Notes,
            CreatedAt = part.CreatedAt,
            UpdatedAt = part.UpdatedAt
        };
    }
}

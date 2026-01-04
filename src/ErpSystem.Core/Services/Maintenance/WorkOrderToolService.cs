using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

public class WorkOrderToolService : IWorkOrderToolService
{
    private readonly IWorkOrderToolRepository _workOrderToolRepository;
    private readonly IMaintenanceToolRepository _toolRepository;
    private readonly IToolCheckoutRepository _checkoutRepository;
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly IMaintenanceStaffScheduleRepository _scheduleRepository;
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly ILogger<WorkOrderToolService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public WorkOrderToolService(
        IWorkOrderToolRepository workOrderToolRepository,
        IMaintenanceToolRepository toolRepository,
        IToolCheckoutRepository checkoutRepository,
        IWorkOrderRepository workOrderRepository,
        IMaintenanceStaffScheduleRepository scheduleRepository,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        ILogger<WorkOrderToolService> logger,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _workOrderToolRepository = workOrderToolRepository;
        _toolRepository = toolRepository;
        _checkoutRepository = checkoutRepository;
        _workOrderRepository = workOrderRepository;
        _scheduleRepository = scheduleRepository;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _logger = logger;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<WorkOrderToolDto> AllocateToolAsync(AllocateWorkOrderToolDto allocateDto)
    {
        try
        {
            _logger.LogInformation("Allocating tool {ToolId} to work order {WorkOrderId}",
                allocateDto.ToolId, allocateDto.WorkOrderId);

            // Validate work order exists
            var workOrder = await _workOrderRepository.GetByIdAsync(allocateDto.WorkOrderId) ?? throw new ArgumentException($"Work order with ID {allocateDto.WorkOrderId} not found");

            // Validate tool exists in InventoryItems (tools are stored as ItemType = 4 = FixedAsset)
            var inventoryItemRepo = _unitOfWork.Repository<ErpSystem.Core.Entities.Inventory.InventoryItem>();
            var tool = await inventoryItemRepo.GetByIdAsync(allocateDto.ToolId);
            if (tool == null || (int)tool.ItemType != 4)
            {
                throw new ArgumentException($"Tool with ID {allocateDto.ToolId} not found or is not a tool item (ItemType must be 4)");
            }

            // Check if tool is already allocated to this work order
            if (await _workOrderToolRepository.IsToolAllocatedToWorkOrderAsync(allocateDto.ToolId, allocateDto.WorkOrderId))
            {
                throw new InvalidOperationException("Tool is already allocated to this work order");
            }

            var workOrderTool = new WorkOrderTool
            {
                Id = Guid.NewGuid(),
                WorkOrderId = allocateDto.WorkOrderId,
                ToolId = allocateDto.ToolId,
                IsRequired = allocateDto.IsRequired,
                IsAllocated = true,
                AllocationDate = DateTime.UtcNow,
                Notes = allocateDto.Notes,
                TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required"),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserId.ToString()
            };

            await _workOrderToolRepository.AddAsync(workOrderTool);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully allocated tool {ToolId} to work order {WorkOrderId}",
                allocateDto.ToolId, allocateDto.WorkOrderId);

            return await MapToDto(workOrderTool);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating tool {ToolId} to work order {WorkOrderId}",
                allocateDto.ToolId, allocateDto.WorkOrderId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkOrderToolDto>> GetToolsByWorkOrderAsync(Guid workOrderId)
    {
        try
        {
            var workOrderTools = await _workOrderToolRepository.GetByWorkOrderIdAsync(workOrderId);
            var dtos = new List<WorkOrderToolDto>();

            foreach (var wt in workOrderTools)
            {
                dtos.Add(await MapToDto(wt));
            }

            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tools for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task<WorkOrderToolSummaryDto> GetWorkOrderToolSummaryAsync(Guid workOrderId)
    {
        try
        {
            var tools = await _workOrderToolRepository.GetByWorkOrderIdAsync(workOrderId);
            var now = DateTime.UtcNow;

            // Calculate total rental cost based on checkout duration and daily rate
            decimal totalRentalCost = 0;
            foreach (var tool in tools.Where(t => t.CheckoutId.HasValue && t.Tool != null))
            {
                var checkoutDate = tool.Checkout?.CheckoutDate ?? now;
                var returnDate = tool.Checkout?.ActualReturnDate ?? now;
                var days = Math.Max(1, (int)Math.Ceiling((returnDate - checkoutDate).TotalDays));
                totalRentalCost += tool.Tool!.DailyRentalRate * days;
            }

            return new WorkOrderToolSummaryDto
            {
                TotalTools = tools.Count(),
                RequiredTools = tools.Count(t => t.IsRequired),
                AllocatedTools = tools.Count(t => t.IsAllocated),
                CheckedOutTools = tools.Count(t => t.CheckoutId.HasValue && t.Checkout?.ActualReturnDate == null),
                ReturnedTools = tools.Count(t => t.CheckoutId.HasValue && t.Checkout?.ActualReturnDate != null),
                OverdueTools = tools.Count(t => t.CheckoutId.HasValue
                    && t.Checkout?.ActualReturnDate == null
                    && t.Checkout?.ExpectedReturnDate < now),
                TotalRentalCost = totalRentalCost
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating tool summary for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task RemoveToolAllocationAsync(Guid workOrderId, Guid toolId)
    {
        // Use execution strategy to handle retries with transaction
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Removing tool {ToolId} allocation from work order {WorkOrderId}",
                    toolId, workOrderId);

                var workOrderTool = await _workOrderToolRepository.GetByWorkOrderAndToolIdAsync(workOrderId, toolId) ?? throw new ArgumentException("Tool allocation not found");

                // Cannot remove if tool is checked out
                if (workOrderTool.CheckoutId.HasValue && workOrderTool.Checkout?.ActualReturnDate == null)
                {
                    throw new InvalidOperationException("Cannot remove tool allocation while tool is checked out");
                }

                // Cannot remove if tool has been returned (has checkout history)
                if (workOrderTool.CheckoutId.HasValue && workOrderTool.Checkout?.ActualReturnDate != null)
                {
                    _logger.LogWarning("Cannot remove tool {ToolId} allocation - it has checkout history", toolId);
                    throw new InvalidOperationException("Cannot remove tool allocations with checkout history. This data is needed for work order records.");
                }

                // Release inventory allocation if exists
                if (workOrderTool.AllocationId.HasValue)
                {
                    var allocationRepo = _unitOfWork.Repository<ErpSystem.Core.Entities.Inventory.InventoryAllocation>();
                    var allocation = await allocationRepo.GetByIdAsync(workOrderTool.AllocationId.Value);

                    if (allocation != null)
                    {
                        // Get warehouse quantity
                        var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
                            allocation.WarehouseId, toolId);

                        if (warehouseQuantity != null)
                        {
                            // Release 1 unit back to available stock
                            warehouseQuantity.AvailableStock += 1;
                            warehouseQuantity.AllocatedStock -= 1;
                            warehouseQuantity.LastMovementDate = DateTime.UtcNow;
                            warehouseQuantity.UpdatedAt = DateTime.UtcNow;

                            await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);

                            _logger.LogInformation(
                                "Released tool {ItemCode} allocation back to warehouse",
                                allocation.InventoryItem.ItemCode);
                        }

                        // Update allocation status
                        allocation.Status = "Cancelled";
                        allocation.UpdatedAt = DateTime.UtcNow;
                        await allocationRepo.UpdateAsync(allocation);
                    }
                }

                // Hard delete - permanently remove from database
                await _workOrderToolRepository.HardDeleteAsync(workOrderTool);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully removed tool {ToolId} from work order {WorkOrderId}",
                    toolId, workOrderId);
            }
            catch (Exception ex)
            {
                // ExecuteInStrategyAsync handles rollback automatically
                _logger.LogError(ex, "Error removing tool {ToolId} from work order {WorkOrderId}",
                    toolId, workOrderId);
                throw;
            }
        });
    }

    public async Task<WorkOrderToolDto> CheckoutToolAsync(CheckoutWorkOrderToolDto checkoutDto)
    {
        try
        {
            _logger.LogInformation("Checking out tool {ToolId} for work order {WorkOrderId} by technician {TechnicianId}",
                checkoutDto.ToolId, checkoutDto.WorkOrderId, checkoutDto.TechnicianId);

            // Get work order tool allocation
            var workOrderTool = await _workOrderToolRepository.GetByWorkOrderAndToolIdAsync(
                checkoutDto.WorkOrderId, checkoutDto.ToolId) ?? throw new ArgumentException("Tool is not allocated to this work order");

            // Check if already checked out
            if (workOrderTool.CheckoutId.HasValue)
            {
                throw new InvalidOperationException("Tool is already checked out");
            }

            // Get work order to verify it exists
            var workOrder = await _workOrderRepository.GetByIdAsync(checkoutDto.WorkOrderId) ?? throw new ArgumentException("Work order not found");

            // Validate that the technician is assigned to this work order via schedules
            var schedules = await _scheduleRepository.GetByWorkOrderIdAsync(checkoutDto.WorkOrderId);
            var technicianSchedule = schedules.FirstOrDefault(s => s.TechnicianId == checkoutDto.TechnicianId) ?? throw new InvalidOperationException(
                    $"Technician {checkoutDto.TechnicianId} is not assigned to this work order. " +
                    $"Only technicians with active schedules for this work order can checkout tools.");

            // Get tool (InventoryItem) to verify it exists
            var inventoryItemRepo = _unitOfWork.Repository<ErpSystem.Core.Entities.Inventory.InventoryItem>();
            var tool = await inventoryItemRepo.GetByIdAsync(checkoutDto.ToolId);
            if (tool == null || (int)tool.ItemType != 4)
            {
                throw new ArgumentException("Tool not found");
            }

            // Create checkout record
            var checkout = new ToolCheckout
            {
                Id = Guid.NewGuid(),
                ToolId = checkoutDto.ToolId,
                CheckedOutById = Guid.Parse(checkoutDto.TechnicianId.ToString()),
                WorkOrderId = checkoutDto.WorkOrderId,
                CheckoutDate = DateTime.UtcNow,
                ExpectedReturnDate = checkoutDto.ExpectedReturnDate,
                ConditionOnCheckout = checkoutDto.ConditionOnCheckout,
                CheckoutNotes = checkoutDto.CheckoutNotes,
                Status = "CheckedOut",
                TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required"),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserId.ToString()
            };

            await _checkoutRepository.AddAsync(checkout);

            // Update work order tool
            workOrderTool.CheckoutId = checkout.Id;
            await _workOrderToolRepository.UpdateAsync(workOrderTool);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully checked out tool {ToolId} for work order {WorkOrderId} by technician {TechnicianId}",
                checkoutDto.ToolId, checkoutDto.WorkOrderId, checkoutDto.TechnicianId);

            // Reload to get checkout details
            workOrderTool = await _workOrderToolRepository.GetByWorkOrderAndToolIdAsync(
                checkoutDto.WorkOrderId, checkoutDto.ToolId);

            return await MapToDto(workOrderTool!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out tool {ToolId} for work order {WorkOrderId}",
                checkoutDto.ToolId, checkoutDto.WorkOrderId);
            throw;
        }
    }

    public async Task<WorkOrderToolDto> ReturnToolAsync(Guid workOrderId, Guid toolId, ReturnWorkOrderToolDto returnDto)
    {
        try
        {
            _logger.LogInformation("Returning tool {ToolId} from work order {WorkOrderId}",
                toolId, workOrderId);

            var workOrderTool = await _workOrderToolRepository.GetByWorkOrderAndToolIdAsync(workOrderId, toolId) ?? throw new ArgumentException("Tool allocation not found");
            if (!workOrderTool.CheckoutId.HasValue)
            {
                throw new InvalidOperationException("Tool is not checked out");
            }

            var checkout = await _checkoutRepository.GetByIdAsync(workOrderTool.CheckoutId.Value) ?? throw new ArgumentException("Checkout record not found");
            if (checkout.ActualReturnDate.HasValue)
            {
                throw new InvalidOperationException("Tool has already been returned");
            }

            // Update checkout record
            checkout.ActualReturnDate = DateTime.UtcNow;
            checkout.ConditionOnReturn = returnDto.ConditionOnReturn;
            checkout.ReturnNotes = returnDto.ReturnNotes;
            checkout.DamageReported = returnDto.DamageReported;
            checkout.DamageDescription = returnDto.DamageDescription;
            checkout.DamageCost = returnDto.DamageCost;
            checkout.Status = "Returned";
            checkout.CheckedInById = !string.IsNullOrEmpty(_currentUserService.UserId) ? Guid.Parse(_currentUserService.UserId) : null;
            await _checkoutRepository.UpdateAsync(checkout);

            // Release warehouse allocation if exists
            if (workOrderTool.AllocationId.HasValue)
            {
                var allocationRepo = _unitOfWork.Repository<ErpSystem.Core.Entities.Inventory.InventoryAllocation>();
                var allocation = await allocationRepo.GetByIdAsync(workOrderTool.AllocationId.Value);

                if (allocation != null)
                {
                    // Get warehouse quantity
                    var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
                        allocation.WarehouseId, toolId);

                    if (warehouseQuantity != null)
                    {
                        // Release 1 unit back to available stock
                        warehouseQuantity.AvailableStock += 1;
                        warehouseQuantity.AllocatedStock -= 1;
                        warehouseQuantity.LastMovementDate = DateTime.UtcNow;
                        warehouseQuantity.UpdatedAt = DateTime.UtcNow;

                        await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);

                        _logger.LogInformation(
                            "Released tool {ItemCode} allocation back to warehouse",
                            warehouseQuantity.InventoryItem.ItemCode);
                    }

                    // Update allocation status
                    allocation.Status = "Returned";
                    allocation.UpdatedAt = DateTime.UtcNow;
                    await allocationRepo.UpdateAsync(allocation);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully returned tool {ToolId} from work order {WorkOrderId}",
                toolId, workOrderId);

            // Reload to get updated details
            workOrderTool = await _workOrderToolRepository.GetByWorkOrderAndToolIdAsync(workOrderId, toolId);
            return await MapToDto(workOrderTool!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning tool {ToolId} from work order {WorkOrderId}",
                toolId, workOrderId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkOrderToolDto>> GetCheckedOutToolsForWorkOrderAsync(Guid workOrderId)
    {
        try
        {
            var workOrderTools = await _workOrderToolRepository.GetCheckedOutToolsAsync(workOrderId);
            var dtos = new List<WorkOrderToolDto>();

            foreach (var wt in workOrderTools)
            {
                dtos.Add(await MapToDto(wt));
            }

            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checked out tools for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkOrderToolDto>> GetOverdueToolsForWorkOrderAsync(Guid workOrderId)
    {
        try
        {
            var workOrderTools = await _workOrderToolRepository.GetOverdueToolsAsync(workOrderId);
            var dtos = new List<WorkOrderToolDto>();

            foreach (var wt in workOrderTools)
            {
                dtos.Add(await MapToDto(wt));
            }

            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue tools for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task<decimal> GetTotalToolCostAsync(Guid workOrderId)
    {
        try
        {
            var tools = await _workOrderToolRepository.GetByWorkOrderIdAsync(workOrderId);
            var now = DateTime.UtcNow;

            // Calculate total rental cost based on checkout duration and daily rate
            decimal totalCost = 0;
            foreach (var tool in tools.Where(t => t.CheckoutId.HasValue && t.Tool != null))
            {
                var checkoutDate = tool.Checkout?.CheckoutDate ?? now;
                var returnDate = tool.Checkout?.ActualReturnDate ?? now;
                var days = Math.Max(1, (int)Math.Ceiling((returnDate - checkoutDate).TotalDays));
                totalCost += tool.Tool!.DailyRentalRate * days;
            }

            return totalCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total tool cost for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkOrderToolDto>> AllocateToolsBulkAsync(IEnumerable<AllocateWorkOrderToolDto> allocateDtos)
    {
        var allocatedTools = new List<WorkOrderToolDto>();

        // Use execution strategy to handle retries with transaction
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var inventoryItemRepo = _unitOfWork.Repository<ErpSystem.Core.Entities.Inventory.InventoryItem>();

                foreach (var allocateDto in allocateDtos)
                {
                    // Validate work order exists
                    var workOrder = await _workOrderRepository.GetByIdAsync(allocateDto.WorkOrderId);
                    if (workOrder == null)
                    {
                        _logger.LogWarning("Work order {WorkOrderId} not found, skipping tool {ToolId}",
                            allocateDto.WorkOrderId, allocateDto.ToolId);
                        continue;
                    }

                    // Validate tool exists in InventoryItems
                    var tool = await inventoryItemRepo.GetByIdAsync(allocateDto.ToolId);
                    if (tool == null || (int)tool.ItemType != 4)
                    {
                        _logger.LogWarning("Tool {ToolId} not found or not a tool item, skipping", allocateDto.ToolId);
                        continue;
                    }

                    // Check if tool is already allocated
                    if (await _workOrderToolRepository.IsToolAllocatedToWorkOrderAsync(allocateDto.ToolId, allocateDto.WorkOrderId))
                    {
                        _logger.LogInformation("Tool {ToolId} already allocated to work order {WorkOrderId}, skipping",
                            allocateDto.ToolId, allocateDto.WorkOrderId);
                        continue;
                    }

                    // Check warehouse quantity availability (quantity of 1 for tools)
                    var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
                        allocateDto.WarehouseId, allocateDto.ToolId);

                    if (warehouseQuantity == null)
                    {
                        _logger.LogWarning("Tool {ToolId} not found in warehouse {WarehouseId}, skipping",
                            allocateDto.ToolId, allocateDto.WarehouseId);
                        continue;
                    }

                    if (warehouseQuantity.AvailableStock < 1)
                    {
                        _logger.LogWarning(
                            "Tool {ItemCode} not available in warehouse. Available: {Available}",
                            tool.ItemCode, warehouseQuantity.AvailableStock);
                        continue;
                    }

                    // Create inventory allocation first
                    var allocationRepo = _unitOfWork.Repository<ErpSystem.Core.Entities.Inventory.InventoryAllocation>();
                    var allocation = new ErpSystem.Core.Entities.Inventory.InventoryAllocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required"),
                        InventoryItemId = allocateDto.ToolId,
                        WarehouseId = allocateDto.WarehouseId,
                        LocationId = null, // Tools don't typically have specific warehouse locations
                        AllocationType = "WorkOrder",
                        ReferenceNumber = $"WO-{allocateDto.WorkOrderId}",
                        ReferenceId = allocateDto.WorkOrderId,
                        AllocatedQuantity = 1, // Tools are allocated as 1 unit
                        ConsumedQuantity = 0,
                        RemainingQuantity = 1,
                        AllocationDate = DateTime.UtcNow,
                        Status = "Active",
                        Notes = allocateDto.Notes,
                        AllocatedById = Guid.Parse(_currentUserService.UserId ?? throw new InvalidOperationException("User ID is required")),
                        CreatedAt = DateTime.UtcNow
                    };

                    var workOrderTool = new WorkOrderTool
                    {
                        Id = Guid.NewGuid(),
                        WorkOrderId = allocateDto.WorkOrderId,
                        ToolId = allocateDto.ToolId,
                        IsRequired = allocateDto.IsRequired,
                        IsAllocated = true,
                        AllocationDate = DateTime.UtcNow,
                        Notes = allocateDto.Notes,
                        TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required"),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = _currentUserService.UserId.ToString(),
                        Allocation = allocation // Use navigation property
                    };

                    // Update warehouse quantity - allocate 1 unit of the tool
                    warehouseQuantity.AvailableStock -= 1;
                    warehouseQuantity.AllocatedStock += 1;
                    warehouseQuantity.LastMovementDate = DateTime.UtcNow;
                    warehouseQuantity.UpdatedAt = DateTime.UtcNow;

                    await allocationRepo.AddAsync(allocation);
                    await _workOrderToolRepository.AddAsync(workOrderTool);
                    await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);

                    allocatedTools.Add(await MapToDto(workOrderTool));

                    _logger.LogInformation(
                        "Allocated tool {ItemCode} from warehouse for work order {WorkOrderId}",
                        tool.ItemCode, allocateDto.WorkOrderId);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully allocated {Count} tools in bulk", allocatedTools.Count);
            }
            catch (Exception ex)
            {
                // ExecuteInStrategyAsync handles rollback automatically
                _logger.LogError(ex, "Error allocating tools in bulk");
                throw;
            }
        });

        return allocatedTools;
    }

    public async Task RemoveToolAllocationsBulkAsync(IEnumerable<Guid> toolIds, Guid workOrderId)
    {
        // Use execution strategy to handle retries with transaction
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                foreach (var toolId in toolIds)
                {
                    var workOrderTool = await _workOrderToolRepository.GetByWorkOrderAndToolIdAsync(workOrderId, toolId);
                    if (workOrderTool == null)
                    {
                        _logger.LogWarning("Tool {ToolId} allocation not found for work order {WorkOrderId}, skipping",
                            toolId, workOrderId);
                        continue;
                    }

                    // Cannot remove if tool is checked out
                    if (workOrderTool.CheckoutId.HasValue && workOrderTool.Checkout?.ActualReturnDate == null)
                    {
                        _logger.LogWarning("Tool {ToolId} is checked out, cannot remove allocation, skipping", toolId);
                        continue;
                    }

                    // Cannot remove if tool has been returned (has checkout history)
                    if (workOrderTool.CheckoutId.HasValue && workOrderTool.Checkout?.ActualReturnDate != null)
                    {
                        _logger.LogWarning("Tool {ToolId} has checkout history, cannot remove allocation, skipping", toolId);
                        continue;
                    }

                    // Release inventory allocation if exists
                    if (workOrderTool.AllocationId.HasValue)
                    {
                        var allocationRepo = _unitOfWork.Repository<ErpSystem.Core.Entities.Inventory.InventoryAllocation>();
                        var allocation = await allocationRepo.GetByIdAsync(workOrderTool.AllocationId.Value);

                        if (allocation != null)
                        {
                            // Get warehouse quantity
                            var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
                                allocation.WarehouseId, toolId);

                            if (warehouseQuantity != null)
                            {
                                // Release 1 unit back to available stock
                                warehouseQuantity.AvailableStock += 1;
                                warehouseQuantity.AllocatedStock -= 1;
                                warehouseQuantity.LastMovementDate = DateTime.UtcNow;
                                warehouseQuantity.UpdatedAt = DateTime.UtcNow;

                                await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);

                                _logger.LogInformation(
                                    "Released tool {ItemCode} allocation back to warehouse",
                                    allocation.InventoryItem.ItemCode);
                            }

                            // Update allocation status
                            allocation.Status = "Cancelled";
                            allocation.UpdatedAt = DateTime.UtcNow;
                            await allocationRepo.UpdateAsync(allocation);
                        }
                    }

                    // Hard delete
                    await _workOrderToolRepository.HardDeleteAsync(workOrderTool);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully removed {Count} tool allocations in bulk for work order {WorkOrderId}",
                    toolIds.Count(), workOrderId);
            }
            catch (Exception ex)
            {
                // ExecuteInStrategyAsync handles rollback automatically
                _logger.LogError(ex, "Error removing tool allocations in bulk for work order {WorkOrderId}", workOrderId);
                throw;
            }
        });
    }

    private async Task<WorkOrderToolDto> MapToDto(WorkOrderTool workOrderTool)
    {
        // Reload if needed to ensure navigation properties are loaded
        if (workOrderTool.Tool == null)
        {
            workOrderTool = await _workOrderToolRepository.GetByWorkOrderAndToolIdAsync(
                workOrderTool.WorkOrderId, workOrderTool.ToolId) ?? workOrderTool;
        }

        // Get the technician name from Employee
        string? checkedOutByName = workOrderTool.Checkout?.CheckedOutBy != null
            ? $"{workOrderTool.Checkout.CheckedOutBy.FirstName} {workOrderTool.Checkout.CheckedOutBy.LastName}"
            : null;

        return new WorkOrderToolDto
        {
            Id = workOrderTool.Id,
            WorkOrderId = workOrderTool.WorkOrderId,
            ToolId = workOrderTool.ToolId,
            ToolCode = workOrderTool.Tool?.ItemCode ?? string.Empty,
            ToolName = workOrderTool.Tool?.Name ?? string.Empty,
            Description = workOrderTool.Tool?.Description,
            Category = workOrderTool.Tool?.Category?.Name ?? string.Empty,
            CurrentLocation = null,
            IsRequired = workOrderTool.IsRequired,
            IsAllocated = workOrderTool.IsAllocated,
            AllocationDate = workOrderTool.AllocationDate,
            CheckoutId = workOrderTool.CheckoutId,
            IsCheckedOut = workOrderTool.CheckoutId.HasValue && workOrderTool.Checkout?.ActualReturnDate == null,
            CheckoutDate = workOrderTool.Checkout?.CheckoutDate,
            ExpectedReturnDate = workOrderTool.Checkout?.ExpectedReturnDate,
            ActualReturnDate = workOrderTool.Checkout?.ActualReturnDate,
            CheckoutStatus = workOrderTool.Checkout?.Status,
            CheckedOutById = workOrderTool.Checkout?.CheckedOutById,
            CheckedOutByName = checkedOutByName,
            DailyRentalRate = workOrderTool.Tool?.DailyRentalRate ?? 0,
            RequiresCertification = false,
            RequiresTraining = false,
            SafetyNotes = null,
            Notes = workOrderTool.Notes,
            CreatedAt = workOrderTool.CreatedAt,
            UpdatedAt = workOrderTool.UpdatedAt
        };
    }
}

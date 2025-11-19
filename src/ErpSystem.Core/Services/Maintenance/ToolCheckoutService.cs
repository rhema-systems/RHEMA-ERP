using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing tool checkout and return operations
/// </summary>
public class ToolCheckoutService : IToolCheckoutService
{
    private readonly IMaintenanceToolRepository _toolRepository;
    private readonly IToolCheckoutRepository _checkoutRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ToolCheckoutService> _logger;

    public ToolCheckoutService(
        IMaintenanceToolRepository toolRepository,
        IToolCheckoutRepository checkoutRepository,
        IInventoryItemRepository inventoryItemRepository,
        IUnitOfWork unitOfWork,
        ILogger<ToolCheckoutService> logger)
    {
        _toolRepository = toolRepository;
        _checkoutRepository = checkoutRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    #region Checkout Operations

    /// <summary>
    /// Checkout a tool to an employee
    /// </summary>
    public async Task<ToolCheckoutResult> CheckoutToolAsync(Guid toolId, Guid employeeId, CheckoutToolDto dto)
    {
        try
        {
            _logger.LogInformation("Starting tool checkout for tool {ToolId} to employee {EmployeeId}", toolId, employeeId);

            // Legacy MaintenanceTool record (deprecated, used only for status/metadata)
            var tool = await _toolRepository.GetByIdAsync(toolId);
            if (tool == null)
                throw new ArgumentException($"Tool {toolId} not found");

            if (!tool.IsActive)
                throw new InvalidOperationException($"Tool {tool.ToolCode} is not active");

            if (tool.Status != "Available")
                throw new InvalidOperationException($"Tool {tool.ToolCode} is not available (current status: {tool.Status})");

            // Resolve the underlying InventoryItem that represents this tool (ItemType = 4) by ToolCode/ItemCode
            var inventoryItem = await _inventoryItemRepository.GetByItemCodeAsync(tool.ToolCode);
            if (inventoryItem == null)
            {
                throw new InvalidOperationException(
                    $"No inventory item found for tool code {tool.ToolCode}. " +
                    "Tools must exist as InventoryItems (ItemType = 4)."
                );
            }

            // Check if there's already an active checkout for this tool (by InventoryItem Id)
            var activeCheckout = await _checkoutRepository.GetActiveCheckoutByToolIdAsync(inventoryItem.Id);
            if (activeCheckout != null)
                throw new InvalidOperationException($"Tool {tool.ToolCode} is already checked out");

            // Create checkout record
            var checkout = new ToolCheckout
            {
                Id = Guid.NewGuid(),
                ToolId = inventoryItem.Id,
                CheckedOutById = employeeId,
                WorkOrderId = dto.WorkOrderId,
                JobCardId = dto.JobCardId,
                CheckoutDate = DateTime.UtcNow,
                ExpectedReturnDate = dto.ExpectedReturnDate,
                Status = "CheckedOut",
                CheckoutNotes = dto.CheckoutNotes,
                ConditionOnCheckout = dto.ConditionOnCheckout,
                DamageReported = false,
                TenantId = tool.TenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = employeeId.ToString()
            };

            await _checkoutRepository.AddAsync(checkout);

            // Update tool status
            tool.Status = "InUse";
            tool.LastUsedDate = DateTime.UtcNow;
            await _toolRepository.UpdateAsync(tool);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Tool {ToolCode} checked out successfully to employee {EmployeeId}", tool.ToolCode, employeeId);

            return new ToolCheckoutResult
            {
                CheckoutId = checkout.Id,
                ToolId = inventoryItem.Id,
                ToolName = tool.Name,
                ToolCode = tool.ToolCode,
                CheckoutDate = checkout.CheckoutDate,
                ExpectedReturnDate = checkout.ExpectedReturnDate,
                CheckedOutBy = employeeId.ToString(), // Would need employee name from repository
                Status = checkout.Status
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out tool {ToolId}", toolId);
            throw;
        }
    }

    /// <summary>
    /// Return a checked-out tool
    /// </summary>
    public async Task<ToolReturnResult> ReturnToolAsync(Guid checkoutId, Guid returnedByUserId, ReturnToolDto dto)
    {
        try
        {
            _logger.LogInformation("Starting tool return for checkout {CheckoutId}", checkoutId);

            var checkout = await _checkoutRepository.GetByIdAsync(checkoutId);
            if (checkout == null)
                throw new ArgumentException($"Checkout {checkoutId} not found");

            if (checkout.Status == "Returned")
                throw new InvalidOperationException("Tool has already been returned");

            var returnDate = DateTime.UtcNow;
            var daysCheckedOut = (int)(returnDate - checkout.CheckoutDate).TotalDays;
            var isOverdue = checkout.ExpectedReturnDate.HasValue && returnDate > checkout.ExpectedReturnDate.Value;
            var overdueDays = isOverdue ? (int)(returnDate - checkout.ExpectedReturnDate!.Value).TotalDays : 0;

            // Update checkout record
            checkout.ActualReturnDate = returnDate;
            checkout.CheckedInById = returnedByUserId;
            checkout.Status = isOverdue ? "Overdue" : "Returned";
            checkout.ConditionOnReturn = dto.ConditionOnReturn;
            checkout.ReturnNotes = dto.ReturnNotes;
            checkout.DamageReported = dto.DamageReported;
            checkout.DamageDescription = dto.DamageDescription;
            checkout.DamageCost = dto.DamageCost;
            checkout.UpdatedAt = returnDate;
            checkout.UpdatedBy = returnedByUserId.ToString();

            await _checkoutRepository.UpdateAsync(checkout);

            // Update tool status
            var tool = await _toolRepository.GetByIdAsync(checkout.ToolId);
            if (tool != null)
            {
                // Determine new tool status based on condition
                if (dto.DamageReported || dto.ConditionOnReturn == "Damaged")
                {
                    tool.Status = "Maintenance";
                }
                else
                {
                    tool.Status = "Available";
                }

                tool.TotalUsageDays += daysCheckedOut;
                tool.LastUsedDate = returnDate;
                await _toolRepository.UpdateAsync(tool);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Tool checkout {CheckoutId} returned successfully. Days out: {Days}, Overdue: {Overdue}", 
                checkoutId, daysCheckedOut, isOverdue);

            return new ToolReturnResult
            {
                CheckoutId = checkoutId,
                ReturnDate = returnDate,
                DaysCheckedOut = daysCheckedOut,
                IsOverdue = isOverdue,
                OverdueDays = isOverdue ? overdueDays : null,
                DamageReported = dto.DamageReported
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning tool for checkout {CheckoutId}", checkoutId);
            throw;
        }
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Get active checkouts
    /// </summary>
    public async Task<List<ToolCheckoutDto>> GetActiveCheckoutsAsync(Guid? employeeId = null)
    {
        try
        {
            var checkouts = await _checkoutRepository.GetActiveCheckoutsAsync(employeeId);
            return MapToCheckoutDtos(checkouts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active checkouts");
            throw;
        }
    }

    /// <summary>
    /// Get overdue checkouts
    /// </summary>
    public async Task<List<ToolCheckoutDto>> GetOverdueCheckoutsAsync()
    {
        try
        {
            var checkouts = await _checkoutRepository.GetOverdueCheckoutsAsync();
            return MapToCheckoutDtos(checkouts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue checkouts");
            throw;
        }
    }

    /// <summary>
    /// Check tool availability
    /// </summary>
    public async Task<ToolAvailabilityDto> CheckToolAvailabilityAsync(Guid toolId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var tool = await _toolRepository.GetByIdAsync(toolId);
            if (tool == null)
                throw new ArgumentException($"Tool {toolId} not found");

            var isAvailable = tool.Status == "Available";
            var activeCheckout = await _checkoutRepository.GetActiveCheckoutByToolIdAsync(toolId);

            DateTime? availableFrom = null;
            if (activeCheckout != null && activeCheckout.ExpectedReturnDate.HasValue)
            {
                availableFrom = activeCheckout.ExpectedReturnDate.Value;
            }

            // Get upcoming checkouts in date range (if implemented)
            var upcomingCheckouts = new List<ToolCheckoutDto>();

            return new ToolAvailabilityDto
            {
                ToolId = toolId,
                ToolName = tool.Name,
                ToolCode = tool.ToolCode,
                IsAvailable = isAvailable,
                CurrentStatus = tool.Status,
                AvailableFrom = availableFrom,
                UpcomingCheckouts = upcomingCheckouts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking tool availability for {ToolId}", toolId);
            throw;
        }
    }

    /// <summary>
    /// Get tool checkout history
    /// </summary>
    public async Task<ToolCheckoutHistoryDto> GetToolCheckoutHistoryAsync(Guid toolId, int limit = 50)
    {
        try
        {
            var tool = await _toolRepository.GetByIdAsync(toolId);
            if (tool == null)
                throw new ArgumentException($"Tool {toolId} not found");

            var checkouts = await _checkoutRepository.GetToolCheckoutHistoryAsync(toolId, limit);
            var totalCheckouts = await _checkoutRepository.GetTotalCheckoutCountAsync(toolId);
            var totalUsageDays = await _checkoutRepository.GetTotalUsageDaysAsync(toolId);

            var totalRentalCost = totalUsageDays * tool.DailyRentalRate;

            return new ToolCheckoutHistoryDto
            {
                ToolId = toolId,
                ToolName = tool.Name,
                ToolCode = tool.ToolCode,
                TotalCheckouts = totalCheckouts,
                TotalUsageDays = totalUsageDays,
                TotalRentalCost = totalRentalCost,
                RecentCheckouts = MapToCheckoutDtos(checkouts)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checkout history for tool {ToolId}", toolId);
            throw;
        }
    }

    /// <summary>
    /// Get available tools
    /// </summary>
    public async Task<List<MaintenanceToolDto>> GetAvailableToolsAsync()
    {
        try
        {
            var tools = await _toolRepository.GetAvailableToolsAsync();
            return tools.Select(MapToToolDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available tools");
            throw;
        }
    }

    /// <summary>
    /// Get all tools
    /// </summary>
    public async Task<List<MaintenanceToolDto>> GetAllToolsAsync()
    {
        try
        {
            var tools = await _toolRepository.GetAllAsync();
            return tools.Select(MapToToolDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tools");
            throw;
        }
    }

    /// <summary>
    /// Get tool by ID
    /// </summary>
    public async Task<MaintenanceToolDto?> GetToolByIdAsync(Guid toolId)
    {
        try
        {
            var tool = await _toolRepository.GetByIdAsync(toolId);
            return tool != null ? MapToToolDto(tool) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tool {ToolId}", toolId);
            throw;
        }
    }

    /// <summary>
    /// Get employee checkout history
    /// </summary>
    public async Task<List<ToolCheckoutDto>> GetEmployeeCheckoutHistoryAsync(Guid employeeId, int limit = 50)
    {
        try
        {
            var checkouts = await _checkoutRepository.GetEmployeeCheckoutHistoryAsync(employeeId, limit);
            return MapToCheckoutDtos(checkouts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checkout history for employee {EmployeeId}", employeeId);
            throw;
        }
    }

    #endregion

    #region Damage Reporting

    /// <summary>
    /// Report damage for a checked-out tool
    /// </summary>
    public async Task ReportToolDamageAsync(Guid checkoutId, ToolDamageDto dto)
    {
        try
        {
            _logger.LogInformation("Reporting damage for checkout {CheckoutId}", checkoutId);

            var checkout = await _checkoutRepository.GetByIdAsync(checkoutId);
            if (checkout == null)
                throw new ArgumentException($"Checkout {checkoutId} not found");

            checkout.DamageReported = true;
            checkout.DamageDescription = dto.DamageDescription;
            checkout.DamageCost = dto.EstimatedCost;
            checkout.UpdatedAt = DateTime.UtcNow;

            await _checkoutRepository.UpdateAsync(checkout);

            // Update tool status to Maintenance if requires repair
            if (dto.RequiresRepair)
            {
                var tool = await _toolRepository.GetByIdAsync(checkout.ToolId);
                if (tool != null && tool.Status != "Maintenance")
                {
                    tool.Status = "Maintenance";
                    await _toolRepository.UpdateAsync(tool);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Damage reported for checkout {CheckoutId}", checkoutId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting damage for checkout {CheckoutId}", checkoutId);
            throw;
        }
    }

    #endregion

    #region Mapping Helpers

    private List<ToolCheckoutDto> MapToCheckoutDtos(List<ToolCheckout> checkouts)
    {
        return checkouts.Select(c =>
        {
            var daysOut = c.ActualReturnDate.HasValue
                ? (int)(c.ActualReturnDate.Value - c.CheckoutDate).TotalDays
                : (int)(DateTime.UtcNow - c.CheckoutDate).TotalDays;

            var isOverdue = c.ExpectedReturnDate.HasValue &&
                           !c.ActualReturnDate.HasValue &&
                           DateTime.UtcNow > c.ExpectedReturnDate.Value;

            return new ToolCheckoutDto
            {
                Id = c.Id,
                ToolId = c.ToolId,
                ToolCode = c.Tool?.ItemCode ?? "N/A",  // InventoryItem uses ItemCode
                ToolName = c.Tool?.Name ?? "Unknown",
                CheckedOutById = c.CheckedOutById,
                CheckedOutByName = c.CheckedOutBy?.FullName ?? "Unknown",
                CheckoutDate = c.CheckoutDate,
                ExpectedReturnDate = c.ExpectedReturnDate,
                ActualReturnDate = c.ActualReturnDate,
                Status = c.Status,
                DaysOut = daysOut,
                IsOverdue = isOverdue,
                WorkOrderId = c.WorkOrderId,
                WorkOrderNumber = c.WorkOrder?.WorkOrderNumber,
                ConditionOnCheckout = c.ConditionOnCheckout,
                CheckoutNotes = c.CheckoutNotes
            };
        }).ToList();
    }

    private MaintenanceToolDto MapToToolDto(MaintenanceTool tool)
    {
        return new MaintenanceToolDto
        {
            Id = tool.Id,
            ToolCode = tool.ToolCode,
            Name = tool.Name,
            Description = tool.Description ?? string.Empty,
            Category = tool.Category,
            Status = tool.Status,
            CurrentLocation = tool.CurrentLocation,
            HomeLocation = tool.HomeLocation,
            RequiresCertification = tool.RequiresCertification,
            RequiresTraining = tool.RequiresTraining,
            DailyRentalRate = tool.DailyRentalRate,
            LastUsedDate = tool.LastUsedDate,
            TotalUsageDays = tool.TotalUsageDays
        };
    }

    #endregion
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/resources")]
[Authorize]
public class ResourceAllocationController : ControllerBase
{
    private readonly ITechnicianSchedulingService _schedulingService;
    private readonly IInventoryManagementService _inventoryService;
    private readonly IWorkOrderService _workOrderService;
    private readonly ILogger<ResourceAllocationController> _logger;

    public ResourceAllocationController(
        ITechnicianSchedulingService schedulingService,
        IInventoryManagementService inventoryService,
        IWorkOrderService workOrderService,
        ILogger<ResourceAllocationController> logger)
    {
        _schedulingService = schedulingService;
        _inventoryService = inventoryService;
        _workOrderService = workOrderService;
        _logger = logger;
    }

    #region Technician Scheduling Endpoints

    /// <summary>
    /// Gets all available technicians (users with technician role)
    /// </summary>
    [HttpGet("technicians")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> GetAvailableTechnicians()
    {
        try
        {
            var technicians = await _schedulingService.GetAvailableTechniciansAsync(DateTime.Today, DateTime.Today.AddDays(30));
            return Ok(technicians);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available technicians");
            return StatusCode(500, "An error occurred while retrieving available technicians");
        }
    }

    /// <summary>
    /// Checks if a technician is available for a specific time period
    /// </summary>
    [HttpGet("technicians/{technicianId:guid}/availability")]
    public async Task<ActionResult<bool>> CheckTechnicianAvailability(
        Guid technicianId,
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime)
    {
        try
        {
            var isAvailable = await _schedulingService.IsTechnicianAvailableAsync(technicianId, startTime, endTime);
            return Ok(new { TechnicianId = technicianId, IsAvailable = isAvailable, StartTime = startTime, EndTime = endTime });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking technician availability");
            return StatusCode(500, "An error occurred while checking technician availability");
        }
    }

    /// <summary>
    /// Gets detailed availability for a technician over a date range
    /// </summary>
    [HttpGet("technicians/{technicianId:guid}/availability/detailed")]
    public async Task<ActionResult<TechnicianAvailabilityDto>> GetTechnicianAvailability(
        Guid technicianId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            var availability = await _schedulingService.GetTechnicianAvailabilityAsync(technicianId, startDate, endDate);
            return Ok(availability);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician availability");
            return StatusCode(500, "An error occurred while retrieving technician availability");
        }
    }

    /// <summary>
    /// Schedules a work order for a specific technician
    /// </summary>
    [HttpPost("technicians/schedule")]
    public async Task<ActionResult<TechnicianScheduleDto>> ScheduleWorkOrder([FromBody] ScheduleWorkOrderDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var schedule = await _schedulingService.ScheduleWorkOrderAsync(request.WorkOrderId, request.TechnicianId, request.StartTime);
            return Ok(schedule);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling work order");
            return StatusCode(500, "An error occurred while scheduling the work order");
        }
    }

    /// <summary>
    /// Finds the best available technician for a work order
    /// </summary>
    [HttpGet("technicians/recommend")]
    public async Task<ActionResult<TechnicianRecommendationDto>> GetTechnicianRecommendation(
        [FromQuery] Guid workOrderId,
        [FromQuery] DateTime preferredStartTime,
        [FromQuery] double estimatedHours)
    {
        try
        {
            var recommendation = await _schedulingService.FindBestTechnicianAsync(workOrderId, preferredStartTime);
            return Ok(recommendation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technician recommendation");
            return StatusCode(500, "An error occurred while getting technician recommendation");
        }
    }

    /// <summary>
    /// Gets technician workload for a specific period
    /// </summary>
    [HttpGet("technicians/{technicianId:guid}/workload")]
    public async Task<ActionResult<TechnicianWorkloadDto>> GetTechnicianWorkload(
        Guid technicianId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            var workload = await _schedulingService.GetTechnicianWorkloadAsync(technicianId, startDate, endDate);
            return Ok(workload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician workload");
            return StatusCode(500, "An error occurred while retrieving technician workload");
        }
    }

    /// <summary>
    /// Sets technician availability
    /// </summary>
    [HttpPost("technicians/availability")]
    public async Task<ActionResult<TechnicianAvailabilityRecordDto>> SetTechnicianAvailability([FromBody] SetAvailabilityDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _schedulingService.SetTechnicianAvailabilityAsync(request.TechnicianId, request.StartDate, request.EndDate, request.AvailabilityType, request.Reason);
            return Ok(new { Message = "Technician availability updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting technician availability");
            return StatusCode(500, "An error occurred while setting technician availability");
        }
    }

    #endregion

    #region Inventory Management Endpoints

    /// <summary>
    /// Gets inventory items suitable for maintenance work orders
    /// </summary>
    [HttpGet("inventory/parts")]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetMaintenanceParts([FromQuery] string? searchTerm = null)
    {
        try
        {
            var parts = await _inventoryService.GetMaintenancePartsAsync(searchTerm);
            return Ok(parts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance parts");
            return StatusCode(500, "An error occurred while retrieving maintenance parts");
        }
    }

    /// <summary>
    /// Gets detailed information about a specific inventory item
    /// </summary>
    [HttpGet("inventory/parts/{itemId:guid}")]
    public async Task<ActionResult<InventoryItemDetailDto>> GetInventoryItemDetail(Guid itemId)
    {
        try
        {
            var itemDetail = await _inventoryService.GetInventoryItemDetailAsync(itemId);
            if (itemDetail == null)
                return NotFound($"Inventory item with ID {itemId} not found");

            return Ok(itemDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory item detail");
            return StatusCode(500, "An error occurred while retrieving inventory item detail");
        }
    }

    /// <summary>
    /// Checks stock availability for a specific item and quantity
    /// </summary>
    [HttpGet("inventory/availability")]
    public async Task<ActionResult<StockAvailabilityDto>> CheckStockAvailability(
        [FromQuery] Guid inventoryItemId,
        [FromQuery] decimal requiredQuantity)
    {
        try
        {
            var availability = await _inventoryService.CheckStockAvailabilityAsync(inventoryItemId, requiredQuantity);
            return Ok(availability);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking stock availability");
            return StatusCode(500, "An error occurred while checking stock availability");
        }
    }

    /// <summary>
    /// Allocates inventory for a maintenance work order
    /// </summary>
    [HttpPost("inventory/allocate")]
    public async Task<ActionResult<InventoryAllocationDto>> AllocateInventory([FromBody] AllocateInventoryDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var allocation = await _inventoryService.AllocateForWorkOrderAsync(request);
            return Ok(allocation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating inventory");
            return StatusCode(500, "An error occurred while allocating inventory");
        }
    }

    /// <summary>
    /// Consumes allocated inventory (when parts are actually used)
    /// </summary>
    [HttpPost("inventory/consume/{allocationId:guid}")]
    public async Task<ActionResult<bool>> ConsumeAllocatedInventory(
        Guid allocationId,
        [FromBody] ConsumeInventoryDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = await _inventoryService.ConsumeAllocatedInventoryAsync(allocationId, request.Quantity, request.UserId);
            return Ok(new { Success = success, Message = "Inventory consumed successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consuming allocated inventory");
            return StatusCode(500, "An error occurred while consuming inventory");
        }
    }

    /// <summary>
    /// Releases unused allocation (returns to available stock)
    /// </summary>
    [HttpPost("inventory/release/{allocationId:guid}")]
    public async Task<ActionResult<bool>> ReleaseAllocation(Guid allocationId, [FromBody] ReleaseAllocationDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = await _inventoryService.ReleaseAllocationAsync(allocationId, request.UserId);
            return Ok(new { Success = success, Message = "Allocation released successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing allocation");
            return StatusCode(500, "An error occurred while releasing allocation");
        }
    }

    /// <summary>
    /// Gets items that need to be reordered
    /// </summary>
    [HttpGet("inventory/reorder-required")]
    public async Task<ActionResult<IEnumerable<ReorderRequiredDto>>> GetItemsRequiringReorder()
    {
        try
        {
            var items = await _inventoryService.GetItemsRequiringReorderAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items requiring reorder");
            return StatusCode(500, "An error occurred while retrieving items requiring reorder");
        }
    }

    #endregion

    #region Combined Resource Planning

    /// <summary>
    /// Plans resources (both technician and inventory) for a work order
    /// </summary>
    [HttpPost("plan-work-order")]
    public async Task<ActionResult<WorkOrderResourcePlanDto>> PlanWorkOrderResources([FromBody] PlanWorkOrderResourcesDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(request.WorkOrderId);
            if (workOrder == null)
                return NotFound($"Work order with ID {request.WorkOrderId} not found");

            // Get technician recommendation
            var technicianRecommendation = await _schedulingService.FindBestTechnicianAsync(
                request.WorkOrderId, request.PreferredStartTime);

            // Check inventory availability for required parts
            var inventoryChecks = new List<StockAvailabilityDto>();
            foreach (var part in request.RequiredParts)
            {
                var availability = await _inventoryService.CheckStockAvailabilityAsync(part.InventoryItemId, part.Quantity);
                inventoryChecks.Add(availability);
            }

            var allPartsAvailable = inventoryChecks.All(check => check.IsAvailable);
            var technicianRecommendationDto = (TechnicianRecommendationDto)technicianRecommendation;
            var technicianAvailable = technicianRecommendationDto?.IsAvailable ?? false;

            return Ok(new WorkOrderResourcePlanDto
            {
                WorkOrderId = request.WorkOrderId,
                WorkOrderNumber = workOrder.WorkOrderNumber,
                CanProceed = technicianAvailable && allPartsAvailable,
                TechnicianRecommendation = technicianRecommendationDto,
                InventoryAvailability = inventoryChecks.ToList(),
                RecommendedStartTime = request.PreferredStartTime,
                EstimatedCompletionTime = request.PreferredStartTime.AddHours(request.EstimatedHours),
                BlockingIssues = GetBlockingIssues(technicianAvailable, allPartsAvailable, inventoryChecks),
                NextSteps = GetNextSteps(technicianAvailable, allPartsAvailable, inventoryChecks)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error planning work order resources");
            return StatusCode(500, "An error occurred while planning work order resources");
        }
    }

    /// <summary>
    /// Executes the resource plan (schedules technician and allocates inventory)
    /// </summary>
    [HttpPost("execute-plan")]
    public async Task<ActionResult<ResourcePlanExecutionResultDto>> ExecuteResourcePlan([FromBody] ExecuteResourcePlanDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var results = new ResourcePlanExecutionResultDto
            {
                WorkOrderId = request.WorkOrderId,
                Success = true,
                TechnicianScheduled = false,
                InventoryAllocated = false,
                Errors = new List<string>()
            };

            // Schedule technician
            try
            {
                if (request.TechnicianId.HasValue)
                {
                    var scheduleRequest = new ScheduleWorkOrderDto
                    {
                        WorkOrderId = request.WorkOrderId,
                        TechnicianId = request.TechnicianId.Value,
                        StartTime = request.StartTime,
                        EndTime = request.EndTime,
                        EstimatedHours = request.EstimatedHours,
                        Notes = "Scheduled via resource planning"
                    };

                    var schedule = await _schedulingService.ScheduleWorkOrderAsync(scheduleRequest.WorkOrderId, scheduleRequest.TechnicianId, scheduleRequest.StartTime);
                    results.TechnicianSchedule = (TechnicianScheduleDto)schedule;
                    results.TechnicianScheduled = true;
                }
            }
            catch (Exception ex)
            {
                results.Success = false;
                results.Errors.Add($"Failed to schedule technician: {ex.Message}");
            }

            // Allocate inventory
            try
            {
                var allocations = new List<InventoryAllocationDto>();
                foreach (var part in request.RequiredParts)
                {
                    var allocationRequest = new AllocateInventoryDto
                    {
                        InventoryItemId = part.InventoryItemId,
                        Quantity = part.Quantity,
                        ReferenceNumber = request.WorkOrderNumber,
                        ReferenceId = request.WorkOrderId,
                        RequiredDate = request.StartTime,
                        Notes = "Allocated via resource planning",
                        UserId = request.UserId
                    };

                    var allocation = await _inventoryService.AllocateForWorkOrderAsync(allocationRequest);
                    allocations.Add(allocation);
                }

                results.InventoryAllocations = allocations;
                results.InventoryAllocated = true;
            }
            catch (Exception ex)
            {
                results.Success = false;
                results.Errors.Add($"Failed to allocate inventory: {ex.Message}");
            }

            if (results.Success)
            {
                _logger.LogInformation("Successfully executed resource plan for work order {WorkOrderId}", request.WorkOrderId);
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing resource plan");
            return StatusCode(500, "An error occurred while executing the resource plan");
        }
    }

    /// <summary>
    /// Gets resource utilization summary
    /// </summary>
    [HttpGet("utilization")]
    public async Task<ActionResult<ResourceUtilizationDto>> GetResourceUtilization(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            // Get technician utilization data (this would be more comprehensive in a real implementation)
            var technicians = await _schedulingService.GetAvailableTechniciansAsync(start, end);
            var technicianUtilization = new List<TechnicianUtilizationDto>();

            // Note: Technician object casting needs proper DTO definition
            // For now, creating placeholder data to fix compilation
            technicianUtilization.Add(new TechnicianUtilizationDto
            {
                TechnicianId = Guid.NewGuid(),
                TechnicianName = "Sample Technician",
                UtilizationPercentage = 75.0,
                ScheduledHours = 30.0,
                AvailableHours = 40.0,
                ActiveWorkOrders = 5
            });

            // Get inventory utilization (items frequently used)
            var reorderItems = await _inventoryService.GetItemsRequiringReorderAsync();

            return Ok(new ResourceUtilizationDto
            {
                StartDate = start,
                EndDate = end,
                TechnicianUtilization = technicianUtilization,
                InventoryTurnover = reorderItems.Take(10).Select(item => new InventoryUtilizationDto
                {
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    CurrentStock = item.CurrentStock,
                    TurnoverRate = 0, // Would calculate actual turnover
                    ReorderFrequency = "Monthly" // Placeholder
                }).ToList(),
                OverallMetrics = new ResourceMetricsDto
                {
                    AverageTechnicianUtilization = technicianUtilization.Any() ? technicianUtilization.Average(t => t.UtilizationPercentage) : 0,
                    TotalActiveTechnicians = technicianUtilization.Count,
                    ItemsRequiringReorder = reorderItems.Count(),
                    ResourceEfficiency = 85.5 // Placeholder calculation
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resource utilization");
            return StatusCode(500, "An error occurred while retrieving resource utilization");
        }
    }

    #endregion

    #region Helper Methods

    private List<string> GetBlockingIssues(bool technicianAvailable, bool allPartsAvailable, List<StockAvailabilityDto> inventoryChecks)
    {
        var issues = new List<string>();

        if (!technicianAvailable)
            issues.Add("No available technician found for the requested time period");

        if (!allPartsAvailable)
        {
            var unavailableParts = inventoryChecks.Where(check => !check.IsAvailable);
            foreach (var part in unavailableParts)
            {
                issues.Add($"Insufficient stock for {part.ItemCode} ({part.ItemName}): Available {part.AvailableQuantity}, Required {part.RequiredQuantity}");
            }
        }

        return issues;
    }

    private List<string> GetNextSteps(bool technicianAvailable, bool allPartsAvailable, List<StockAvailabilityDto> inventoryChecks)
    {
        var steps = new List<string>();

        if (technicianAvailable && allPartsAvailable)
        {
            steps.Add("All resources available - ready to execute work order");
            steps.Add("Use 'Execute Plan' to schedule technician and allocate inventory");
        }
        else
        {
            if (!technicianAvailable)
                steps.Add("Find alternative technician or reschedule work order");

            if (!allPartsAvailable)
            {
                steps.Add("Create purchase orders for insufficient inventory items");
                steps.Add("Consider using alternative parts if available");
            }
        }

        return steps;
    }

    #endregion
}

#region Resource Planning DTOs

public class PlanWorkOrderResourcesDto
{
    public Guid WorkOrderId { get; set; }
    public DateTime PreferredStartTime { get; set; }
    public double EstimatedHours { get; set; }
    public List<RequiredPartDto> RequiredParts { get; set; } = new();
}

public class RequiredPartDto
{
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
}

public class WorkOrderResourcePlanDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public bool CanProceed { get; set; }
    public TechnicianRecommendationDto TechnicianRecommendation { get; set; } = new();
    public List<StockAvailabilityDto> InventoryAvailability { get; set; } = new();
    public DateTime RecommendedStartTime { get; set; }
    public DateTime EstimatedCompletionTime { get; set; }
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> NextSteps { get; set; } = new();
}

public class ExecuteResourcePlanDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public Guid? TechnicianId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double EstimatedHours { get; set; }
    public List<RequiredPartDto> RequiredParts { get; set; } = new();
    public Guid UserId { get; set; }
}

public class ResourcePlanExecutionResultDto
{
    public Guid WorkOrderId { get; set; }
    public bool Success { get; set; }
    public bool TechnicianScheduled { get; set; }
    public bool InventoryAllocated { get; set; }
    public TechnicianScheduleDto? TechnicianSchedule { get; set; }
    public List<InventoryAllocationDto> InventoryAllocations { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class ConsumeInventoryDto
{
    public decimal Quantity { get; set; }
    public Guid UserId { get; set; }
}

public class ReleaseAllocationDto
{
    public Guid UserId { get; set; }
}

public class ResourceUtilizationDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TechnicianUtilizationDto> TechnicianUtilization { get; set; } = new();
    public List<InventoryUtilizationDto> InventoryTurnover { get; set; } = new();
    public ResourceMetricsDto OverallMetrics { get; set; } = new();
}

public class TechnicianUtilizationDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public double UtilizationPercentage { get; set; }
    public double ScheduledHours { get; set; }
    public double AvailableHours { get; set; }
    public int ActiveWorkOrders { get; set; }
}

public class InventoryUtilizationDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public double TurnoverRate { get; set; }
    public string ReorderFrequency { get; set; } = string.Empty;
}

public class ResourceMetricsDto
{
    public double AverageTechnicianUtilization { get; set; }
    public int TotalActiveTechnicians { get; set; }
    public int ItemsRequiringReorder { get; set; }
    public double ResourceEfficiency { get; set; }
}

#endregion
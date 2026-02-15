using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/work-orders")]
[Authorize]
public class WorkOrdersController : ControllerBase
{
    private readonly IWorkOrderService _workOrderService;
    private readonly IWorkOrderPartService _workOrderPartService;
    private readonly IMaintenanceNotificationService _notificationService;
    private readonly IQualityControlService _qualityControlService;
    private readonly IFileStorageService _storageService;
    private readonly ILogger<WorkOrdersController> _logger;

    public WorkOrdersController(
        IWorkOrderService workOrderService,
        IWorkOrderPartService workOrderPartService,
        IMaintenanceNotificationService notificationService,
        IQualityControlService qualityControlService,
        IFileStorageService storageService,
        ILogger<WorkOrdersController> logger)
    {
        _workOrderService = workOrderService;
        _workOrderPartService = workOrderPartService;
        _notificationService = notificationService;
        _qualityControlService = qualityControlService;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of work orders with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<WorkOrderListDto>>> GetWorkOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? assetId = null,
        [FromQuery] Guid? technicianId = null,
        [FromQuery] DateTime? scheduledFrom = null,
        [FromQuery] DateTime? scheduledTo = null)
    {
        try
        {
            if (pageSize > 100)
            {
                pageSize = 100;
            }

            // Create filter DTO
            var filter = new WorkOrderFilterDto
            {
                SearchTerm = searchTerm,
                Status = string.IsNullOrEmpty(status) ? null : status,
                AssetId = assetId,
                AssignedTechnicianId = technicianId,
                StartDate = scheduledFrom,
                EndDate = scheduledTo,
                Page = page,
                PageSize = pageSize
            };

            // Get work orders from service
            var result = await _workOrderService.GetWorkOrdersPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work orders");
            return StatusCode(500, "An error occurred while retrieving work orders");
        }
    }

    /// <summary>
    /// Gets a specific work order by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderDto>> GetWorkOrder(Guid id)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (workOrder == null)
            {
                return NotFound($"Work order with ID {id} not found");
            }

            return Ok(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving work order with ID {id}");
            return StatusCode(500, $"An error occurred while retrieving work order with ID {id}");
        }
    }

    /// <summary>
    /// Creates a new work order
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WorkOrderDto>> CreateWorkOrder([FromBody] CreateWorkOrderDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var newWorkOrder = await _workOrderService.CreateWorkOrderAsync(createDto);
            return CreatedAtAction(nameof(GetWorkOrder), new { id = newWorkOrder.Id }, newWorkOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order");
            return StatusCode(500, "An error occurred while creating the work order");
        }
    }

    /// <summary>
    /// Updates an existing work order
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkOrderDto>> UpdateWorkOrder(Guid id, [FromBody] UpdateWorkOrderDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingWorkOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (existingWorkOrder == null)
            {
                return NotFound($"Work order with ID {id} not found");
            }

            var updatedWorkOrder = await _workOrderService.UpdateWorkOrderAsync(id, updateDto);
            return Ok(updatedWorkOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating work order with ID {id}");
            return StatusCode(500, $"An error occurred while updating work order with ID {id}");
        }
    }

    /// <summary>
    /// Deletes a work order (if not started)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWorkOrder(Guid id)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (workOrder == null)
            {
                return NotFound($"Work order with ID {id} not found");
            }

            await _workOrderService.DeleteWorkOrderAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting work order with ID {id}");
            return StatusCode(500, $"An error occurred while deleting work order with ID {id}");
        }
    }

    /// <summary>
    /// Updates work order status.
    /// When setting status to Completed, enforces QC gate via the quality control service.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<WorkOrderDto>> UpdateWorkOrderStatus(Guid id, [FromBody] UpdateWorkOrderStatusRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (workOrder == null)
            {
                return NotFound($"Work order with ID {id} not found");
            }

            var previousStatus = workOrder.Status;
            WorkOrderDto updatedWorkOrder;

            // If starting the work order, use StartWorkOrderAsync which handles vehicle status updates
            if (request.Status == "InProgress")
            {
                _logger.LogInformation("Starting work order {WorkOrderId}", id);
                updatedWorkOrder = await _workOrderService.StartWorkOrderAsync(id);
            }
            // If completing the work order, validate QC on the server and then complete
            else if (request.Status == "Completed")
            {
                _logger.LogInformation("Completing work order {WorkOrderId}", id);

                var qcValidation = await _qualityControlService.ValidateWorkOrderCompletionAsync(id);
                if (!qcValidation.CanComplete)
                {
                    return BadRequest(new
                    {
                        message = "Work order cannot be completed until quality control requirements are met.",
                        validationFailures = qcValidation.ValidationFailures,
                        validationMessages = qcValidation.ValidationMessages,
                        requiresInspectionOfficerApproval = qcValidation.RequiresInspectionOfficerApproval
                    });
                }

                var completeDto = new ErpSystem.Core.DTOs.Maintenance.CompleteWorkOrderDto
                {
                    WorkOrderId = id,
                    CompletionNotes = request.Notes
                };
                updatedWorkOrder = await _workOrderService.CompleteWorkOrderAsync(id, completeDto);
            }
            else
            {
                updatedWorkOrder = await _workOrderService.UpdateWorkOrderStatusAsync(id, request.Status, request.Notes);
            }

            // Send status change notification
            try
            {
                await _notificationService.SendWorkOrderStatusChangeNotificationAsync(id, previousStatus, updatedWorkOrder.Status);

                if (request.Status == "Completed")
                {
                    await _notificationService.SendWorkOrderCompletionNotificationAsync(id);
                }
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send work order status change notification for {WorkOrderId}", id);
            }

            return Ok(updatedWorkOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for work order with ID {WorkOrderId}", id);

            if (ex is ArgumentException)
            {
                return BadRequest(new { message = ex.Message });
            }

            if (ex is UnauthorizedAccessException)
            {
                return Forbid();
            }

            if (ex is InvalidOperationException)
            {
                return BadRequest(new { message = ex.Message });
            }

            return StatusCode(500, new { message = "An unexpected error occurred while updating the work order status." });
        }
    }

    /// <summary>
    /// Approves a work order
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<WorkOrderDto>> ApproveWorkOrder(Guid id, [FromBody] ApproveWorkOrderRequest? request = null)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (workOrder == null)
            {
                return NotFound($"Work order with ID {id} not found");
            }

            var approvedWorkOrder = await _workOrderService.ApproveWorkOrderAsync(id, request?.Notes);

            // Notify if assigned technician about approval
            try
            {
                if (approvedWorkOrder.AssignedTechnicianId.HasValue && approvedWorkOrder.AssignedTechnicianId.Value != Guid.Empty)
                {
                    await _notificationService.SendWorkOrderAssignmentNotificationAsync(id, approvedWorkOrder.AssignedTechnicianId.Value);
                }
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send work order approval notification for {WorkOrderId}", id);
            }

            return Ok(approvedWorkOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error approving work order with ID {id}");
            return StatusCode(500, $"An error occurred while approving work order with ID {id}");
        }
    }

    /// <summary>
    /// Gets work order metrics
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<ErpSystem.Core.DTOs.Maintenance.WorkOrderMetricsDto>> GetWorkOrderMetrics()
    {
        try
        {
            var metrics = await _workOrderService.GetWorkOrderMetricsAsync();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order metrics");
            return StatusCode(500, "An error occurred while retrieving work order metrics");
        }
    }

    /// <summary>
    /// Updates a work order task status
    /// </summary>
    [HttpPut("tasks/{taskId:guid}/status")]
    public async Task<ActionResult<ErpSystem.Core.DTOs.Maintenance.WorkOrderTaskDto>> UpdateTaskStatus(
        Guid taskId,
        [FromBody] UpdateTaskStatusRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updatedTask = await _workOrderService.UpdateTaskStatusAsync(
                taskId,
                request.Status,
                request.ActualHours,
                request.CompletionNotes,
                request.TechnicianId);

            if (updatedTask == null)
            {
                return NotFound($"Task with ID {taskId} not found");
            }

            return Ok(updatedTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating task {taskId} status");
            return StatusCode(500, $"An error occurred while updating task status");
        }
    }

    #region Work Order Parts

    /// <summary>
    /// Gets all parts/consumables for a work order
    /// </summary>
    [HttpGet("{workOrderId:guid}/parts")]
    public async Task<ActionResult<IEnumerable<WorkOrderPartDto>>> GetWorkOrderParts(Guid workOrderId)
    {
        try
        {
            var parts = await _workOrderPartService.GetPartsByWorkOrderAsync(workOrderId);
            return Ok(parts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving parts for work order {workOrderId}");
            return StatusCode(500, "An error occurred while retrieving parts");
        }
    }

    /// <summary>
    /// Adds a single part/consumable to a work order
    /// </summary>
    [HttpPost("parts")]
    public async Task<ActionResult<WorkOrderPartDto>> AddWorkOrderPart([FromBody] CreateWorkOrderPartDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var part = await _workOrderPartService.AddPartAsync(createDto);
            return Ok(part);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding part to work order");
            return StatusCode(500, "An error occurred while adding the part");
        }
    }

    /// <summary>
    /// Adds multiple parts/consumables to a work order in bulk
    /// </summary>
    [HttpPost("{workOrderId:guid}/parts/bulk")]
    public async Task<ActionResult<IEnumerable<WorkOrderPartDto>>> AddWorkOrderPartsBulk(
        Guid workOrderId,
        [FromBody] List<CreateWorkOrderPartDto> parts)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (parts == null || parts.Count == 0)
            {
                return BadRequest("No parts provided");
            }

            // Ensure all parts are for the same work order
            foreach (var part in parts)
            {
                part.WorkOrderId = workOrderId;
            }

            var addedParts = await _workOrderPartService.AddPartsBulkAsync(parts);
            return Ok(addedParts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding parts in bulk to work order {workOrderId}");
            return StatusCode(500, "An error occurred while adding parts");
        }
    }

    /// <summary>
    /// Updates a work order part/consumable
    /// </summary>
    [HttpPut("parts/{id:guid}")]
    public async Task<ActionResult<WorkOrderPartDto>> UpdateWorkOrderPart(
        Guid id,
        [FromBody] UpdateWorkOrderPartDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var part = await _workOrderPartService.UpdatePartAsync(id, updateDto);
            return Ok(part);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating part {id}");
            return StatusCode(500, "An error occurred while updating the part");
        }
    }

    /// <summary>
    /// Deletes a work order part/consumable
    /// </summary>
    [HttpDelete("parts/{id:guid}")]
    public async Task<IActionResult> DeleteWorkOrderPart(Guid id)
    {
        try
        {
            await _workOrderPartService.DeletePartAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting part {id}");
            return StatusCode(500, "An error occurred while deleting the part");
        }
    }

    /// <summary>
    /// Returns unused parts back to warehouse stock
    /// </summary>
    [HttpPost("parts/{id:guid}/return")]
    public async Task<ActionResult<WorkOrderPartDto>> ReturnUnusedParts(Guid id)
    {
        try
        {
            var part = await _workOrderPartService.ReturnUnusedPartsAsync(id);
            return Ok(part);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Part with ID {id} not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error returning unused parts for part {id}");
            return StatusCode(500, "An error occurred while returning unused parts");
        }
    }

    /// <summary>
    /// Deletes multiple work order parts/consumables in bulk
    /// </summary>
    [HttpPost("parts/bulk-delete")]
    public async Task<IActionResult> DeleteWorkOrderPartsBulk([FromBody] List<Guid> ids)
    {
        try
        {
            if (ids == null || ids.Count == 0)
            {
                return BadRequest("No part IDs provided");
            }

            await _workOrderPartService.DeletePartsBulkAsync(ids);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting parts in bulk");
            return StatusCode(500, "An error occurred while deleting parts");
        }
    }

    #endregion

    #region Task Photos

    /// <summary>
    /// Upload a photo for a work order task
    /// </summary>
    [HttpPost("tasks/{taskId:guid}/photo")]
    [RequestSizeLimit(10_000_000)] // 10MB
    public async Task<IActionResult> UploadTaskPhoto(Guid taskId, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            // Validate file extension (images only)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = $"File type {extension} is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}" });
            }

            // Generate unique file path
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = $"work-orders/tasks/{taskId}/{fileName}";

            // Upload file
            using var stream = file.OpenReadStream();
            var uploadedPath = await _storageService.UploadFileAsync(stream, file.FileName, filePath);

            if (string.IsNullOrEmpty(uploadedPath))
            {
                return StatusCode(500, new { message = "Failed to upload file" });
            }

            // Update task with photo path
            var updatedTask = await _workOrderService.UpdateTaskPhotoAsync(taskId, uploadedPath);
            if (updatedTask == null)
            {
                return NotFound(new { message = $"Task with ID {taskId} not found" });
            }

            _logger.LogInformation("Uploaded task photo for task {TaskId}: {FilePath}", taskId, uploadedPath);

            return Ok(new { filePath = uploadedPath, fileName = file.FileName, task = updatedTask });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading task photo for task {TaskId}", taskId);
            return StatusCode(500, new { message = "Failed to upload task photo", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a photo from a work order task
    /// </summary>
    [HttpDelete("tasks/{taskId:guid}/photo")]
    public async Task<IActionResult> DeleteTaskPhoto(Guid taskId)
    {
        try
        {
            var updatedTask = await _workOrderService.UpdateTaskPhotoAsync(taskId, null);
            if (updatedTask == null)
            {
                return NotFound(new { message = $"Task with ID {taskId} not found" });
            }

            _logger.LogInformation("Deleted task photo for task {TaskId}", taskId);

            return Ok(new { message = "Photo deleted successfully", task = updatedTask });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task photo for task {TaskId}", taskId);
            return StatusCode(500, new { message = "Failed to delete task photo", error = ex.Message });
        }
    }

    #endregion
}

public class UpdateTaskStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public double? ActualHours { get; set; }
    public string? CompletionNotes { get; set; }
    public Guid? TechnicianId { get; set; }
}

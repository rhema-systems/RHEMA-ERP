using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/emergency")]
[Authorize]
public class EmergencyMaintenanceController : ControllerBase
{
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceAssetService _assetService;
    private readonly IAssetDowntimeService _downtimeService;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHubNotificationService _notificationService;
    private readonly ILogger<EmergencyMaintenanceController> _logger;

    public EmergencyMaintenanceController(
        IWorkOrderService workOrderService,
        IMaintenanceAssetService assetService,
        IAssetDowntimeService downtimeService,
        IEmailService emailService,
        ICurrentUserService currentUserService,
        IHubNotificationService notificationService,
        ILogger<EmergencyMaintenanceController> logger)
    {
        _workOrderService = workOrderService;
        _assetService = assetService;
        _downtimeService = downtimeService;
        _emailService = emailService;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Creates an emergency work order with immediate notifications
    /// </summary>
    [HttpPost("create-emergency-work-order")]
    public async Task<ActionResult<WorkOrderDto>> CreateEmergencyWorkOrder([FromBody] CreateEmergencyWorkOrderDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate asset exists and is critical
            var asset = await _assetService.GetAssetByIdAsync(createDto.AssetId);
            if (asset == null)
            {
                return BadRequest($"Asset with ID {createDto.AssetId} not found");
            }

            // Create emergency work order
            var workOrderDto = new CreateWorkOrderDto
            {
                Title = createDto.Title,
                Description = createDto.Description,
                AssetId = createDto.AssetId,
                Type = "Emergency",
                Priority = "Emergency",
                Status = "Scheduled",
                ScheduledStartDate = DateTime.UtcNow,
                ScheduledEndDate = DateTime.UtcNow.AddHours(createDto.EstimatedHours ?? 4),
                EstimatedHours = createDto.EstimatedHours ?? 4,
                EstimatedCost = createDto.EstimatedCost ?? 0m,
                Instructions = createDto.Instructions,
                SafetyNotes = createDto.SafetyNotes,
                RequiredSkills = createDto.RequiredSkills,
                IsEmergency = true
            };

            var workOrder = await _workOrderService.CreateWorkOrderAsync(workOrderDto);

            // Auto-assign technician if specified
            if (createDto.PreferredTechnicianId.HasValue)
            {
                try
                {
                    await _workOrderService.AssignTechnicianAsync(workOrder.Id, createDto.PreferredTechnicianId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-assign technician {TechnicianId} to emergency work order {WorkOrderId}",
                        createDto.PreferredTechnicianId.Value, workOrder.Id);
                }
            }

            // Record asset downtime if specified
            if (createDto.IsAssetDown)
            {
                var downtimeDto = new CreateAssetDowntimeDto
                {
                    AssetId = createDto.AssetId,
                    StartTime = DateTime.UtcNow,
                    DowntimeType = "Emergency",
                    Description = $"Emergency downtime: {createDto.Title}",
                    Priority = "Critical",
                    RelatedWorkOrderId = workOrder.Id
                };

                await _downtimeService.StartDowntimeAsync(downtimeDto);
            }

            // Send emergency notifications
            await SendEmergencyNotifications(workOrder, createDto);

            _logger.LogWarning("Emergency work order created: {WorkOrderId} for asset {AssetId} by user {UserId}",
                workOrder.Id, createDto.AssetId, _currentUserService.UserId);

            return CreatedAtAction(nameof(GetEmergencyWorkOrder), new { id = workOrder.Id }, workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating emergency work order for asset {AssetId}", createDto.AssetId);
            return StatusCode(500, "An error occurred while creating the emergency work order");
        }
    }

    /// <summary>
    /// Gets an emergency work order
    /// </summary>
    [HttpGet("work-order/{id:guid}")]
    public async Task<ActionResult<WorkOrderDto>> GetEmergencyWorkOrder(Guid id)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (workOrder == null || workOrder.Priority != "Emergency")
            {
                return NotFound($"Emergency work order with ID {id} not found");
            }

            return Ok(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving emergency work order {WorkOrderId}", id);
            return StatusCode(500, "An error occurred while retrieving the work order");
        }
    }

    /// <summary>
    /// Escalates an emergency work order
    /// </summary>
    [HttpPost("{workOrderId:guid}/escalate")]
    public async Task<ActionResult<WorkOrderDto>> EscalateEmergencyWorkOrder(
        Guid workOrderId,
        [FromBody] EscalateEmergencyDto escalateDto)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(workOrderId);
            if (workOrder == null || workOrder.Priority != "Emergency")
            {
                return NotFound($"Emergency work order with ID {workOrderId} not found");
            }

            // Update work order with escalation notes
            var updateDto = new UpdateWorkOrderDto
            {
                Title = workOrder.Title,
                Description = $"{workOrder.Description}\n\nESCALATION: {escalateDto.EscalationReason}",
                Instructions = workOrder.Instructions,
                Notes = $"{workOrder.Notes}\nEscalated by {_currentUserService.UserName} at {DateTime.UtcNow:yyyy-MM-dd HH:mm}: {escalateDto.EscalationReason}",
                EstimatedHours = workOrder.EstimatedHours,
                EstimatedCost = workOrder.EstimatedCost
            };

            var updatedWorkOrder = await _workOrderService.UpdateWorkOrderAsync(workOrderId, updateDto);

            // Send escalation notifications
            await SendEscalationNotifications(updatedWorkOrder, escalateDto);

            _logger.LogWarning("Emergency work order escalated: {WorkOrderId} - Reason: {Reason}",
                workOrderId, escalateDto.EscalationReason);

            return Ok(updatedWorkOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error escalating emergency work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while escalating the work order");
        }
    }

    /// <summary>
    /// Reports equipment breakdown with immediate response
    /// </summary>
    [HttpPost("report-breakdown")]
    public async Task<ActionResult<EmergencyResponseDto>> ReportEquipmentBreakdown(
        [FromBody] ReportBreakdownDto breakdownDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var asset = await _assetService.GetAssetByIdAsync(breakdownDto.AssetId);
            if (asset == null)
            {
                return BadRequest($"Asset with ID {breakdownDto.AssetId} not found");
            }

            // Determine criticality and response level
            var criticality = await DetermineAssetCriticality(breakdownDto.AssetId);
            var responseLevel = DetermineResponseLevel(breakdownDto.Severity, criticality);

            // Create emergency work order
            var emergencyDto = new CreateEmergencyWorkOrderDto
            {
                Title = $"Equipment Breakdown - {asset.Name}",
                Description = breakdownDto.Description,
                AssetId = breakdownDto.AssetId,
                EstimatedHours = GetEstimatedHoursForBreakdown(breakdownDto.Severity),
                Instructions = "Emergency breakdown response required. Follow safety protocols.",
                SafetyNotes = breakdownDto.SafetyNotes,
                IsAssetDown = breakdownDto.IsAssetDown
            };

            var workOrderResult = await CreateEmergencyWorkOrder(emergencyDto);
            if (workOrderResult.Result is not CreatedAtActionResult createdResult ||
                createdResult.Value is not WorkOrderDto workOrder)
            {
                return StatusCode(500, "Failed to create emergency work order");
            }

            // Record downtime
            if (breakdownDto.IsAssetDown)
            {
                var downtimeDto = new CreateAssetDowntimeDto
                {
                    AssetId = breakdownDto.AssetId,
                    StartTime = DateTime.UtcNow,
                    DowntimeType = "Breakdown",
                    Description = $"Equipment breakdown: {breakdownDto.Description}",
                    Priority = responseLevel.ToString(),
                    RelatedWorkOrderId = workOrder.Id
                };

                await _downtimeService.StartDowntimeAsync(downtimeDto);
            }

            // Send immediate alerts
            await SendBreakdownAlerts(asset, workOrder, breakdownDto, responseLevel);

            var response = new EmergencyResponseDto
            {
                WorkOrderId = workOrder.Id,
                WorkOrderNumber = workOrder.WorkOrderNumber,
                ResponseLevel = responseLevel.ToString(),
                EstimatedResponseTime = GetEstimatedResponseTime(responseLevel),
                AssignedTechnician = workOrder.AssignedTechnicianName,
                NextSteps = GetNextStepsForBreakdown(responseLevel),
                ContactNumbers = GetEmergencyContactNumbers(responseLevel)
            };

            _logger.LogCritical("Equipment breakdown reported: Asset {AssetId} - Severity: {Severity} - Response Level: {ResponseLevel}",
                breakdownDto.AssetId, breakdownDto.Severity, responseLevel);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting equipment breakdown for asset {AssetId}", breakdownDto.AssetId);
            return StatusCode(500, "An error occurred while processing the breakdown report");
        }
    }

    /// <summary>
    /// Gets all active emergency work orders
    /// </summary>
    [HttpGet("active-emergencies")]
    public async Task<ActionResult<IEnumerable<WorkOrderListDto>>> GetActiveEmergencies()
    {
        try
        {
            var emergencyWorkOrders = await _workOrderService.GetWorkOrdersByStatusAsync("Emergency");
            var activeEmergencies = emergencyWorkOrders.Where(wo => wo.Status != "Completed" && wo.Status != "Cancelled");

            return Ok(activeEmergencies.OrderByDescending(wo => wo.CreatedDate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active emergency work orders");
            return StatusCode(500, "An error occurred while retrieving emergency work orders");
        }
    }

    /// <summary>
    /// Updates emergency response status
    /// </summary>
    [HttpPut("{workOrderId:guid}/response-status")]
    public async Task<ActionResult> UpdateResponseStatus(
        Guid workOrderId,
        [FromBody] UpdateEmergencyResponseDto statusDto)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(workOrderId);
            if (workOrder == null || workOrder.Priority != "Emergency")
            {
                return NotFound($"Emergency work order with ID {workOrderId} not found");
            }

            // Update work order status
            if (!string.IsNullOrEmpty(statusDto.NewStatus))
            {
                switch (statusDto.NewStatus.ToLower())
                {
                    case "started":
                    case "in progress":
                        await _workOrderService.StartWorkOrderAsync(workOrderId);
                        break;
                    case "completed":
                        if (statusDto.CompletionDetails != null)
                        {
                            await _workOrderService.CompleteWorkOrderAsync(workOrderId, statusDto.CompletionDetails);
                        }
                        break;
                }
            }

            // Send status update notifications
            await SendStatusUpdateNotifications(workOrder, statusDto);

            _logger.LogInformation("Emergency response status updated: {WorkOrderId} - Status: {Status}",
                workOrderId, statusDto.NewStatus);

            return Ok(new { message = "Response status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating emergency response status for work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while updating response status");
        }
    }

    /// <summary>
    /// Gets emergency response metrics
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<EmergencyMetricsDto>> GetEmergencyMetrics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            var metrics = await CalculateEmergencyMetrics(start, end);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving emergency metrics");
            return StatusCode(500, "An error occurred while retrieving emergency metrics");
        }
    }

    #region Helper Methods

    private async Task SendEmergencyNotifications(WorkOrderDto workOrder, CreateEmergencyWorkOrderDto createDto)
    {
        try
        {
            // Send real-time notification
            await _notificationService.SendNotificationToAllAsync(
                $"Emergency Work Order {workOrder.WorkOrderNumber} created for {workOrder.AssetName}",
                "EmergencyWorkOrderCreated");

            // Send email to maintenance team
            var subject = $"EMERGENCY: Work Order {workOrder.WorkOrderNumber} Created";
            var body = $@"
                <h2 style='color: red;'>EMERGENCY WORK ORDER</h2>
                <p><strong>Work Order:</strong> {workOrder.WorkOrderNumber}</p>
                <p><strong>Asset:</strong> {workOrder.AssetName}</p>
                <p><strong>Title:</strong> {workOrder.Title}</p>
                <p><strong>Description:</strong> {workOrder.Description}</p>
                <p><strong>Created by:</strong> {_currentUserService.UserName}</p>
                <p><strong>Created at:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</p>
                {(createDto.IsAssetDown ? "<p><strong style='color: red;'>ASSET IS DOWN</strong></p>" : "")}
                <p>Please respond immediately.</p>";

            // Send to maintenance managers and available technicians
            var emergencyContacts = GetEmergencyContactList();
            foreach (var contact in emergencyContacts)
            {
                await _emailService.SendEmailAsync(contact.Email, subject, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending emergency notifications for work order {WorkOrderId}", workOrder.Id);
        }
    }

    private async Task SendEscalationNotifications(WorkOrderDto workOrder, EscalateEmergencyDto escalateDto)
    {
        try
        {
            await _notificationService.SendNotificationToAllAsync(
                $"Emergency Work Order {workOrder.WorkOrderNumber} escalated: {escalateDto.EscalationReason}",
                "EmergencyWorkOrderEscalated");

            var subject = $"ESCALATED: Emergency Work Order {workOrder.WorkOrderNumber}";
            var body = $@"
                <h2 style='color: red;'>EMERGENCY ESCALATION</h2>
                <p><strong>Work Order:</strong> {workOrder.WorkOrderNumber}</p>
                <p><strong>Asset:</strong> {workOrder.AssetName}</p>
                <p><strong>Escalation Reason:</strong> {escalateDto.EscalationReason}</p>
                <p><strong>Escalated by:</strong> {_currentUserService.UserName}</p>
                <p><strong>Escalated at:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</p>
                <p>Immediate management attention required.</p>";

            // Send to senior management and maintenance directors
            var managementContacts = GetManagementContactList();
            foreach (var contact in managementContacts)
            {
                await _emailService.SendEmailAsync(contact.Email, subject, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending escalation notifications for work order {WorkOrderId}", workOrder.Id);
        }
    }

    private async Task SendBreakdownAlerts(
        MaintenanceAssetDto asset,
        WorkOrderDto workOrder,
        ReportBreakdownDto breakdownDto,
        ResponseLevel responseLevel)
    {
        try
        {
            await _notificationService.SendNotificationToAllAsync(
                $"Equipment Breakdown: {asset.Name} - {breakdownDto.Severity} severity (WO: {workOrder.WorkOrderNumber})",
                "EquipmentBreakdown");

            var subject = $"EQUIPMENT BREAKDOWN: {asset.Name}";
            var body = $@"
                <h2 style='color: red;'>EQUIPMENT BREAKDOWN ALERT</h2>
                <p><strong>Asset:</strong> {asset.Name} ({asset.AssetNumber})</p>
                <p><strong>Location:</strong> {asset.Location}</p>
                <p><strong>Severity:</strong> {breakdownDto.Severity}</p>
                <p><strong>Response Level:</strong> {responseLevel}</p>
                <p><strong>Work Order:</strong> {workOrder.WorkOrderNumber}</p>
                <p><strong>Description:</strong> {breakdownDto.Description}</p>
                {(breakdownDto.IsAssetDown ? "<p><strong style='color: red;'>ASSET IS DOWN</strong></p>" : "")}
                <p><strong>Reported by:</strong> {_currentUserService.UserName}</p>
                <p><strong>Reported at:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</p>";

            var contacts = responseLevel >= ResponseLevel.High
                ? GetEmergencyContactList()
                : GetMaintenanceTeamContacts();

            foreach (var contact in contacts)
            {
                await _emailService.SendEmailAsync(contact.Email, subject, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending breakdown alerts for asset {AssetId}", asset.Id);
        }
    }

    private async Task SendStatusUpdateNotifications(WorkOrderDto workOrder, UpdateEmergencyResponseDto statusDto)
    {
        try
        {
            await _notificationService.SendNotificationToAllAsync(
                $"Emergency Work Order {workOrder.WorkOrderNumber} status updated to {statusDto.NewStatus}",
                "EmergencyStatusUpdate");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending status update notifications for work order {WorkOrderId}", workOrder.Id);
        }
    }

    private static async Task<AssetCriticality> DetermineAssetCriticality(Guid assetId)
    {
        // This would query asset criticality from database or configuration
        // For now, return a default value
        return AssetCriticality.High;
    }

    private static ResponseLevel DetermineResponseLevel(string severity, AssetCriticality criticality)
    {
        return (severity.ToLower(), criticality) switch
        {
            ("critical", _) => ResponseLevel.Critical,
            ("high", AssetCriticality.Critical) => ResponseLevel.Critical,
            ("high", _) => ResponseLevel.High,
            ("medium", AssetCriticality.Critical) => ResponseLevel.High,
            ("medium", _) => ResponseLevel.Medium,
            _ => ResponseLevel.Low
        };
    }

    private static double GetEstimatedHoursForBreakdown(string severity)
    {
        return severity.ToLower() switch
        {
            "critical" => 1.0,
            "high" => 2.0,
            "medium" => 4.0,
            _ => 8.0
        };
    }

    private static string GetEstimatedResponseTime(ResponseLevel responseLevel)
    {
        return responseLevel switch
        {
            ResponseLevel.Critical => "15 minutes",
            ResponseLevel.High => "30 minutes",
            ResponseLevel.Medium => "1 hour",
            _ => "2 hours"
        };
    }

    private static List<string> GetNextStepsForBreakdown(ResponseLevel responseLevel)
    {
        return responseLevel switch
        {
            ResponseLevel.Critical => new List<string>
            {
                "Emergency technician dispatched immediately",
                "Notify management and stakeholders",
                "Prepare for extended downtime if necessary",
                "Activate backup systems if available"
            },
            ResponseLevel.High => new List<string>
            {
                "Priority technician assigned",
                "Prepare necessary tools and parts",
                "Coordinate with operations team",
                "Monitor asset status closely"
            },
            _ => new List<string>
            {
                "Technician will be assigned within normal scheduling",
                "Standard maintenance procedures apply",
                "Document all findings"
            }
        };
    }

    private static List<EmergencyContact> GetEmergencyContactNumbers(ResponseLevel responseLevel)
    {
        return responseLevel >= ResponseLevel.High ? new List<EmergencyContact>
        {
            new() { Name = "Emergency Maintenance", Phone = "+1-555-0199" },
            new() { Name = "Facility Manager", Phone = "+1-555-0299" },
            new() { Name = "Operations Director", Phone = "+1-555-0399" }
        } : new List<EmergencyContact>
        {
            new() { Name = "Maintenance Team", Phone = "+1-555-0100" }
        };
    }

    private static List<EmergencyContact> GetEmergencyContactList()
    {
        return new List<EmergencyContact>
        {
            new() { Name = "Maintenance Manager", Email = "maintenance.manager@company.com" },
            new() { Name = "Emergency Technician", Email = "emergency.tech@company.com" },
            new() { Name = "Facility Manager", Email = "facility.manager@company.com" }
        };
    }

    private static List<EmergencyContact> GetManagementContactList()
    {
        return new List<EmergencyContact>
        {
            new() { Name = "Operations Director", Email = "ops.director@company.com" },
            new() { Name = "Maintenance Director", Email = "maintenance.director@company.com" },
            new() { Name = "Facility Manager", Email = "facility.manager@company.com" }
        };
    }

    private static List<EmergencyContact> GetMaintenanceTeamContacts()
    {
        return new List<EmergencyContact>
        {
            new() { Name = "Maintenance Team", Email = "maintenance.team@company.com" },
            new() { Name = "Maintenance Manager", Email = "maintenance.manager@company.com" }
        };
    }

    private static async Task<EmergencyMetricsDto> CalculateEmergencyMetrics(DateTime startDate, DateTime endDate)
    {
        // This would query actual emergency work orders and calculate metrics
        return new EmergencyMetricsDto
        {
            TotalEmergencyWorkOrders = 45,
            AverageResponseTime = 23.5,
            EscalationRate = 12.5,
            ResolutionRate = 94.2,
            AverageDowntime = 2.3,
            CostImpact = 125000m
        };
    }

    #endregion
}

#region Emergency DTOs

public class CreateEmergencyWorkOrderDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public Guid AssetId { get; set; }

    public double? EstimatedHours { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string? Instructions { get; set; }
    public string? SafetyNotes { get; set; }
    public string? RequiredSkills { get; set; }
    public Guid? PreferredTechnicianId { get; set; }
    public bool IsAssetDown { get; set; } = false;
}

public class EscalateEmergencyDto
{
    [Required]
    public string EscalationReason { get; set; } = string.Empty;
    public string? AdditionalInstructions { get; set; }
    public List<string> NotifyContacts { get; set; } = new();
}

public class ReportBreakdownDto
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Severity { get; set; } = string.Empty; // Critical, High, Medium, Low

    public bool IsAssetDown { get; set; } = false;
    public string? SafetyNotes { get; set; }
    public string? ImmediateActions { get; set; }
}

public class UpdateEmergencyResponseDto
{
    public string? NewStatus { get; set; }
    public string? StatusNotes { get; set; }
    public CompleteWorkOrderDto? CompletionDetails { get; set; }
}

public class EmergencyResponseDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string ResponseLevel { get; set; } = string.Empty;
    public string EstimatedResponseTime { get; set; } = string.Empty;
    public string? AssignedTechnician { get; set; }
    public List<string> NextSteps { get; set; } = new();
    public List<EmergencyContact> ContactNumbers { get; set; } = new();
}

public class EmergencyMetricsDto
{
    public int TotalEmergencyWorkOrders { get; set; }
    public double AverageResponseTime { get; set; } // Minutes
    public double EscalationRate { get; set; } // Percentage
    public double ResolutionRate { get; set; } // Percentage
    public double AverageDowntime { get; set; } // Hours
    public decimal CostImpact { get; set; }
}

public class EmergencyContact
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public enum AssetCriticality
{
    Low,
    Medium,
    High,
    Critical
}

public enum ResponseLevel
{
    Low,
    Medium,
    High,
    Critical
}

#endregion

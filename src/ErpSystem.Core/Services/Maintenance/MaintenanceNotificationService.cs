using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Enums;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing maintenance notifications, alerts, and reminders
/// </summary>
public class MaintenanceNotificationService : IMaintenanceNotificationService
{
    private readonly IEmailService _emailService;
    private readonly IMaintenanceScheduleService _scheduleService;
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceAssetService _assetService;
    private readonly ITechnicianService _technicianService;
    private readonly ILogger<MaintenanceNotificationService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    public MaintenanceNotificationService(
        IEmailService emailService,
        IMaintenanceScheduleService scheduleService,
        IWorkOrderService workOrderService,
        IMaintenanceAssetService assetService,
        ITechnicianService technicianService,
        ILogger<MaintenanceNotificationService> logger,
        ICurrentUserProvider currentUserProvider)
    {
        _emailService = emailService;
        _scheduleService = scheduleService;
        _workOrderService = workOrderService;
        _assetService = assetService;
        _technicianService = technicianService;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
    }

    #region Scheduled Maintenance Notifications

    public async Task SendMaintenanceDueNotificationsAsync()
    {
        try
        {
            _logger.LogInformation("Processing maintenance due notifications");

            // Get schedules due in the next 7 days
            var upcomingSchedules = await _scheduleService.GetSchedulesDueInDaysAsync(7);
            
            foreach (var schedule in upcomingSchedules)
            {
                await SendScheduleReminderAsync(schedule);
            }

            // Get overdue schedules
            var overdueSchedules = await _scheduleService.GetOverdueSchedulesAsync();
            
            foreach (var schedule in overdueSchedules)
            {
                await SendOverdueMaintenanceAlertAsync(schedule);
            }

            _logger.LogInformation("Processed {UpcomingCount} upcoming and {OverdueCount} overdue maintenance notifications", 
                upcomingSchedules.Count(), overdueSchedules.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing maintenance due notifications");
            throw;
        }
    }

    public async Task SendScheduleReminderAsync(MaintenanceScheduleDto schedule)
    {
        try
        {
            _logger.LogInformation("Sending schedule reminder for schedule {ScheduleId}", schedule.Id);

            var asset = await _assetService.GetAssetByIdAsync(schedule.AssetId);
            if (asset == null)
            {
                _logger.LogWarning("Asset not found for schedule {ScheduleId}", schedule.Id);
                return;
            }

            var notification = new MaintenanceNotificationDto
            {
                Title = "Upcoming Maintenance Reminder",
                Message = $"Maintenance '{schedule.Name}' is due for asset '{asset.Name}' on {schedule.NextDueDate:MMM dd, yyyy}",
                Type = NotificationType.Reminder,
                Priority = MapPriorityToNotificationPriority(schedule.Priority),
                RelatedEntityType = "MaintenanceSchedule",
                RelatedEntityId = schedule.Id,
                Recipients = await GetScheduleRecipientsAsync(schedule),
                Data = new Dictionary<string, object>
                {
                    ["ScheduleId"] = schedule.Id,
                    ["AssetId"] = schedule.AssetId,
                    ["AssetName"] = asset.Name,
                    ["DueDate"] = schedule.NextDueDate
                }
            };

            await ProcessNotificationAsync(notification);

            _logger.LogInformation("Schedule reminder sent for schedule {ScheduleId}", schedule.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending schedule reminder for schedule {ScheduleId}", schedule.Id);
            throw;
        }
    }

    public async Task SendOverdueMaintenanceAlertAsync(MaintenanceScheduleDto schedule)
    {
        try
        {
            _logger.LogInformation("Sending overdue maintenance alert for schedule {ScheduleId}", schedule.Id);

            var asset = await _assetService.GetAssetByIdAsync(schedule.AssetId);
            if (asset == null)
            {
                _logger.LogWarning("Asset not found for schedule {ScheduleId}", schedule.Id);
                return;
            }

            var daysOverdue = (DateTime.Now - schedule.NextDueDate).Days;

            var notification = new MaintenanceNotificationDto
            {
                Title = "OVERDUE: Maintenance Alert",
                Message = $"URGENT: Maintenance '{schedule.Name}' for asset '{asset.Name}' is {daysOverdue} days overdue!",
                Type = NotificationType.Alert,
                Priority = NotificationPriority.Critical,
                RelatedEntityType = "MaintenanceSchedule",
                RelatedEntityId = schedule.Id,
                Recipients = await GetScheduleRecipientsAsync(schedule),
                Data = new Dictionary<string, object>
                {
                    ["ScheduleId"] = schedule.Id,
                    ["AssetId"] = schedule.AssetId,
                    ["AssetName"] = asset.Name,
                    ["DueDate"] = schedule.NextDueDate,
                    ["DaysOverdue"] = daysOverdue
                }
            };

            await ProcessNotificationAsync(notification);

            _logger.LogInformation("Overdue maintenance alert sent for schedule {ScheduleId}", schedule.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending overdue maintenance alert for schedule {ScheduleId}", schedule.Id);
            throw;
        }
    }

    #endregion

    #region Work Order Notifications

    public async Task SendWorkOrderAssignmentNotificationAsync(Guid workOrderId, Guid technicianId)
    {
        try
        {
            _logger.LogInformation("Sending work order assignment notification for WO {WorkOrderId} to technician {TechnicianId}", 
                workOrderId, technicianId);

            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(workOrderId);
            if (workOrder == null)
            {
                _logger.LogWarning("Work order not found: {WorkOrderId}", workOrderId);
                return;
            }

            var technician = await _technicianService.GetTechnicianByIdAsync(technicianId);
            if (technician == null)
            {
                _logger.LogWarning("Technician not found: {TechnicianId}", technicianId);
                return;
            }

            var notification = new MaintenanceNotificationDto
            {
                Title = "New Work Order Assignment",
                Message = $"You have been assigned work order '{workOrder.Title}' (#{workOrder.WorkOrderNumber})",
                Type = NotificationType.Assignment,
                Priority = MapPriorityToNotificationPriority(workOrder.Priority),
                RelatedEntityType = "WorkOrder",
                RelatedEntityId = workOrderId,
                Recipients = new List<NotificationRecipientDto>
                {
                    new NotificationRecipientDto
                    {
                        UserId = technicianId,
                        Name = technician.Name,
                        Email = technician.Email,
                        Type = RecipientType.Primary
                    }
                },
                Data = new Dictionary<string, object>
                {
                    ["WorkOrderId"] = workOrderId,
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["AssetName"] = workOrder.Asset?.Name,
                    ["ScheduledDate"] = workOrder.ScheduledStartDate
                }
            };

            await ProcessNotificationAsync(notification);

            _logger.LogInformation("Work order assignment notification sent for WO {WorkOrderId}", workOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending work order assignment notification for WO {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task SendWorkOrderStatusChangeNotificationAsync(Guid workOrderId, string previousStatus, string newStatus)
    {
        try
        {
            _logger.LogInformation("Sending work order status change notification for WO {WorkOrderId}: {PreviousStatus} -> {NewStatus}", 
                workOrderId, previousStatus, newStatus);

            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(workOrderId);
            if (workOrder == null)
            {
                _logger.LogWarning("Work order not found: {WorkOrderId}", workOrderId);
                return;
            }

            var recipients = await GetWorkOrderRecipientsAsync(workOrder);

            var notification = new MaintenanceNotificationDto
            {
                Title = $"Work Order Status Updated: {newStatus}",
                Message = $"Work order '{workOrder.Title}' (#{workOrder.WorkOrderNumber}) status changed from '{previousStatus}' to '{newStatus}'",
                Type = NotificationType.StatusUpdate,
                Priority = GetStatusChangeNotificationPriority(newStatus),
                RelatedEntityType = "WorkOrder",
                RelatedEntityId = workOrderId,
                Recipients = recipients,
                Data = new Dictionary<string, object>
                {
                    ["WorkOrderId"] = workOrderId,
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["PreviousStatus"] = previousStatus,
                    ["NewStatus"] = newStatus,
                    ["AssetName"] = workOrder.Asset?.Name
                }
            };

            await ProcessNotificationAsync(notification);

            _logger.LogInformation("Work order status change notification sent for WO {WorkOrderId}", workOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending work order status change notification for WO {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task SendWorkOrderCompletionNotificationAsync(Guid workOrderId)
    {
        try
        {
            _logger.LogInformation("Sending work order completion notification for WO {WorkOrderId}", workOrderId);

            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(workOrderId);
            if (workOrder == null)
            {
                _logger.LogWarning("Work order not found: {WorkOrderId}", workOrderId);
                return;
            }

            var recipients = await GetWorkOrderRecipientsAsync(workOrder);

            var notification = new MaintenanceNotificationDto
            {
                Title = "Work Order Completed",
                Message = $"Work order '{workOrder.Title}' (#{workOrder.WorkOrderNumber}) has been completed successfully",
                Type = NotificationType.Completion,
                Priority = NotificationPriority.Normal,
                RelatedEntityType = "WorkOrder",
                RelatedEntityId = workOrderId,
                Recipients = recipients,
                Data = new Dictionary<string, object>
                {
                    ["WorkOrderId"] = workOrderId,
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["AssetName"] = workOrder.Asset?.Name,
                    ["CompletionDate"] = workOrder.ActualEndDate,
                    ["ActualHours"] = workOrder.ActualHours,
                    ["ActualCost"] = workOrder.ActualCost
                }
            };

            await ProcessNotificationAsync(notification);

            _logger.LogInformation("Work order completion notification sent for WO {WorkOrderId}", workOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending work order completion notification for WO {WorkOrderId}", workOrderId);
            throw;
        }
    }

    #endregion

    #region Asset Notifications

    public async Task SendAssetCriticalAlertAsync(Guid assetId, string alertReason)
    {
        try
        {
            _logger.LogInformation("Sending critical asset alert for asset {AssetId}: {Reason}", assetId, alertReason);

            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
            {
                _logger.LogWarning("Asset not found: {AssetId}", assetId);
                return;
            }

            var notification = new MaintenanceNotificationDto
            {
                Title = "CRITICAL ASSET ALERT",
                Message = $"URGENT: Asset '{asset.Name}' ({asset.AssetNumber}) requires immediate attention: {alertReason}",
                Type = NotificationType.CriticalAlert,
                Priority = NotificationPriority.Critical,
                RelatedEntityType = "MaintenanceAsset",
                RelatedEntityId = assetId,
                Recipients = await GetAssetRecipientsAsync(asset),
                Data = new Dictionary<string, object>
                {
                    ["AssetId"] = assetId,
                    ["AssetName"] = asset.Name,
                    ["AssetNumber"] = asset.AssetNumber,
                    ["AlertReason"] = alertReason,
                    ["Location"] = asset.Location
                }
            };

            await ProcessNotificationAsync(notification);

            _logger.LogInformation("Critical asset alert sent for asset {AssetId}", assetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending critical asset alert for asset {AssetId}", assetId);
            throw;
        }
    }

    public async Task SendAssetWarrantyExpirationNotificationAsync(Guid assetId)
    {
        try
        {
            _logger.LogInformation("Sending warranty expiration notification for asset {AssetId}", assetId);

            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null || !asset.WarrantyEndDate.HasValue)
            {
                _logger.LogWarning("Asset not found or no warranty end date: {AssetId}", assetId);
                return;
            }

            var daysToExpiry = (asset.WarrantyEndDate.Value - DateTime.Now).Days;

            var notification = new MaintenanceNotificationDto
            {
                Title = "Asset Warranty Expiration Notice",
                Message = $"Asset '{asset.Name}' ({asset.AssetNumber}) warranty expires in {daysToExpiry} days on {asset.WarrantyEndDate.Value:MMM dd, yyyy}",
                Type = NotificationType.WarrantyExpiration,
                Priority = daysToExpiry <= 30 ? NotificationPriority.High : NotificationPriority.Normal,
                RelatedEntityType = "MaintenanceAsset",
                RelatedEntityId = assetId,
                Recipients = await GetAssetRecipientsAsync(asset),
                Data = new Dictionary<string, object>
                {
                    ["AssetId"] = assetId,
                    ["AssetName"] = asset.Name,
                    ["AssetNumber"] = asset.AssetNumber,
                    ["WarrantyEndDate"] = asset.WarrantyEndDate.Value,
                    ["DaysToExpiry"] = daysToExpiry
                }
            };

            await ProcessNotificationAsync(notification);

            _logger.LogInformation("Warranty expiration notification sent for asset {AssetId}", assetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending warranty expiration notification for asset {AssetId}", assetId);
            throw;
        }
    }

    #endregion

    #region Safety and Compliance Notifications

    public async Task SendSafetyViolationAlertAsync(Guid violationId, string violationType, string severity)
    {
        try
        {
            _logger.LogInformation("Sending safety violation alert for violation {ViolationId}", violationId);

            var notification = new MaintenanceNotificationDto
            {
                Title = $"SAFETY VIOLATION ALERT - {severity.ToUpper()}",
                Message = $"A {severity.ToLower()} {violationType} safety violation has been reported and requires immediate attention",
                Type = NotificationType.SafetyViolation,
                Priority = severity.ToLower() == "critical" ? NotificationPriority.Critical : NotificationPriority.High,
                RelatedEntityType = "SafetyViolation",
                RelatedEntityId = violationId,
                Recipients = await GetSafetyManagerRecipientsAsync(),
                Data = new Dictionary<string, object>
                {
                    ["ViolationId"] = violationId,
                    ["ViolationType"] = violationType,
                    ["Severity"] = severity,
                    ["ReportedDate"] = DateTime.Now
                }
            };

            await ProcessNotificationAsync(notification);

            _logger.LogInformation("Safety violation alert sent for violation {ViolationId}", violationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending safety violation alert for violation {ViolationId}", violationId);
            throw;
        }
    }

    public async Task SendComplianceReminderAsync(string complianceType, DateTime dueDate)
    {
        try
        {
            _logger.LogInformation("Sending compliance reminder for {ComplianceType}", complianceType);

            var daysUntilDue = (dueDate - DateTime.Now).Days;

            var notification = new MaintenanceNotificationDto
            {
                Title = $"Compliance Reminder: {complianceType}",
                Message = $"Compliance requirement '{complianceType}' is due in {daysUntilDue} days on {dueDate:MMM dd, yyyy}",
                Type = NotificationType.ComplianceReminder,
                Priority = daysUntilDue <= 7 ? NotificationPriority.High : NotificationPriority.Normal,
                RelatedEntityType = "Compliance",
                RelatedEntityId = Guid.NewGuid(), // Would be actual compliance record ID
                Recipients = await GetComplianceManagerRecipientsAsync(),
                Data = new Dictionary<string, object>
                {
                    ["ComplianceType"] = complianceType,
                    ["DueDate"] = dueDate,
                    ["DaysUntilDue"] = daysUntilDue
                }
            };

            await ProcessNotificationAsync(notification);

            _logger.LogInformation("Compliance reminder sent for {ComplianceType}", complianceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending compliance reminder for {ComplianceType}", complianceType);
            throw;
        }
    }

    #endregion

    #region Background Processing

    public async Task ProcessDailyNotificationsAsync()
    {
        try
        {
            _logger.LogInformation("Processing daily notifications");

            await SendMaintenanceDueNotificationsAsync();
            await ProcessWarrantyExpirationNotificationsAsync();
            await ProcessComplianceRemindersAsync();

            _logger.LogInformation("Daily notifications processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing daily notifications");
            throw;
        }
    }

    private async Task ProcessWarrantyExpirationNotificationsAsync()
    {
        try
        {
            // Get assets with warranties expiring in the next 60 days
            var assets = await _assetService.GetAllAssetsAsync();
            var assetsWithExpiringWarranty = assets.Where(a => 
                a.WarrantyEndDate.HasValue && 
                a.WarrantyEndDate.Value >= DateTime.Now && 
                a.WarrantyEndDate.Value <= DateTime.Now.AddDays(60)).ToList();

            foreach (var asset in assetsWithExpiringWarranty)
            {
                await SendAssetWarrantyExpirationNotificationAsync(asset.Id);
            }

            _logger.LogInformation("Processed {Count} warranty expiration notifications", assetsWithExpiringWarranty.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing warranty expiration notifications");
        }
    }

    private async Task ProcessComplianceRemindersAsync()
    {
        try
        {
            // Mock compliance reminders - would be retrieved from actual compliance system
            var complianceItems = new List<(string Type, DateTime DueDate)>
            {
                ("Annual Safety Inspection", DateTime.Now.AddDays(30)),
                ("Environmental Compliance Report", DateTime.Now.AddDays(14)),
                ("Equipment Certification Renewal", DateTime.Now.AddDays(45))
            };

            foreach (var (type, dueDate) in complianceItems)
            {
                await SendComplianceReminderAsync(type, dueDate);
            }

            _logger.LogInformation("Processed {Count} compliance reminders", complianceItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing compliance reminders");
        }
    }

    #endregion

    #region Helper Methods

    private async Task ProcessNotificationAsync(MaintenanceNotificationDto notification)
    {
        try
        {
            // Send email notifications
            foreach (var recipient in notification.Recipients)
            {
                if (!string.IsNullOrEmpty(recipient.Email))
                {
                    var subject = notification.Title;
                    var body = FormatNotificationEmailBody(notification, recipient);
                    
                    await _emailService.SendEmailAsync(recipient.Email, subject, body, true);
                }
            }

            // Here you would also:
            // - Save notification to database
            // - Send push notifications
            // - Send SMS for critical alerts
            // - Post to communication channels (Teams, Slack, etc.)

            _logger.LogInformation("Notification processed: {Title} to {RecipientCount} recipients", 
                notification.Title, notification.Recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification: {Title}", notification.Title);
            throw;
        }
    }

    private string FormatNotificationEmailBody(MaintenanceNotificationDto notification, NotificationRecipientDto recipient)
    {
        var body = $@"
        <html>
        <body>
            <h2>{notification.Title}</h2>
            <p>Hello {recipient.Name},</p>
            <p>{notification.Message}</p>
            
            {(notification.Data.Any() ? FormatNotificationData(notification.Data) : "")}
            
            <p>Please log into the maintenance system for more details.</p>
            <p>
                Best regards,<br>
                Maintenance Management System
            </p>
        </body>
        </html>";

        return body;
    }

    private string FormatNotificationData(Dictionary<string, object> data)
    {
        var details = "<h3>Details:</h3><ul>";
        
        foreach (var item in data)
        {
            var value = item.Value?.ToString() ?? "N/A";
            details += $"<li><strong>{item.Key}:</strong> {value}</li>";
        }
        
        details += "</ul>";
        return details;
    }

    private async Task<List<NotificationRecipientDto>> GetScheduleRecipientsAsync(MaintenanceScheduleDto schedule)
    {
        var recipients = new List<NotificationRecipientDto>();

        // Add assigned technician if specified
        if (schedule.AssignedTechnicianId.HasValue)
        {
            var technician = await _technicianService.GetTechnicianByIdAsync(schedule.AssignedTechnicianId.Value);
            if (technician != null)
            {
                recipients.Add(new NotificationRecipientDto
                {
                    UserId = technician.Id,
                    Name = technician.Name,
                    Email = technician.Email,
                    Type = RecipientType.Primary
                });
            }
        }

        // Add maintenance managers (mock data)
        recipients.AddRange(await GetMaintenanceManagerRecipientsAsync());

        return recipients;
    }

    private async Task<List<NotificationRecipientDto>> GetWorkOrderRecipientsAsync(WorkOrderDto workOrder)
    {
        var recipients = new List<NotificationRecipientDto>();

        // Add assigned technician
        if (workOrder.AssignedTechnician != null)
        {
            recipients.Add(new NotificationRecipientDto
            {
                UserId = workOrder.AssignedTechnician.Id,
                Name = workOrder.AssignedTechnician.Name,
                Email = workOrder.AssignedTechnician.Email,
                Type = RecipientType.Primary
            });
        }

        // Add supervisors and managers
        recipients.AddRange(await GetMaintenanceManagerRecipientsAsync());

        return recipients;
    }

    private async Task<List<NotificationRecipientDto>> GetAssetRecipientsAsync(MaintenanceAssetDto asset)
    {
        var recipients = new List<NotificationRecipientDto>();

        // Add asset custodian/responsible employee
        if (asset.Employee != null)
        {
            recipients.Add(new NotificationRecipientDto
            {
                UserId = asset.Employee.Id,
                Name = asset.Employee.Name,
                Email = asset.Employee.Email,
                Type = RecipientType.Primary
            });
        }

        // Add maintenance managers
        recipients.AddRange(await GetMaintenanceManagerRecipientsAsync());

        return recipients;
    }

    private async Task<List<NotificationRecipientDto>> GetMaintenanceManagerRecipientsAsync()
    {
        // Mock data - would be retrieved from actual user/role system
        return new List<NotificationRecipientDto>
        {
            new NotificationRecipientDto
            {
                UserId = Guid.NewGuid(),
                Name = "Maintenance Manager",
                Email = "maintenance.manager@company.com",
                Type = RecipientType.Manager
            }
        };
    }

    private async Task<List<NotificationRecipientDto>> GetSafetyManagerRecipientsAsync()
    {
        // Mock data - would be retrieved from actual user/role system
        return new List<NotificationRecipientDto>
        {
            new NotificationRecipientDto
            {
                UserId = Guid.NewGuid(),
                Name = "Safety Manager",
                Email = "safety.manager@company.com",
                Type = RecipientType.SafetyManager
            }
        };
    }

    private async Task<List<NotificationRecipientDto>> GetComplianceManagerRecipientsAsync()
    {
        // Mock data - would be retrieved from actual user/role system
        return new List<NotificationRecipientDto>
        {
            new NotificationRecipientDto
            {
                UserId = Guid.NewGuid(),
                Name = "Compliance Manager",
                Email = "compliance.manager@company.com",
                Type = RecipientType.ComplianceManager
            }
        };
    }

    private NotificationPriority MapPriorityToNotificationPriority(string priority)
    {
        return priority?.ToLower() switch
        {
            "critical" => NotificationPriority.Critical,
            "high" => NotificationPriority.High,
            "medium" => NotificationPriority.Normal,
            "low" => NotificationPriority.Low,
            _ => NotificationPriority.Normal
        };
    }

    private NotificationPriority GetStatusChangeNotificationPriority(string status)
    {
        return status?.ToLower() switch
        {
            "completed" => NotificationPriority.Normal,
            "cancelled" => NotificationPriority.High,
            "on hold" => NotificationPriority.Normal,
            "in progress" => NotificationPriority.Low,
            _ => NotificationPriority.Low
        };
    }

    #endregion
}

#region Supporting Enums and DTOs

public enum NotificationType
{
    Reminder,
    Alert,
    Assignment,
    StatusUpdate,
    Completion,
    CriticalAlert,
    WarrantyExpiration,
    SafetyViolation,
    ComplianceReminder
}

public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}

public enum RecipientType
{
    Primary,
    Manager,
    SafetyManager,
    ComplianceManager,
    Supervisor
}

public class MaintenanceNotificationDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; }
    public string RelatedEntityType { get; set; } = string.Empty;
    public Guid RelatedEntityId { get; set; }
    public List<NotificationRecipientDto> Recipients { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class NotificationRecipientDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public RecipientType Type { get; set; }
}

#endregion
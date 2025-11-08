using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing maintenance notifications, alerts, and reminders
/// Delegates actual notification delivery to the unified INotificationService
/// </summary>
public class MaintenanceNotificationService : IMaintenanceNotificationService
{
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IMaintenanceScheduleService _scheduleService;
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceAssetService _assetService;
    private readonly ITechnicianService _technicianService;
    private readonly ILogger<MaintenanceNotificationService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ErpSystem.Core.Interfaces.HR.IEmployeeRepository _employeeRepository;

    public MaintenanceNotificationService(
        INotificationService notificationService,
        IEmailService emailService,
        IMaintenanceScheduleService scheduleService,
        IWorkOrderService workOrderService,
        IMaintenanceAssetService assetService,
        ITechnicianService technicianService,
        ILogger<MaintenanceNotificationService> logger,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ErpSystem.Core.Interfaces.HR.IEmployeeRepository employeeRepository)
    {
        _notificationService = notificationService;
        _emailService = emailService;
        _scheduleService = scheduleService;
        _workOrderService = workOrderService;
        _assetService = assetService;
        _technicianService = technicianService;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _employeeRepository = employeeRepository;
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

            // Create notification using the proper service methods
            var createDto = new CreateMaintenanceNotificationDto
            {
                NotificationType = "Reminder",
                EntityType = "MaintenanceSchedule",
                EntityId = schedule.Id,
                RecipientId = schedule.AssignedTechnicianId ?? Guid.Empty,
                Title = "Upcoming Maintenance Reminder",
                Message = $"Maintenance '{schedule.Name}' is due for asset '{asset.Name}' on {schedule.NextDueDate:MMM dd, yyyy}",
                Priority = MapPriorityToString(schedule.Priority),
                ScheduledFor = DateTime.UtcNow
            };
            
            await CreateNotificationAsync(createDto);

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

            var createDto = new CreateMaintenanceNotificationDto
            {
                NotificationType = "Alert",
                EntityType = "MaintenanceSchedule",
                EntityId = schedule.Id,
                RecipientId = schedule.AssignedTechnicianId ?? Guid.Empty,
                Title = "OVERDUE: Maintenance Alert",
                Message = $"URGENT: Maintenance '{schedule.Name}' for asset '{asset.Name}' is {daysOverdue} days overdue!",
                Priority = "Critical",
                ScheduledFor = DateTime.UtcNow
            };

            await CreateNotificationAsync(createDto);

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

            var createDto = new CreateMaintenanceNotificationDto
            {
                NotificationType = "Assignment",
                EntityType = "WorkOrder",
                EntityId = workOrderId,
                RecipientId = technicianId,
                Title = "New Work Order Assignment",
                Message = "You have been assigned a new work order",
                Priority = "Normal"
            };

            await CreateNotificationAsync(createDto);
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
                _logger.LogWarning("Work order {WorkOrderId} not found when sending status change notification", workOrderId);
                return;
            }

            var notifications = new List<CreateMaintenanceNotificationDto>();

            var priority = GetStatusChangeNotificationPriority(newStatus);

            // Notify assigned technician if any
            if (workOrder.AssignedTechnicianId.HasValue)
            {
                notifications.Add(new CreateMaintenanceNotificationDto
                {
                    NotificationType = "WorkOrderStatusChanged",
                    EntityType = "WorkOrder",
                    EntityId = workOrderId,
                    RecipientId = workOrder.AssignedTechnicianId.Value,
                    Title = $"Work Order Status Updated: {newStatus}",
                    Message = $"Work order '{workOrder.Title}' changed from {previousStatus} to {newStatus}.",
                    Priority = priority,
                    ScheduledFor = DateTime.UtcNow
                });
            }

            // Notify requester if available
            if (workOrder.RequestedById.HasValue && workOrder.RequestedById.Value != Guid.Empty)
            {
                notifications.Add(new CreateMaintenanceNotificationDto
                {
                    NotificationType = "WorkOrderStatusChanged",
                    EntityType = "WorkOrder",
                    EntityId = workOrderId,
                    RecipientId = workOrder.RequestedById.Value,
                    Title = $"Your Work Order Status Updated: {newStatus}",
                    Message = $"Your work order '{workOrder.Title}' changed from {previousStatus} to {newStatus}.",
                    Priority = priority,
                    ScheduledFor = DateTime.UtcNow
                });
            }

            foreach (var n in notifications)
            {
                await CreateNotificationAsync(n);
            }
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
                _logger.LogWarning("Work order {WorkOrderId} not found when sending completion notification", workOrderId);
                return;
            }

            var notifications = new List<CreateMaintenanceNotificationDto>();

            // Notify requester
            if (workOrder.RequestedById.HasValue && workOrder.RequestedById.Value != Guid.Empty)
            {
                notifications.Add(new CreateMaintenanceNotificationDto
                {
                    NotificationType = "WorkOrderCompleted",
                    EntityType = "WorkOrder",
                    EntityId = workOrderId,
                    RecipientId = workOrder.RequestedById.Value,
                    Title = "Work Order Completed",
                    Message = $"Your work order '{workOrder.Title}' has been completed.",
                    Priority = "Normal",
                    ScheduledFor = DateTime.UtcNow
                });
            }

            // Notify assigned technician (confirmation)
            if (workOrder.AssignedTechnicianId.HasValue)
            {
                notifications.Add(new CreateMaintenanceNotificationDto
                {
                    NotificationType = "WorkOrderCompleted",
                    EntityType = "WorkOrder",
                    EntityId = workOrderId,
                    RecipientId = workOrder.AssignedTechnicianId.Value,
                    Title = "Work Order Marked Completed",
                    Message = $"Work order '{workOrder.Title}' has been marked as completed.",
                    Priority = "Low",
                    ScheduledFor = DateTime.UtcNow
                });
            }

            foreach (var n in notifications)
            {
                await CreateNotificationAsync(n);
            }
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
        _logger.LogInformation("Asset critical alert notification stubbed for asset {AssetId}: {Reason}", assetId, alertReason);
        // Stub implementation - would create proper notification in production
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
                Priority = daysToExpiry <= 30 ? NotificationPriority.High.ToString() : NotificationPriority.Normal.ToString(),
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
                Priority = severity.ToLower() == "critical" ? NotificationPriority.Critical.ToString() : NotificationPriority.High.ToString(),
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
                Priority = daysUntilDue <= 7 ? NotificationPriority.High.ToString() : NotificationPriority.Normal.ToString(),
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
            // Delegate to unified notification service for all delivery channels
            foreach (var recipient in notification.Recipients)
            {
                if (!string.IsNullOrEmpty(recipient.Email))
                {
                    // Send via unified service which handles email, SMS, push, in-app, database persistence, etc.
                    await _notificationService.SendEmailAsync(
                        recipient.Email,
                        notification.Title,
                        FormatNotificationEmailBody(notification, recipient));
                }
            }

            _logger.LogInformation("Notification processed via unified service: {Title} to {RecipientCount} recipients", 
                notification.Title, notification.Recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification: {Title}", notification.Title);
            throw;
        }
    }

    // Helper methods removed - would be implemented with proper recipient management in production

    private string MapPriorityToString(string priority)
    {
        return priority?.ToLower() switch
        {
            "critical" => "Critical",
            "high" => "High",
            "medium" => "Normal",
            "low" => "Low",
            _ => "Normal"
        };
    }

    private string GetStatusChangeNotificationPriority(string status)
    {
        return status?.ToLower() switch
        {
            "completed" => "Normal",
            "cancelled" => "High",
            "on hold" => "Normal",
            "in progress" => "Low",
            _ => "Low"
        };
    }

    #endregion

    #region Interface Implementation

    /// <summary>
    /// Gets notifications with filtering
    /// </summary>
    public async Task<PagedResult<MaintenanceNotificationDto>> GetNotificationsAsync(NotificationFilterDto filter)
    {
        try
        {
            _logger.LogInformation("Getting notifications with filter");
            
            // Mock implementation - would retrieve from database
            var notifications = new List<MaintenanceNotificationDto>();
            
            return new PagedResult<MaintenanceNotificationDto>
            {
                Items = notifications,
                TotalCount = notifications.Count,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notifications");
            throw;
        }
    }

    /// <summary>
    /// Gets a notification by ID
    /// </summary>
    public async Task<MaintenanceNotificationDto?> GetNotificationByIdAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Getting notification {NotificationId}", id);
            
            // Mock implementation - would retrieve from database
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification {NotificationId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new notification
    /// </summary>
    public async Task<MaintenanceNotificationDto> CreateNotificationAsync(CreateMaintenanceNotificationDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating notification via unified service: {Title}", createDto.Title);

            // Use DTO's TenantId if provided (for background service), otherwise use current user's tenant
            var tenantId = createDto.TenantId ?? _currentUserProvider.TenantId;
            var userId = _currentUserProvider.UserId;

            // Build unified notification DTO
            var notificationData = new Dictionary<string, object>
            {
                ["NotificationType"] = createDto.NotificationType,
                ["EntityType"] = createDto.EntityType,
                ["EntityId"] = createDto.EntityId.ToString()
            };

            if (!string.IsNullOrWhiteSpace(createDto.AdditionalData))
            {
                notificationData["AdditionalData"] = createDto.AdditionalData;
            }

            // Create in-app notification via unified service
            // Use provided tenantId from DTO (for background service), or current user's tenant
            var effectiveTenantId = createDto.TenantId ?? tenantId;
            
            if (effectiveTenantId != Guid.Empty)
            {
                // Use explicit tenant overload for background service context
                await _notificationService.CreateInAppNotificationAsync(
                    userId: createDto.RecipientId != Guid.Empty ? createDto.RecipientId : userId,
                    title: createDto.Title,
                    message: createDto.Message,
                    type: createDto.NotificationType,
                    data: notificationData,
                    tenantId: effectiveTenantId
                );
            }
            else
            {
                // Use default overload (will use current user's tenant)
                await _notificationService.CreateInAppNotificationAsync(
                    userId: createDto.RecipientId != Guid.Empty ? createDto.RecipientId : userId,
                    title: createDto.Title,
                    message: createDto.Message,
                    type: createDto.NotificationType,
                    data: notificationData
                );
            }

            // Return a DTO for backwards compatibility
            var dto = new MaintenanceNotificationDto
            {
                Id = Guid.NewGuid(),
                NotificationType = createDto.NotificationType,
                EntityType = createDto.EntityType,
                EntityId = createDto.EntityId,
                RecipientId = createDto.RecipientId,
                RecipientRole = createDto.RecipientRole,
                Title = createDto.Title,
                Message = createDto.Message,
                Priority = createDto.Priority,
                Status = "Sent",
                ScheduledFor = createDto.ScheduledFor ?? DateTime.UtcNow,
                AdditionalData = createDto.AdditionalData,
                ActionUrl = createDto.ActionUrl,
                CreatedDate = DateTime.UtcNow
            };

            _logger.LogInformation("Created notification via unified service: {Title}", createDto.Title);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification via unified service");
            throw;
        }
    }

    /// <summary>
    /// Creates multiple notifications
    /// </summary>
    public async Task<List<MaintenanceNotificationDto>> CreateBulkNotificationsAsync(BulkCreateNotificationDto bulkDto)
    {
        try
        {
            _logger.LogInformation("Creating bulk notifications for {EntityCount} entities", bulkDto.EntityIds.Count);
            
            var results = new List<MaintenanceNotificationDto>();
            var recipientIds = bulkDto.RecipientIds ?? new List<Guid>();
            
            // Create notification for each entity-recipient combination
            foreach (var entityId in bulkDto.EntityIds)
            {
                if (recipientIds.Any())
                {
                    foreach (var recipientId in recipientIds)
                    {
                        var createDto = new CreateMaintenanceNotificationDto
                        {
                            NotificationType = bulkDto.NotificationType,
                            EntityType = bulkDto.EntityType,
                            EntityId = entityId,
                            RecipientId = recipientId,
                            RecipientRole = bulkDto.RecipientRole,
                            Title = bulkDto.Title,
                            Message = bulkDto.Message,
                            Priority = bulkDto.Priority,
                            ScheduledFor = bulkDto.ScheduledFor
                        };
                        
                        var notification = await CreateNotificationAsync(createDto);
                        results.Add(notification);
                    }
                }
                else
                {
                    // Create notification without specific recipient (role-based)
                    var createDto = new CreateMaintenanceNotificationDto
                    {
                        NotificationType = bulkDto.NotificationType,
                        EntityType = bulkDto.EntityType,
                        EntityId = entityId,
                        RecipientId = Guid.Empty,
                        RecipientRole = bulkDto.RecipientRole,
                        Title = bulkDto.Title,
                        Message = bulkDto.Message,
                        Priority = bulkDto.Priority,
                        ScheduledFor = bulkDto.ScheduledFor
                    };
                    
                    var notification = await CreateNotificationAsync(createDto);
                    results.Add(notification);
                }
            }
            
            _logger.LogInformation("Created {Count} bulk notifications", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bulk notifications");
            throw;
        }
    }

    /// <summary>
    /// Updates notification status
    /// </summary>
    public async Task<MaintenanceNotificationDto> UpdateNotificationStatusAsync(Guid id, UpdateNotificationStatusDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating notification status {NotificationId} to {Status}", id, updateDto.Status);
            
            // Mock implementation - would update in database
            var notification = new MaintenanceNotificationDto
            {
                Id = id,
                Status = updateDto.Status,
                CreatedDate = DateTime.UtcNow
            };
            
            if (updateDto.Status.Equals("Read", StringComparison.OrdinalIgnoreCase))
            {
                notification.ReadAt = DateTime.UtcNow;
                notification.IsRead = true;
            }
            else if (updateDto.Status.Equals("Dismissed", StringComparison.OrdinalIgnoreCase))
            {
                notification.DismissedAt = DateTime.UtcNow;
            }
            
            return notification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification status {NotificationId}", id);
            throw;
        }
    }

    /// <summary>
    /// Marks notification as read
    /// </summary>
    public async Task<MaintenanceNotificationDto> MarkAsReadAsync(Guid id, Guid userId)
    {
        try
        {
            _logger.LogInformation("Marking notification {NotificationId} as read for user {UserId}", id, userId);
            
            var updateDto = new UpdateNotificationStatusDto { Status = "Read" };
            return await UpdateNotificationStatusAsync(id, updateDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read {NotificationId}", id);
            throw;
        }
    }

    /// <summary>
    /// Dismisses a notification
    /// </summary>
    public async Task<MaintenanceNotificationDto> DismissNotificationAsync(Guid id, Guid userId)
    {
        try
        {
            _logger.LogInformation("Dismissing notification {NotificationId} for user {UserId}", id, userId);
            
            var updateDto = new UpdateNotificationStatusDto { Status = "Dismissed" };
            return await UpdateNotificationStatusAsync(id, updateDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing notification {NotificationId}", id);
            throw;
        }
    }

    /// <summary>
    /// Gets unread notifications for a user
    /// </summary>
    public async Task<IEnumerable<MaintenanceNotificationDto>> GetUnreadNotificationsAsync(Guid userId)
    {
        try
        {
            _logger.LogInformation("Getting unread notifications for user {UserId}", userId);
            
            // Mock implementation - would retrieve from database
            return new List<MaintenanceNotificationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread notifications for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets notifications for a user by type
    /// </summary>
    public async Task<IEnumerable<MaintenanceNotificationDto>> GetNotificationsByTypeAsync(Guid userId, string notificationType)
    {
        try
        {
            _logger.LogInformation("Getting notifications for user {UserId} by type {Type}", userId, notificationType);
            
            // Mock implementation - would retrieve from database
            return new List<MaintenanceNotificationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notifications by type for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Sends pending notifications
    /// </summary>
    public async Task<int> SendPendingNotificationsAsync()
    {
        try
        {
            _logger.LogInformation("Sending pending notifications");

            var now = DateTime.UtcNow;
            var repo = _unitOfWork.Repository<MaintenanceNotification>();
            var pending = await repo.FindAsync(n => n.Status == "Pending" && n.ScheduledFor <= now && n.AttemptCount < 5);

            var sentCount = 0;
            foreach (var entity in pending)
            {
                try
                {
                    var dto = new MaintenanceNotificationDto
                    {
                        Id = entity.Id,
                        NotificationType = entity.NotificationType,
                        EntityType = entity.EntityType,
                        EntityId = entity.EntityId,
                        RecipientId = entity.RecipientId,
                        RecipientRole = entity.RecipientRole,
                        Title = entity.Title,
                        Message = entity.Message,
                        Priority = entity.Priority,
                        Status = entity.Status,
                        ScheduledFor = entity.ScheduledFor,
                        AdditionalData = entity.AdditionalData,
                        ActionUrl = entity.ActionUrl,
                        CreatedDate = entity.CreatedAt
                    };

                    dto.Recipients = await ResolveRecipientsAsync(entity);
                    await ProcessNotificationAsync(dto);

                    entity.Status = "Sent";
                    entity.SentAt = DateTime.UtcNow;
                    entity.AttemptCount += 1;
                    await repo.UpdateAsync(entity);
                    await _unitOfWork.SaveChangesAsync();
                    sentCount++;
                }
                catch (Exception sendEx)
                {
                    entity.AttemptCount += 1;
                    entity.LastError = sendEx.Message;
                    await repo.UpdateAsync(entity);
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogWarning(sendEx, "Failed sending notification {Id}", entity.Id);
                }
            }

            _logger.LogInformation("Sent {Count} pending notifications", sentCount);
            return sentCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending pending notifications");
            throw;
        }
    }

    /// <summary>
    /// Gets notification summary for a user
    /// </summary>
    public async Task<NotificationSummaryDto> GetNotificationSummaryAsync(Guid? userId = null)
    {
        try
        {
            _logger.LogInformation("Getting notification summary for user {UserId}", userId);
            
            // Mock implementation - would calculate from database
            return new NotificationSummaryDto
            {
                TotalNotifications = 0,
                UnreadNotifications = 0,
                CriticalNotifications = 0,
                LastUpdated = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification summary");
            throw;
        }
    }

    /// <summary>
    /// Deletes old notifications
    /// </summary>
    public async Task<int> DeleteOldNotificationsAsync(int olderThanDays = 90)
    {
        try
        {
            _logger.LogInformation("Deleting notifications older than {Days} days", olderThanDays);
            
            // Mock implementation - would delete from database
            var deletedCount = 0;
            
            _logger.LogInformation("Deleted {Count} old notifications", deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting old notifications");
            throw;
        }
    }

    #endregion

    #region Missing Methods

    private async Task<List<NotificationRecipientDto>> GetAssetRecipientsAsync(MaintenanceAssetDto asset)
    {
        _logger.LogInformation("Getting recipients for asset {AssetId}", asset.Id);
        // Mock implementation - would get actual recipients based on asset responsibility
        var recipients = new List<MaintenanceNotificationRecipientDto>();
        return recipients.Select(r => new NotificationRecipientDto
        {
            Id = r.Id,
            Name = r.Name,
            Email = r.Email,
            Role = r.Role,
            DeliveryMethod = "Email"
        }).ToList();
    }

    private async Task<List<NotificationRecipientDto>> GetSafetyManagerRecipientsAsync()
    {
        _logger.LogInformation("Getting safety manager recipients");
        // Mock implementation - would get actual safety managers
        var recipients = new List<MaintenanceNotificationRecipientDto>();
        return recipients.Select(r => new NotificationRecipientDto
        {
            Id = r.Id,
            Name = r.Name,
            Email = r.Email,
            Role = r.Role,
            DeliveryMethod = "Email"
        }).ToList();
    }

    private async Task<List<NotificationRecipientDto>> GetComplianceManagerRecipientsAsync()
    {
        _logger.LogInformation("Getting compliance manager recipients");
        // Mock implementation - would get actual compliance managers
        var recipients = new List<MaintenanceNotificationRecipientDto>();
        return recipients.Select(r => new NotificationRecipientDto
        {
            Id = r.Id,
            Name = r.Name,
            Email = r.Email,
            Role = r.Role,
            DeliveryMethod = "Email"
        }).ToList();
    }

    private string FormatNotificationEmailBody(MaintenanceNotificationDto notification, NotificationRecipientDto recipient = null)
    {
        var recipientSection = recipient != null ? $"<p>Dear {recipient.Name},</p>" : "";
        return $"{recipientSection}<h2>{notification.Title}</h2><p>{notification.Message}</p><p>Priority: {notification.Priority}</p>";
    }

    private async Task<List<NotificationRecipientDto>> ResolveRecipientsAsync(MaintenanceNotification entity)
    {
        var recipients = new List<NotificationRecipientDto>();
        if (entity.RecipientId != Guid.Empty)
        {
            var emp = await _employeeRepository.GetByIdAsync(entity.RecipientId);
            if (emp != null && !string.IsNullOrWhiteSpace(emp.EmailAddress))
            {
                recipients.Add(new NotificationRecipientDto
                {
                    Id = emp.Id,
                    Name = emp.FullName,
                    Email = emp.EmailAddress,
                    Role = string.Empty,
                    DeliveryMethod = "Email"
                });
            }
        }
        // Role-based recipient resolution can be added here later.
        return recipients;
    }

    #endregion

    #region Job Card Notifications

    /// <summary>
    /// Notifies approvers when a job card is submitted for approval
    /// </summary>
    public async Task NotifyJobCardSubmittedAsync(Guid jobCardId)
    {
        try
        {
            _logger.LogInformation("Sending job card submitted notification for job card {JobCardId}", jobCardId);
            
            var dto = new CreateMaintenanceNotificationDto
            {
                NotificationType = "JobCardSubmitted",
                EntityType = "JobCard",
                EntityId = jobCardId,
                RecipientId = Guid.Empty,
                RecipientRole = "Manager",
                Title = "Job Card Submitted for Approval",
                Message = $"Job card {jobCardId} has been submitted for approval.",
                Priority = "Normal",
                ScheduledFor = DateTime.UtcNow
            };
            await CreateNotificationAsync(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending job card submitted notification for {JobCardId}", jobCardId);
        }
    }

    /// <summary>
    /// Notifies requestor when their job card is approved
    /// </summary>
    public async Task NotifyJobCardApprovedAsync(Guid jobCardId)
    {
        try
        {
            _logger.LogInformation("Sending job card approved notification for job card {JobCardId}", jobCardId);
            
            var dto = new CreateMaintenanceNotificationDto
            {
                NotificationType = "JobCardApproved",
                EntityType = "JobCard",
                EntityId = jobCardId,
                Title = "Your Job Card Was Approved",
                Message = $"Your job card {jobCardId} has been approved.",
                Priority = "Normal",
                ScheduledFor = DateTime.UtcNow
            };
            await CreateNotificationAsync(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending job card approved notification for {JobCardId}", jobCardId);
        }
    }

    /// <summary>
    /// Notifies requestor when their job card is rejected
    /// </summary>
    public async Task NotifyJobCardRejectedAsync(Guid jobCardId, string? reason)
    {
        try
        {
            _logger.LogInformation("Sending job card rejected notification for job card {JobCardId}", jobCardId);
            
            var dto = new CreateMaintenanceNotificationDto
            {
                NotificationType = "JobCardRejected",
                EntityType = "JobCard",
                EntityId = jobCardId,
                Title = "Your Job Card Was Rejected",
                Message = $"Your job card {jobCardId} was rejected. Reason: {reason ?? "Not specified"}.",
                Priority = "High",
                ScheduledFor = DateTime.UtcNow
            };
            await CreateNotificationAsync(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending job card rejected notification for {JobCardId}", jobCardId);
        }
    }

    /// <summary>
    /// Notifies requestor when changes are requested for their job card
    /// </summary>
    public async Task NotifyJobCardChangesRequestedAsync(Guid jobCardId, string? comments)
    {
        try
        {
            _logger.LogInformation("Sending job card changes requested notification for job card {JobCardId}", jobCardId);
            
            var dto = new CreateMaintenanceNotificationDto
            {
                NotificationType = "JobCardChangesRequested",
                EntityType = "JobCard",
                EntityId = jobCardId,
                Title = "Changes Requested For Your Job Card",
                Message = $"Changes were requested for job card {jobCardId}. Comments: {comments ?? "N/A"}.",
                Priority = "Normal",
                ScheduledFor = DateTime.UtcNow
            };
            await CreateNotificationAsync(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending job card changes requested notification for {JobCardId}", jobCardId);
        }
    }

    #endregion

}


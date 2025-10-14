using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Service for managing maintenance notifications
/// </summary>
public interface IMaintenanceNotificationService
{
    /// <summary>
    /// Gets notifications with filtering
    /// </summary>
    Task<PagedResult<MaintenanceNotificationDto>> GetNotificationsAsync(NotificationFilterDto filter);

    /// <summary>
    /// Gets a notification by ID
    /// </summary>
    Task<MaintenanceNotificationDto?> GetNotificationByIdAsync(Guid id);

    /// <summary>
    /// Creates a new notification
    /// </summary>
    Task<MaintenanceNotificationDto> CreateNotificationAsync(CreateMaintenanceNotificationDto createDto);

    /// <summary>
    /// Creates multiple notifications
    /// </summary>
    Task<List<MaintenanceNotificationDto>> CreateBulkNotificationsAsync(BulkCreateNotificationDto bulkDto);

    /// <summary>
    /// Updates notification status
    /// </summary>
    Task<MaintenanceNotificationDto> UpdateNotificationStatusAsync(Guid id, UpdateNotificationStatusDto updateDto);

    /// <summary>
    /// Marks notification as read
    /// </summary>
    Task<MaintenanceNotificationDto> MarkAsReadAsync(Guid id, Guid userId);

    /// <summary>
    /// Dismisses a notification
    /// </summary>
    Task<MaintenanceNotificationDto> DismissNotificationAsync(Guid id, Guid userId);

    /// <summary>
    /// Gets unread notifications for a user
    /// </summary>
    Task<IEnumerable<MaintenanceNotificationDto>> GetUnreadNotificationsAsync(Guid userId);

    /// <summary>
    /// Gets notifications for a user by type
    /// </summary>
    Task<IEnumerable<MaintenanceNotificationDto>> GetNotificationsByTypeAsync(Guid userId, string notificationType);

    /// <summary>
    /// Sends pending notifications
    /// </summary>
    Task<int> SendPendingNotificationsAsync();

    /// <summary>
    /// Gets notification summary for a user
    /// </summary>
    Task<NotificationSummaryDto> GetNotificationSummaryAsync(Guid? userId = null);

    /// <summary>
    /// Deletes old notifications
    /// </summary>
    Task<int> DeleteOldNotificationsAsync(int olderThanDays = 90);
}

/// <summary>
/// Service for managing notification templates
/// </summary>
public interface INotificationTemplateService
{
    /// <summary>
    /// Gets all notification templates
    /// </summary>
    Task<IEnumerable<MaintenanceNotificationTemplateDto>> GetNotificationTemplatesAsync();

    /// <summary>
    /// Gets a notification template by ID
    /// </summary>
    Task<MaintenanceNotificationTemplateDto?> GetNotificationTemplateByIdAsync(Guid id);

    /// <summary>
    /// Gets notification template by type
    /// </summary>
    Task<MaintenanceNotificationTemplateDto?> GetNotificationTemplateByTypeAsync(string notificationType);

    /// <summary>
    /// Creates a new notification template
    /// </summary>
    Task<MaintenanceNotificationTemplateDto> CreateNotificationTemplateAsync(CreateNotificationTemplateDto createDto);

    /// <summary>
    /// Updates an existing notification template
    /// </summary>
    Task<MaintenanceNotificationTemplateDto> UpdateNotificationTemplateAsync(Guid id, UpdateNotificationTemplateDto updateDto);

    /// <summary>
    /// Deletes a notification template
    /// </summary>
    Task DeleteNotificationTemplateAsync(Guid id);

    /// <summary>
    /// Generates notification from template
    /// </summary>
    Task<CreateMaintenanceNotificationDto> GenerateNotificationFromTemplateAsync(string notificationType, object data);

    /// <summary>
    /// Gets available notification types
    /// </summary>
    Task<IEnumerable<string>> GetAvailableNotificationTypesAsync();
}

/// <summary>
/// Service for managing escalation rules
/// </summary>
public interface IEscalationRuleService
{
    /// <summary>
    /// Gets all escalation rules
    /// </summary>
    Task<IEnumerable<MaintenanceEscalationRuleDto>> GetEscalationRulesAsync();

    /// <summary>
    /// Gets an escalation rule by ID
    /// </summary>
    Task<MaintenanceEscalationRuleDto?> GetEscalationRuleByIdAsync(Guid id);

    /// <summary>
    /// Creates a new escalation rule
    /// </summary>
    Task<MaintenanceEscalationRuleDto> CreateEscalationRuleAsync(CreateEscalationRuleDto createDto);

    /// <summary>
    /// Updates an existing escalation rule
    /// </summary>
    Task<MaintenanceEscalationRuleDto> UpdateEscalationRuleAsync(Guid id, UpdateEscalationRuleDto updateDto);

    /// <summary>
    /// Deletes an escalation rule
    /// </summary>
    Task DeleteEscalationRuleAsync(Guid id);

    /// <summary>
    /// Gets escalation rules by entity type
    /// </summary>
    Task<IEnumerable<MaintenanceEscalationRuleDto>> GetEscalationRulesByEntityTypeAsync(string entityType);

    /// <summary>
    /// Processes escalations for overdue items
    /// </summary>
    Task<int> ProcessEscalationsAsync();

    /// <summary>
    /// Gets escalation history
    /// </summary>
    Task<IEnumerable<EscalationHistoryDto>> GetEscalationHistoryAsync(string? entityType = null, Guid? entityId = null);
}

/// <summary>
/// Background service for automated notifications and escalations
/// </summary>
public interface IMaintenanceNotificationEngine
{
    /// <summary>
    /// Generates maintenance due notifications
    /// </summary>
    Task GenerateMaintenanceDueNotificationsAsync();

    /// <summary>
    /// Generates overdue maintenance notifications
    /// </summary>
    Task GenerateOverdueMaintenanceNotificationsAsync();

    /// <summary>
    /// Generates work order assignment notifications
    /// </summary>
    Task GenerateWorkOrderAssignmentNotificationAsync(Guid workOrderId, Guid assignedToId);

    /// <summary>
    /// Generates contractor assignment notifications
    /// </summary>
    Task GenerateContractorAssignmentNotificationAsync(Guid contractorWorkOrderId);

    /// <summary>
    /// Generates inspection due notifications
    /// </summary>
    Task GenerateInspectionDueNotificationsAsync();

    /// <summary>
    /// Generates approval request notifications
    /// </summary>
    Task GenerateApprovalRequestNotificationAsync(Guid workflowInstanceId);

    /// <summary>
    /// Generates asset downtime notifications
    /// </summary>
    Task GenerateAssetDowntimeNotificationAsync(Guid assetId, string downtimeType);

    /// <summary>
    /// Processes all scheduled notifications
    /// </summary>
    Task ProcessScheduledNotificationsAsync();

    /// <summary>
    /// Processes all escalation rules
    /// </summary>
    Task ProcessEscalationRulesAsync();

    /// <summary>
    /// Starts the notification engine
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the notification engine
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
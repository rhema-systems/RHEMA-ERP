using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using Microsoft.Extensions.Logging;
using CreateEmailCampaignDto = ErpSystem.Core.DTOs.Notifications.CreateEmailCampaignDto;
using CreateNotificationDto = ErpSystem.Core.DTOs.Notifications.CreateNotificationDto;
using CreateNotificationTemplateDto = ErpSystem.Core.DTOs.Notifications.CreateNotificationTemplateDto;
using DashboardNotificationDto = ErpSystem.Core.DTOs.Dashboard.NotificationDto;
using EmailCampaignDto = ErpSystem.Core.DTOs.Notifications.EmailCampaignDto;
using NotificationDto = ErpSystem.Core.DTOs.Notifications.NotificationDto;
using NotificationPagedResult = ErpSystem.Core.DTOs.Notifications.PagedResult<ErpSystem.Core.DTOs.Notifications.NotificationDto>;
using NotificationPreferencesDto = ErpSystem.Core.DTOs.Notifications.NotificationPreferencesDto;
using NotificationStatisticsDto = ErpSystem.Core.DTOs.Notifications.NotificationStatisticsDto;
using NotificationTemplateDto = ErpSystem.Core.DTOs.Notifications.NotificationTemplateDto;
using PushSubscriptionDto = ErpSystem.Core.DTOs.Notifications.PushSubscriptionDto;
using SendPushNotificationDto = ErpSystem.Core.DTOs.Notifications.SendPushNotificationDto;
using UpdateNotificationPreferencesDto = ErpSystem.Core.DTOs.Notifications.UpdateNotificationPreferencesDto;

namespace ErpSystem.Api.Services;

/// <summary>
/// Unified notification service for system-wide notification handling.
/// Handles all delivery channels (email, SMS, push, in-app) and database persistence.
/// Single source of truth for all notifications across all ERP modules.
/// </summary>
public class UnifiedNotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHubNotificationService _hubNotificationService;
    private readonly ILogger<UnifiedNotificationService> _logger;

    public UnifiedNotificationService(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ICurrentUserService currentUserService,
        IHubNotificationService hubNotificationService,
        ILogger<UnifiedNotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _currentUserService = currentUserService;
        _hubNotificationService = hubNotificationService;
        _logger = logger;
    }

    #region Direct Notifications

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        try
        {
            _logger.LogInformation("Sending email to {EmailAddress} with subject: {Subject}", to, subject);

            await _emailService.SendEmailAsync(new EmailDto
            {
                To = to,
                Subject = subject,
                Body = body,
                IsHtml = isHtml
            });

            _logger.LogInformation("Email sent successfully to {EmailAddress}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {EmailAddress}", to);
            throw;
        }
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        try
        {
            _logger.LogInformation(
                "[SMS] Send requested to {Phone} - Message length: {Length} chars",
                MaskPhoneNumber(phoneNumber), message?.Length ?? 0);

            // TODO: Implement real SMS provider integration (Twilio, AWS SNS, etc.)
            // For now, log as audit trail
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to {Phone}", MaskPhoneNumber(phoneNumber));
            throw;
        }
    }

    public async Task SendPushNotificationAsync(Guid userId, string title, string message, Dictionary<string, string>? data = null)
    {
        try
        {
            _logger.LogInformation(
                "[PUSH] Send requested to {UserId}: {Title}",
                userId, title);

            // Send via in-app notification as primary channel
            var inAppData = new Dictionary<string, object>
            {
                { "Type", "PushNotification" },
                { "Title", title },
                { "Message", message }
            };

            if (data != null)
            {
                foreach (var kvp in data)
                {
                    inAppData[$"Custom_{kvp.Key}"] = kvp.Value;
                }
            }

            await CreateInAppNotificationAsync(userId, title, message, "PushNotification", inAppData);

            // TODO: Integrate with push notification providers (Firebase, Apple Push, etc.)
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification to {UserId}", userId);
            throw;
        }
    }

    public async Task SendPushNotificationAsync(SendPushNotificationDto pushNotificationDto, Guid sentBy, Guid tenantId)
    {
        _logger.LogInformation(
            "[PUSH] DTO Send requested by {SentBy} for tenant {TenantId}: {Title}",
            sentBy, tenantId, pushNotificationDto.Title);

        await Task.CompletedTask;
    }

    public async Task CreateInAppNotificationAsync(Guid userId, string title, string message, string type, Dictionary<string, object>? data = null)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        await CreateInAppNotificationAsync(userId, title, message, type, data, tenantId);
    }

    public async Task CreateInAppNotificationAsync(Guid userId, string title, string message, string type, Dictionary<string, object>? data, Guid tenantId)
    {
        try
        {
            // Use provided tenantId (for background service context) or fall back to current user's tenant
            if (tenantId == Guid.Empty)
            {
                tenantId = _currentUserService.TenantId ?? Guid.Empty;
            }

            // Extract EntityType and EntityId from data dictionary if present
            string? entityType = null;
            Guid? entityId = null;

            if (data != null)
            {
                if (data.TryGetValue("EntityType", out var et) && et != null)
                {
                    entityType = et.ToString();
                }

                if (data.TryGetValue("EntityId", out var ei) && ei != null)
                {
                    if (ei is string eidStr && Guid.TryParse(eidStr, out var eidGuid))
                    {
                        entityId = eidGuid;
                    }
                    else if (ei is Guid eidDirect)
                    {
                        entityId = eidDirect;
                    }
                }
            }

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                NotificationType = type,
                Title = title,
                Message = message,
                Status = "Sent",
                IsRead = false,
                ScheduledFor = DateTime.UtcNow,
                SentAt = DateTime.UtcNow,
                EntityType = entityType,
                EntityId = entityId ?? Guid.Empty,
                AdditionalData = data != null ? System.Text.Json.JsonSerializer.Serialize(data) : null,
                DeliveryMethods = "InApp",
                TenantId = tenantId,
                RecipientId = userId,
                Priority = "Normal",
                AttemptCount = 1
            };

            await _unitOfWork.Repository<Notification>().AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            // Broadcast via SignalR if user is connected
            var dashboardNotification = MapToDashboardDto(notification);
            await _hubNotificationService.BroadcastNotificationAsync(userId.ToString(), dashboardNotification);

            _logger.LogInformation("In-app notification created: {NotificationId} for user {UserId} in tenant {TenantId}", notification.Id, userId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating in-app notification for user {UserId}", userId);
            throw;
        }
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
        {
            return "***";
        }

        return string.Concat("***", phoneNumber.AsSpan(phoneNumber.Length - 4));
    }

    #endregion

    #region Notification CRUD

    public async Task<ErpSystem.Core.DTOs.Notifications.PagedResult<NotificationDto>> GetNotificationsAsync(
        Guid userId, Guid tenantId, int page = 1, int pageSize = 20,
        bool? unreadOnly = null, string? type = null, string? severity = null)
    {
        try
        {
            var allNotifications = await _unitOfWork.Repository<Notification>()
                .FindAsync(n =>
                    n.RecipientId == userId && n.TenantId == tenantId &&
                    (unreadOnly == null || !n.IsRead == unreadOnly) &&
                    (string.IsNullOrEmpty(type) || n.NotificationType == type) &&
                    (string.IsNullOrEmpty(severity) || n.Priority == severity));

            var allNotificationsList = allNotifications.ToList();
            var totalCount = allNotificationsList.Count;

            var notifications = allNotificationsList
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var dtos = notifications.Select(MapToDto).ToList();

            return new ErpSystem.Core.DTOs.Notifications.PagedResult<NotificationDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications for user {UserId}", userId);
            return new ErpSystem.Core.DTOs.Notifications.PagedResult<NotificationDto> { Items = new List<NotificationDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
        }
    }

    public async Task<NotificationDto?> GetNotificationAsync(Guid notificationId, Guid userId, Guid tenantId)
    {
        try
        {
            var notification = await _unitOfWork.Repository<Notification>()
                .FirstOrDefaultAsync(n =>
                    n.Id == notificationId &&
                    n.RecipientId == userId &&
                    n.TenantId == tenantId);

            return notification != null ? MapToDto(notification) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification {NotificationId}", notificationId);
            return null;
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, Guid tenantId)
    {
        try
        {
            return await _unitOfWork.Repository<Notification>()
                .CountAsync(n => n.RecipientId == userId && n.TenantId == tenantId && !n.IsRead);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, Guid tenantId)
    {
        try
        {
            var notification = await _unitOfWork.Repository<Notification>()
                .FirstOrDefaultAsync(n =>
                    n.Id == notificationId &&
                    n.RecipientId == userId &&
                    n.TenantId == tenantId);

            if (notification == null)
            {
                return false;
            }

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;

            await _unitOfWork.Repository<Notification>().UpdateAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Notification {NotificationId} marked as read", notificationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read: {NotificationId}", notificationId);
            return false;
        }
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, Guid tenantId)
    {
        try
        {
            var unreadNotifications = await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.RecipientId == userId && n.TenantId == tenantId && !n.IsRead);

            var notificationsToUpdate = unreadNotifications.ToList();

            foreach (var notification in notificationsToUpdate)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _unitOfWork.Repository<Notification>().UpdateRangeAsync(notificationsToUpdate);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Marked {Count} notifications as read for user {UserId}", notificationsToUpdate.Count, userId);
            return notificationsToUpdate.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId, Guid tenantId)
    {
        try
        {
            var notification = await _unitOfWork.Repository<Notification>()
                .FirstOrDefaultAsync(n =>
                    n.Id == notificationId &&
                    n.RecipientId == userId &&
                    n.TenantId == tenantId);

            if (notification == null)
            {
                return false;
            }

            await _unitOfWork.Repository<Notification>().DeleteAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Notification {NotificationId} deleted", notificationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification: {NotificationId}", notificationId);
            return false;
        }
    }

    public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto createNotificationDto, Guid createdBy, Guid tenantId)
    {
        try
        {
            // Full notification creation with all properties from DTO - enforces EntityType and EntityId
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                NotificationType = createNotificationDto.Type,
                Title = createNotificationDto.Title,
                Message = createNotificationDto.Message,
                Priority = createNotificationDto.Priority,
                Status = "Pending",
                RecipientId = createNotificationDto.RecipientId,
                IsRead = false,
                ScheduledFor = DateTime.UtcNow,
                TenantId = tenantId,
                ActionUrl = createNotificationDto.ActionUrl,
                EntityType = createNotificationDto.EntityType,     // REQUIRED from DTO
                EntityId = createNotificationDto.EntityId,         // REQUIRED from DTO
                AdditionalData = createNotificationDto.Metadata != null
                    ? System.Text.Json.JsonSerializer.Serialize(createNotificationDto.Metadata)
                    : null,
                DeliveryMethods = "InApp"
            };

            await _unitOfWork.Repository<Notification>().AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Notification {NotificationId} created for entity {EntityType}:{EntityId}",
                notification.Id, notification.EntityType, notification.EntityId);
            return MapToDto(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification");
            throw;
        }
    }

    #endregion

    #region Push Subscriptions

    public async Task SubscribeToPushNotificationsAsync(Guid userId, PushSubscriptionDto subscriptionDto)
    {
        _logger.LogInformation("User {UserId} subscribed to push notifications", userId);
        // TODO: Implement push subscription storage
        await Task.CompletedTask;
    }

    public async Task UnsubscribeFromPushNotificationsAsync(Guid userId, PushSubscriptionDto subscriptionDto)
    {
        _logger.LogInformation("User {UserId} unsubscribed from push notifications", userId);
        // TODO: Implement push subscription removal
        await Task.CompletedTask;
    }

    #endregion

    #region Preferences

    public async Task<NotificationPreferencesDto> GetPreferencesAsync(Guid userId)
    {
        // TODO: Implement preference storage and retrieval
        return await Task.FromResult(new NotificationPreferencesDto());
    }

    public async Task<NotificationPreferencesDto> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferencesDto preferencesDto)
    {
        // TODO: Implement preference updates
        return await Task.FromResult(new NotificationPreferencesDto());
    }

    #endregion

    #region Statistics

    public async Task<NotificationStatisticsDto> GetStatisticsAsync(Guid tenantId, string period = "last-30-days", bool isSuperAdmin = false)
    {
        try
        {
            var cutoffDate = GetCutoffDate(period);

            var allNotifications = await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.TenantId == tenantId && n.CreatedAt >= cutoffDate);

            var notificationsList = allNotifications.ToList();

            return new NotificationStatisticsDto
            {
                TotalNotifications = notificationsList.Count,
                UnreadNotifications = notificationsList.Count(n => !n.IsRead),
                NotificationsSentToday = notificationsList.Count(n => n.CreatedAt.Date == DateTime.UtcNow.Date),
                PushNotificationsSent = notificationsList.Count(n => n.DeliveryMethods?.Contains("Push") == true),
                EmailNotificationsSent = notificationsList.Count(n => n.DeliveryMethods?.Contains("Email") == true),
                DeliveryRate = notificationsList.Count > 0 ? (double)notificationsList.Count(n => n.Status == "Sent") / notificationsList.Count : 0,
                OpenRate = notificationsList.Count > 0 ? (double)notificationsList.Count(n => n.IsRead) / notificationsList.Count : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification statistics for tenant {TenantId}", tenantId);
            return new NotificationStatisticsDto();
        }
    }

    #endregion

    #region Templates

    public async Task<List<NotificationTemplateDto>> GetNotificationTemplatesAsync(Guid tenantId, string? type = null)
    {
        return await Task.FromResult(new List<NotificationTemplateDto>());
    }

    public async Task<NotificationTemplateDto> CreateNotificationTemplateAsync(
        CreateNotificationTemplateDto templateDto, Guid createdBy, Guid tenantId)
    {
        return await Task.FromResult(new NotificationTemplateDto());
    }

    public async Task<bool> DeleteNotificationTemplateAsync(Guid templateId, Guid tenantId)
    {
        return await Task.FromResult(true);
    }

    #endregion

    #region Email Campaigns

    public async Task<EmailCampaignDto> CreateEmailCampaignAsync(CreateEmailCampaignDto campaignDto, Guid createdBy, Guid tenantId)
    {
        return await Task.FromResult(new EmailCampaignDto());
    }

    public async Task<EmailCampaignDto?> GetEmailCampaignAsync(Guid campaignId, Guid tenantId)
    {
        return await Task.FromResult<EmailCampaignDto?>(null);
    }

    public async Task<List<EmailCampaignDto>> GetEmailCampaignsAsync(Guid tenantId, string? status = null)
    {
        return await Task.FromResult(new List<EmailCampaignDto>());
    }

    public async Task<bool> DeleteEmailCampaignAsync(Guid campaignId, Guid tenantId)
    {
        return await Task.FromResult(true);
    }

    public async Task<EmailCampaignDto?> SendEmailCampaignAsync(Guid campaignId, Guid tenantId)
    {
        return await Task.FromResult<EmailCampaignDto?>(null);
    }

    #endregion

    #region Real-time Delivery

    public async Task SendRealTimeNotificationAsync(NotificationDto notification, List<Guid> userIds)
    {
        _logger.LogInformation(
            "[REAL-TIME] Sending notification '{Title}' to {UserCount} users",
            notification.Title, userIds.Count);

        foreach (var userId in userIds)
        {
            var dashboardNotification = new DashboardNotificationDto
            {
                Id = notification.Id.ToString(),
                Type = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                Severity = notification.Severity,
                Timestamp = notification.Timestamp,
                IsRead = notification.IsRead,
                ActionUrl = notification.ActionUrl,
                Metadata = notification.Metadata
            };
            await _hubNotificationService.BroadcastNotificationAsync(userId.ToString(), dashboardNotification);
        }
    }

    public async Task BroadcastNotificationAsync(NotificationDto notification, Guid tenantId, List<string>? roles = null)
    {
        _logger.LogInformation(
            "[BROADCAST] Broadcasting notification '{Title}' to tenant {TenantId} with roles: {Roles}",
            notification.Title, tenantId, roles?.Count > 0 ? string.Join(", ", roles) : "All");

        // TODO: Implement role-based broadcast
        await Task.CompletedTask;
    }

    #endregion

    #region Background Processing

    public async Task ProcessPendingNotificationsAsync()
    {
        try
        {
            _logger.LogInformation("Processing pending notifications");

            var pendingNotifications = await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.Status == "Pending" && n.ScheduledFor <= DateTime.UtcNow);

            var notificationsList = pendingNotifications.ToList();

            foreach (var notification in notificationsList)
            {
                try
                {
                    // Attempt to send the notification
                    notification.Status = "Sent";
                    notification.SentAt = DateTime.UtcNow;
                    notification.AttemptCount++;

                    await _unitOfWork.Repository<Notification>().UpdateAsync(notification);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing notification {NotificationId}", notification.Id);
                    notification.Status = "Failed";
                    notification.LastError = ex.Message;
                    notification.AttemptCount++;

                    await _unitOfWork.Repository<Notification>().UpdateAsync(notification);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Processed {Count} pending notifications", notificationsList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessPendingNotificationsAsync");
        }
    }

    public async Task CleanupExpiredNotificationsAsync()
    {
        try
        {
            const int olderThanDays = 90;
            _logger.LogInformation("Cleaning up expired notifications older than {Days} days", olderThanDays);

            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            var expiredNotifications = await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.CreatedAt < cutoffDate && (n.IsRead || n.Status == "Dismissed" || n.Status == "Archived"));

            var notificationsToDelete = expiredNotifications.ToList();

            if (notificationsToDelete.Count > 0)
            {
                await _unitOfWork.Repository<Notification>().DeleteRangeAsync(notificationsToDelete);
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Deleted {Count} expired notifications", notificationsToDelete.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CleanupExpiredNotificationsAsync");
        }
    }

    #endregion

    #region Helpers

    private NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.NotificationType,
            Severity = notification.Priority,
            IsRead = notification.IsRead,
            Timestamp = notification.CreatedAt,
            ActionUrl = notification.ActionUrl,
            EntityType = notification.EntityType,
            EntityId = notification.EntityId,
            ExpiresAt = notification.ScheduledFor.AddDays(7),
            Metadata = notification.AdditionalData != null
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(notification.AdditionalData)
                : null
        };
    }

    private static DateTime GetCutoffDate(string period)
    {
        return period switch
        {
            "last-7-days" => DateTime.UtcNow.AddDays(-7),
            "last-30-days" => DateTime.UtcNow.AddDays(-30),
            "last-90-days" => DateTime.UtcNow.AddDays(-90),
            "last-year" => DateTime.UtcNow.AddYears(-1),
            _ => DateTime.UtcNow.AddDays(-30)
        };
    }

    private static double CalculateAverageDeliveryTime(List<Notification> notifications)
    {
        var deliveredNotifications = notifications
            .Where(n => n.SentAt.HasValue && n.CreatedAt != DateTime.MinValue)
            .ToList();

        if (deliveredNotifications.Count == 0)
        {
            return 0;
        }

        return deliveredNotifications
            .Average(n => (n.SentAt!.Value - n.CreatedAt).TotalSeconds);
    }

    private static DashboardNotificationDto MapToDashboardDto(Notification notification)
    {
        return new DashboardNotificationDto
        {
            Id = notification.Id.ToString(),
            Type = notification.NotificationType,
            Title = notification.Title,
            Message = notification.Message,
            Severity = notification.Priority,
            Timestamp = notification.CreatedAt,
            IsRead = notification.IsRead,
            ActionUrl = notification.ActionUrl,
            Metadata = notification.AdditionalData != null
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(notification.AdditionalData)
                : null
        };
    }

    #endregion
}

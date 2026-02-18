using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using EmailAttachmentInfo = ErpSystem.Core.Interfaces.EmailAttachmentInfo;
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
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UnifiedNotificationService> _logger;

    public UnifiedNotificationService(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ICurrentUserService currentUserService,
        IHubNotificationService hubNotificationService,
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<UnifiedNotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _currentUserService = currentUserService;
        _hubNotificationService = hubNotificationService;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    #region Direct Notifications

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        Notification? logEntity = null;
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;

        try
        {
            _logger.LogInformation("Sending email to {EmailAddress} with subject: {Subject}", to, subject);

            logEntity = await TryCreateEmailLogAsync(tenantId, to, subject, body, isHtml, attachments: null);

            await _emailService.SendEmailAsync(new EmailDto
            {
                To = to,
                Subject = subject,
                Body = body,
                IsHtml = isHtml
            });

            await TryMarkEmailLogSentAsync(logEntity);
            _logger.LogInformation("Email sent successfully to {EmailAddress}", to);
        }
        catch (Exception ex)
        {
            await TryMarkEmailLogFailedAsync(logEntity, ex);
            _logger.LogError(ex, "Error sending email to {EmailAddress}", to);
            throw;
        }
    }

    public async Task SendEmailWithAttachmentsAsync(string to, string subject, string body, List<EmailAttachmentInfo> attachments, bool isHtml = true)
    {
        Notification? logEntity = null;
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;

        try
        {
            _logger.LogInformation("Sending email with {AttachmentCount} attachments to {EmailAddress} with subject: {Subject}", 
                attachments.Count, to, subject);

            logEntity = await TryCreateEmailLogAsync(tenantId, to, subject, body, isHtml, attachments);

            var emailDto = new EmailDto
            {
                To = to,
                Subject = subject,
                Body = body,
                IsHtml = isHtml,
                Attachments = attachments.Select(a => new EmailAttachmentDto
                {
                    FileName = a.FileName,
                    Content = a.Content,
                    ContentType = a.ContentType
                }).ToList()
            };

            await _emailService.SendEmailAsync(emailDto);

            await TryMarkEmailLogSentAsync(logEntity);
            _logger.LogInformation("Email with attachments sent successfully to {EmailAddress}", to);
        }
        catch (Exception ex)
        {
            await TryMarkEmailLogFailedAsync(logEntity, ex);
            _logger.LogError(ex, "Error sending email with attachments to {EmailAddress}", to);
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
            string? actionUrl = null;

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

                // Optional: allow services to provide an explicit navigation URL
                if (data.TryGetValue("ActionUrl", out var au) && au != null)
                {
                    actionUrl = au.ToString();
                }
                else if (data.TryGetValue("actionUrl", out var aul) && aul != null)
                {
                    actionUrl = aul.ToString();
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
                ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl,
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

    #region Email Logging (Message Queue)

    private async Task<Notification?> TryCreateEmailLogAsync(
        Guid tenantId,
        string to,
        string subject,
        string body,
        bool isHtml,
        List<EmailAttachmentInfo>? attachments)
    {
        // If we don't know the tenant (e.g. background context), we still send the email but we can't safely persist.
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        try
        {
            const int maxTotalAttachmentBytesToPersist = 2 * 1024 * 1024; // 2MB safety cap
            var totalBytes = attachments?.Sum(a => a.Content?.Length ?? 0) ?? 0;
            var persistAttachmentBytes = attachments != null && totalBytes > 0 && totalBytes <= maxTotalAttachmentBytesToPersist;

            object payload = new
            {
                email = new
                {
                    isHtml,
                    attachmentsOmitted = attachments != null && !persistAttachmentBytes,
                    attachments = attachments?.Select(a => new
                    {
                        fileName = a.FileName,
                        contentType = a.ContentType,
                        size = a.Content?.Length ?? 0,
                        contentBase64 = persistAttachmentBytes ? Convert.ToBase64String(a.Content) : null
                    }).ToList()
                }
            };

            var entity = new Notification
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                NotificationType = "Email",
                Title = subject ?? string.Empty,
                Message = body ?? string.Empty,
                Priority = "Normal",
                Status = "Pending",
                IsRead = false,
                ScheduledFor = DateTime.UtcNow,
                SentAt = null,
                AttemptCount = 0,
                LastError = null,
                DeliveryMethods = "Email",
                EmailAddress = to,
                PhoneNumber = null,
                RecipientId = Guid.Empty,
                AdditionalData = JsonSerializer.Serialize(payload)
            };

            await _unitOfWork.Repository<Notification>().AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();
            return entity;
        }
        catch (Exception ex)
        {
            // Best-effort: never block sending on logging failures.
            _logger.LogWarning(ex, "Failed to persist email log entry for recipient {EmailAddress}", to);
            return null;
        }
    }

    private async Task TryMarkEmailLogSentAsync(Notification? logEntity)
    {
        if (logEntity == null) return;

        try
        {
            logEntity.Status = "Sent";
            logEntity.SentAt = DateTime.UtcNow;
            logEntity.AttemptCount += 1;
            logEntity.LastError = null;

            await _unitOfWork.Repository<Notification>().UpdateAsync(logEntity);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to update email log {NotificationId} to Sent", logEntity.Id);
        }
    }

    private async Task TryMarkEmailLogFailedAsync(Notification? logEntity, Exception exception)
    {
        if (logEntity == null) return;

        try
        {
            logEntity.Status = "Failed";
            logEntity.SentAt = null;
            logEntity.AttemptCount += 1;
            logEntity.LastError = exception.Message;

            await _unitOfWork.Repository<Notification>().UpdateAsync(logEntity);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to update email log {NotificationId} to Failed", logEntity.Id);
        }
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
                OpenRate = notificationsList.Count > 0 ? (double)notificationsList.Count(n => n.IsRead) / notificationsList.Count : 0,
                AverageDeliveryTimeSeconds = CalculateAverageDeliveryTime(notificationsList)
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
        try
        {
            var query = _dbContext.EmailTemplates
                .AsNoTracking()
                .Where(t =>
                    t.TenantId == tenantId &&
                    !t.IsDeleted &&
                    t.Module == "Notifications");

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(t => t.Category == type);
            }

            var templates = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return templates.Select(t => new NotificationTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Type = t.Category ?? string.Empty,
                Subject = t.Subject,
                HtmlTemplate = t.HtmlBody,
                TextTemplate = t.PlainTextBody,
                Variables = TryParseTemplateVariables(t.TemplateVariables),
                IsActive = t.IsActive,
                CreatedBy = t.CreatedBy ?? string.Empty,
                CreatedAt = t.CreatedAt,
                LastUsed = null,
                UsageCount = 0
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification templates for tenant {TenantId}", tenantId);
            return new List<NotificationTemplateDto>();
        }
    }

    public async Task<NotificationTemplateDto> CreateNotificationTemplateAsync(
        CreateNotificationTemplateDto templateDto, Guid createdBy, Guid tenantId)
    {
        try
        {
            var name = templateDto.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Template name is required");
            }

            var exists = await _dbContext.EmailTemplates
                .AsNoTracking()
                .AnyAsync(t =>
                    t.TenantId == tenantId &&
                    !t.IsDeleted &&
                    t.Module == "Notifications" &&
                    t.Name == name);

            if (exists)
            {
                throw new InvalidOperationException($"A template with name '{name}' already exists");
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == createdBy);

            var entity = new EmailTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                Module = "Notifications",
                Category = templateDto.Type?.Trim(),
                Subject = templateDto.Subject?.Trim() ?? string.Empty,
                HtmlBody = templateDto.HtmlTemplate ?? string.Empty,
                PlainTextBody = templateDto.TextTemplate,
                TemplateVariables = templateDto.Variables != null ? JsonSerializer.Serialize(templateDto.Variables) : null,
                IsActive = templateDto.IsActive,
                CreatedById = createdBy,
                CreatedBy = user?.UserName ?? user?.Email ?? string.Empty
            };

            await _dbContext.EmailTemplates.AddAsync(entity);
            await _dbContext.SaveChangesAsync();

            return new NotificationTemplateDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Type = entity.Category ?? string.Empty,
                Subject = entity.Subject,
                HtmlTemplate = entity.HtmlBody,
                TextTemplate = entity.PlainTextBody,
                Variables = templateDto.Variables,
                IsActive = entity.IsActive,
                CreatedBy = entity.CreatedBy ?? string.Empty,
                CreatedAt = entity.CreatedAt,
                LastUsed = null,
                UsageCount = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification template for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<bool> DeleteNotificationTemplateAsync(Guid templateId, Guid tenantId)
    {
        try
        {
            var template = await _dbContext.EmailTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId && !t.IsDeleted && t.Module == "Notifications");

            if (template == null) return false;

            template.IsActive = false;
            template.IsDeleted = true;
            template.DeletedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification template {TemplateId} for tenant {TenantId}", templateId, tenantId);
            return false;
        }
    }

    #endregion

    #region Email Campaigns

    public async Task<EmailCampaignDto> CreateEmailCampaignAsync(CreateEmailCampaignDto campaignDto, Guid createdBy, Guid tenantId)
    {
        try
        {
            var name = campaignDto.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Campaign name is required");
            }

            if (string.IsNullOrWhiteSpace(campaignDto.Subject))
            {
                throw new InvalidOperationException("Campaign subject is required");
            }

            if (string.IsNullOrWhiteSpace(campaignDto.HtmlContent) && string.IsNullOrWhiteSpace(campaignDto.TextContent))
            {
                throw new InvalidOperationException("Campaign content is required");
            }

            var exists = await _dbContext.EmailCampaigns
                .AsNoTracking()
                .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Name == name);

            if (exists)
            {
                throw new InvalidOperationException($"A campaign with name '{name}' already exists");
            }

            var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == createdBy);

            var campaign = new EmailCampaign
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                Subject = campaignDto.Subject?.Trim() ?? string.Empty,
                HtmlContent = campaignDto.HtmlContent ?? string.Empty,
                TextContent = campaignDto.TextContent,
                FromName = campaignDto.FromName,
                FromEmail = campaignDto.FromEmail,
                ReplyTo = campaignDto.ReplyTo,
                ScheduledFor = campaignDto.ScheduledFor,
                Status = "draft",
                IsTemplate = campaignDto.IsTemplate,
                TemplateData = campaignDto.TemplateData,
                TagsJson = campaignDto.Tags != null ? JsonSerializer.Serialize(campaignDto.Tags) : null,
                CreatedById = createdBy,
                CreatedBy = user?.UserName ?? user?.Email ?? string.Empty
            };

            await _dbContext.EmailCampaigns.AddAsync(campaign);

            // Pre-create recipients (best effort)
            var recipientRows = await ResolveCampaignRecipientsAsync(campaignDto, tenantId);
            foreach (var r in recipientRows)
            {
                r.EmailCampaignId = campaign.Id;
                r.TenantId = tenantId;
            }
            if (recipientRows.Count > 0)
            {
                await _dbContext.EmailCampaignRecipients.AddRangeAsync(recipientRows);
            }

            await _dbContext.SaveChangesAsync();

            return await MapCampaignToDtoAsync(campaign, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating email campaign for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<EmailCampaignDto?> GetEmailCampaignAsync(Guid campaignId, Guid tenantId)
    {
        try
        {
            var campaign = await _dbContext.EmailCampaigns
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.TenantId == tenantId && !c.IsDeleted);

            if (campaign == null) return null;

            return await MapCampaignToDtoAsync(campaign, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email campaign {CampaignId} for tenant {TenantId}", campaignId, tenantId);
            return null;
        }
    }

    public async Task<List<EmailCampaignDto>> GetEmailCampaignsAsync(Guid tenantId, string? status = null)
    {
        try
        {
            var query = _dbContext.EmailCampaigns
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.Status == status);
            }

            var campaigns = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var result = new List<EmailCampaignDto>(campaigns.Count);
            foreach (var c in campaigns)
            {
                var dto = await MapCampaignToDtoAsync(c, tenantId);
                result.Add(dto);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email campaigns for tenant {TenantId}", tenantId);
            return new List<EmailCampaignDto>();
        }
    }

    public async Task<bool> DeleteEmailCampaignAsync(Guid campaignId, Guid tenantId)
    {
        try
        {
            var campaign = await _dbContext.EmailCampaigns
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.TenantId == tenantId && !c.IsDeleted);

            if (campaign == null) return false;

            campaign.IsDeleted = true;
            campaign.DeletedAt = DateTime.UtcNow;
            campaign.Status = "cancelled";

            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting email campaign {CampaignId} for tenant {TenantId}", campaignId, tenantId);
            return false;
        }
    }

    public async Task<EmailCampaignDto?> SendEmailCampaignAsync(Guid campaignId, Guid tenantId)
    {
        try
        {
            var campaign = await _dbContext.EmailCampaigns
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.TenantId == tenantId && !c.IsDeleted);

            if (campaign == null) return null;

            if (campaign.Status == "cancelled")
            {
                throw new InvalidOperationException("Cannot send a cancelled campaign");
            }

            // Ensure recipients exist (campaign may have been created without recipients or roles changed)
            var existingRecipients = await _dbContext.EmailCampaignRecipients
                .Where(r => r.TenantId == tenantId && r.EmailCampaignId == campaignId && !r.IsDeleted)
                .ToListAsync();

            if (existingRecipients.Count == 0)
            {
                // No recipients were stored; do not attempt send.
                throw new InvalidOperationException("Campaign has no recipients");
            }

            var scheduledFor = campaign.ScheduledFor ?? DateTime.UtcNow;
            campaign.Status = scheduledFor > DateTime.UtcNow ? "scheduled" : "sending";

            // Enqueue notifications for recipients that are not yet enqueued.
            foreach (var recipient in existingRecipients.Where(r => r.NotificationId == null))
            {
                var payload = new
                {
                    email = new
                    {
                        isHtml = true,
                        attachmentsOmitted = true,
                        attachments = new List<object>()
                    },
                    campaign = new
                    {
                        id = campaign.Id,
                        recipientId = recipient.Id
                    }
                };

                var n = new Notification
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    NotificationType = "EmailCampaign",
                    Title = campaign.Subject,
                    Message = campaign.HtmlContent,
                    Priority = "Normal",
                    Status = "Pending",
                    IsRead = false,
                    ScheduledFor = scheduledFor,
                    SentAt = null,
                    AttemptCount = 0,
                    LastError = null,
                    DeliveryMethods = "Email",
                    EmailAddress = recipient.Email,
                    PhoneNumber = null,
                    RecipientId = recipient.UserId ?? Guid.Empty,
                    AdditionalData = JsonSerializer.Serialize(payload)
                };

                await _unitOfWork.Repository<Notification>().AddAsync(n);

                recipient.NotificationId = n.Id;
                recipient.Status = "Pending";
                recipient.AttemptCount = 0;
                recipient.LastError = null;
            }

            await _unitOfWork.SaveChangesAsync();
            await _dbContext.SaveChangesAsync();

            return await MapCampaignToDtoAsync(campaign, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email campaign {CampaignId} for tenant {TenantId}", campaignId, tenantId);
            throw;
        }
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
                EntityType = string.IsNullOrWhiteSpace(notification.EntityType) ? null : notification.EntityType,
                EntityId = notification.EntityId.HasValue ? notification.EntityId.Value.ToString() : null,
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

        try
        {
            if (tenantId == Guid.Empty)
            {
                _logger.LogWarning("[BROADCAST] TenantId is empty; skipping broadcast for '{Title}'", notification.Title);
                return;
            }

            var now = DateTime.UtcNow;

            // Resolve tenant user IDs (prefer UserTenants; fall back to primary TenantId).
            var tenantUserIds = await _dbContext.UserTenants
                .AsNoTracking()
                .Where(ut =>
                    ut.TenantId == tenantId &&
                    ut.Status == UserTenantStatus.Active &&
                    !ut.IsDeleted &&
                    (ut.ExpiresAt == null || ut.ExpiresAt > now))
                .Select(ut => ut.UserId)
                .Distinct()
                .ToListAsync();

            if (tenantUserIds.Count == 0)
            {
                tenantUserIds = await _dbContext.Users
                    .AsNoTracking()
                    .Where(u => u.TenantId == tenantId && u.IsActive)
                    .Select(u => u.Id)
                    .Distinct()
                    .ToListAsync();
            }

            if (tenantUserIds.Count == 0)
            {
                _logger.LogWarning("[BROADCAST] No tenant users found for tenant {TenantId}", tenantId);
                return;
            }

            // If roles are supplied, filter down to users that have ANY of the roles.
            if (roles != null && roles.Count > 0)
            {
                var roleNames = roles
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (roleNames.Count > 0)
                {
                    var roleIds = await _dbContext.Roles
                        .AsNoTracking()
                        .Where(r => r.Name != null && roleNames.Contains(r.Name))
                        .Select(r => r.Id)
                        .ToListAsync();

                    if (roleIds.Count == 0)
                    {
                        _logger.LogWarning("[BROADCAST] No matching roles found for {Roles}", string.Join(", ", roleNames));
                        return;
                    }

                    var roleUserIds = await _dbContext.UserRoles
                        .AsNoTracking()
                        .Where(ur => roleIds.Contains(ur.RoleId))
                        .Select(ur => ur.UserId)
                        .Distinct()
                        .ToListAsync();

                    tenantUserIds = tenantUserIds.Intersect(roleUserIds).ToList();
                }
            }

            if (tenantUserIds.Count == 0)
            {
                _logger.LogInformation("[BROADCAST] No recipients after role filtering for tenant {TenantId}", tenantId);
                return;
            }

            // Persist per-user notifications and push via SignalR.
            var repo = _unitOfWork.Repository<Notification>();
            var entities = tenantUserIds.Select(userId => new Notification
            {
                NotificationType = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                Status = "Sent",
                IsRead = false,
                ScheduledFor = DateTime.UtcNow,
                SentAt = DateTime.UtcNow,
                EntityType = notification.EntityType,
                EntityId = notification.EntityId ?? Guid.Empty,
                ActionUrl = notification.ActionUrl,
                AdditionalData = notification.Metadata != null
                    ? System.Text.Json.JsonSerializer.Serialize(notification.Metadata)
                    : null,
                DeliveryMethods = "InApp",
                TenantId = tenantId,
                RecipientId = userId,
                Priority = string.IsNullOrWhiteSpace(notification.Severity) ? "Normal" : notification.Severity,
                AttemptCount = 1
            }).ToList();

            await repo.AddRangeAsync(entities);
            await _unitOfWork.SaveChangesAsync();

            foreach (var n in entities)
            {
                try
                {
                    var dashboardNotification = MapToDashboardDto(n);
                    await _hubNotificationService.BroadcastNotificationAsync(n.RecipientId.ToString(), dashboardNotification);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[BROADCAST] Failed realtime broadcast for notification {NotificationId}", n.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BROADCAST] Error broadcasting notification '{Title}'", notification.Title);
        }
    }

    #endregion

    #region Background Processing

    public async Task ProcessPendingNotificationsAsync()
    {
        try
        {
            _logger.LogInformation("Processing pending notifications");

            var maxRetryAttempts = int.TryParse(_configuration["Notifications:MaxRetryAttempts"], out var max) ? max : 5;
            var initialBackoffSeconds = int.TryParse(_configuration["Notifications:InitialBackoffSeconds"], out var backoff) ? backoff : 30;
            var backoffMultiplier = double.TryParse(_configuration["Notifications:BackoffMultiplier"], out var mult) ? mult : 1.5;
            var batchSize = int.TryParse(_configuration["Notifications:DispatchBatchSize"], out var bs) && bs > 0 ? bs : 200;
            var processingLeaseSeconds = int.TryParse(_configuration["Notifications:ProcessingLeaseSeconds"], out var pls) && pls > 0 ? pls : 300;

            var now = DateTime.UtcNow;

            var dueIds = await _dbContext.Notifications
                .AsNoTracking()
                .Where(n =>
                    !n.IsDeleted &&
                    (n.Status == "Pending" || n.Status == "Failed" || n.Status == "Processing") &&
                    n.SentAt == null &&
                    n.ScheduledFor <= now &&
                    n.AttemptCount < maxRetryAttempts)
                .OrderBy(n => n.ScheduledFor)
                .ThenByDescending(n => n.CreatedAt)
                .Select(n => n.Id)
                .Take(batchSize)
                .ToListAsync();

            var processed = 0;
            var skipped = 0;

            foreach (var id in dueIds)
            {
                // Atomically claim the notification before sending so multiple dispatchers/admin retries don't double-send.
                var claimed = await ClaimNotificationForProcessingAsync(id, now, maxRetryAttempts, processingLeaseSeconds);
                if (claimed == 0)
                {
                    skipped++;
                    continue;
                }

                var notification = await _dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
                if (notification == null)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var sentAt = DateTime.UtcNow;
                    var methods = notification.DeliveryMethods ?? string.Empty;
                    var campaignRecipientId = TryParseCampaignRecipientId(notification.AdditionalData);

                    if (methods.Contains("Email", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(notification.EmailAddress))
                        {
                            throw new InvalidOperationException("Missing recipient email address");
                        }

                        var emailPayload = TryParseEmailPayload(notification.AdditionalData);

                        var emailDto = new ErpSystem.Core.Interfaces.Common.EmailDto
                        {
                            To = notification.EmailAddress,
                            Subject = notification.Title ?? string.Empty,
                            Body = notification.Message ?? string.Empty,
                            IsHtml = emailPayload?.IsHtml ?? true,
                            Attachments = emailPayload?.Attachments?.Select(a => new ErpSystem.Core.Interfaces.Common.EmailAttachmentDto
                            {
                                FileName = a.FileName,
                                ContentType = a.ContentType,
                                Content = string.IsNullOrWhiteSpace(a.ContentBase64) ? Array.Empty<byte>() : Convert.FromBase64String(a.ContentBase64)
                            }).Where(a => a.Content.Length > 0).ToList() ?? new List<ErpSystem.Core.Interfaces.Common.EmailAttachmentDto>()
                        };

                        var ok = await _emailService.SendEmailAsync(emailDto);
                        if (!ok)
                        {
                            throw new InvalidOperationException("Email service returned failure");
                        }

                        notification.Status = "Sent";
                        notification.SentAt = sentAt;
                        notification.ScheduledFor = sentAt;

                        if (campaignRecipientId.HasValue)
                        {
                            var recipient = await _dbContext.EmailCampaignRecipients
                                .FirstOrDefaultAsync(r => r.Id == campaignRecipientId.Value && r.TenantId == notification.TenantId && !r.IsDeleted);
                            if (recipient != null)
                            {
                                recipient.Status = "Sent";
                                recipient.SentAt = sentAt;
                                recipient.AttemptCount = notification.AttemptCount;
                                recipient.LastError = null;
                            }
                        }
                    }
                    else if (methods.Contains("InApp", StringComparison.OrdinalIgnoreCase))
                    {
                        if (notification.RecipientId == Guid.Empty)
                        {
                            throw new InvalidOperationException("Missing recipient user id");
                        }

                        var dashboardNotification = new DashboardNotificationDto
                        {
                            Id = notification.Id.ToString(),
                            Type = notification.NotificationType,
                            Title = notification.Title,
                            Message = notification.Message,
                            Severity = notification.Priority,
                            Timestamp = notification.CreatedAt,
                            IsRead = notification.IsRead,
                            ActionUrl = notification.ActionUrl,
                            EntityType = string.IsNullOrWhiteSpace(notification.EntityType) ? null : notification.EntityType,
                            EntityId = notification.EntityId != Guid.Empty ? notification.EntityId.ToString() : null,
                            Metadata = notification.AdditionalData != null
                                ? JsonSerializer.Deserialize<Dictionary<string, object>>(notification.AdditionalData)
                                : null
                        };

                        await _hubNotificationService.BroadcastNotificationAsync(notification.RecipientId.ToString(), dashboardNotification);

                        notification.Status = "Sent";
                        notification.SentAt = sentAt;
                        notification.ScheduledFor = sentAt;
                    }
                    else if (methods.Contains("Push", StringComparison.OrdinalIgnoreCase))
                    {
                        if (notification.RecipientId == Guid.Empty)
                        {
                            throw new InvalidOperationException("Missing recipient user id for push notification");
                        }

                        await SendPushNotificationAsync(notification.RecipientId, notification.Title ?? string.Empty, notification.Message ?? string.Empty);
                        notification.Status = "Sent";
                        notification.SentAt = sentAt;
                        notification.ScheduledFor = sentAt;
                    }
                    else if (methods.Contains("SMS", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(notification.PhoneNumber))
                        {
                            throw new InvalidOperationException("Missing recipient phone number");
                        }

                        await SendSmsAsync(notification.PhoneNumber, notification.Message ?? string.Empty);
                        notification.Status = "Sent";
                        notification.SentAt = sentAt;
                        notification.ScheduledFor = sentAt;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported delivery method");
                    }

                    await _unitOfWork.Repository<Notification>().UpdateAsync(notification);
                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing notification {NotificationId}", notification.Id);

                    notification.LastError = ex.Message;

                    if (notification.AttemptCount >= maxRetryAttempts)
                    {
                        notification.Status = "DeadLetter";
                    }
                    else
                    {
                        notification.Status = "Failed";
                        // Exponential backoff for next attempt.
                        var delaySeconds = initialBackoffSeconds * Math.Pow(backoffMultiplier, Math.Max(0, notification.AttemptCount - 1));
                        delaySeconds = Math.Min(delaySeconds, 24 * 60 * 60); // cap at 24h
                        notification.ScheduledFor = DateTime.UtcNow.AddSeconds(delaySeconds);
                    }

                    var campaignRecipientId = TryParseCampaignRecipientId(notification.AdditionalData);
                    if (campaignRecipientId.HasValue)
                    {
                        try
                        {
                            var recipient = await _dbContext.EmailCampaignRecipients
                                .FirstOrDefaultAsync(r => r.Id == campaignRecipientId.Value && r.TenantId == notification.TenantId && !r.IsDeleted);
                            if (recipient != null)
                            {
                                recipient.Status = notification.Status == "DeadLetter" ? "Failed" : "Failed";
                                recipient.SentAt = null;
                                recipient.AttemptCount = notification.AttemptCount;
                                recipient.LastError = notification.LastError;
                            }
                        }
                        catch (Exception innerEx)
                        {
                            _logger.LogDebug(innerEx, "Failed to update email campaign recipient status for notification {NotificationId}", notification.Id);
                        }
                    }

                    await _unitOfWork.Repository<Notification>().UpdateAsync(notification);
                    processed++;
                }
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation(
                "Processed {Processed} due notifications (skipped {Skipped} already-claimed/not-due).",
                processed,
                skipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessPendingNotificationsAsync");
        }
    }

    private async Task<int> ClaimNotificationForProcessingAsync(Guid notificationId, DateTime now, int maxRetryAttempts, int processingLeaseSeconds)
    {
        var leaseUntil = now.AddSeconds(processingLeaseSeconds);

        // NOTE: This uses raw SQL to ensure only one dispatcher/admin retry can claim a record at a time.
        // It also uses ScheduledFor as a processing lease expiry (so crashed processing can be recovered).
        return await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [Notifications]
SET [Status] = {"Processing"},
    [ScheduledFor] = {leaseUntil},
    [AttemptCount] = [AttemptCount] + 1,
    [LastError] = NULL
WHERE [Id] = {notificationId}
  AND [IsDeleted] = 0
  AND [SentAt] IS NULL
  AND ([Status] = {"Pending"} OR [Status] = {"Failed"} OR [Status] = {"Processing"})
  AND [ScheduledFor] <= {now}
  AND [AttemptCount] < {maxRetryAttempts};
");
    }

    public async Task CleanupExpiredNotificationsAsync()
    {
        try
        {
            const int olderThanDays = 90;
            _logger.LogInformation("Cleaning up expired notifications older than {Days} days", olderThanDays);

            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);

            // --------------------------------------------------------------------------------------------------
            // OPTIMIZATION NOTE (2026-02-15):
            // Replaced the previous fetch-into-memory-and-delete loop with ExecuteUpdateAsync.
            //
            // Previous Implementation:
            //   fetched all expired records (potentially thousands) -> FindAsync()
            //   loaded them into application memory
            //   deleted them one-by-one (or in batches via EF Change Tracker)
            //   Result: System.ComponentModel.Win32Exception (Timeout) when volume was high.
            //
            // New Implementation:
            //   Issued a single SQL UPDATE command to soft-delete records directly on the database server.
            //   Bypasses the Change Tracker for performance.
            //   Eliminates network latency of pulling dead records just to mark them valid.
            // --------------------------------------------------------------------------------------------------
            var count = await _dbContext.Notifications
                .Where(n => n.CreatedAt < cutoffDate && 
                           (n.IsRead || n.Status == "Dismissed" || n.Status == "Archived") &&
                           !n.IsDeleted) // Ensure we don't update already deleted ones
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.IsDeleted, true)
                    .SetProperty(n => n.DeletedAt, DateTime.UtcNow));

            _logger.LogInformation("Deleted {Count} expired notifications", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CleanupExpiredNotificationsAsync");
        }
    }

    #endregion

    #region Helpers

    private async Task<List<EmailCampaignRecipient>> ResolveCampaignRecipientsAsync(CreateEmailCampaignDto campaignDto, Guid tenantId)
    {
        var recipients = new List<EmailCampaignRecipient>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static string Normalize(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        // Explicit emails
        if (campaignDto.RecipientEmails != null)
        {
            foreach (var raw in campaignDto.RecipientEmails)
            {
                var email = Normalize(raw);
                if (string.IsNullOrWhiteSpace(email)) continue;
                if (!seenEmails.Add(email)) continue;

                recipients.Add(new EmailCampaignRecipient
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Email = email,
                    Source = "Email",
                    Status = "Pending"
                });
            }
        }

        // Specific users
        if (campaignDto.RecipientUserIds != null && campaignDto.RecipientUserIds.Count > 0)
        {
            var users = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.IsActive && campaignDto.RecipientUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email })
                .ToListAsync();

            foreach (var u in users)
            {
                if (string.IsNullOrWhiteSpace(u.Email)) continue;
                var email = Normalize(u.Email);
                if (!seenEmails.Add(email)) continue;

                recipients.Add(new EmailCampaignRecipient
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UserId = u.Id,
                    Email = email,
                    Source = "UserId",
                    Status = "Pending"
                });
            }
        }

        // Roles
        if (campaignDto.RecipientRoles != null && campaignDto.RecipientRoles.Count > 0)
        {
            var roleNames = campaignDto.RecipientRoles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roleNames.Count > 0)
            {
                var roleIds = await _dbContext.Roles
                    .AsNoTracking()
                    .Where(r => r.Name != null && roleNames.Contains(r.Name))
                    .Select(r => new { r.Id, r.Name })
                    .ToListAsync();

                if (roleIds.Count > 0)
                {
                    var ids = roleIds.Select(r => r.Id).ToList();
                    var userIds = await _dbContext.UserRoles
                        .AsNoTracking()
                        .Where(ur => ids.Contains(ur.RoleId))
                        .Select(ur => ur.UserId)
                        .Distinct()
                        .ToListAsync();

                    var users = await _dbContext.Users
                        .AsNoTracking()
                        .Where(u => u.TenantId == tenantId && u.IsActive && userIds.Contains(u.Id))
                        .Select(u => new { u.Id, u.Email })
                        .ToListAsync();

                    foreach (var user in users)
                    {
                        if (string.IsNullOrWhiteSpace(user.Email)) continue;
                        var email = Normalize(user.Email);
                        if (!seenEmails.Add(email)) continue;

                        recipients.Add(new EmailCampaignRecipient
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            UserId = user.Id,
                            Email = email,
                            Source = "Role",
                            SourceRole = string.Join(",", roleNames),
                            Status = "Pending"
                        });
                    }
                }
            }
        }

        return recipients;
    }

    private async Task<EmailCampaignDto> MapCampaignToDtoAsync(EmailCampaign campaign, Guid tenantId)
    {
        var recipients = await _dbContext.EmailCampaignRecipients
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.EmailCampaignId == campaign.Id && !r.IsDeleted)
            .ToListAsync();

        var tags = (Dictionary<string, string>?)null;
        if (!string.IsNullOrWhiteSpace(campaign.TagsJson))
        {
            try
            {
                tags = JsonSerializer.Deserialize<Dictionary<string, string>>(campaign.TagsJson);
            }
            catch
            {
                tags = null;
            }
        }

        var sentCount = recipients.Count(r => r.Status == "Sent");
        var failedCount = recipients.Count(r => r.Status == "Failed");

        return new EmailCampaignDto
        {
            Id = campaign.Id,
            Name = campaign.Name,
            Subject = campaign.Subject,
            HtmlContent = campaign.HtmlContent,
            TextContent = campaign.TextContent,
            FromName = campaign.FromName,
            FromEmail = campaign.FromEmail,
            ReplyTo = campaign.ReplyTo,
            TotalRecipients = recipients.Count,
            SentCount = sentCount,
            DeliveredCount = sentCount,
            OpenedCount = 0,
            ClickedCount = 0,
            BouncedCount = failedCount,
            UnsubscribedCount = 0,
            Status = campaign.Status,
            CreatedAt = campaign.CreatedAt,
            ScheduledFor = campaign.ScheduledFor,
            SentAt = campaign.SentAt,
            CreatedBy = campaign.CreatedBy ?? string.Empty,
            Tags = tags,
            Statistics = new ErpSystem.Core.DTOs.Notifications.EmailCampaignStatsDto
            {
                DeliveryRate = recipients.Count > 0 ? (double)sentCount / recipients.Count : 0,
                OpenRate = 0,
                ClickRate = 0,
                BounceRate = recipients.Count > 0 ? (double)failedCount / recipients.Count : 0,
                UnsubscribeRate = 0
            }
        };
    }

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

    private static List<string>? TryParseTemplateVariables(string? templateVariablesJson)
    {
        if (string.IsNullOrWhiteSpace(templateVariablesJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(templateVariablesJson);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement
                    .EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // Support legacy object map: { "var": "desc" }
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                return doc.RootElement
                    .EnumerateObject()
                    .Select(p => p.Name)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class EmailPayload
    {
        public bool IsHtml { get; set; } = true;
        public List<EmailPayloadAttachment>? Attachments { get; set; }
    }

    private sealed class EmailPayloadAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public string? ContentBase64 { get; set; }
    }

    private static EmailPayload? TryParseEmailPayload(string? additionalData)
    {
        if (string.IsNullOrWhiteSpace(additionalData)) return null;

        try
        {
            using var doc = JsonDocument.Parse(additionalData);

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("email", out var emailElem) &&
                emailElem.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<EmailPayload>(
                    emailElem.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static Guid? TryParseCampaignRecipientId(string? additionalData)
    {
        if (string.IsNullOrWhiteSpace(additionalData)) return null;

        try
        {
            using var doc = JsonDocument.Parse(additionalData);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            if (!doc.RootElement.TryGetProperty("campaign", out var campElem) || campElem.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!campElem.TryGetProperty("recipientId", out var ridElem))
            {
                return null;
            }

            if (ridElem.ValueKind == JsonValueKind.String && Guid.TryParse(ridElem.GetString(), out var rid))
            {
                return rid;
            }

            return null;
        }
        catch
        {
            return null;
        }
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
            EntityType = string.IsNullOrWhiteSpace(notification.EntityType) ? null : notification.EntityType,
            EntityId = notification.EntityId != Guid.Empty ? notification.EntityId.ToString() : null,
            Metadata = notification.AdditionalData != null
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(notification.AdditionalData)
                : null
        };
    }

    #endregion
}

using ErpSystem.Core.Entities;
using ErpSystem.Core.DTOs.Notifications;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Repository for managing generic notifications across the system
/// </summary>
public interface INotificationRepository : IGenericRepository<Notification>
{
    /// <summary>
    /// Gets notifications for a user with pagination and filtering
    /// </summary>
    Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(
        Guid userId, Guid tenantId, int page = 1, int pageSize = 20, 
        bool? unreadOnly = null, string? type = null);

    /// <summary>
    /// Gets unread count for a user
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId, Guid tenantId);

    /// <summary>
    /// Gets all pending notifications due for delivery
    /// </summary>
    Task<List<Notification>> GetPendingNotificationsAsync(int maxResults = 100);

    /// <summary>
    /// Gets failed notifications for retry
    /// </summary>
    Task<List<Notification>> GetFailedNotificationsAsync(int maxResults = 100);

    /// <summary>
    /// Deletes notifications older than specified days
    /// </summary>
    Task<int> DeleteOldNotificationsAsync(int olderThanDays = 90);
}

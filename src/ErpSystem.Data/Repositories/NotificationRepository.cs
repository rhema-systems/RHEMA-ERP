using ErpSystem.Core.Entities;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories
{
    /// <summary>
    /// Repository implementation for managing generic notifications
    /// </summary>
    public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
    {
        private readonly ApplicationDbContext _context;

        public NotificationRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
    }

    /// <summary>
    /// Gets notifications for a user with pagination and filtering
    /// </summary>
    public async Task<ErpSystem.Core.DTOs.Notifications.PagedResult<NotificationDto>> GetUserNotificationsAsync(
        Guid userId, Guid tenantId, int page = 1, int pageSize = 20,
        bool? unreadOnly = null, string? type = null)
    {
        try
        {
            var query = _dbSet.AsQueryable()
                .Where(n => n.RecipientId == userId && n.TenantId == tenantId);

            if (unreadOnly == true)
            {
                query = query.Where(n => !n.IsRead);
            }

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(n => n.NotificationType == type);
            }

            var totalCount = await query.CountAsync();

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.NotificationType,
                    Severity = n.Priority,
                    IsRead = n.IsRead,
                    Timestamp = n.CreatedAt,
                    ActionUrl = n.ActionUrl,
                    EntityType = n.EntityType,
                    EntityId = n.EntityId,
                    Metadata = new Dictionary<string, object>
                    {
                        { "status", n.Status ?? "" },
                        { "recipientId", n.RecipientId },
                        { "attemptCount", n.AttemptCount },
                        { "sentAt", n.SentAt ?? DateTime.MinValue },
                        { "scheduledFor", n.ScheduledFor },
                        { "lastError", n.LastError ?? "" }
                    },
                    ExpiresAt = n.ScheduledFor.AddDays(7) // Default expiry 7 days from scheduled time
                })
                .ToListAsync();

            return new ErpSystem.Core.DTOs.Notifications.PagedResult<NotificationDto>
            {
                Items = notifications,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

        public async Task<int> GetUnreadCountAsync(Guid userId, Guid tenantId)
        {
            return await _dbSet
                .Where(n => n.RecipientId == userId && n.TenantId == tenantId && !n.IsRead)
                .CountAsync();
        }

        public async Task<List<Notification>> GetPendingNotificationsAsync(int maxResults = 100)
        {
            var now = DateTime.UtcNow;
            return await _dbSet
                .Where(n => n.Status == "Pending" && n.ScheduledFor <= now && n.AttemptCount < 5)
                .OrderBy(n => n.ScheduledFor)
                .Take(maxResults)
                .ToListAsync();
        }

        public async Task<List<Notification>> GetFailedNotificationsAsync(int maxResults = 100)
        {
            return await _dbSet
                .Where(n => n.Status == "Failed" && n.AttemptCount < 5)
                .OrderBy(n => n.CreatedAt)
                .Take(maxResults)
                .ToListAsync();
        }

        public async Task<int> DeleteOldNotificationsAsync(int olderThanDays = 90)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            var notificationsToDelete = await _dbSet
                .Where(n => n.CreatedAt < cutoffDate && (n.IsRead || n.Status == "Dismissed" || n.Status == "Archived"))
                .ToListAsync();

            _dbSet.RemoveRange(notificationsToDelete);
            await _context.SaveChangesAsync();

            return notificationsToDelete.Count;
        }
    }
}

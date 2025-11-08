using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing notifications that have failed to send after maximum retry attempts.
/// Provides mechanisms for manual review, analysis, and retry of dead-letter notifications.
/// </summary>
public interface IDeadLetterNotificationService
{
    Task<PagedResult<MaintenanceNotificationDto>> GetDeadLetterNotificationsAsync(int page = 1, int pageSize = 20);
    Task<List<MaintenanceNotificationDto>> GetDeadLettersByEntityTypeAsync(string entityType);
    Task<List<MaintenanceNotificationDto>> GetDeadLettersByErrorTypeAsync(string errorType);
    Task<MaintenanceNotificationDto?> GetDeadLetterByIdAsync(Guid id);
    Task<int> RetryDeadLetterNotificationAsync(Guid notificationId);
    Task<int> RetryAllDeadLettersAsync();
    Task DeleteDeadLetterPermanentlyAsync(Guid id);
    Task<Dictionary<string, int>> GetDeadLetterStatisticsAsync();
}

public class DeadLetterNotificationService : IDeadLetterNotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMaintenanceNotificationService _notificationService;
    private readonly ILogger<DeadLetterNotificationService> _logger;

    public DeadLetterNotificationService(
        IUnitOfWork unitOfWork,
        IMaintenanceNotificationService notificationService,
        ILogger<DeadLetterNotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<PagedResult<MaintenanceNotificationDto>> GetDeadLetterNotificationsAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            var repo = _unitOfWork.Repository<MaintenanceNotification>();
            var deadLetters = await repo.FindAsync(n => n.Status == "DeadLetter");

            var pagedList = deadLetters
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var total = deadLetters.Count();

            return new PagedResult<MaintenanceNotificationDto>
            {
                Items = pagedList.Select(MapToDto).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dead letter notifications");
            throw;
        }
    }

    public async Task<List<MaintenanceNotificationDto>> GetDeadLettersByEntityTypeAsync(string entityType)
    {
        try
        {
            var repo = _unitOfWork.Repository<MaintenanceNotification>();
            var deadLetters = await repo.FindAsync(n =>
                n.Status == "DeadLetter" && n.EntityType == entityType);

            return deadLetters.Select(MapToDto).OrderByDescending(n => n.CreatedDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dead letters for entity type {EntityType}", entityType);
            throw;
        }
    }

    public async Task<List<MaintenanceNotificationDto>> GetDeadLettersByErrorTypeAsync(string errorType)
    {
        try
        {
            var repo = _unitOfWork.Repository<MaintenanceNotification>();
            var deadLetters = await repo.FindAsync(n =>
                n.Status == "DeadLetter" && n.LastError != null && n.LastError.Contains(errorType));

            return deadLetters.Select(MapToDto).OrderByDescending(n => n.CreatedDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dead letters by error type {ErrorType}", errorType);
            throw;
        }
    }

    public async Task<MaintenanceNotificationDto?> GetDeadLetterByIdAsync(Guid id)
    {
        try
        {
            var notification = await _unitOfWork.Repository<MaintenanceNotification>().GetByIdAsync(id);
            if (notification == null || notification.Status != "DeadLetter")
                return null;

            return MapToDto(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dead letter notification {NotificationId}", id);
            throw;
        }
    }

    public async Task<int> RetryDeadLetterNotificationAsync(Guid notificationId)
    {
        try
        {
            var repo = _unitOfWork.Repository<MaintenanceNotification>();
            var notification = await repo.GetByIdAsync(notificationId);

            if (notification == null)
                throw new ArgumentException($"Notification with ID {notificationId} not found");

            if (notification.Status != "DeadLetter")
                throw new InvalidOperationException($"Notification is not in DeadLetter status, current status: {notification.Status}");

            // Reset for retry
            notification.Status = "Pending";
            notification.AttemptCount = 0;
            notification.LastError = null;
            notification.ScheduledFor = DateTime.UtcNow;

            await repo.UpdateAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Moved dead letter notification {NotificationId} back to Pending for retry", notificationId);

            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying dead letter notification {NotificationId}", notificationId);
            throw;
        }
    }

    public async Task<int> RetryAllDeadLettersAsync()
    {
        try
        {
            var repo = _unitOfWork.Repository<MaintenanceNotification>();
            var deadLetters = await repo.FindAsync(n => n.Status == "DeadLetter");

            var retryCount = 0;

            foreach (var notification in deadLetters)
            {
                try
                {
                    notification.Status = "Pending";
                    notification.AttemptCount = 0;
                    notification.LastError = null;
                    notification.ScheduledFor = DateTime.UtcNow;

                    await repo.UpdateAsync(notification);
                    retryCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrying dead letter notification {NotificationId}", notification.Id);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Moved {Count} dead letter notifications back to Pending for retry", retryCount);

            return retryCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying all dead letter notifications");
            throw;
        }
    }

    public async Task DeleteDeadLetterPermanentlyAsync(Guid id)
    {
        try
        {
            var repo = _unitOfWork.Repository<MaintenanceNotification>();
            var notification = await repo.GetByIdAsync(id);

            if (notification == null)
                throw new ArgumentException($"Notification with ID {id} not found");

            if (notification.Status != "DeadLetter")
                throw new InvalidOperationException($"Only DeadLetter notifications can be permanently deleted");

            await repo.HardDeleteAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Permanently deleted dead letter notification {NotificationId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error permanently deleting dead letter notification {NotificationId}", id);
            throw;
        }
    }

    public async Task<Dictionary<string, int>> GetDeadLetterStatisticsAsync()
    {
        try
        {
            var repo = _unitOfWork.Repository<MaintenanceNotification>();
            var deadLetters = await repo.FindAsync(n => n.Status == "DeadLetter");

            var stats = new Dictionary<string, int>
            {
                { "TotalDeadLetters", deadLetters.Count() },
                { "ByWorkOrder", deadLetters.Count(n => n.EntityType == "WorkOrder") },
                { "BySchedule", deadLetters.Count(n => n.EntityType == "MaintenanceSchedule") },
                { "ByJobCard", deadLetters.Count(n => n.EntityType == "JobCard") },
                { "Critical", deadLetters.Count(n => n.Priority == "Critical") },
                { "High", deadLetters.Count(n => n.Priority == "High") },
                { "Normal", deadLetters.Count(n => n.Priority == "Normal") },
                { "Low", deadLetters.Count(n => n.Priority == "Low") },
                { "OlderThan7Days", deadLetters.Count(n => n.CreatedAt < DateTime.UtcNow.AddDays(-7)) },
                { "OlderThan30Days", deadLetters.Count(n => n.CreatedAt < DateTime.UtcNow.AddDays(-30)) }
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dead letter statistics");
            throw;
        }
    }

    private MaintenanceNotificationDto MapToDto(MaintenanceNotification entity)
    {
        return new MaintenanceNotificationDto
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
            SentAt = entity.SentAt,
            AdditionalData = entity.AdditionalData,
            ActionUrl = entity.ActionUrl,
            CreatedDate = entity.CreatedAt,
            AttemptCount = entity.AttemptCount,
            LastError = entity.LastError
        };
    }
}

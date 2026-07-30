using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

public class AppraisalNotificationService : IAppraisalNotificationService
{
    private readonly IGenericRepository<AppraisalNotification> _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<AppraisalNotificationService> _logger;

    public AppraisalNotificationService(
        IGenericRepository<AppraisalNotification> repository,
        ICurrentUserProvider currentUserProvider,
        ILogger<AppraisalNotificationService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _logger     = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<AppraisalNotificationSummaryDto> GetNotificationSummaryAsync(
        Guid employeeId, int recentCount = 20, CancellationToken ct = default)
    {
        try
        {
            var tenantId = GetTenantId();
            var query = _repository.GetQueryable()
                .Where(n => n.TenantId == tenantId && n.RecipientEmployeeId == employeeId)
                .OrderByDescending(n => n.CreatedDate);

            var unreadCount = await query.CountAsync(n => !n.IsRead, ct);

            var recent = await query
                .Take(recentCount)
                .ToListAsync(ct);

            return new AppraisalNotificationSummaryDto
            {
                UnreadCount         = unreadCount,
                RecentNotifications = recent.Select(MapToDto).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notification summary for employee {Id}", employeeId);
            return new AppraisalNotificationSummaryDto();
        }
    }

    public async Task<List<AppraisalNotificationDto>> GetAllNotificationsAsync(
        Guid employeeId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var tenantId = GetTenantId();
            var items = await _repository.GetQueryable()
                .Where(n => n.TenantId == tenantId && n.RecipientEmployeeId == employeeId)
                .OrderByDescending(n => n.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return items.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notifications for employee {Id}", employeeId);
            return new List<AppraisalNotificationDto>();
        }
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        try
        {
            var tenantId = GetTenantId();
            var notification = await _repository.GetQueryable()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.TenantId == tenantId, ct);

            if (notification == null || notification.IsRead) return;

            notification.IsRead  = true;
            notification.ReadDate = DateTime.UtcNow;
            await _repository.UpdateAsync(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {Id} as read", notificationId);
        }
    }

    public async Task MarkAllAsReadAsync(Guid employeeId, CancellationToken ct = default)
    {
        try
        {
            var tenantId = GetTenantId();
            var unread = await _repository.GetQueryable()
                .Where(n => n.TenantId == tenantId && n.RecipientEmployeeId == employeeId && !n.IsRead)
                .ToListAsync(ct);

            if (!unread.Any()) return;

            var now = DateTime.UtcNow;
            foreach (var n in unread)
            {
                n.IsRead   = true;
                n.ReadDate = now;
            }

            await _repository.UpdateRangeAsync(unread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications read for employee {Id}", employeeId);
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid employeeId, CancellationToken ct = default)
    {
        try
        {
            var tenantId = GetTenantId();
            return await _repository.GetQueryable()
                .CountAsync(n => n.TenantId == tenantId && n.RecipientEmployeeId == employeeId && !n.IsRead, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching unread count for employee {Id}", employeeId);
            return 0;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static AppraisalNotificationDto MapToDto(AppraisalNotification n) => new()
    {
        NotificationId       = n.Id,
        RecipientEmployeeId  = n.RecipientEmployeeId,
        Type                 = n.Type,
        Title                = n.Title,
        Message              = n.Message,
        SubjectEmployeeName  = n.SubjectEmployeeName,
        CycleName            = n.CycleName,
        NavigationUrl        = n.NavigationUrl,
        AppraisalId          = n.AppraisalId,
        CreatedDate          = n.CreatedDate,
        IsRead               = n.IsRead,
        ReadDate             = n.ReadDate,
        Urgency              = n.Urgency,
        TimeAgo              = string.Empty   // computed client-side
    };
}

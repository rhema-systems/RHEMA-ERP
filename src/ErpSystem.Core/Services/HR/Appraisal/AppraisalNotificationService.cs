using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

public class AppraisalNotificationService : IAppraisalNotificationService
{
    private readonly IGenericRepository<AppraisalNotification> _repository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalNotificationService> _logger;

    public AppraisalNotificationService(
        IGenericRepository<AppraisalNotification> repository,
        IGenericRepository<Employee> employeeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalNotificationService> logger)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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
            // UpdateAsync only marks the entity modified — without this the read state was
            // discarded when the request ended, so notifications never stopped being unread.
            await _unitOfWork.SaveChangesAsync(ct);
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
            await _unitOfWork.SaveChangesAsync(ct);
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

    /// <inheritdoc />
    public async Task<int> RaiseAsync(IEnumerable<AppraisalNotificationRequest> requests, CancellationToken ct = default)
    {
        var wanted = requests?.ToList() ?? new List<AppraisalNotificationRequest>();
        if (wanted.Count == 0) return 0;

        var tenantId = GetTenantId();

        // RecipientEmployeeId is an Employee foreign key, so an id that no longer resolves
        // would fail the whole INSERT batch. Recipients are filtered down to employees that
        // actually exist in this tenant rather than trusting the caller's list.
        var recipientIds = wanted.Select(r => r.RecipientEmployeeId).Distinct().ToList();
        var knownRecipients = await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && recipientIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(ct);
        var known = knownRecipients.ToHashSet();

        // Suppress a repeat of an unread notification of the same type, cycle and recipient.
        // Deadline reminders are re-run on demand, and nobody wants five copies of the same
        // "self-evaluation closes on Friday".
        var existingUnread = await _repository.GetQueryable()
            .Where(n => n.TenantId == tenantId && !n.IsRead && recipientIds.Contains(n.RecipientEmployeeId))
            .Select(n => new { n.RecipientEmployeeId, n.Type, n.CycleName, n.Title })
            .ToListAsync(ct);
        var alreadyPending = existingUnread
            .Select(n => (n.RecipientEmployeeId, n.Type, n.CycleName ?? string.Empty, n.Title))
            .ToHashSet();

        var toInsert = new List<AppraisalNotification>();
        foreach (var request in wanted)
        {
            if (!known.Contains(request.RecipientEmployeeId)) continue;

            var key = (request.RecipientEmployeeId, request.Type, request.CycleName ?? string.Empty, request.Title);
            if (!alreadyPending.Add(key)) continue;

            toInsert.Add(new AppraisalNotification
            {
                TenantId            = tenantId,
                RecipientEmployeeId = request.RecipientEmployeeId,
                Type                = request.Type,
                Title               = Truncate(request.Title, 255),
                Message             = Truncate(request.Message, 1000),
                SubjectEmployeeName = TruncateOptional(request.SubjectEmployeeName, 255),
                CycleName           = TruncateOptional(request.CycleName, 255),
                NavigationUrl       = TruncateOptional(request.NavigationUrl, 500),
                AppraisalId         = request.AppraisalId,
                CreatedDate         = DateTime.UtcNow,
                IsRead              = false,
                Urgency             = request.Urgency
            });
        }

        if (toInsert.Count == 0) return 0;

        await _repository.AddRangeAsync(toInsert);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Raised {Count} appraisal notification(s)", toInsert.Count);
        return toInsert.Count;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Column widths are enforced here so a long goal or cycle name cannot fail the insert.</summary>
    private static string Truncate(string? value, int maxLength)
        => value is null ? string.Empty : value.Length > maxLength ? value[..maxLength] : value;

    private static string? TruncateOptional(string? value, int maxLength)
        => value is null ? null : value.Length > maxLength ? value[..maxLength] : value;


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

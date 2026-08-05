using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class OrientationNotificationService : IOrientationNotificationService
{
    private readonly IOrientationNotificationRepository _notificationRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrientationNotificationService> _logger;

    public OrientationNotificationService(
        IOrientationNotificationRepository notificationRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<OrientationNotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<OrientationNotification> GetOwnedNotificationAsync(Guid id)
    {
        var entity = await _notificationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation notification with ID '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<OrientationNotificationDto>> GetByRecipientAsync(Guid recipientEmployeeId, bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _notificationRepository.GetByRecipientAsync(recipientEmployeeId, unreadOnly))
            .Where(n => n.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<int> GetUnreadCountAsync(Guid recipientEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var unread = await _notificationRepository.GetByRecipientAsync(recipientEmployeeId, unreadOnly: true);
        return unread.Count(n => n.TenantId == tenantId);
    }

    public async Task<IEnumerable<OrientationNotificationDto>> GetByEnrollmentIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _notificationRepository.GetByEnrollmentIdAsync(enrollmentId))
            .Where(n => n.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<OrientationNotificationDto> CreateAsync(CreateOrientationNotificationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _notificationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNotificationAsync(notificationId);

        if (!entity.IsRead)
        {
            entity.IsRead = true;
            entity.ReadAt = DateTime.UtcNow;
            await _notificationRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(Guid recipientEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var unread = (await _notificationRepository.GetByRecipientAsync(recipientEmployeeId, unreadOnly: true))
            .Where(n => n.TenantId == tenantId);
        var now = DateTime.UtcNow;
        var count = 0;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
            await _notificationRepository.UpdateAsync(n);
            count++;
        }
        if (count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        return count;
    }
}

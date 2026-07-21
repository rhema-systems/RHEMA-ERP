using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class OrientationNotificationService : IOrientationNotificationService
{
    private readonly IOrientationNotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrientationNotificationService> _logger;

    public OrientationNotificationService(
        IOrientationNotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrientationNotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<OrientationNotificationDto>> GetByRecipientAsync(Guid recipientEmployeeId, bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        var entities = await _notificationRepository.GetByRecipientAsync(recipientEmployeeId, unreadOnly);
        return entities.ToDtoList();
    }

    public async Task<int> GetUnreadCountAsync(Guid recipientEmployeeId, CancellationToken cancellationToken = default)
    {
        return await _notificationRepository.GetUnreadCountAsync(recipientEmployeeId);
    }

    public async Task<IEnumerable<OrientationNotificationDto>> GetByEnrollmentIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var entities = await _notificationRepository.GetByEnrollmentIdAsync(enrollmentId);
        return entities.ToDtoList();
    }

    public async Task<OrientationNotificationDto> CreateAsync(CreateOrientationNotificationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _notificationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var entity = await _notificationRepository.GetByIdAsync(notificationId)
            ?? throw new ArgumentException($"Orientation notification with ID '{notificationId}' not found.");

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
        var unread = await _notificationRepository.GetByRecipientAsync(recipientEmployeeId, unreadOnly: true);
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

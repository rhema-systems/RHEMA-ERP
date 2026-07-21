using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingWaitlistService : ITrainingWaitlistService
{
    private readonly ITrainingWaitlistRepository _waitlistRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingWaitlistService> _logger;

    public TrainingWaitlistService(
        ITrainingWaitlistRepository waitlistRepository,
        IUnitOfWork unitOfWork,
        ILogger<TrainingWaitlistService> logger)
    {
        _waitlistRepository = waitlistRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<TrainingWaitlistDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _waitlistRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Waitlist entry with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingWaitlistDto>> GetByScheduleIdAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var entities = await _waitlistRepository.GetByScheduleIdAsync(scheduleId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingWaitlistDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _waitlistRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingWaitlistDto>> GetActiveWaitlistAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var entities = await _waitlistRepository.GetActiveWaitlistAsync(scheduleId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    // ── Waitlist workflow ─────────────────────────────────────────────────────

    public async Task<TrainingWaitlistDto> AddToWaitlistAsync(CreateTrainingWaitlistDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        // Use the max existing position (not a count of active rows): once entries are offered/accepted/
        // removed the active count drops and a count-based position would collide with existing rows.
        var maxPosition = await _waitlistRepository.GetQueryable()
            .Where(w => w.ScheduleId == dto.ScheduleId)
            .Select(w => (int?)w.Position)
            .MaxAsync(cancellationToken) ?? 0;

        var entity = dto.ToEntity(tenantId, createdByUserId);
        entity.Position = maxPosition + 1;
        entity.AddedDate = DateTime.UtcNow;
        entity.Status = TrainingWaitlistStatus.Active;

        await _waitlistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee {EmployeeId} added to waitlist for schedule {ScheduleId} at position {Position}", dto.EmployeeId, dto.ScheduleId, entity.Position);

        return entity.ToDto();
    }

    public async Task<TrainingWaitlistDto> OfferSlotAsync(OfferWaitlistPositionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _waitlistRepository.GetByIdAsync(dto.WaitlistId);

        if (entity == null)
            throw new ArgumentException($"Waitlist entry with ID '{dto.WaitlistId}' not found.");

        if (entity.Status != TrainingWaitlistStatus.Active)
            throw new InvalidOperationException($"Cannot offer position to waitlist entry with status '{entity.Status}'.");

        entity.Status = TrainingWaitlistStatus.Offered;
        entity.OfferDate = dto.OfferDate;
        entity.OfferExpiryDate = dto.OfferExpiryDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _waitlistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Waitlist position offered to entry {WaitlistId}", dto.WaitlistId);

        return entity.ToDto();
    }

    public async Task<TrainingWaitlistDto> RecordOfferResponseAsync(RespondToWaitlistOfferDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _waitlistRepository.GetByIdAsync(dto.WaitlistId);

        if (entity == null)
            throw new ArgumentException($"Waitlist entry with ID '{dto.WaitlistId}' not found.");

        if (entity.Status != TrainingWaitlistStatus.Offered)
            throw new InvalidOperationException("Cannot respond to an offer that has not been made.");

        entity.Status = dto.OfferAccepted ? TrainingWaitlistStatus.Accepted : TrainingWaitlistStatus.Declined;
        entity.OfferAccepted = dto.OfferAccepted;
        entity.ResponseDate = dto.ResponseDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _waitlistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Waitlist offer {WaitlistId} {Response}", dto.WaitlistId, dto.OfferAccepted ? "accepted" : "declined");

        return entity.ToDto();
    }

    public async Task<bool> RemoveFromWaitlistAsync(Guid waitlistId, CancellationToken cancellationToken = default)
    {
        var entity = await _waitlistRepository.GetByIdAsync(waitlistId);

        if (entity == null)
            throw new ArgumentException($"Waitlist entry with ID '{waitlistId}' not found.");

        entity.Status = TrainingWaitlistStatus.Removed;
        entity.UpdatedAt = DateTime.UtcNow;

        await _waitlistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Waitlist entry {WaitlistId} removed", waitlistId);

        return true;
    }
}

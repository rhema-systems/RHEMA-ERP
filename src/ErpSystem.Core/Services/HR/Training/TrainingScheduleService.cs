using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingScheduleService : ITrainingScheduleService
{
    private readonly ITrainingScheduleRepository _scheduleRepository;
    private readonly ITrainingSessionRepository _sessionRepository;
    private readonly ITrainerAvailabilityRepository _availabilityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingScheduleService> _logger;

    private readonly INumberSequenceService _numberSequence;

    public TrainingScheduleService(
        ITrainingScheduleRepository scheduleRepository,
        ITrainingSessionRepository sessionRepository,
        ITrainerAvailabilityRepository availabilityRepository,
        IUnitOfWork unitOfWork,
        INumberSequenceService numberSequence,
        ILogger<TrainingScheduleService> logger)
    {
        _scheduleRepository = scheduleRepository;
        _sessionRepository = sessionRepository;
        _availabilityRepository = availabilityRepository;
        _unitOfWork = unitOfWork;
        _numberSequence = numberSequence;
        _logger = logger;
    }

    // ── Trainer availability / conflict detection ─────────────────────────────

    public async Task<TrainerAvailabilityCheckDto> CheckTrainerAvailabilityAsync(
        Guid trainerProfileId, DateTime from, DateTime to, Guid? excludeScheduleId, CancellationToken cancellationToken = default)
    {
        var result = new TrainerAvailabilityCheckDto { TrainerProfileId = trainerProfileId };

        // Overlapping schedules already assigned to this trainer (excluding cancelled ones and self).
        var schedules = await _scheduleRepository.GetByTrainerProfileIdAsync(trainerProfileId);
        result.ConflictingSchedules = schedules
            .Where(s => s.Id != excludeScheduleId
                && s.Status != ScheduleStatus.Cancelled
                && s.StartDate <= to && s.EndDate >= from)
            .OrderBy(s => s.StartDate)
            .Select(s => new TrainerScheduleConflictDto
            {
                ScheduleId = s.Id,
                ScheduleNumber = s.ScheduleNumber,
                ProgramName = s.Program?.ProgramName ?? string.Empty,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                Priority = s.Priority,
                Status = s.Status
            })
            .ToList();

        // Blocked availability windows overlapping the requested range.
        var blocked = await _availabilityRepository.GetBlockedPeriodsAsync(trainerProfileId);
        result.BlockedPeriods = blocked
            .Where(b => b.FromDate <= to && b.ToDate >= from)
            .OrderBy(b => b.FromDate)
            .Select(b => new TrainerBlockedPeriodDto
            {
                FromDate = b.FromDate,
                ToDate = b.ToDate,
                EngagementType = b.EngagementType,
                Notes = b.Notes
            })
            .ToList();

        result.HasConflicts = result.ConflictingSchedules.Any() || result.BlockedPeriods.Any();
        return result;
    }

    // ── Schedule queries ──────────────────────────────────────────────────────

    public async Task<TrainingScheduleDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _scheduleRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training schedule with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingScheduleDto?> GetByScheduleNumberAsync(string scheduleNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _scheduleRepository.GetByScheduleNumberAsync(scheduleNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _scheduleRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<TrainingScheduleSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _scheduleRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        // Eager-load navigations the summary DTO reads (program/trainer/vendor names + confirmed count),
        // otherwise the list shows blanks or degrades into N+1 queries.
        var items = await query
            .Include(s => s.Program)
            .Include(s => s.TrainerProfile)
            .Include(s => s.Vendor)
            .Include(s => s.Nominations)
            .OrderByDescending(s => s.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TrainingScheduleSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetByProgramIdAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var entities = await _scheduleRepository.GetByProgramIdAsync(programId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetByTrainerProfileIdAsync(Guid trainerProfileId, CancellationToken cancellationToken = default)
    {
        var entities = await _scheduleRepository.GetByTrainerProfileIdAsync(trainerProfileId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetByStatusAsync(ScheduleStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _scheduleRepository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetUpcomingAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var entities = await _scheduleRepository.GetUpcomingSchedulesAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetOpenForRegistrationAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _scheduleRepository.GetWithRegistrationOpenAsync();
        return entities.ToSummaryDtoList();
    }

    // ── Schedule CRUD ─────────────────────────────────────────────────────────

    public async Task<TrainingScheduleDto> CreateAsync(CreateTrainingScheduleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        ValidateScheduleDates(entity);
        entity.ScheduleNumber = await GenerateScheduleNumberAsync(cancellationToken);
        entity.Status = ScheduleStatus.Planned;

        await _scheduleRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training schedule created: {ScheduleNumber}", entity.ScheduleNumber);

        return entity.ToDto();
    }

    public async Task<TrainingScheduleDto> UpdateAsync(UpdateTrainingScheduleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _scheduleRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Training schedule with ID '{updateDto.Id}' not found.");

        if (entity.Status == ScheduleStatus.Completed || entity.Status == ScheduleStatus.Cancelled)
            throw new InvalidOperationException($"A {entity.Status} training schedule cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        ValidateScheduleDates(entity);

        await _scheduleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training schedule updated: {ScheduleNumber}", entity.ScheduleNumber);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _scheduleRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training schedule with ID '{id}' not found.");

        if (entity.Status != ScheduleStatus.Planned)
            throw new InvalidOperationException("Only planned (not yet approved) schedules can be deleted.");

        await _scheduleRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training schedule deleted: {ScheduleNumber}", entity.ScheduleNumber);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> ApproveAsync(ApproveTrainingScheduleDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _scheduleRepository.GetByIdAsync(dto.ScheduleId);

        if (entity == null)
            throw new ArgumentException($"Training schedule with ID '{dto.ScheduleId}' not found.");

        if (entity.Status != ScheduleStatus.Planned)
            throw new InvalidOperationException("Only planned schedules can be approved.");

        entity.Status = ScheduleStatus.RegistrationOpen;
        entity.ApprovedById = dto.ApprovedById;
        entity.ApprovalDate = dto.ApprovalDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = dto.ApprovedById.ToString();

        await _scheduleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training schedule approved: {ScheduleNumber}", entity.ScheduleNumber);

        return true;
    }

    public async Task<bool> CancelAsync(CancelTrainingScheduleDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _scheduleRepository.GetByIdAsync(dto.ScheduleId);

        if (entity == null)
            throw new ArgumentException($"Training schedule with ID '{dto.ScheduleId}' not found.");

        if (entity.Status == ScheduleStatus.Completed || entity.Status == ScheduleStatus.Cancelled)
            throw new InvalidOperationException($"A {entity.Status} schedule cannot be cancelled.");

        entity.Status = ScheduleStatus.Cancelled;
        entity.CancellationReason = dto.CancellationReason;
        entity.CancelledDate = DateTime.UtcNow;
        entity.CancelledById = dto.CancelledById;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = dto.CancelledById.ToString();

        await _scheduleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training schedule cancelled: {ScheduleNumber} — Reason: {Reason}", entity.ScheduleNumber, dto.CancellationReason);

        return true;
    }

    public async Task<bool> CompleteAsync(CompleteTrainingScheduleDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _scheduleRepository.GetByIdAsync(dto.ScheduleId);

        if (entity == null)
            throw new ArgumentException($"Training schedule with ID '{dto.ScheduleId}' not found.");

        if (entity.Status == ScheduleStatus.Cancelled || entity.Status == ScheduleStatus.Completed)
            throw new InvalidOperationException($"A {entity.Status} schedule cannot be marked as completed.");

        entity.Status = ScheduleStatus.Completed;
        entity.CompletionDate = dto.CompletionDate;
        entity.CompletionNotes = dto.CompletionNotes;
        entity.UpdatedAt = DateTime.UtcNow;

        await _scheduleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training schedule completed: {ScheduleNumber}", entity.ScheduleNumber);

        return true;
    }

    // ── Session sub-operations ────────────────────────────────────────────────

    public async Task<TrainingSessionDto> AddSessionAsync(CreateTrainingSessionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(createDto.ScheduleId);

        if (schedule == null)
            throw new ArgumentException($"Training schedule with ID '{createDto.ScheduleId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session added to schedule {ScheduleId}: {Topic}", createDto.ScheduleId, createDto.Topic);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingSessionDto>> GetSessionsAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var entities = await _sessionRepository.GetByScheduleIdAsync(scheduleId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainingSessionDto> UpdateSessionAsync(UpdateTrainingSessionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Training session with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(sessionId);

        if (entity == null)
            throw new ArgumentException($"Training session with ID '{sessionId}' not found.");

        await _sessionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task<string> GenerateScheduleNumberAsync(CancellationToken ct)
        => _numberSequence.GenerateAsync("SCHED", ct);

    /// <summary>Guards against nonsensical schedule/registration date windows.</summary>
    private static void ValidateScheduleDates(TrainingSchedule entity)
    {
        if (entity.EndDate < entity.StartDate)
            throw new InvalidOperationException("Schedule end date cannot be before the start date.");

        if (entity.RegistrationCloseDate < entity.RegistrationOpenDate)
            throw new InvalidOperationException("Registration close date cannot be before the registration open date.");

        if (entity.RegistrationCloseDate > entity.StartDate)
            throw new InvalidOperationException("Registration must close on or before the schedule start date.");

        if (entity.StartTime.HasValue && entity.EndTime.HasValue && entity.EndTime < entity.StartTime)
            throw new InvalidOperationException("Schedule end time cannot be before the start time.");
    }
}

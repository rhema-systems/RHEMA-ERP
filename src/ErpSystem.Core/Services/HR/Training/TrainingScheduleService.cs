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
    private readonly IGenericRepository<TrainingProgram> _programRepository;
    private readonly IGenericRepository<TrainerProfile> _trainerProfileRepository;
    private readonly IGenericRepository<TrainingVendor> _vendorRepository;
    private readonly IGenericRepository<TrainingBudget> _budgetRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingScheduleService> _logger;

    private readonly INumberSequenceService _numberSequence;

    public TrainingScheduleService(
        ITrainingScheduleRepository scheduleRepository,
        ITrainingSessionRepository sessionRepository,
        ITrainerAvailabilityRepository availabilityRepository,
        IGenericRepository<TrainingProgram> programRepository,
        IGenericRepository<TrainerProfile> trainerProfileRepository,
        IGenericRepository<TrainingVendor> vendorRepository,
        IGenericRepository<TrainingBudget> budgetRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        INumberSequenceService numberSequence,
        ILogger<TrainingScheduleService> logger)
    {
        _scheduleRepository = scheduleRepository;
        _sessionRepository = sessionRepository;
        _availabilityRepository = availabilityRepository;
        _programRepository = programRepository;
        _trainerProfileRepository = trainerProfileRepository;
        _vendorRepository = vendorRepository;
        _budgetRepository = budgetRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _numberSequence = numberSequence;
        _logger = logger;
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

    // A row owned by another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<TrainingSchedule> GetOwnedScheduleAsync(Guid id)
    {
        var entity = await _scheduleRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training schedule with ID '{id}' not found.");
        return entity;
    }

    private async Task<TrainingSession> GetOwnedSessionAsync(Guid id)
    {
        var entity = await _sessionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training session with ID '{id}' not found.");
        return entity;
    }

    // ── Trainer availability / conflict detection ─────────────────────────────

    public async Task<TrainerAvailabilityCheckDto> CheckTrainerAvailabilityAsync(
        Guid trainerProfileId, DateTime from, DateTime to, Guid? excludeScheduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var result = new TrainerAvailabilityCheckDto { TrainerProfileId = trainerProfileId };

        // Overlapping schedules already assigned to this trainer (excluding cancelled ones and self).
        // Another tenant's rows must not count as conflicts, or the check reports phantom clashes.
        var schedules = await _scheduleRepository.GetByTrainerProfileIdAsync(trainerProfileId);
        result.ConflictingSchedules = schedules
            .Where(s => s.TenantId == tenantId
                && s.Id != excludeScheduleId
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
            .Where(b => b.TenantId == tenantId && b.FromDate <= to && b.ToDate >= from)
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
        var tenantId = GetTenantId();
        var entity = await _scheduleRepository.GetWithFullDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Training schedule with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingScheduleDto?> GetByScheduleNumberAsync(string scheduleNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _scheduleRepository.GetByScheduleNumberAsync(scheduleNumber);

        // Schedule numbers are unique per tenant, so a match owned by another tenant is reported as none.
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _scheduleRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<PagedResult<TrainingScheduleSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = _scheduleRepository.GetQueryable().Where(s => s.TenantId == tenantId);
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
        var tenantId = GetTenantId();
        var entities = await _scheduleRepository.GetByProgramIdAsync(programId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetByTrainerProfileIdAsync(Guid trainerProfileId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _scheduleRepository.GetByTrainerProfileIdAsync(trainerProfileId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetByStatusAsync(ScheduleStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _scheduleRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetUpcomingAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _scheduleRepository.GetUpcomingSchedulesAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingScheduleSummaryDto>> GetOpenForRegistrationAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _scheduleRepository.GetWithRegistrationOpenAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Schedule CRUD ─────────────────────────────────────────────────────────

    public async Task<TrainingScheduleDto> CreateAsync(CreateTrainingScheduleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await ValidateScheduleForeignKeysAsync(
            createDto.ProgramId,
            createDto.TrainerProfileId,
            createDto.VendorId,
            createDto.TrainingBudgetId,
            cancellationToken);

        var entity = createDto.ToEntity(current, createdByUserId);
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
        var entity = await GetOwnedScheduleAsync(updateDto.Id);

        if (entity.Status == ScheduleStatus.Completed || entity.Status == ScheduleStatus.Cancelled)
            throw new InvalidOperationException($"A {entity.Status} training schedule cannot be edited.");

        await ValidateScheduleForeignKeysAsync(
            null,
            updateDto.TrainerProfileId,
            updateDto.VendorId,
            updateDto.TrainingBudgetId,
            cancellationToken);

        entity.UpdateEntity(updateDto, updatedByUserId);
        ValidateScheduleDates(entity);

        await _scheduleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training schedule updated: {ScheduleNumber}", entity.ScheduleNumber);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedScheduleAsync(id);

        if (entity.Status != ScheduleStatus.Planned)
            throw new InvalidOperationException("Only planned (not yet approved) schedules can be deleted.");

        await _scheduleRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training schedule deleted: {ScheduleNumber}", entity.ScheduleNumber);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> ApproveAsync(ApproveTrainingScheduleDto dto, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedScheduleAsync(dto.ScheduleId);

        if (entity.Status != ScheduleStatus.Planned)
            throw new InvalidOperationException("Only planned schedules can be approved.");

        entity.Status = ScheduleStatus.RegistrationOpen;
        entity.ApprovedById = approvedById;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = approvedById.ToString();

        await _scheduleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training schedule approved: {ScheduleNumber}", entity.ScheduleNumber);

        return true;
    }

    public async Task<bool> CancelAsync(CancelTrainingScheduleDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedScheduleAsync(dto.ScheduleId);

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
        var entity = await GetOwnedScheduleAsync(dto.ScheduleId);

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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedScheduleAsync(createDto.ScheduleId);

        var entity = createDto.ToEntity(current, createdByUserId);

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session added to schedule {ScheduleId}: {Topic}", createDto.ScheduleId, createDto.Topic);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingSessionDto>> GetSessionsAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _sessionRepository.GetByScheduleIdAsync(scheduleId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<TrainingSessionDto> UpdateSessionAsync(UpdateTrainingSessionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(sessionId);

        await _sessionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task ValidateScheduleForeignKeysAsync(
        Guid? programId,
        Guid? trainerProfileId,
        Guid? vendorId,
        Guid? trainingBudgetId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        if (programId.HasValue)
        {
            var program = await _programRepository.GetByIdAsync(programId.Value);
            if (program == null || program.TenantId != tenantId)
                throw new ArgumentException($"Training program with ID '{programId.Value}' not found.");
        }

        if (trainerProfileId.HasValue)
        {
            var trainer = await _trainerProfileRepository.GetByIdAsync(trainerProfileId.Value);
            if (trainer == null || trainer.TenantId != tenantId)
                throw new ArgumentException($"Trainer profile with ID '{trainerProfileId.Value}' not found.");
        }

        if (vendorId.HasValue)
        {
            var vendor = await _vendorRepository.GetByIdAsync(vendorId.Value);
            if (vendor == null || vendor.TenantId != tenantId)
                throw new ArgumentException($"Training vendor with ID '{vendorId.Value}' not found.");
        }

        if (trainingBudgetId.HasValue)
        {
            var budget = await _budgetRepository.GetByIdAsync(trainingBudgetId.Value);
            if (budget == null || budget.TenantId != tenantId)
                throw new ArgumentException($"Training budget with ID '{trainingBudgetId.Value}' not found.");
        }
    }

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

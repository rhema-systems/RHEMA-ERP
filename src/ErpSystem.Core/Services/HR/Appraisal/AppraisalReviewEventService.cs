using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class AppraisalReviewEventService : IAppraisalReviewEventService
{
    private readonly IGenericRepository<AppraisalReviewEvent> _reviewEventRepository;
    private readonly IGenericRepository<GoalProgressEntry> _progressEntryRepository;
    private readonly IGenericRepository<AppraisalAttachment> _attachmentRepository;
    private readonly IGenericRepository<EmployeeGoal> _goalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<AppraisalReviewEventService> _logger;

    public AppraisalReviewEventService(
        IGenericRepository<AppraisalReviewEvent> reviewEventRepository,
        IGenericRepository<GoalProgressEntry> progressEntryRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        IGenericRepository<EmployeeGoal> goalRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<AppraisalReviewEventService> logger)
    {
        _reviewEventRepository = reviewEventRepository;
        _progressEntryRepository = progressEntryRepository;
        _attachmentRepository = attachmentRepository;
        _goalRepository = goalRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
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

    // A review event owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<AppraisalReviewEvent> GetOwnedReviewEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _reviewEventRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Review event with ID '{id}' not found.");
        return entity;
    }

    // ─── Settings-driven review gates (Phase 2B) ──────────────────────────────

    /// <summary>Loads the AppraisalSettings governing a review event (via its cycle), or null.</summary>
    private async Task<AppraisalSettings?> GetSettingsForEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        return await _reviewEventRepository.GetQueryable(e => e.Id == eventId && e.TenantId == tenantId)
            .Include(e => e.Cycle).ThenInclude(c => c.AppraisalSettings)
            .Select(e => e.Cycle.AppraisalSettings)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Enforces RequireGoalProgressUpdateAtReview: every active (non-rejected) goal in the appraisal
    /// must have a progress entry recorded against this review event. Throws when any are missing.
    /// </summary>
    private async Task EnsureGoalProgressRecordedAsync(AppraisalReviewEvent ev, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var employeeId = await _reviewEventRepository.GetQueryable(e => e.Id == ev.Id && e.TenantId == tenantId)
            .Select(e => e.Appraisal.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken);

        var activeGoals = await _goalRepository
            .GetQueryable(g => g.TenantId == tenantId
                            && g.EmployeeId == employeeId
                            && g.AppraisalCycleId == ev.AppraisalCycleId
                            && g.Status != GoalStatus.Rejected)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        if (!activeGoals.Any())
            return; // nothing to update against

        var goalsWithProgress = (await _progressEntryRepository
                .GetQueryable(p => p.ReviewEventId == ev.Id && p.TenantId == tenantId)
                .Select(p => p.EmployeeGoalId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var missing = activeGoals.Count(id => !goalsWithProgress.Contains(id));
        if (missing > 0)
            throw new InvalidOperationException(
                $"A goal progress update is required for every goal at this review. {missing} of {activeGoals.Count} goal(s) still need an update.");
    }

    // ─── Full interim appraisal (Theme 7) ─────────────────────────────────────

    public async Task<FullInterimAppraisalContextDto> GetFullAppraisalContextAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var ev = await BaseQuery.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
            ?? throw new ArgumentException($"Review event with ID '{eventId}' not found.");

        var employeeId = ev.Appraisal.EmployeeId;

        var goals = await _goalRepository
            .GetQueryable(g => g.TenantId == GetTenantId() && g.EmployeeId == employeeId && g.AppraisalCycleId == ev.AppraisalCycleId)
            .ToListAsync(cancellationToken);

        var tenantId = GetTenantId();
        var existing = await _progressEntryRepository
            .GetQueryable(p => p.ReviewEventId == eventId && p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var goalDtos = goals
            .OrderBy(g => g.Title)
            .Select(g => new InterimGoalScoreDto
            {
                EmployeeGoalId = g.Id,
                Title = g.Title,
                Weight = g.Weight,
                Period = g.Period.ToString(),
                CurrentProgress = g.ProgressPercent,
                ExistingScore = existing
                    .Where(e => e.EmployeeGoalId == g.Id)
                    .OrderByDescending(e => e.EntryDate)
                    .FirstOrDefault()?.ProgressPercent
            })
            .ToList();

        return new FullInterimAppraisalContextDto
        {
            Event = ev.ToDto(),
            EmployeeName = ev.Appraisal.Employee?.FullName,
            Goals = goalDtos
        };
    }

    public async Task<AppraisalReviewEventDto> FinalizeFullAppraisalAsync(
        Guid eventId, FinalizeFullInterimAppraisalDto dto, Guid recordedById, CancellationToken cancellationToken = default)
    {
        // GoalProgressEntry.RecordedById is a required Employee FK; reject a missing/empty
        // employee_id claim with a clear error instead of letting the insert crash on the FK.
        if (recordedById == Guid.Empty)
            throw new InvalidOperationException("Unable to determine the recording employee. Please ensure your account is linked to an employee record.");

        var ev = await GetOwnedReviewEventAsync(eventId, cancellationToken);

        if (!ev.IsFullAppraisal)
            throw new InvalidOperationException("This review event is not configured as a full appraisal.");

        var tenantId = GetTenantId();
        // Record a score per goal as a GoalProgressEntry tied to this review event.
        foreach (var s in dto.Scores)
        {
            await _progressEntryRepository.AddAsync(new GoalProgressEntry
            {
                TenantId = tenantId,
                EmployeeGoalId = s.EmployeeGoalId,
                ProgressPercent = s.Score,
                Status = s.Score >= 100 ? GoalProgressStatus.Completed : GoalProgressStatus.OnTrack,
                Notes = s.Note,
                RecordedById = recordedById,
                EntryDate = DateTime.UtcNow,
                ReviewEventId = eventId
            });
        }

        // Weighted period score (fall back to a simple average when no weights are set).
        decimal periodScore = 0;
        if (dto.Scores.Any())
        {
            var goalIds = dto.Scores.Select(s => s.EmployeeGoalId).ToList();
            var weightById = (await _goalRepository.GetQueryable(g => g.TenantId == tenantId && goalIds.Contains(g.Id)).ToListAsync(cancellationToken))
                .ToDictionary(g => g.Id, g => g.Weight);

            var totalWeight = dto.Scores.Sum(s => weightById.GetValueOrDefault(s.EmployeeGoalId, 0));
            periodScore = totalWeight > 0
                ? dto.Scores.Sum(s => s.Score * weightById.GetValueOrDefault(s.EmployeeGoalId, 0)) / totalWeight
                : dto.Scores.Average(s => s.Score);
        }

        ev.OverallPeriodScore = Math.Round(periodScore, 2);
        ev.IsLightTouch = false;
        ev.Status = AppraisalReviewStatus.Completed;
        ev.ManagerNotes = dto.ManagerNotes ?? ev.ManagerNotes;

        await _reviewEventRepository.UpdateAsync(ev);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Full interim appraisal finalized for review event {Id}: period score {Score}", eventId, ev.OverallPeriodScore);
        return await GetByIdAsync(eventId, cancellationToken);
    }

    private IQueryable<AppraisalReviewEvent> BaseQuery
    {
        get
        {
            var tenantId = GetTenantId();
            return _reviewEventRepository.GetQueryable()
                .Where(e => e.TenantId == tenantId)
                .Include(e => e.Appraisal)
                    .ThenInclude(a => a.Employee)
                .Include(e => e.Cycle);
        }
    }

    public async Task<AppraisalReviewEventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Review event with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalReviewEventDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(e => e.PerformanceAppraisalId == appraisalId)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalReviewEventDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(e => e.AppraisalCycleId == cycleId)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalReviewEventDto>> GetByTypeAsync(Guid appraisalId, ReviewEventType type, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(e => e.PerformanceAppraisalId == appraisalId && e.Type == type)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<AppraisalReviewEventDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.OrderByDescending(e => e.EventDate);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AppraisalReviewEventDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AppraisalReviewEventDto> CreateAsync(CreateAppraisalReviewEventDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        entity.Status = AppraisalReviewStatus.Pending;

        await _reviewEventRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review event created: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<AppraisalReviewEventDto> UpdateAsync(UpdateAppraisalReviewEventDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewEventAsync(updateDto.Id, cancellationToken);

        if (entity.Status == AppraisalReviewStatus.Completed)
            throw new InvalidOperationException("Cannot update a completed review event.");

        updateDto.UpdateEntity(entity);
        await _reviewEventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review event updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewEventAsync(id, cancellationToken);

        await _reviewEventRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Review event deleted: {Id}", id);
        return true;
    }

    public async Task<AppraisalReviewEventDto> SubmitEventAsync(
        Guid eventId, string? achievementsSummary, string? challengesSummary,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewEventAsync(eventId, cancellationToken);

        if (entity.Status == AppraisalReviewStatus.Completed)
            throw new InvalidOperationException("Review event is already completed.");
        if (entity.Status == AppraisalReviewStatus.EmployeeSubmitted)
            throw new InvalidOperationException("Employee has already submitted this review event.");

        // Gate: require goal progress updates at the review when configured.
        var submitSettings = await GetSettingsForEventAsync(eventId, cancellationToken);
        if (submitSettings?.RequireGoalProgressUpdateAtReview == true)
            await EnsureGoalProgressRecordedAsync(entity, cancellationToken);

        entity.Status = AppraisalReviewStatus.EmployeeSubmitted;
        entity.AchievementsSummary = achievementsSummary ?? entity.AchievementsSummary;
        entity.ChallengesSummary   = challengesSummary   ?? entity.ChallengesSummary;

        await _reviewEventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review event {Id} submitted by employee", eventId);
        return await GetByIdAsync(eventId, cancellationToken);
    }

    public async Task<AppraisalReviewEventDto> CompleteEventAsync(
        Guid eventId, string? notes, string? managerNotes,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewEventAsync(eventId, cancellationToken);

        if (entity.Status == AppraisalReviewStatus.Completed)
            throw new InvalidOperationException("Review event is already completed.");

        // Gates: employee self-assessment + goal progress updates when configured.
        var settings = await GetSettingsForEventAsync(eventId, cancellationToken);
        if (settings?.RequireMidYearSelfAssessment == true && entity.Status != AppraisalReviewStatus.EmployeeSubmitted)
            throw new InvalidOperationException("The employee must submit their self-assessment before this review can be completed.");
        if (settings?.RequireGoalProgressUpdateAtReview == true)
            await EnsureGoalProgressRecordedAsync(entity, cancellationToken);

        entity.Status      = AppraisalReviewStatus.Completed;
        entity.Notes       = notes       ?? entity.Notes;
        entity.ManagerNotes = managerNotes ?? entity.ManagerNotes;

        await _reviewEventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review event {Id} completed by manager", eventId);
        return await GetByIdAsync(eventId, cancellationToken);
    }

    // ─── Goal Progress Entries ────────────────────────────────────────────────

    public async Task<GoalProgressEntryDto> RecordProgressEntryAsync(
        Guid eventId, CreateGoalProgressEntryDto dto, CancellationToken cancellationToken = default)
    {
        await GetOwnedReviewEventAsync(eventId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        entity.ReviewEventId = eventId;
        entity.EntryDate = DateTime.UtcNow;

        await _progressEntryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Progress entry recorded for review event {EventId}: {EntryId}", eventId, entity.Id);

        entity = await _progressEntryRepository.GetQueryable()
            .Include(p => p.EmployeeGoal)
            .FirstOrDefaultAsync(p => p.Id == entity.Id, cancellationToken);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<GoalProgressEntryDto>> GetProgressEntriesAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await GetOwnedReviewEventAsync(eventId, cancellationToken);

        var tenantId = GetTenantId();
        var entities = await _progressEntryRepository.GetQueryable(p => p.ReviewEventId == eventId && p.TenantId == tenantId)
            .Include(p => p.EmployeeGoal)
            .OrderByDescending(p => p.EntryDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    // ─── Attachments ─────────────────────────────────────────────────────────

    public async Task<AppraisalAttachmentDto> AddAttachmentAsync(
        Guid eventId, CreateAppraisalAttachmentDto dto, CancellationToken cancellationToken = default)
    {
        await GetOwnedReviewEventAsync(eventId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        entity.ReviewEventId = eventId;
        entity.UploadDate = DateTime.UtcNow;

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment added to review event {EventId}: {AttachmentId}", eventId, entity.Id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _attachmentRepository.GetQueryable(a => a.ReviewEventId == eventId && a.TenantId == tenantId)
            .Include(a => a.UploadedBy)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid eventId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _attachmentRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ReviewEventId == eventId && a.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Attachment not found.");

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

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

public class CalibrationSessionService : ICalibrationSessionService
{
    private readonly IGenericRepository<CalibrationSession> _sessionRepository;
    private readonly IGenericRepository<CalibrationParticipant> _participantRepository;
    private readonly IGenericRepository<CalibrationRatingAdjustment> _adjustmentRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<AppraisalAttachment> _attachmentRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<OrganizationUnit> _orgUnitRepository;
    private readonly IGenericRepository<CriterionScore> _criterionScoreRepository;
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeDefinitionRepository;
    private readonly IGenericRepository<PerformanceAppraisalCriterionConfig> _criterionConfigRepository;
    private readonly IPerformanceAppraisalService _appraisalService;
    private readonly IAppraisalNotificationService _notifications;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalibrationSessionService> _logger;

    public CalibrationSessionService(
        IGenericRepository<CalibrationSession> sessionRepository,
        IGenericRepository<CalibrationParticipant> participantRepository,
        IGenericRepository<CalibrationRatingAdjustment> adjustmentRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<OrganizationUnit> orgUnitRepository,
        IGenericRepository<CriterionScore> criterionScoreRepository,
        IGenericRepository<AppraisalGradeDefinition> gradeDefinitionRepository,
        IGenericRepository<PerformanceAppraisalCriterionConfig> criterionConfigRepository,
        IPerformanceAppraisalService appraisalService,
        IAppraisalNotificationService notifications,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CalibrationSessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _participantRepository = participantRepository;
        _adjustmentRepository = adjustmentRepository;
        _appraisalRepository = appraisalRepository;
        _attachmentRepository = attachmentRepository;
        _employeeRepository = employeeRepository;
        _orgUnitRepository = orgUnitRepository;
        _criterionScoreRepository = criterionScoreRepository;
        _gradeDefinitionRepository = gradeDefinitionRepository;
        _criterionConfigRepository = criterionConfigRepository;
        _appraisalService = appraisalService;
        _notifications = notifications;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    // A calibration session owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<CalibrationSession> GetOwnedSessionAsync(Guid id)
    {
        var entity = await _sessionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Calibration session with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<CalibrationSession> BaseQuery
    {
        get
        {
            var tenantId = GetTenantId();
            // OrganizationLevel and CompletedBy are mapped onto the DTO, so they have to be
            // included or the level name and "completed by" read as blank on every row.
            return _sessionRepository.GetQueryable()
                .Where(s => s.TenantId == tenantId)
                .Include(s => s.AppraisalCycle)
                .Include(s => s.OrganizationUnit)
                .Include(s => s.OrganizationLevel)
                .Include(s => s.FacilitatedBy)
                .Include(s => s.CompletedBy);
        }
    }

    // ─── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<CalibrationSessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Calibration session with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<CalibrationSessionDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(s => s.AppraisalCycleId == cycleId)
            .OrderByDescending(s => s.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<CalibrationSessionDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.OrderByDescending(s => s.ScheduledDate);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<CalibrationSessionDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<CalibrationSessionDto> CreateAsync(CreateCalibrationSessionDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        entity.Status = CalibrationStatus.Pending;

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calibration session created: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<CalibrationSessionDto> UpdateAsync(UpdateCalibrationSessionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(updateDto.Id);

        if (entity.Status == CalibrationStatus.Completed)
            throw new InvalidOperationException("Cannot update a completed calibration session.");

        updateDto.UpdateEntity(entity);
        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calibration session updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(id);

        if (entity.Status == CalibrationStatus.Completed)
            throw new InvalidOperationException("Cannot delete a completed calibration session.");

        await _sessionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Calibration session deleted: {Id}", id);
        return true;
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    public async Task<CalibrationSessionDto> OpenSessionAsync(Guid sessionId, Guid facilitatedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(sessionId);

        if (entity.Status != CalibrationStatus.Pending)
            throw new InvalidOperationException($"Session must be in Pending status to open. Current: {entity.Status}");

        entity.Status = CalibrationStatus.InProgress;
        if (facilitatedById != Guid.Empty)
            entity.FacilitatedById = facilitatedById;

        await _sessionRepository.UpdateAsync(entity);

        // Link the scoped appraisals to the session as it opens. Until this happens the
        // sub-status resolver has no way to tell "waiting for a calibration session" from
        // "sitting in one" — CalibrationInProgress was unreachable, because the only thing that
        // ever set CalibrationSessionId was the commit at the very end.
        var scoped = await GetScopedAppraisalsAsync(entity, cancellationToken);
        foreach (var appraisal in scoped.Where(a => a.CalibrationSessionId == null))
        {
            appraisal.CalibrationSessionId = sessionId;
            await _appraisalRepository.UpdateAsync(appraisal);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Calibration session {Id} opened by {FacilitatedById} over {Count} appraisal(s)",
            sessionId, facilitatedById, scoped.Count);
        return await GetByIdAsync(sessionId, cancellationToken);
    }

    public async Task<CalibrationSessionDto> StartSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(sessionId);

        if (entity.Status != CalibrationStatus.InProgress)
            throw new InvalidOperationException("Session must be opened before it can be started.");

        entity.StartedDate = DateTime.UtcNow;

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calibration session {Id} started", sessionId);
        return await GetByIdAsync(sessionId, cancellationToken);
    }

    public async Task<CalibrationSessionDto> CompleteSessionAsync(
        Guid sessionId, Guid completedById, string? meetingNotes, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(sessionId);

        if (entity.Status != CalibrationStatus.InProgress)
            throw new InvalidOperationException("Only in-progress sessions can be completed.");

        entity.Status = CalibrationStatus.Completed;
        entity.CompletedDate = DateTime.UtcNow;
        // CompletedById was accepted, logged and then thrown away, so "completed by" was blank on
        // every closed session even though the DTO exposes it.
        entity.CompletedById = completedById == Guid.Empty ? null : completedById;
        if (!string.IsNullOrWhiteSpace(meetingNotes))
            entity.MeetingNotes = meetingNotes;

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyPanelCompleteAsync(entity, cancellationToken);

        _logger.LogInformation("Calibration session {Id} completed by {CompletedById}", sessionId, completedById);
        return await GetByIdAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// Tells the room the session is closed and the ratings can be committed.
    ///
    /// <para>Deliberately goes to the <em>participants</em> — the managers and HR staff who sat in
    /// it — and not to the employees being calibrated. A calibrated score is not final until HR
    /// signs the appraisal off, and telling someone their rating moved before then would hand
    /// them a number that can still change. The appraisee hears about it at HR approval.</para>
    ///
    /// <para>Best-effort: the session is already closed by the time this runs, so a notification
    /// failure is logged rather than thrown.</para>
    /// </summary>
    private async Task NotifyPanelCompleteAsync(CalibrationSession session, CancellationToken cancellationToken)
    {
        try
        {
            var recipientIds = await _participantRepository.GetQueryable()
                .Where(p => p.CalibrationSessionId == session.Id)
                .Select(p => p.EmployeeId)
                .ToListAsync(cancellationToken);

            if (session.FacilitatedById.HasValue)
                recipientIds.Add(session.FacilitatedById.Value);

            var cycleName = await _sessionRepository.GetQueryable()
                .Where(s => s.Id == session.Id)
                .Select(s => s.AppraisalCycle.CycleName)
                .FirstOrDefaultAsync(cancellationToken);

            var requests = recipientIds.Distinct().Select(id => new AppraisalNotificationRequest(
                id,
                AppraisalNotificationType.CalibrationComplete,
                $"Calibration session closed: {session.SessionName}",
                "The panel has finished. Commit the agreed ratings to release the appraisals for HR review.",
                CycleName: cycleName,
                NavigationUrl: $"/hr/performance/calibration/{session.Id}"));

            await _notifications.RaiseAsync(requests, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify participants that calibration session {Id} closed; the session stands.", session.Id);
        }
    }

    // ─── Participants ─────────────────────────────────────────────────────────

    public async Task<CalibrationParticipantDto> AddParticipantAsync(Guid sessionId, CreateCalibrationParticipantDto dto, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);

        var duplicate = await _participantRepository.ExistsAsync(p =>
            p.CalibrationSessionId == sessionId && p.EmployeeId == dto.EmployeeId);
        if (duplicate)
            throw new InvalidOperationException("This employee is already a participant in the session.");

        var entity = dto.ToEntity();
        entity.CalibrationSessionId = sessionId;
        entity.TenantId = session.TenantId;

        await _participantRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Participant {EmployeeId} added to calibration session {SessionId}", dto.EmployeeId, sessionId);

        entity = await _participantRepository.GetQueryable()
            .Include(p => p.Employee)
            .FirstOrDefaultAsync(p => p.Id == entity.Id, cancellationToken);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<CalibrationParticipantDto>> GetParticipantsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var entities = await _participantRepository.GetQueryable(p => p.CalibrationSessionId == sessionId)
            .Include(p => p.Employee)
            .OrderBy(p => p.Employee.LastName)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<bool> RemoveParticipantAsync(Guid sessionId, Guid participantId, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var entity = await _participantRepository.GetQueryable()
            .FirstOrDefaultAsync(p => p.Id == participantId && p.CalibrationSessionId == sessionId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Participant not found in this session.");

        await _participantRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordAttendanceAsync(
        Guid sessionId, Guid participantId, bool attended, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var entity = await _participantRepository.GetQueryable()
            .Include(p => p.Employee)
            .FirstOrDefaultAsync(p => p.Id == participantId && p.CalibrationSessionId == sessionId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Participant not found in this session.");

        entity.Attended = attended;
        await _participantRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attendance recorded for participant {ParticipantId}: Attended={Attended}", participantId, attended);
        return true;
    }

    // ─── Rating Adjustments ───────────────────────────────────────────────────

    public async Task<CalibrationRatingAdjustmentDto> AddRatingAdjustmentAsync(
        Guid sessionId, CreateCalibrationRatingAdjustmentDto dto, Guid adjustedById, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);

        // Guard: adjustments can only be recorded on an in-progress session
        if (session.Status == CalibrationStatus.Completed)
            throw new InvalidOperationException(
                "Rating adjustments cannot be added to a completed calibration session. " +
                "Reopen the session to make further adjustments.");

        if (session.Status == CalibrationStatus.Cancelled)
            throw new InvalidOperationException(
                "Rating adjustments cannot be added to a cancelled calibration session.");

        if (session.Status == CalibrationStatus.Pending)
            throw new InvalidOperationException(
                "Open the calibration session before recording adjustments.");

        // The appraisal has to be one this session actually covers — otherwise a session for one
        // department could quietly restate a score in another.
        var scopedIds = await GetScopedAppraisalIdsAsync(sessionId, cancellationToken);
        if (!scopedIds.Contains(dto.PerformanceAppraisalId))
            throw new InvalidOperationException("That appraisal is not in this calibration session's scope.");

        if (adjustedById == Guid.Empty)
            throw new InvalidOperationException("Your user account is not linked to an employee record, so the adjustment cannot be attributed.");

        var entity = dto.ToEntity();
        entity.CalibrationSessionId = sessionId;
        entity.TenantId = session.TenantId;
        entity.AdjustedById = adjustedById;
        entity.AdjustmentDate = DateTime.UtcNow;

        await _adjustmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Rating adjustment added to session {SessionId}, appraisal {AppraisalId}", sessionId, dto.PerformanceAppraisalId);

        entity = await _adjustmentRepository.GetQueryable()
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(ap => ap.Employee)
            .Include(a => a.AdjustedBy)
            .FirstOrDefaultAsync(a => a.Id == entity.Id, cancellationToken);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<CalibrationRatingAdjustmentDto>> GetRatingAdjustmentsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var entities = await _adjustmentRepository.GetQueryable(a => a.CalibrationSessionId == sessionId)
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(ap => ap.Employee)
            .Include(a => a.AdjustedBy)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<CalibrationRatingAdjustmentDto>> GetAdjustmentsByAppraisalAsync(Guid sessionId, Guid appraisalId, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var entities = await _adjustmentRepository.GetQueryable(
                a => a.CalibrationSessionId == sessionId && a.PerformanceAppraisalId == appraisalId)
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(ap => ap.Employee)
            .Include(a => a.AdjustedBy)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    /// <summary>
    /// The appraisal's frozen criteria as a calibration panel needs them: weight, the manager's
    /// score, and whatever this panel has already moved each one to.
    /// </summary>
    /// <remarks>
    /// The criterion snapshot was otherwise reachable only inside a manager's or HR's evaluation
    /// context — which a panellist is not entitled to — so the calibration dialog could only move
    /// the overall score. The API has always accepted per-criterion adjustments; nothing could
    /// enumerate the criteria to offer them.
    /// </remarks>
    public async Task<IEnumerable<CalibrationCriterionDto>> GetAppraisalCriteriaAsync(
        Guid sessionId, Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        var tenantId = session.TenantId;

        var configs = await _criterionConfigRepository
            .GetQueryable(c => c.PerformanceAppraisalId == appraisalId && c.TenantId == tenantId)
            .Include(c => c.TemplateItem).ThenInclude(i => i.Competency)
            .Include(c => c.TemplateItem).ThenInclude(i => i.KpiDefinition)
            .ToListAsync(cancellationToken);

        // What the panel is moving away from. The manager's is the authoritative pre-calibration
        // figure — see the PreCalibrationScore note on CommitAsync.
        var managerScores = await _criterionScoreRepository
            .GetQueryable(s => s.TenantId == tenantId
                            && s.EvaluatorEvaluation.AppraisalId == appraisalId
                            && s.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager)
            .ToListAsync(cancellationToken);

        var adjustments = await _adjustmentRepository
            .GetQueryable(a => a.CalibrationSessionId == sessionId
                            && a.PerformanceAppraisalId == appraisalId
                            && a.TemplateItemId != null
                            && a.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return configs
            .Select(c =>
            {
                var score = managerScores.FirstOrDefault(s => s.TemplateItemId == c.TemplateItemId);
                var adjustment = adjustments.FirstOrDefault(a => a.TemplateItemId == c.TemplateItemId);

                return new CalibrationCriterionDto
                {
                    TemplateItemId = c.TemplateItemId,
                    TemplateItemName = c.TemplateItem?.Competency?.CriteriaName
                                       ?? c.TemplateItem?.KpiDefinition?.KpiName,
                    WeightUsed = c.WeightUsed,
                    ManagerScore = score?.NumericScore,
                    ManagerActualValue = score?.ActualValue,
                    AdjustmentId = adjustment?.Id,
                    AdjustedScore = adjustment?.AdjustedScore,
                    Rationale = adjustment?.Rationale,
                };
            })
            .OrderByDescending(c => c.WeightUsed)
            .ThenBy(c => c.TemplateItemName)
            .ToList();
    }

    public async Task<CalibrationRatingAdjustmentDto> UpdateRatingAdjustmentAsync(
        Guid sessionId, UpdateCalibrationRatingAdjustmentDto dto, Guid adjustedById, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        var entity = await _adjustmentRepository.GetQueryable()
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(ap => ap.Employee)
            .Include(a => a.AdjustedBy)
            .FirstOrDefaultAsync(a => a.Id == dto.Id && a.CalibrationSessionId == sessionId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Rating adjustment not found in this session.");

        // Guard: cannot edit adjustments on a closed session
        if (session.Status == CalibrationStatus.Completed)
            throw new InvalidOperationException("Cannot modify adjustments on a completed calibration session.");
        if (session.Status == CalibrationStatus.Cancelled)
            throw new InvalidOperationException("Cannot modify adjustments on a cancelled calibration session.");

        dto.UpdateEntity(entity);
        // A restated decision is still this caller's decision — the audit trail follows the edit.
        if (adjustedById != Guid.Empty)
            entity.AdjustedById = adjustedById;
        entity.AdjustmentDate = DateTime.UtcNow;

        await _adjustmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Rating adjustment updated: {Id}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteRatingAdjustmentAsync(Guid sessionId, Guid adjustmentId, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var entity = await _adjustmentRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == adjustmentId && a.CalibrationSessionId == sessionId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Rating adjustment not found in this session.");

        await _adjustmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ─── Session scope ────────────────────────────────────────────────────────

    public async Task<IReadOnlyCollection<Guid>> GetScopedAppraisalIdsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        var appraisals = await GetScopedAppraisalsAsync(session, cancellationToken);
        return appraisals.Select(a => a.Id).ToHashSet();
    }

    /// <summary>
    /// Appraisals the session covers: everything in its cycle, narrowed to its organization unit
    /// (and every unit beneath it) or, failing that, its organization level. A session with
    /// neither set covers the whole cycle.
    ///
    /// <para>Anything already adjusted or already linked to the session is unioned back in, so a
    /// row cannot vanish from the grid because someone was moved to another unit mid-cycle.</para>
    /// </summary>
    private async Task<List<PerformanceAppraisal>> GetScopedAppraisalsAsync(
        CalibrationSession session, CancellationToken cancellationToken)
    {
        var tenantId = session.TenantId;

        var query = _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.AppraisalCycleId == session.AppraisalCycleId);

        if (session.OrganizationUnitId.HasValue)
        {
            var unitIds = await GetUnitAndDescendantIdsAsync(session.OrganizationUnitId.Value, tenantId, cancellationToken);
            query = query.Where(a => a.Employee.OrganizationUnitId.HasValue
                                  && unitIds.Contains(a.Employee.OrganizationUnitId.Value));
        }
        else if (session.OrganizationLevelId.HasValue)
        {
            query = query.Where(a => a.Employee.OrganizationLevelId == session.OrganizationLevelId);
        }

        var scoped = await query
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .ToListAsync(cancellationToken);

        var scopedIds = scoped.Select(a => a.Id).ToHashSet();

        var strayIds = await _adjustmentRepository.GetQueryable()
            .Where(a => a.CalibrationSessionId == session.Id && !scopedIds.Contains(a.PerformanceAppraisalId))
            .Select(a => a.PerformanceAppraisalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var linkedIds = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.CalibrationSessionId == session.Id && !scopedIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var extraIds = strayIds.Concat(linkedIds).Distinct().ToList();
        if (extraIds.Count > 0)
        {
            var extras = await _appraisalRepository.GetQueryable()
                .Where(a => a.TenantId == tenantId && extraIds.Contains(a.Id))
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Position)
                .Include(a => a.Employee)
                    .ThenInclude(e => e.OrganizationUnit)
                .ToListAsync(cancellationToken);
            scoped.AddRange(extras);
        }

        return scoped;
    }

    /// <summary>Breadth-first walk of the unit tree, matching the cycle-coverage rule.</summary>
    private async Task<HashSet<Guid>> GetUnitAndDescendantIdsAsync(
        Guid rootUnitId, Guid tenantId, CancellationToken cancellationToken)
    {
        var result = new HashSet<Guid> { rootUnitId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootUnitId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = await _orgUnitRepository.GetQueryable()
                .Where(u => u.TenantId == tenantId && u.ParentUnitId == current && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            foreach (var child in children)
                if (result.Add(child))
                    queue.Enqueue(child);
        }

        return result;
    }

    // ─── Apply All Adjustments ────────────────────────────────────────────────

    /// <summary>
    /// Commits the session.
    ///
    /// <para>Item-level adjustments are written onto the manager's criterion scores and the
    /// overall score is then recomputed from them; an overall adjustment (no template item) is
    /// applied last and wins, because that is the panel restating the final number directly.
    /// Applying an item-level adjustment as though it were the overall score — which is what the
    /// ported code did — silently replaced a 78 with a single criterion's 4.</para>
    ///
    /// <para>Every appraisal in scope is marked calibrated, adjusted or not. When the cycle
    /// requires calibration, <c>IsCalibrated</c> is the gate that lets an appraisal reach HR
    /// review, so leaving the untouched ones false stranded everyone the panel agreed about.</para>
    /// </summary>
    public async Task<CalibrationApplyResultDto> ApplyAllAdjustmentsAsync(
        Guid sessionId, Guid appliedById, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);

        if (session.Status != CalibrationStatus.Completed)
            throw new InvalidOperationException("Adjustments can only be applied to a completed session.");

        var scoped = await GetScopedAppraisalsAsync(session, cancellationToken);
        if (scoped.Count == 0)
            throw new InvalidOperationException(
                "This session covers no appraisals. Check its cycle and organization scope before committing it.");

        var adjustments = await _adjustmentRepository
            .GetQueryable(a => a.CalibrationSessionId == sessionId)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync(cancellationToken);

        var byAppraisal = adjustments.GroupBy(a => a.PerformanceAppraisalId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Calibration normally runs *before* HR sign-off, and the appraisal's own OverallScore is
        // not computed until finalisation — so at this point it is usually still null. Preserving
        // it as the pre-calibration score therefore preserved nothing. PreCalibrationScore is
        // documented as "the manager's proposed score before calibration adjustments", so the
        // manager's evaluation total is what it should hold, and that is the number the panel was
        // actually looking at on the grid.
        var scopedIds = scoped.Select(a => a.Id).ToList();
        var managerTotals = (await _appraisalRepository.GetQueryable()
                .Where(a => scopedIds.Contains(a.Id))
                .SelectMany(a => a.EvaluatorEvaluations)
                .Where(e => e.EvaluatorRole == EvaluatorRole.Manager)
                .Select(e => new { e.AppraisalId, e.TotalScore })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.AppraisalId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.TotalScore));

        var result = new CalibrationApplyResultDto();

        foreach (var appraisal in scoped)
        {
            byAppraisal.TryGetValue(appraisal.Id, out var forThis);
            forThis ??= new List<CalibrationRatingAdjustment>();

            // Capture the manager's number once, the first time this appraisal is calibrated.
            if (!appraisal.IsCalibrated && appraisal.PreCalibrationScore == null)
            {
                appraisal.PreCalibrationScore = appraisal.OverallScore
                    ?? (managerTotals.TryGetValue(appraisal.Id, out var managerTotal) ? managerTotal : null);
            }

            var itemAdjustments = forThis
                .Where(a => a.TemplateItemId.HasValue && a.AdjustedScore.HasValue)
                .GroupBy(a => a.TemplateItemId!.Value)
                .Select(g => g.First())   // already ordered newest-first
                .ToList();

            var scoreBefore = appraisal.OverallScore;

            if (itemAdjustments.Count > 0)
            {
                var applied = await ApplyItemAdjustmentsAsync(appraisal.Id, itemAdjustments, cancellationToken);
                result.AdjustmentsApplied += applied;

                if (applied > 0)
                {
                    // Recomputes the weighted total from the amended criterion scores and
                    // re-resolves the grade band. Saves on its own; the same tracked appraisal
                    // instance is updated in place, so the overall override below still wins.
                    await _appraisalService.CalculateOverallScoreAsync(appraisal.Id, cancellationToken);
                }
            }

            var overallAdjustment = forThis.FirstOrDefault(a => !a.TemplateItemId.HasValue && a.AdjustedScore.HasValue);
            if (overallAdjustment != null)
            {
                appraisal.OverallScore = overallAdjustment.AdjustedScore;
                appraisal.OverallGradeDefinitionId =
                    (await ResolveGradeAsync(appraisal.OverallScore, session.TenantId, cancellationToken))?.Id;
                result.AdjustmentsApplied++;
            }

            if (appraisal.OverallScore != scoreBefore) result.ScoresChanged++;

            appraisal.IsCalibrated = true;
            appraisal.CalibrationSessionId = sessionId;
            result.AppraisalsCalibrated++;

            await _appraisalRepository.UpdateAsync(appraisal);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Calibration session {SessionId} committed by {AppliedById}: {Adjustments} adjustment(s), {Changed} score(s) changed, {Calibrated} appraisal(s) calibrated",
            sessionId, appliedById, result.AdjustmentsApplied, result.ScoresChanged, result.AppraisalsCalibrated);

        return result;
    }

    /// <summary>
    /// Writes item-level panel decisions onto the manager's criterion scores. A criterion the
    /// manager never scored is skipped rather than invented — calibration restates a judgement,
    /// it does not make one that was never given.
    /// </summary>
    private async Task<int> ApplyItemAdjustmentsAsync(
        Guid appraisalId, List<CalibrationRatingAdjustment> itemAdjustments, CancellationToken cancellationToken)
    {
        var templateItemIds = itemAdjustments.Select(a => a.TemplateItemId!.Value).ToList();

        var scores = await _criterionScoreRepository.GetQueryable()
            .Include(cs => cs.EvaluatorEvaluation)
            .Where(cs => cs.EvaluatorEvaluation.AppraisalId == appraisalId
                      && cs.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager
                      && templateItemIds.Contains(cs.TemplateItemId))
            .ToListAsync(cancellationToken);

        var applied = 0;
        foreach (var adjustment in itemAdjustments)
        {
            var score = scores.FirstOrDefault(s => s.TemplateItemId == adjustment.TemplateItemId!.Value);
            if (score == null)
            {
                _logger.LogWarning(
                    "Calibration: appraisal {AppraisalId} has no manager score for template item {ItemId}; adjustment skipped.",
                    appraisalId, adjustment.TemplateItemId);
                continue;
            }

            score.NumericScore = (int)Math.Round(adjustment.AdjustedScore!.Value, MidpointRounding.AwayFromZero);
            score.Notes = string.IsNullOrWhiteSpace(adjustment.Rationale)
                ? score.Notes
                : $"{score.Notes}\n\n[Calibration {adjustment.AdjustmentDate:yyyy-MM-dd}]: {adjustment.Rationale}".TrimStart();

            await _criterionScoreRepository.UpdateAsync(score);
            applied++;
        }

        if (applied > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return applied;
    }

    /// <summary>Maps a score onto the tenant's grade bands — the same rule the scoring engine uses.</summary>
    private async Task<AppraisalGradeDefinition?> ResolveGradeAsync(
        decimal? score, Guid tenantId, CancellationToken cancellationToken)
    {
        if (score is not decimal value) return null;

        var bands = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId
                     && g.IsActive
                     && g.OverallMinScore != null
                     && g.OverallMaxScore != null)
            .ToListAsync(cancellationToken);

        return bands.FirstOrDefault(g => value >= g.OverallMinScore!.Value && value <= g.OverallMaxScore!.Value);
    }

    // ─── Calibration Matrix ───────────────────────────────────────────────────

    public async Task<CalibrationMatrixDto> GetCalibrationMatrixAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await BaseQuery.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new ArgumentException("Calibration session not found.");

        var appraisals = await GetScopedAppraisalsAsync(session, cancellationToken);

        var adjustments = await _adjustmentRepository
            .GetQueryable(a => a.CalibrationSessionId == sessionId)
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(a => a.Employee)
            .Include(a => a.TemplateItem)
                .ThenInclude(ti => ti!.Competency)
            .Include(a => a.TemplateItem)
                .ThenInclude(ti => ti!.KpiDefinition)
            .Include(a => a.AdjustedBy)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync(cancellationToken);

        // The manager's own submitted total, per appraisal — the number the panel is arguing
        // about. It was hardcoded to null, so the "manager proposed" column was always blank.
        var appraisalIds = appraisals.Select(a => a.Id).ToList();
        var managerTotals = await _appraisalRepository.GetQueryable()
            .Where(a => appraisalIds.Contains(a.Id))
            .SelectMany(a => a.EvaluatorEvaluations)
            .Where(e => e.EvaluatorRole == EvaluatorRole.Manager)
            .Select(e => new { e.AppraisalId, e.TotalScore })
            .ToListAsync(cancellationToken);
        var managerTotalByAppraisal = managerTotals
            .GroupBy(x => x.AppraisalId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.TotalScore));

        var managerIds = appraisals
            .Where(a => a.Employee?.ManagerId != null)
            .Select(a => a.Employee!.ManagerId!.Value)
            .Distinct()
            .ToList();
        var managerNames = await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == session.TenantId && managerIds.Contains(e.Id))
            .Select(e => new { e.Id, e.FirstName, e.LastName })
            .ToListAsync(cancellationToken);
        var managerNameById = managerNames.ToDictionary(m => m.Id, m => $"{m.FirstName} {m.LastName}".Trim());

        var rows = new List<CalibrationMatrixRowDto>();
        foreach (var appraisal in appraisals.OrderBy(a => a.Employee?.LastName).ThenBy(a => a.Employee?.FirstName))
        {
            var appraisalAdjustments = adjustments
                .Where(a => a.PerformanceAppraisalId == appraisal.Id)
                .ToList();

            // Only an overall adjustment restates the final number; an item-level one moves a
            // single criterion and is shown in the detail list instead.
            var latestOverall = appraisalAdjustments.FirstOrDefault(a => !a.TemplateItemId.HasValue);
            var calibratedScore = latestOverall?.AdjustedScore;

            // Same fallback as the commit: before HR sign-off the appraisal has no OverallScore,
            // so the manager's evaluation total is the number standing to be calibrated. Without
            // it the "pre-calibration" column read blank on exactly the appraisals a panel is
            // convened to look at.
            var managerTotal = managerTotalByAppraisal.TryGetValue(appraisal.Id, out var mt) ? mt : null;
            var preCalibration = appraisal.PreCalibrationScore ?? appraisal.OverallScore ?? managerTotal;

            rows.Add(new CalibrationMatrixRowDto
            {
                AppraisalId = appraisal.Id,
                EmployeeId = appraisal.EmployeeId,
                EmployeeName = $"{appraisal.Employee?.FirstName} {appraisal.Employee?.LastName}".Trim(),
                EmployeeNumber = appraisal.Employee?.EmployeeNumber ?? string.Empty,
                PositionName = appraisal.Employee?.Position?.Title,
                DepartmentName = appraisal.Employee?.OrganizationUnit?.Name,
                AppraisalStatus = appraisal.Status,
                ManagerProposedScore = managerTotal,
                PreCalibrationScore = preCalibration,
                CalibratedScore = calibratedScore,
                ScoreAdjustment = calibratedScore.HasValue && preCalibration.HasValue
                    ? calibratedScore.Value - preCalibration.Value
                    : null,
                AdjustmentRationale = latestOverall?.Rationale,
                IsCalibrated = appraisal.IsCalibrated,
                ManagerName = appraisal.Employee?.ManagerId != null
                    && managerNameById.TryGetValue(appraisal.Employee.ManagerId.Value, out var managerName)
                        ? managerName
                        : null,
                Adjustments = appraisalAdjustments.Select(a => a.ToDto()).ToList()
            });
        }

        var scores = rows.Select(r => r.CalibratedScore ?? r.PreCalibrationScore).Where(s => s.HasValue).Select(s => s!.Value).ToList();

        return new CalibrationMatrixDto
        {
            SessionId = session.Id,
            SessionName = session.SessionName,
            SessionStatus = session.Status,
            OrganizationUnitName = session.OrganizationUnit?.Name,
            OrganizationLevelName = session.OrganizationLevel?.Name,
            TotalEmployees = rows.Count,
            AdjustedCount = rows.Count(r => r.Adjustments.Count > 0),
            CalibratedCount = rows.Count(r => r.IsCalibrated),
            AverageScore = scores.Count > 0 ? Math.Round(scores.Average(), 2) : null,
            Rows = rows
        };
    }

    // ─── Attachments ─────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a file to a calibration session.
    ///
    /// <para>Replaces a path that could never have run — see the note on
    /// <c>CheckInService.AddAttachmentAsync</c>; <c>UploadedById</c> is a required Employee FK and
    /// was never set, so every call died on a foreign-key violation.</para>
    /// </summary>
    public async Task<AppraisalAttachmentDto> AddAttachmentAsync(
        Guid sessionId, Guid uploadedById, string fileName, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null)
    {
        var session = await GetOwnedSessionAsync(sessionId);

        var entity = new AppraisalAttachment
        {
            TenantId              = session.TenantId,
            CalibrationSessionId  = sessionId,
            EntityType            = AppraisalAttachmentEntityType.CalibrationSession,
            FileName              = fileName,
            FilePath              = string.Empty,
            FileSizeBytes         = fileSizeBytes,
            Description           = description,
            UploadDate            = DateTime.UtcNow,
            UploadedById          = uploadedById,
            FileUploadRecordId    = fileUploadRecordId,
            DocumentRecordId      = documentRecordId,
            DocumentVersionId     = documentVersionId,
        };

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _attachmentRepository.GetQueryable()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == entity.Id && a.TenantId == session.TenantId, cancellationToken);

        _logger.LogInformation("Attachment added to calibration session {SessionId}: {AttachmentId}", sessionId, entity.Id);
        return saved!.ToDto();
    }

    public async Task<AppraisalAttachmentDto?> GetAttachmentAsync(Guid sessionId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        var entity = await _attachmentRepository.GetQueryable()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(
                a => a.Id == attachmentId && a.CalibrationSessionId == sessionId && a.TenantId == session.TenantId,
                cancellationToken);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var entities = await _attachmentRepository.GetQueryable(a => a.CalibrationSessionId == sessionId)
            .Include(a => a.UploadedBy)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid sessionId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var entity = await _attachmentRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CalibrationSessionId == sessionId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Attachment not found.");

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

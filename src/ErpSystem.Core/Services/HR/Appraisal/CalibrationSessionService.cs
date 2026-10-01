using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
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
    private readonly IAppraisalScoreService _scores;
    private readonly IAppraisalLifecycleService _lifecycle;
    private readonly IAppraisalNotificationService _notifications;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalibrationSessionService> _logger;

    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;

    public CalibrationSessionService(
        IGenericRepository<CalibrationSession> sessionRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<CalibrationParticipant> participantRepository,
        IGenericRepository<CalibrationRatingAdjustment> adjustmentRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<OrganizationUnit> orgUnitRepository,
        IGenericRepository<CriterionScore> criterionScoreRepository,
        IGenericRepository<AppraisalGradeDefinition> gradeDefinitionRepository,
        IGenericRepository<PerformanceAppraisalCriterionConfig> criterionConfigRepository,
        IAppraisalScoreService scores,
        IAppraisalLifecycleService lifecycle,
        IAppraisalNotificationService notifications,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CalibrationSessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _cycleRepository = cycleRepository;
        _participantRepository = participantRepository;
        _adjustmentRepository = adjustmentRepository;
        _appraisalRepository = appraisalRepository;
        _attachmentRepository = attachmentRepository;
        _employeeRepository = employeeRepository;
        _orgUnitRepository = orgUnitRepository;
        _criterionScoreRepository = criterionScoreRepository;
        _gradeDefinitionRepository = gradeDefinitionRepository;
        _criterionConfigRepository = criterionConfigRepository;
        _scores = scores;
        _lifecycle = lifecycle;
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

    /// <summary>
    /// A panel sits on a running cycle's appraisals (performance closure E-d2b, D-59): refused unless the cycle is Open.
    /// An unknown cycle, or another tenant's, is not found — creating a session checked neither.
    /// </summary>
    private Task EnsureCycleOpenAsync(Guid cycleId, string action, CancellationToken cancellationToken)
        => AppraisalLiveCycle.EnsureCycleOpenAsync(_cycleRepository.GetQueryable(), GetTenantId(), cycleId, action, cancellationToken);

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

    /// <summary>
    /// The sessions a panellist sits on (performance closure P3): those they are a participant of,
    /// or facilitate. The desk passes no panellist and sees every session in the tenant — before
    /// this, so did any authenticated user.
    /// </summary>
    private static IQueryable<CalibrationSession> ForPanellist(IQueryable<CalibrationSession> query, Guid? panellistEmployeeId)
        => panellistEmployeeId is Guid me
            ? query.Where(s => s.FacilitatedById == me || s.Participants.Any(p => p.EmployeeId == me && !p.IsDeleted))
            : query;

    /// <summary>
    /// The viewer's own appraisals, which no calibration read shows them (P3, the two-actor rule):
    /// a panellist — or an HR officer — whose own appraisal is in scope would otherwise read their
    /// manager's proposal and the panel's restatement before the outcome is released to them.
    /// </summary>
    /// <remarks>A list, not a set: it is used inside an EF query, where a list's Contains translates.</remarks>
    private async Task<List<Guid>> OwnAppraisalIdsAsync(Guid tenantId, Guid? viewerEmployeeId, CancellationToken cancellationToken)
        => viewerEmployeeId is Guid me
            ? await _appraisalRepository.GetQueryable()
                .Where(a => a.TenantId == tenantId && a.EmployeeId == me)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken)
            : new List<Guid>();

    public async Task<IEnumerable<CalibrationSessionDto>> GetByCycleIdAsync(Guid cycleId, Guid? panellistEmployeeId, CancellationToken cancellationToken = default)
    {
        var entities = await ForPanellist(BaseQuery, panellistEmployeeId)
            .Where(s => s.AppraisalCycleId == cycleId)
            .OrderByDescending(s => s.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<CalibrationSessionDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? panellistEmployeeId, CancellationToken cancellationToken = default)
    {
        var query = ForPanellist(BaseQuery, panellistEmployeeId).OrderByDescending(s => s.ScheduledDate);
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

    /// <summary>
    /// A new session, Pending, facilitated by its creator until someone opens it (performance
    /// closure E-b). The facilitator came from the body, so any caller could record a session as
    /// run by someone else — and a facilitator reads the session's grid (P3).
    /// </summary>
    public async Task<CalibrationSessionDto> CreateAsync(CreateCalibrationSessionDto createDto, Guid? facilitatorId, CancellationToken cancellationToken = default)
    {
        await EnsureCycleOpenAsync(createDto.AppraisalCycleId, "A calibration session cannot be set up", cancellationToken);

        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        entity.Status = CalibrationStatus.Pending;
        entity.FacilitatedById = facilitatorId is Guid me && me != Guid.Empty ? me : null;

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calibration session created: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    /// <summary>
    /// The session's particulars (performance closure E-b). Its scope — cycle, unit and level — is
    /// fixed once it is open: opening linked every appraisal in it, and a re-scoped session kept
    /// appraisals it no longer covered while covering others it had never linked. The facilitator
    /// is the opener, not a field. A completed or cancelled session is not edited at all.
    /// </summary>
    public async Task<CalibrationSessionDto> UpdateAsync(UpdateCalibrationSessionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(updateDto.Id);

        switch (entity.Status)
        {
            case CalibrationStatus.Completed:
                throw new InvalidOperationException("A completed calibration session is not edited: it is the record of the panel's decisions.");
            case CalibrationStatus.Cancelled:
                throw new InvalidOperationException("A cancelled calibration session is not edited.");
        }

        var rescoped = updateDto.AppraisalCycleId != entity.AppraisalCycleId
            || updateDto.OrganizationUnitId != entity.OrganizationUnitId
            || updateDto.OrganizationLevelId != entity.OrganizationLevelId;
        if (rescoped && entity.Status != CalibrationStatus.Pending)
            throw new InvalidOperationException(
                "A session's scope is fixed once it is open: every appraisal in it is linked to it. Cancel it and set up another.");

        if (updateDto.AppraisalCycleId != entity.AppraisalCycleId)
            await EnsureCycleOpenAsync(updateDto.AppraisalCycleId, "The session cannot move to that cycle", cancellationToken);

        updateDto.UpdateEntity(entity);
        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calibration session updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    /// <summary>
    /// Removes a session that has not completed — a completed one is the record of a panel's
    /// decisions. The appraisals its opening linked are released with it (performance closure
    /// E-b): the link outlived the session, and the next session's opening passed them over as
    /// linked already.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(id);

        if (entity.Status == CalibrationStatus.Completed)
            throw new InvalidOperationException("A completed calibration session is not deleted: it is the record of the panel's decisions.");

        var released = await ReleaseAppraisalsAsync(entity, cancellationToken);
        await _sessionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Calibration session deleted: {Id}; {Released} appraisal(s) released", id, released);
        return true;
    }

    /// <summary>
    /// Frees the appraisals a session holds without having calibrated them — its opening's links
    /// (E-b). The link is what reads as "in a calibration session", and one to a session that will
    /// never commit kept its appraisals from the next session's opening. Saved by the caller.
    /// </summary>
    private async Task<int> ReleaseAppraisalsAsync(CalibrationSession session, CancellationToken cancellationToken)
    {
        var held = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == session.TenantId && a.CalibrationSessionId == session.Id && !a.IsCalibrated)
            .ToListAsync(cancellationToken);

        foreach (var appraisal in held)
        {
            appraisal.CalibrationSessionId = null;
            await _appraisalRepository.UpdateAsync(appraisal);
        }

        return held.Count;
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Convenes the session: Pending → InProgress, the opener recorded as its facilitator and the
    /// start stamped. A separate "start" stamped only that date, from a session already open, and
    /// changed nothing else (performance closure E-b).
    /// </summary>
    public async Task<CalibrationSessionDto> OpenSessionAsync(Guid sessionId, Guid facilitatedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(sessionId);

        if (entity.Status != CalibrationStatus.Pending)
            throw new InvalidOperationException($"Only a pending session is opened. This one is {entity.Status}.");

        await EnsureCycleOpenAsync(entity.AppraisalCycleId, "The calibration session cannot be opened", cancellationToken);

        entity.Status = CalibrationStatus.InProgress;
        entity.StartedDate = DateTime.UtcNow;
        if (facilitatedById != Guid.Empty)
            entity.FacilitatedById = facilitatedById;

        await _sessionRepository.UpdateAsync(entity);

        // Link the scoped appraisals to the session as it opens. Until this happens the
        // sub-status resolver has no way to tell "waiting for a calibration session" from
        // "sitting in one" — CalibrationInProgress was unreachable, because the only thing that
        // ever set CalibrationSessionId was the commit at the very end.
        //
        // Only those waiting for calibration (E-b): a link on one already calibrated read as this
        // session's calibration of it, and the commit skips what its session calibrated. A panel
        // restating a calibrated appraisal reaches it through its adjustment.
        var scoped = await GetScopedAppraisalsAsync(entity, cancellationToken);
        var linked = 0;
        foreach (var appraisal in scoped.Where(a => a.CalibrationSessionId == null && !a.IsCalibrated))
        {
            appraisal.CalibrationSessionId = sessionId;
            await _appraisalRepository.UpdateAsync(appraisal);
            linked++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Calibration session {Id} opened by {FacilitatedById} over {Count} appraisal(s), {Linked} linked",
            sessionId, facilitatedById, scoped.Count, linked);
        return await GetByIdAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// Calls off a session that has not completed (performance closure E-b, D-46) — <c>Cancelled</c>
    /// had no door. The appraisals its opening linked are released, so the next session's opening
    /// takes them; its panel and any adjustments stay as the record, and nothing of them is ever
    /// applied. The reason is kept at the head of the session's meeting notes.
    /// </summary>
    public async Task<CalibrationSessionDto> CancelSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(sessionId);

        switch (entity.Status)
        {
            case CalibrationStatus.Completed:
                throw new InvalidOperationException("This session has completed: its decisions stand, to be committed, not cancelled.");
            case CalibrationStatus.Cancelled:
                throw new InvalidOperationException("This session is cancelled already.");
        }

        var why = reason?.Trim();
        if (string.IsNullOrEmpty(why))
            throw new InvalidOperationException("A cancellation needs a reason.");

        entity.Status = CalibrationStatus.Cancelled;
        var line = $"Cancelled: {why}";
        var notes = string.IsNullOrWhiteSpace(entity.MeetingNotes) ? line : $"{line}\n\n{entity.MeetingNotes}";
        entity.MeetingNotes = notes.Length <= 4000 ? notes : notes[..4000];
        await _sessionRepository.UpdateAsync(entity);

        var released = await ReleaseAppraisalsAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calibration session {Id} cancelled; {Released} appraisal(s) released", sessionId, released);
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
        EnsurePanelOpen(session, "added");

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
        EnsurePanelOpen(await GetOwnedSessionAsync(sessionId), "removed");
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
        EnsurePanelOpen(await GetOwnedSessionAsync(sessionId), "marked");
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
        EnsureSitting(session, "recorded");
        await EnsureCycleOpenAsync(session.AppraisalCycleId, "An adjustment cannot be recorded", cancellationToken);

        // The appraisal has to be one this session actually covers — otherwise a session for one
        // department could quietly restate a score in another.
        var scopedIds = await GetScopedAppraisalIdsAsync(sessionId, cancellationToken);
        if (!scopedIds.Contains(dto.PerformanceAppraisalId))
            throw new InvalidOperationException("That appraisal is not in this calibration session's scope.");

        if (adjustedById == Guid.Empty)
            throw new InvalidOperationException("Your user account is not linked to an employee record, so the adjustment cannot be attributed.");

        var criterion = await ValidateAdjustmentAsync(
            dto.PerformanceAppraisalId, dto.TemplateItemId, dto.CriterionConfigId, dto.AdjustedScore, cancellationToken);

        var entity = dto.ToEntity();
        entity.TemplateItemId = criterion?.TemplateItemId;
        entity.CriterionConfigId = criterion?.CriterionConfigId;
        entity.IsOverall = criterion == null;
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

    /// <summary>
    /// The panel — who sits on a session and who attended — changes until the session completes or is cancelled
    /// (performance closure E-g1, § 5): the screen hid the controls on a closed session, and the service took the
    /// writes, so a completed session's record of who calibrated could be rewritten after the fact.
    /// </summary>
    private static void EnsurePanelOpen(CalibrationSession session, string action)
    {
        switch (session.Status)
        {
            case CalibrationStatus.Completed:
                throw new InvalidOperationException($"This calibration session has completed: its panel members are no longer {action}.");
            case CalibrationStatus.Cancelled:
                throw new InvalidOperationException($"This calibration session is cancelled: its panel members are no longer {action}.");
        }
    }

    /// <summary>
    /// Panel decisions are recorded, changed and removed while the session sits, and at no other
    /// time (performance closure E-b: the removal had no check, so a completed session's decisions
    /// could be taken back before its commit read them, and a cancelled one's record thinned).
    /// </summary>
    private static void EnsureSitting(CalibrationSession session, string action)
    {
        switch (session.Status)
        {
            case CalibrationStatus.Pending:
                throw new InvalidOperationException($"Open the calibration session first: adjustments are {action} while it sits.");
            case CalibrationStatus.Completed:
                throw new InvalidOperationException($"This calibration session has completed: its adjustments are no longer {action}.");
            case CalibrationStatus.Cancelled:
                throw new InvalidOperationException($"This calibration session is cancelled: its adjustments are no longer {action}.");
        }
    }

    /// <summary>
    /// The criterion an adjustment restates — null for the overall — and whether its score is one
    /// the item can hold (performance closure A11, A13). A criterion is named as a form input names
    /// it: a template item, or a goal row's snapshot id (lane L3); one that is not the appraisal's
    /// is refused. An overall is 0–100. An item's score is a whole number —
    /// <c>CriterionScore.NumericScore</c> is an integer, and a 72.5 used to be rounded silently at
    /// commit — from 0 to the top of the item's own scale: its highest grade band, or 100 for a
    /// measured row, whose adjustment is a restated achievement percentage (D-22).
    /// </summary>
    private async Task<CriterionRef?> ValidateAdjustmentAsync(
        Guid appraisalId, Guid? templateItemId, Guid? criterionConfigId, decimal? adjustedScore, CancellationToken cancellationToken)
    {
        if (templateItemId is null && criterionConfigId is null)
        {
            if (adjustedScore is decimal overall && !AppraisalScoring.IsValidScore(overall))
                throw new InvalidOperationException(
                    $"An overall score must be between {AppraisalScoring.MinScore:0} and {AppraisalScoring.MaxScore:0}.");
            return null;
        }

        var scoring = await _scores.LoadScoringAsync(appraisalId, cancellationToken);
        var criterion = scoring.Resolve(new EvaluationItemInputDto { TemplateItemId = templateItemId, CriterionConfigId = criterionConfigId })
            ?? throw new InvalidOperationException("That criterion is not one of this appraisal's.");

        if (adjustedScore is not decimal score) return criterion;

        if (score != decimal.Truncate(score))
            throw new InvalidOperationException("A criterion score is a whole number.");

        var top = await _scores.GetScaleTopAsync(scoring, criterion.Key, cancellationToken);
        if (score < AppraisalScoring.MinScore || score > top)
            throw new InvalidOperationException(
                $"This criterion is scored from {AppraisalScoring.MinScore:0} to {top:0}; {score:0} is outside its scale.");
        return criterion;
    }

    public async Task<IEnumerable<CalibrationRatingAdjustmentDto>> GetRatingAdjustmentsAsync(Guid sessionId, Guid? viewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        var own = await OwnAppraisalIdsAsync(session.TenantId, viewerEmployeeId, cancellationToken);
        var entities = await _adjustmentRepository.GetQueryable(a => a.CalibrationSessionId == sessionId)
            .Where(a => !own.Contains(a.PerformanceAppraisalId))
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(ap => ap.Employee)
            .Include(a => a.AdjustedBy)
            // The criterion's name: without these every item adjustment read "one criterion".
            .Include(a => a.TemplateItem).ThenInclude(ti => ti!.Competency)
            .Include(a => a.TemplateItem).ThenInclude(ti => ti!.KpiDefinition)
            .Include(a => a.CriterionConfig)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<CalibrationRatingAdjustmentDto>> GetAdjustmentsByAppraisalAsync(Guid sessionId, Guid appraisalId, Guid? viewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if ((await OwnAppraisalIdsAsync(session.TenantId, viewerEmployeeId, cancellationToken)).Contains(appraisalId))
            throw new ArgumentException("That appraisal is not in this calibration session.");
        var entities = await _adjustmentRepository.GetQueryable(
                a => a.CalibrationSessionId == sessionId && a.PerformanceAppraisalId == appraisalId)
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(ap => ap.Employee)
            .Include(a => a.AdjustedBy)
            .Include(a => a.TemplateItem).ThenInclude(ti => ti!.Competency)
            .Include(a => a.TemplateItem).ThenInclude(ti => ti!.KpiDefinition)
            .Include(a => a.CriterionConfig)
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
        Guid sessionId, Guid appraisalId, Guid? viewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        var tenantId = session.TenantId;

        // P3: an appraisal this session covers, and not the viewer's own. Any session id opened
        // every appraisal's criteria and the manager's item scores in the tenant — the id in the
        // route was never checked against the session's scope.
        var inScope = (await GetScopedAppraisalsAsync(session, cancellationToken)).Any(a => a.Id == appraisalId);
        if (!inScope || (await OwnAppraisalIdsAsync(tenantId, viewerEmployeeId, cancellationToken)).Contains(appraisalId))
            throw new ArgumentException("That appraisal is not in this calibration session.");

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
                            && !a.IsOverall
                            && a.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var scoring = await _scores.LoadScoringAsync(appraisalId, cancellationToken);
        var rows = new List<CalibrationCriterionDto>();

        foreach (var c in configs)
        {
            // Paired by criterion key: a goal row has no template item, and every goal row's null
            // would have matched every other's (lane L3).
            var key = c.CriterionKey();
            var score = managerScores.FirstOrDefault(s => s.CriterionKey() == key);
            var adjustment = adjustments.FirstOrDefault(a => a.CriterionKey() == key);
            var isKpi = c.IsMeasured();

            rows.Add(new CalibrationCriterionDto
            {
                TemplateItemId = c.TemplateItemId,
                CriterionConfigId = c.Id,
                CriterionKey = key,
                IsGoal = c.IsGoalRow(),
                TemplateItemName = c.CriterionName(),
                WeightUsed = c.WeightUsed,
                IsKpi = isKpi,
                // What an adjustment to this row can be (A11): a measured row's is a restated
                // achievement percentage (D-22), a rated item's a whole score up to its top band.
                ScaleTop = await _scores.GetScaleTopAsync(scoring, key, cancellationToken),
                KpiTargetValue = isKpi ? c.KpiTargetValue : null,
                ManagerScore = score?.NumericScore,
                ManagerActualValue = score?.ActualValue,
                // A KPI is scored by its achievement, so that is what the panel moves away from.
                ManagerAchievementPercent = !isKpi || score == null
                    ? null
                    : score.NumericScore.HasValue
                        ? (decimal?)score.NumericScore.Value
                        : score.ActualValue is decimal actual
                            ? AppraisalScoring.KpiAchievementPercent(actual, c.KpiTargetValue, c.KpiMinValue, c.KpiMaxValue)
                            : null,
                AdjustmentId = adjustment?.Id,
                AdjustedScore = adjustment?.AdjustedScore,
                Rationale = adjustment?.Rationale,
            });
        }

        return rows
            .OrderByDescending(c => c.WeightUsed)
            .ThenBy(c => c.TemplateItemName)
            .ToList();
    }

    /// <summary>
    /// Restates a recorded panel decision — its score and rationale (performance closure E-b). An
    /// adjustment stays on the appraisal and the criterion it was recorded against: the edit copied
    /// both from the body, so one decision could be moved onto another appraisal in the session or
    /// another item. That is a removal and a new record, and a body naming anything else is refused.
    /// </summary>
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

        EnsureSitting(session, "changed");
        await EnsureCycleOpenAsync(session.AppraisalCycleId, "The adjustment cannot be changed", cancellationToken);

        if (dto.PerformanceAppraisalId != entity.PerformanceAppraisalId)
            throw new InvalidOperationException(
                "An adjustment stays on the appraisal it was recorded for. Remove it and record one on the other appraisal.");

        // The new score on the adjustment's own scale; the body's criterion has to be the one it restates.
        var criterion = await ValidateAdjustmentAsync(
            entity.PerformanceAppraisalId, dto.TemplateItemId, dto.CriterionConfigId, dto.AdjustedScore, cancellationToken);
        if (criterion?.Key != entity.CriterionKey())
            throw new InvalidOperationException(
                "An adjustment stays on what it restated — the overall score, or its one criterion. Remove it and record a new one.");

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
        var session = await GetOwnedSessionAsync(sessionId);
        var entity = await _adjustmentRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == adjustmentId && a.CalibrationSessionId == sessionId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Rating adjustment not found in this session.");

        EnsureSitting(session, "removed");
        await EnsureCycleOpenAsync(session.AppraisalCycleId, "The adjustment cannot be removed", cancellationToken);

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
    ///
    /// <para>A withdrawn appraisal is out of every session (performance closure E-d1), the union
    /// included: its adjustments stay on record, but it left the grid, the matrix's counts and
    /// average, the opening's links and the commit. Only the commit skipped it before.</para>
    /// </summary>
    private async Task<List<PerformanceAppraisal>> GetScopedAppraisalsAsync(
        CalibrationSession session, CancellationToken cancellationToken)
    {
        var tenantId = session.TenantId;

        var query = _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.AppraisalCycleId == session.AppraisalCycleId
                        && a.Status != AppraisalStatus.Withdrawn);

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
                .Where(a => a.TenantId == tenantId && extraIds.Contains(a.Id) && a.Status != AppraisalStatus.Withdrawn)
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
    /// Commits the session (performance closure A4).
    ///
    /// <para>An appraisal is calibrated when it is at the calibration step — its manager has
    /// submitted and it is in governance — or when it is already final and this session adjusted
    /// it. Everything else in the session's scope is left untouched and listed in the result with
    /// the reason: stamping <c>IsCalibrated</c> on an appraisal whose manager had not yet
    /// submitted let it pass the calibration gate later without any panel seeing its score.</para>
    ///
    /// <para>For each calibrated appraisal, item adjustments are written onto the manager's
    /// criterion scores (<c>NumericScore</c> for both item types — for a KPI it is a restated
    /// achievement percentage, D-22), the session's overall adjustment becomes
    /// <c>CalibratedOverallScore</c>, and the settle recomputes the score from the amended items
    /// with the restated overall ahead of it. Before, the item scores were written and the
    /// overall re-summed from the stale stored weighted scores, so no item adjustment ever changed
    /// a result, and the overall was written straight onto <c>OverallScore</c>, where HR sign-off
    /// recomputed it away.</para>
    ///
    /// <para>A restated overall stands until a later session restates that appraisal. A session
    /// that adjusts only items clears an earlier session's overall, so the overall follows the
    /// items it just changed; a session that adjusts nothing for an appraisal leaves it as the
    /// panel found it.</para>
    ///
    /// <para>Each appraisal commits on its own save — its adjustments, its calibration stamp and
    /// its settled score together — and a final one is published to the talent pools.</para>
    ///
    /// <para><b>Once per appraisal</b> (performance closure E-b). A session commits an appraisal
    /// once, and only the evaluation its panel sat over. Run again — after a failure part-way, or
    /// once more of its scope has reached the step — it skips what it calibrated, and anything
    /// whose manager submitted after the panel closed. Re-running re-applied every adjustment to a
    /// final appraisal it had adjusted: after an upheld appeal it wrote the manager's criterion
    /// back, restored the restated overall, re-settled and published, and appended its rationale
    /// again. And an appraisal HR returned to its manager, re-evaluated and back at the step, took
    /// the old panel's decisions onto an evaluation that panel never saw.</para>
    /// </summary>
    public async Task<CalibrationApplyResultDto> ApplyAllAdjustmentsAsync(
        Guid sessionId, Guid appliedById, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(sessionId);

        if (session.Status != CalibrationStatus.Completed)
            throw new InvalidOperationException("Adjustments can only be applied to a completed session.");

        await EnsureCycleOpenAsync(session.AppraisalCycleId, "The panel's ratings cannot be committed", cancellationToken);

        var scoped = await GetScopedAppraisalsAsync(session, cancellationToken);
        if (scoped.Count == 0)
            throw new InvalidOperationException(
                "This session covers no appraisals. Check its cycle and organization scope before committing it.");

        var adjustments = await _adjustmentRepository
            .GetQueryable(a => a.CalibrationSessionId == sessionId)
            .OrderByDescending(a => a.AdjustmentDate)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var byAppraisal = adjustments.GroupBy(a => a.PerformanceAppraisalId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // PreCalibrationScore is documented as "the manager's proposed score before calibration
        // adjustments": the manager's evaluation total, which is the number the panel was looking
        // at on the grid.
        var scopedIds = scoped.Select(a => a.Id).ToList();
        var managers = await ManagerEvaluationsAsync(scopedIds, cancellationToken);

        var result = new CalibrationApplyResultDto();

        // Where each appraisal is, by the same gates every other write is held to (B1).
        var gateStates = await _lifecycle.GetStatesAsync(scopedIds, cancellationToken);

        foreach (var appraisal in scoped)
        {
            byAppraisal.TryGetValue(appraisal.Id, out var forThis);
            forThis ??= new List<CalibrationRatingAdjustment>();

            var itemAdjustments = forThis
                .Where(a => !a.IsOverall && a.CriterionKey().HasValue && a.AdjustedScore.HasValue)
                .GroupBy(a => a.CriterionKey()!.Value)
                .Select(g => g.First())   // already ordered newest-first
                .ToList();
            var overallAdjustment = forThis.FirstOrDefault(a => a.IsOverall && a.AdjustedScore.HasValue);
            var adjustedHere = itemAdjustments.Count > 0 || overallAdjustment != null;

            gateStates.TryGetValue(appraisal.Id, out var gateState);
            managers.TryGetValue(appraisal.Id, out var manager);
            var skip = CommitSkipReason(session, appraisal, gateState, adjustedHere, manager?.SubmittedDate);
            if (skip != null)
            {
                // Opening the session linked every appraisal in its scope to it; one it does not
                // calibrate leaves with the commit. The link is what reads as "in a calibration
                // session", and it pinned a skipped appraisal to a panel that had already sat — the
                // next session's opening never took it, since it was linked already.
                if (appraisal.CalibrationSessionId == sessionId && !appraisal.IsCalibrated)
                    appraisal.CalibrationSessionId = null;

                result.Skipped.Add(new CalibrationSkippedAppraisalDto
                {
                    AppraisalId = appraisal.Id,
                    EmployeeName = appraisal.Employee?.FullName,
                    Status = appraisal.Status.ToString(),
                    Reason = skip,
                });
                continue;
            }

            // Capture the manager's number once, the first time this appraisal is calibrated.
            if (!appraisal.IsCalibrated && appraisal.PreCalibrationScore == null)
            {
                appraisal.PreCalibrationScore = manager?.Total ?? appraisal.OverallScore;
            }

            if (itemAdjustments.Count > 0)
                result.AdjustmentsApplied += await ApplyItemAdjustmentsAsync(appraisal.Id, itemAdjustments, cancellationToken);

            if (overallAdjustment != null)
            {
                appraisal.CalibratedOverallScore = overallAdjustment.AdjustedScore;
                result.AdjustmentsApplied++;
            }
            else if (itemAdjustments.Count > 0)
            {
                appraisal.CalibratedOverallScore = null;
            }

            appraisal.IsCalibrated = true;
            appraisal.CalibrationSessionId = sessionId;
            result.AppraisalsCalibrated++;

            // Saves this appraisal's adjustments, stamp and score together; a final appraisal —
            // Completed, Closed, or signed off and waiting on the acknowledgment — carries its new
            // rating to the talent pools (A7).
            var settled = await _scores.SettleAsync(appraisal.Id, AppraisalScoreChangeSource.Calibration, publish: true, cancellationToken);
            if (settled.ScoreBefore != settled.ScoreAfter) result.ScoresChanged++;

            // The commit can be the step that completes the appraisal — calibration last, with no HR
            // review or acknowledgment after it — which left it in Governance for good: nothing but
            // HR's advance ever moved it on. The gates decide now, and completion settles again in the
            // same save and publishes (B1).
            await _lifecycle.SyncAsync(appraisal.Id, AppraisalScoreChangeSource.Calibration, publish: true, cancellationToken);
        }

        // The skipped appraisals' released links, when no settle after them saved them.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Calibration session {SessionId} committed by {AppliedById}: {Adjustments} adjustment(s), {Changed} score(s) changed, {Calibrated} appraisal(s) calibrated, {Skipped} skipped",
            sessionId, appliedById, result.AdjustmentsApplied, result.ScoresChanged, result.AppraisalsCalibrated, result.AppraisalsSkipped);

        return result;
    }

    /// <summary>
    /// Why the commit leaves an appraisal in the session's scope alone, or null when it calibrates
    /// it: at the calibration step by the gates (B1); already calibrated and adjusted again by this
    /// session before it is final — a later panel restating it (lane A); or final with an adjustment
    /// from this session (A7). The grid shows it on each row, so the panel sees who a commit takes.
    ///
    /// <para>It calibrated any appraisal in Governance. With HR's review before calibration that
    /// took appraisals HR had not reviewed yet, and on a cycle with no calibration step it stamped
    /// appraisals calibrated that no gate would ever ask about.</para>
    ///
    /// <para>Two reasons come first (E-b). What this session calibrated, it does not calibrate
    /// again — only its own commit writes the link and the stamp together (opening links only
    /// appraisals waiting for calibration, and HR's advance past the step releases the link). And
    /// a panel calibrates the evaluation it sat over: once the session has closed, a manager's
    /// submission after it — a first one, or a revision after HR's return or an appeal's remand —
    /// is a number no panel saw.</para>
    /// </summary>
    private static string? CommitSkipReason(
        CalibrationSession session, PerformanceAppraisal appraisal, AppraisalGateState? state,
        bool adjustedHere, DateTime? managerSubmittedDate)
    {
        if (appraisal.IsCalibrated && appraisal.CalibrationSessionId == session.Id)
            return "Already calibrated by this session: what has happened to it since stands.";

        switch (appraisal.Status)
        {
            case AppraisalStatus.Completed or AppraisalStatus.Closed when !adjustedHere:
                return "Already final, and this session made no adjustment to it.";
            case AppraisalStatus.Appealed:
                return "Under appeal: the appeal decides its score.";
            case AppraisalStatus.Withdrawn:
                return "Withdrawn: it is not being appraised.";
        }

        if (session.CompletedDate is DateTime closed && managerSubmittedDate > closed)
            return "Its manager submitted after this panel closed: the panel did not see that evaluation, and a panel that sits on it calibrates it.";

        if (appraisal.Status is AppraisalStatus.Completed or AppraisalStatus.Closed)
            return null;

        if (state == null)
            return "Its cycle's appraisal settings could not be read.";

        if (AppraisalGates.Check(state.Block, AppraisalSubStatus.PendingCalibration))
            return null;

        if (appraisal.IsCalibrated && adjustedHere
            && AppraisalGates.ExpectedMajorStatus(state.SubStatus) == AppraisalStatus.Governance)
            return null;

        if (!state.Settings.RequireCalibration)
            return $"This cycle does not require calibration (it is at {state.StepLabel}).";

        return state.Block.Reason is { Length: > 0 } reason
            ? $"Not at the calibration step: it is at {state.StepLabel} — {reason}."
            : $"Not at the calibration step: it is at {state.StepLabel}.";
    }

    /// <summary>What a manager evaluation contributes to calibration: its total, and when it was last submitted.</summary>
    private sealed record ManagerEvaluationFacts(decimal? Total, DateTime? SubmittedDate);

    private async Task<Dictionary<Guid, ManagerEvaluationFacts>> ManagerEvaluationsAsync(
        List<Guid> appraisalIds, CancellationToken cancellationToken)
        => (await _appraisalRepository.GetQueryable()
                .Where(a => appraisalIds.Contains(a.Id))
                .SelectMany(a => a.EvaluatorEvaluations)
                .Where(e => e.EvaluatorRole == EvaluatorRole.Manager && !e.IsDeleted)
                .Select(e => new { e.AppraisalId, e.TotalScore, e.SubmittedDate })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.AppraisalId)
            .ToDictionary(g => g.Key, g => new ManagerEvaluationFacts(g.Max(x => x.TotalScore), g.Max(x => x.SubmittedDate)));

    /// <summary>
    /// Writes item-level panel decisions onto the manager's criterion scores. A criterion the
    /// manager never scored is skipped rather than invented — calibration restates a judgement,
    /// it does not make one that was never given. Saved by the settle that follows.
    /// </summary>
    private async Task<int> ApplyItemAdjustmentsAsync(
        Guid appraisalId, List<CalibrationRatingAdjustment> itemAdjustments, CancellationToken cancellationToken)
    {
        // The manager's rows, paired with each adjustment by criterion key — a goal row has no
        // template item (lane L3).
        var scores = await _criterionScoreRepository.GetQueryable()
            .Include(cs => cs.EvaluatorEvaluation)
            .Where(cs => cs.EvaluatorEvaluation.AppraisalId == appraisalId
                      && cs.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager)
            .ToListAsync(cancellationToken);

        var applied = 0;
        foreach (var adjustment in itemAdjustments)
        {
            var key = adjustment.CriterionKey();
            var score = scores.FirstOrDefault(s => (s.TemplateItemId.HasValue || s.CriterionConfigId.HasValue) && s.CriterionKey() == key);
            if (score == null)
            {
                _logger.LogWarning(
                    "Calibration: appraisal {AppraisalId} has no manager score for criterion {Key}; adjustment skipped.",
                    appraisalId, key);
                continue;
            }

            // Item scores are whole numbers. Adjustments are refused unless whole at entry (A13);
            // the explicit rounding is for rows recorded before that check existed.
            score.NumericScore = (int)Math.Round(adjustment.AdjustedScore!.Value, MidpointRounding.AwayFromZero);
            score.Notes = string.IsNullOrWhiteSpace(adjustment.Rationale)
                ? score.Notes
                : $"{score.Notes}\n\n[Calibration {adjustment.AdjustmentDate:yyyy-MM-dd}]: {adjustment.Rationale}".TrimStart();

            applied++;
        }

        return applied;
    }

    // ─── Calibration Matrix ───────────────────────────────────────────────────

    public async Task<CalibrationMatrixDto> GetCalibrationMatrixAsync(Guid sessionId, Guid? viewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var session = await BaseQuery.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new ArgumentException("Calibration session not found.");

        // The viewer's own row goes before the counts and the average are taken (P3): leaving it
        // in the average would let them work their own score out from everyone else's.
        var own = await OwnAppraisalIdsAsync(session.TenantId, viewerEmployeeId, cancellationToken);
        var appraisals = (await GetScopedAppraisalsAsync(session, cancellationToken))
            .Where(a => !own.Contains(a.Id))
            .ToList();

        var adjustments = await _adjustmentRepository
            .GetQueryable(a => a.CalibrationSessionId == sessionId)
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(a => a.Employee)
            .Include(a => a.TemplateItem)
                .ThenInclude(ti => ti!.Competency)
            .Include(a => a.TemplateItem)
                .ThenInclude(ti => ti!.KpiDefinition)
            .Include(a => a.CriterionConfig)
            .Include(a => a.AdjustedBy)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync(cancellationToken);

        // The manager's own submitted total, per appraisal — the number the panel is arguing
        // about. It was hardcoded to null, so the "manager proposed" column was always blank.
        var appraisalIds = appraisals.Select(a => a.Id).ToList();
        var managers = await ManagerEvaluationsAsync(appraisalIds, cancellationToken);

        // Where each row is, so it can say what a commit would do with it (E-b).
        var gateStates = await _lifecycle.GetStatesAsync(appraisalIds, cancellationToken);

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
            // single criterion and is shown in the detail list instead. The flag, not a missing
            // template item: a goal row has none (lane L3), and read as the overall here.
            var latestOverall = appraisalAdjustments.FirstOrDefault(a => a.IsOverall);

            // P-41 (E-b): a calibrated appraisal reads its own settled score — the restated overall,
            // or the overall its restated items give — unless this session is still proposing a new
            // one for it. The grid read the adjustment record alone, so a row this session committed
            // with item adjustments showed no calibrated score, and one whose score an appeal or a
            // later panel moved kept showing the number the panel had typed.
            var committedHere = appraisal.IsCalibrated && appraisal.CalibrationSessionId == session.Id;
            var calibratedScore = appraisal.IsCalibrated && (committedHere || latestOverall == null)
                ? appraisal.OverallScore
                : latestOverall?.AdjustedScore;

            // The commit's own fallback, in the commit's order: the number it captured, else the
            // manager's evaluation total — before HR's sign-off there is no settled score, and the
            // "pre-calibration" column read blank on exactly the appraisals a panel is convened to
            // look at — else a settled score. The settled score came before the manager's total, so
            // an appraisal HR returned showed the old panel's result as the new panel's starting point.
            managers.TryGetValue(appraisal.Id, out var manager);
            var managerTotal = manager?.Total;
            var preCalibration = appraisal.PreCalibrationScore ?? managerTotal ?? appraisal.OverallScore;

            gateStates.TryGetValue(appraisal.Id, out var gateState);
            var adjustedHere = appraisalAdjustments.Any(a => a.AdjustedScore.HasValue && (a.IsOverall || a.CriterionKey().HasValue));

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
                CommitSkipReason = CommitSkipReason(session, appraisal, gateState, adjustedHere, manager?.SubmittedDate),
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

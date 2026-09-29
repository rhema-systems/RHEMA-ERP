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

public class CheckInService : ICheckInService
{
    private readonly IGenericRepository<CheckIn> _checkInRepository;
    private readonly IGenericRepository<CheckInGoalUpdate> _goalUpdateRepository;
    private readonly IGenericRepository<AppraisalAttachment> _attachmentRepository;
    private readonly IGenericRepository<EmployeeGoal> _goalRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CheckInService> _logger;

    public CheckInService(
        IGenericRepository<CheckIn> checkInRepository,
        IGenericRepository<CheckInGoalUpdate> goalUpdateRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        IGenericRepository<EmployeeGoal> goalRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CheckInService> logger)
    {
        _checkInRepository = checkInRepository;
        _goalUpdateRepository = goalUpdateRepository;
        _attachmentRepository = attachmentRepository;
        _goalRepository = goalRepository;
        _cycleRepository = cycleRepository;
        _currentUserProvider = currentUserProvider;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Whether the caller holds this check-in (performance closure P6): the private notes are the
    /// conductor's own record, so they are written by the conductor and nobody else — the desk and
    /// the subject edit the shared record, and their redacted copy never wipes the notes.
    /// </summary>
    private bool CallerConducts(CheckIn entity) =>
        _currentUser.EmployeeId is Guid me && me == entity.ConductedById;

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

    // A check-in owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<CheckIn> GetOwnedCheckInAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _checkInRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Check-in with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<CheckIn> BaseQuery()
    {
        var tenantId = GetTenantId();
        return _checkInRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId)
            .Include(c => c.Employee)
            .Include(c => c.ConductedBy)
            .Include(c => c.Cycle);
    }

    public async Task<CheckInDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Check-in with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<CheckInDto>> GetByEmployeeIdAsync(Guid employeeId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().Where(c => c.EmployeeId == employeeId);
        if (cycleId.HasValue)
            query = query.Where(c => c.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderByDescending(c => c.ScheduledDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<CheckInDto>> GetByConductedByIdAsync(Guid conductedById, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().Where(c => c.ConductedById == conductedById);
        if (cycleId.HasValue)
            query = query.Where(c => c.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderByDescending(c => c.ScheduledDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<CheckInDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().OrderByDescending(c => c.ScheduledDate);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<CheckInDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<CheckInDto>> GetUpcomingAsync(Guid employeeId, int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        var entities = await BaseQuery()
            .Where(c => c.EmployeeId == employeeId &&
                        c.ScheduledDate >= DateTime.UtcNow &&
                        c.ScheduledDate <= cutoff &&
                        c.ConductedDate == null)
            .OrderBy(c => c.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<CheckInDto> CreateAsync(CreateCheckInDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // The cycle has to exist before its settings can say anything. Projecting straight to
        // AppraisalSettings conflated "no such cycle" with "cycle has no settings profile", and
        // both fell through the `is { EnableCheckIns: false }` pattern — so a bad cycle id got
        // past the gate and died on the foreign key as an unreadable 500.
        var cycle = await _cycleRepository.GetQueryable(c => c.Id == createDto.AppraisalCycleId && c.TenantId == tenantId)
            .Include(c => c.AppraisalSettings)
            .FirstOrDefaultAsync(cancellationToken);

        if (cycle is null)
            throw new ArgumentException($"Appraisal cycle '{createDto.AppraisalCycleId}' not found.");

        // Gate: check-ins must be enabled for this cycle.
        if (cycle.AppraisalSettings is { EnableCheckIns: false })
            throw new InvalidOperationException("Check-ins are not enabled for this appraisal cycle.");

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        await _checkInRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Check-in created: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<CheckInDto> UpdateAsync(UpdateCheckInDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCheckInAsync(updateDto.Id, cancellationToken);
        updateDto.UpdateEntity(entity);
        if (CallerConducts(entity))
            entity.PrivateNotes = updateDto.PrivateNotes;
        await _checkInRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Check-in updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCheckInAsync(id, cancellationToken);
        await _checkInRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Check-in deleted: {Id}", id);
        return true;
    }

    public async Task<CheckInDto> CompleteAsync(
        Guid checkInId, string? sharedNotes, string? privateNotes, string? actionItems,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCheckInAsync(checkInId, cancellationToken);

        entity.ConductedDate = DateTime.UtcNow;
        entity.SharedNotes = sharedNotes;
        if (CallerConducts(entity))
            entity.PrivateNotes = privateNotes;
        entity.ActionItems = actionItems;

        await _checkInRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Check-in completed: {Id}", checkInId);
        return await GetByIdAsync(checkInId, cancellationToken);
    }

    // ─── Goal Updates ────────────────────────────────────────────────────────

    /// <summary>
    /// Statuses a goal may receive check-in updates in — approved and still running. A goal the old
    /// lock left in the Locked status reads as approved: a lock freezes what a goal is, not its year
    /// (decision D-29).
    /// </summary>
    private static readonly HashSet<GoalStatus> LiveExecutionStatuses = new()
    {
        GoalStatus.Approved,
        GoalStatus.InProgress,
        GoalStatus.OnTrack,
        GoalStatus.AtRisk,
        GoalStatus.Locked,
    };

    /// <summary>
    /// Carries a check-in's goal update onto the goal itself.
    ///
    /// The percent used to be the only thing copied across, so a manager flagging a goal at risk
    /// in a one-to-one changed nothing on the goal and it stayed out of every at-risk report —
    /// the same defect shape as goal progress entries before <c>ApplyProgressToGoal</c>. The
    /// mapping here is deliberately identical to <c>EmployeeGoalService.ApplyProgressToGoal</c>
    /// so both channels leave a goal in the same state: 100% completes it whatever the update
    /// claims, NotStarted and Cancelled leave the status alone, and an explicit at-risk flag
    /// wins over a rosier reported status.
    ///
    /// A goal that is not live (draft, awaiting approval, rejected, locked, already complete)
    /// keeps its status; the update is still recorded as a note against the check-in.
    /// </summary>
    private async Task ApplyGoalUpdateAsync(EmployeeGoal goal, CheckInGoalUpdate update)
    {
        if (!LiveExecutionStatuses.Contains(goal.Status))
            return;

        if (update.UpdatedProgress.HasValue)
            goal.ProgressPercent = update.UpdatedProgress.Value;

        if (update.UpdatedProgress >= 100)
        {
            goal.Status = GoalStatus.Completed;
        }
        else if (update.FlaggedAtRisk)
        {
            goal.Status = GoalStatus.AtRisk;
        }
        else
        {
            goal.Status = update.UpdatedStatus switch
            {
                GoalProgressStatus.InProgress => GoalStatus.InProgress,
                GoalProgressStatus.OnTrack    => GoalStatus.OnTrack,
                GoalProgressStatus.AtRisk     => GoalStatus.AtRisk,
                GoalProgressStatus.Completed  => GoalStatus.Completed,
                _                             => goal.Status,
            };
        }

        await _goalRepository.UpdateAsync(goal);
    }

    public async Task<CheckInGoalUpdateDto> AddGoalUpdateAsync(Guid checkInId, CreateCheckInGoalUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var checkIn = await GetOwnedCheckInAsync(checkInId, cancellationToken);
        var tenantId = GetTenantId();

        // Only a goal of the person the check-in is about (performance closure P7, E10's add-time
        // check brought forward). Anyone could open a check-in about themselves and, through it,
        // move any colleague's goal progress and status — the goal was checked for tenant only.
        var goal = await _goalRepository.GetByIdAsync(dto.EmployeeGoalId);
        if (goal == null || goal.TenantId != tenantId || goal.EmployeeId != checkIn.EmployeeId)
            throw new ArgumentException("Employee goal not found.");

        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        entity.CheckInId = checkInId;

        await _goalUpdateRepository.AddAsync(entity);
        await ApplyGoalUpdateAsync(goal, entity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _goalUpdateRepository.GetQueryable()
            .Include(u => u.EmployeeGoal)
            .FirstOrDefaultAsync(u => u.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Goal update added to check-in {CheckInId}: {UpdateId}", checkInId, entity!.Id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<CheckInGoalUpdateDto>> GetGoalUpdatesAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCheckInAsync(checkInId, cancellationToken);
        var tenantId = GetTenantId();
        var entities = await _goalUpdateRepository.GetQueryable(u => u.CheckInId == checkInId && u.TenantId == tenantId)
            .Include(u => u.EmployeeGoal)
            .OrderBy(u => u.EmployeeGoal.Title)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<CheckInGoalUpdateDto> UpdateGoalUpdateAsync(Guid checkInId, UpdateCheckInGoalUpdateDto dto, CancellationToken cancellationToken = default)
    {
        await GetOwnedCheckInAsync(checkInId, cancellationToken);
        var tenantId = GetTenantId();
        var entity = await _goalUpdateRepository.GetQueryable()
            .Include(u => u.EmployeeGoal)
            .FirstOrDefaultAsync(u => u.Id == dto.Id && u.CheckInId == checkInId && u.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Goal update not found.");

        dto.UpdateEntity(entity);
        await _goalUpdateRepository.UpdateAsync(entity);

        // A correction has to reach the goal too, or the goal keeps the figure the mistaken
        // update put there. Same rule as the add path.
        var goal = await _goalRepository.GetByIdAsync(entity.EmployeeGoalId);
        if (goal != null && goal.TenantId == tenantId)
            await ApplyGoalUpdateAsync(goal, entity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Check-in goal update updated: {UpdateId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteGoalUpdateAsync(Guid checkInId, Guid updateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCheckInAsync(checkInId, cancellationToken);
        var tenantId = GetTenantId();
        var entity = await _goalUpdateRepository.GetQueryable()
            .FirstOrDefaultAsync(u => u.Id == updateId && u.CheckInId == checkInId && u.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Goal update not found.");

        await _goalUpdateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ─── Attachments ─────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a file to a check-in.
    ///
    /// <para>Replaces a path that could never have run: it mapped a
    /// <c>CreateAppraisalAttachmentDto</c>, which carries no uploader, onto an entity whose
    /// <c>UploadedById</c> is a required Employee FK, so every call died on a foreign-key violation
    /// against <c>Guid.Empty</c>. It also set <c>PerformanceAppraisalId</c> from the payload while
    /// setting <c>CheckInId</c> here, populating two of the polymorphic FKs documented as mutually
    /// exclusive.</para>
    /// </summary>
    public async Task<AppraisalAttachmentDto> AddAttachmentAsync(
        Guid checkInId, Guid uploadedById, string fileName, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null)
    {
        await GetOwnedCheckInAsync(checkInId, cancellationToken);
        var tenantId = GetTenantId();

        var entity = new AppraisalAttachment
        {
            TenantId           = tenantId,
            CheckInId          = checkInId,
            EntityType         = AppraisalAttachmentEntityType.CheckIn,
            FileName           = fileName,
            // The file lives outside the web root and is reachable only through the authorizing
            // download endpoint, so there is no servable path to record.
            FilePath           = string.Empty,
            FileSizeBytes      = fileSizeBytes,
            Description        = description,
            UploadDate         = DateTime.UtcNow,
            UploadedById       = uploadedById,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId   = documentRecordId,
            DocumentVersionId  = documentVersionId,
        };

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read rather than mapping the tracked instance: UploadedBy was never loaded on it, so
        // the create response carried a blank uploader name while a later GET showed it.
        var saved = await _attachmentRepository.GetQueryable()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == entity.Id && a.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Attachment added to check-in {CheckInId}: {AttachmentId}", checkInId, entity.Id);
        return saved!.ToDto();
    }

    public async Task<AppraisalAttachmentDto?> GetAttachmentAsync(Guid checkInId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _attachmentRepository.GetQueryable()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CheckInId == checkInId && a.TenantId == tenantId, cancellationToken);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCheckInAsync(checkInId, cancellationToken);
        var tenantId = GetTenantId();
        var entities = await _attachmentRepository.GetQueryable(a => a.CheckInId == checkInId && a.TenantId == tenantId)
            .Include(a => a.UploadedBy)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    /// <remarks>
    /// Performance closure P9: only the person who attached the file, or the HR desk when it is not
    /// the check-in's subject, removes it — and only until the check-in is completed (its
    /// <c>ConductedDate</c> is set), after which the file is part of the meeting's record.
    /// </remarks>
    public async Task<bool> DeleteAttachmentAsync(Guid checkInId, Guid attachmentId, bool actorIsDesk, CancellationToken cancellationToken = default)
    {
        var checkIn = await GetOwnedCheckInAsync(checkInId, cancellationToken);
        var tenantId = GetTenantId();
        var entity = await _attachmentRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CheckInId == checkInId && a.TenantId == tenantId, cancellationToken);
        if (entity == null)
            throw new ArgumentException("Attachment not found.");

        if (checkIn.ConductedDate != null)
            throw new InvalidOperationException(
                "This check-in has been completed, so its attachments are part of the record and cannot be removed.");

        var me = _currentUser.EmployeeId;
        var isUploader = me is Guid uploader && uploader == entity.UploadedById;
        var deskActs = actorIsDesk && !(me is Guid subject && subject == checkIn.EmployeeId);
        if (!isUploader && !deskActs)
            throw new UnauthorizedAccessException("Only the person who attached this file, or HR, can remove it.");
        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalibrationSessionService> _logger;

    public CalibrationSessionService(
        IGenericRepository<CalibrationSession> sessionRepository,
        IGenericRepository<CalibrationParticipant> participantRepository,
        IGenericRepository<CalibrationRatingAdjustment> adjustmentRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        IUnitOfWork unitOfWork,
        ILogger<CalibrationSessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _participantRepository = participantRepository;
        _adjustmentRepository = adjustmentRepository;
        _appraisalRepository = appraisalRepository;
        _attachmentRepository = attachmentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private IQueryable<CalibrationSession> BaseQuery => _sessionRepository.GetQueryable()
        .Include(s => s.AppraisalCycle)
        .Include(s => s.OrganizationUnit)
        .Include(s => s.FacilitatedBy);

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
        entity.Status = CalibrationStatus.Pending;

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calibration session created: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<CalibrationSessionDto> UpdateAsync(UpdateCalibrationSessionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Calibration session with ID '{updateDto.Id}' not found.");

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
        var entity = await _sessionRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Calibration session with ID '{id}' not found.");

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
        var entity = await _sessionRepository.GetByIdAsync(sessionId);
        if (entity == null)
            throw new ArgumentException("Calibration session not found.");

        if (entity.Status != CalibrationStatus.Pending)
            throw new InvalidOperationException($"Session must be in Pending status to open. Current: {entity.Status}");

        entity.Status = CalibrationStatus.InProgress;
        entity.FacilitatedById = facilitatedById;

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calibration session {Id} opened by {FacilitatedById}", sessionId, facilitatedById);
        return await GetByIdAsync(sessionId, cancellationToken);
    }

    public async Task<CalibrationSessionDto> StartSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(sessionId);
        if (entity == null)
            throw new ArgumentException("Calibration session not found.");

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
        var entity = await _sessionRepository.GetByIdAsync(sessionId);
        if (entity == null)
            throw new ArgumentException("Calibration session not found.");

        if (entity.Status != CalibrationStatus.InProgress)
            throw new InvalidOperationException("Only in-progress sessions can be completed.");

        entity.Status = CalibrationStatus.Completed;
        entity.CompletedDate = DateTime.UtcNow;
        entity.MeetingNotes = meetingNotes;

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calibration session {Id} completed by {CompletedById}", sessionId, completedById);
        return await GetByIdAsync(sessionId, cancellationToken);
    }

    // ─── Participants ─────────────────────────────────────────────────────────

    public async Task<CalibrationParticipantDto> AddParticipantAsync(Guid sessionId, CreateCalibrationParticipantDto dto, CancellationToken cancellationToken = default)
    {
        var sessionExists = await _sessionRepository.ExistsAsync(s => s.Id == sessionId);
        if (!sessionExists)
            throw new ArgumentException("Calibration session not found.");

        var duplicate = await _participantRepository.ExistsAsync(p =>
            p.CalibrationSessionId == sessionId && p.EmployeeId == dto.EmployeeId);
        if (duplicate)
            throw new InvalidOperationException("This employee is already a participant in the session.");

        var entity = dto.ToEntity();
        entity.CalibrationSessionId = sessionId;

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
        var entities = await _participantRepository.GetQueryable(p => p.CalibrationSessionId == sessionId)
            .Include(p => p.Employee)
            .OrderBy(p => p.Employee.LastName)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<bool> RemoveParticipantAsync(Guid sessionId, Guid participantId, CancellationToken cancellationToken = default)
    {
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

    public async Task<CalibrationRatingAdjustmentDto> AddRatingAdjustmentAsync(Guid sessionId, CreateCalibrationRatingAdjustmentDto dto, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
            throw new ArgumentException("Calibration session not found.");

        // Guard: adjustments can only be recorded on an in-progress session
        if (session.Status == CalibrationStatus.Completed)
            throw new InvalidOperationException(
                "Rating adjustments cannot be added to a completed calibration session. " +
                "Reopen the session to make further adjustments.");

        if (session.Status == CalibrationStatus.Cancelled)
            throw new InvalidOperationException(
                "Rating adjustments cannot be added to a cancelled calibration session.");

        var entity = dto.ToEntity();
        entity.CalibrationSessionId = sessionId;
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
        var entities = await _adjustmentRepository.GetQueryable(
                a => a.CalibrationSessionId == sessionId && a.PerformanceAppraisalId == appraisalId)
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(ap => ap.Employee)
            .Include(a => a.AdjustedBy)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<CalibrationRatingAdjustmentDto> UpdateRatingAdjustmentAsync(Guid sessionId, UpdateCalibrationRatingAdjustmentDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _adjustmentRepository.GetQueryable()
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(ap => ap.Employee)
            .Include(a => a.AdjustedBy)
            .FirstOrDefaultAsync(a => a.Id == dto.Id && a.CalibrationSessionId == sessionId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Rating adjustment not found in this session.");

        // Guard: cannot edit adjustments on a closed session
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session?.Status == CalibrationStatus.Completed)
            throw new InvalidOperationException("Cannot modify adjustments on a completed calibration session.");
        if (session?.Status == CalibrationStatus.Cancelled)
            throw new InvalidOperationException("Cannot modify adjustments on a cancelled calibration session.");

        dto.UpdateEntity(entity);
        await _adjustmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Rating adjustment updated: {Id}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteRatingAdjustmentAsync(Guid sessionId, Guid adjustmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _adjustmentRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == adjustmentId && a.CalibrationSessionId == sessionId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Rating adjustment not found in this session.");

        await _adjustmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ─── Apply All Adjustments ────────────────────────────────────────────────

    public async Task<int> ApplyAllAdjustmentsAsync(Guid sessionId, Guid appliedById, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
            throw new ArgumentException("Calibration session not found.");

        if (session.Status != CalibrationStatus.Completed)
            throw new InvalidOperationException("Adjustments can only be applied to a completed session.");

        // Get all latest adjustments per appraisal (most recent wins)
        var adjustments = await _adjustmentRepository
            .GetQueryable(a => a.CalibrationSessionId == sessionId)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync(cancellationToken);

        // Group by appraisal and take the latest
        var latestPerAppraisal = adjustments
            .GroupBy(a => a.PerformanceAppraisalId)
            .ToDictionary(g => g.Key, g => g.First());

        int applied = 0;
        foreach (var (appraisalId, adjustment) in latestPerAppraisal)
        {
            var appraisal = await _appraisalRepository.GetByIdAsync(appraisalId);
            if (appraisal == null) continue;

            if (!appraisal.IsCalibrated)
                appraisal.PreCalibrationScore = appraisal.OverallScore;

            appraisal.OverallScore = adjustment.AdjustedScore;
            appraisal.IsCalibrated = true;
            appraisal.CalibrationSessionId = sessionId;

            await _appraisalRepository.UpdateAsync(appraisal);
            applied++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Applied {Count} calibration adjustments for session {SessionId}", applied, sessionId);
        return applied;
    }

    // ─── Calibration Matrix ───────────────────────────────────────────────────

    public async Task<CalibrationMatrixDto> GetCalibrationMatrixAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await BaseQuery.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new ArgumentException("Calibration session not found.");

        // Load all adjustments for the session
        var adjustments = await _adjustmentRepository
            .GetQueryable(a => a.CalibrationSessionId == sessionId)
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(a => a.Employee)
                    .ThenInclude(e => e.Position)
            .Include(a => a.AdjustedBy)
            .OrderByDescending(a => a.AdjustmentDate)
            .ToListAsync(cancellationToken);

        // Find all appraisals referenced by this session via adjustments
        var appraisalIds = adjustments.Select(a => a.PerformanceAppraisalId).Distinct().ToHashSet();

        var appraisals = await _appraisalRepository
            .GetQueryable(a => appraisalIds.Contains(a.Id))
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .ToListAsync(cancellationToken);

        var rows = new List<CalibrationMatrixRowDto>();
        foreach (var appraisal in appraisals)
        {
            var appraisalAdjustments = adjustments
                .Where(a => a.PerformanceAppraisalId == appraisal.Id)
                .OrderByDescending(a => a.AdjustmentDate)
                .ToList();

            var latestAdjustment = appraisalAdjustments.FirstOrDefault();
            var calibratedScore = latestAdjustment?.AdjustedScore;
            var preCalibration = appraisal.PreCalibrationScore ?? appraisal.OverallScore;

            rows.Add(new CalibrationMatrixRowDto
            {
                AppraisalId = appraisal.Id,
                EmployeeId = appraisal.EmployeeId,
                EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
                PositionName = appraisal.Employee.Position?.Title ?? string.Empty,
                ManagerProposedScore = null,
                PreCalibrationScore = preCalibration,
                CalibratedScore = calibratedScore,
                ScoreAdjustment = calibratedScore.HasValue && preCalibration.HasValue
                    ? calibratedScore.Value - preCalibration.Value
                    : null,
                AdjustmentRationale = latestAdjustment?.Rationale,
                IsCalibrated = appraisal.IsCalibrated,
                Adjustments = appraisalAdjustments.Select(a => a.ToDto()).ToList()
            });
        }

        return new CalibrationMatrixDto
        {
            SessionId = session.Id,
            SessionName = session.SessionName,
            OrganizationUnitName = session.OrganizationUnit?.Name,
            TotalEmployees = rows.Count,
            AdjustedCount = rows.Count(r => r.IsCalibrated),
            Rows = rows
        };
    }

    // ─── Attachments ─────────────────────────────────────────────────────────

    public async Task<AppraisalAttachmentDto> AddAttachmentAsync(Guid sessionId, CreateAppraisalAttachmentDto dto, CancellationToken cancellationToken = default)
    {
        var exists = await _sessionRepository.ExistsAsync(s => s.Id == sessionId);
        if (!exists)
            throw new ArgumentException("Calibration session not found.");

        var entity = dto.ToEntity();
        entity.CalibrationSessionId = sessionId;
        entity.EntityType = AppraisalAttachmentEntityType.CalibrationSession;
        entity.UploadDate = DateTime.UtcNow;

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment added to calibration session {SessionId}: {AttachmentId}", sessionId, entity.Id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entities = await _attachmentRepository.GetQueryable(a => a.CalibrationSessionId == sessionId)
            .Include(a => a.UploadedBy)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid sessionId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _attachmentRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CalibrationSessionId == sessionId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Attachment not found.");

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

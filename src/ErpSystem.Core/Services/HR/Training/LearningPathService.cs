using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class LearningPathService : ILearningPathService
{
    private readonly ILearningPathRepository _pathRepository;
    private readonly ILearningPathProgramRepository _pathProgramRepository;
    private readonly ILearningPathSkillRepository _pathSkillRepository;
    private readonly IEmployeeLearningPathRepository _enrollmentRepository;
    private readonly IEmployeeLearningPathStepRepository _stepRepository;
    private readonly ITrainingScheduleService _scheduleService;
    private readonly ITrainingNominationService _nominationService;
    private readonly ITrainingStatusHistoryService _statusHistoryService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LearningPathService> _logger;

    /// <summary>The <c>TrainingStatusHistory.EntityType</c> discriminator for step rows.</summary>
    public const string StepHistoryEntityType = "EmployeeLearningPathStep";

    public LearningPathService(
        ILearningPathRepository pathRepository,
        ILearningPathProgramRepository pathProgramRepository,
        ILearningPathSkillRepository pathSkillRepository,
        IEmployeeLearningPathRepository enrollmentRepository,
        IEmployeeLearningPathStepRepository stepRepository,
        ITrainingScheduleService scheduleService,
        ITrainingNominationService nominationService,
        ITrainingStatusHistoryService statusHistoryService,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<LearningPathService> logger)
    {
        _pathRepository = pathRepository;
        _pathProgramRepository = pathProgramRepository;
        _pathSkillRepository = pathSkillRepository;
        _enrollmentRepository = enrollmentRepository;
        _stepRepository = stepRepository;
        _scheduleService = scheduleService;
        _nominationService = nominationService;
        _statusHistoryService = statusHistoryService;
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

    // A row owned by another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<LearningPath> GetOwnedPathAsync(Guid id)
    {
        var entity = await _pathRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Learning path with ID '{id}' not found.");
        return entity;
    }

    private async Task<LearningPath> GetOwnedPathWithDetailsAsync(Guid id)
    {
        var entity = await _pathRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Learning path with ID '{id}' not found.");
        return entity;
    }

    private async Task<LearningPathProgram> GetOwnedPathProgramAsync(Guid id)
    {
        var entity = await _pathProgramRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Learning path program with ID '{id}' not found.");
        return entity;
    }

    private async Task<LearningPathSkill> GetOwnedPathSkillAsync(Guid id)
    {
        var entity = await _pathSkillRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Learning path skill with ID '{id}' not found.");
        return entity;
    }

    private async Task<EmployeeLearningPath> GetOwnedEnrollmentAsync(Guid id)
    {
        var entity = await _enrollmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Learning path enrollment with ID '{id}' not found.");
        return entity;
    }

    private async Task<EmployeeLearningPath> GetOwnedEnrollmentWithDetailsAsync(Guid id)
    {
        var entity = await _enrollmentRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Learning path enrollment with ID '{id}' not found.");
        return entity;
    }

    private async Task<EmployeeLearningPathStep> GetOwnedStepAsync(Guid id)
    {
        var entity = await _stepRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Learning path step with ID '{id}' not found.");
        return entity;
    }

    // ── Learning path queries ─────────────────────────────────────────────────

    public async Task<LearningPathDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPathWithDetailsAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<LearningPathSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pathRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<LearningPathSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pathRepository.GetActiveAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<LearningPathSummaryDto>> GetByStatusAsync(LearningPathStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pathRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<LearningPathSummaryDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pathRepository.GetByPositionAsync(positionId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<LearningPathDto> CreateAsync(CreateLearningPathDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _pathRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Learning path created: {PathName}", dto.Name);

        // Freshly written: no scope navigations or Programs loaded, so the response would show a
        // blank unit/position and "0 programmes".
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<LearningPathDto> UpdateAsync(UpdateLearningPathDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPathAsync(dto.Id);

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _pathRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPathAsync(id);

        await _pathRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Learning path {PathId} deleted", id);

        return true;
    }

    // ── Program sub-operations ────────────────────────────────────────────────

    public async Task<LearningPathProgramDto> AddProgramAsync(CreateLearningPathProgramDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedPathAsync(dto.LearningPathId);

        var entity = dto.ToEntity(current, createdByUserId);

        await _pathProgramRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var savedProgram = await _pathProgramRepository.GetByIdWithNavigationsAsync(entity.Id);
        return (savedProgram ?? entity).ToDto();
    }

    public async Task<IEnumerable<LearningPathProgramDto>> GetProgramsAsync(Guid learningPathId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pathProgramRepository.GetByLearningPathIdAsync(learningPathId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<LearningPathProgramDto> UpdateProgramAsync(UpdateLearningPathProgramDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPathProgramAsync(dto.Id);

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _pathProgramRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var savedProgram = await _pathProgramRepository.GetByIdWithNavigationsAsync(entity.Id);
        return (savedProgram ?? entity).ToDto();
    }

    public async Task<bool> DeleteProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPathProgramAsync(id);

        await _pathProgramRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Skill sub-operations ──────────────────────────────────────────────────

    public async Task<LearningPathSkillDto> AddSkillAsync(CreateLearningPathSkillDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedPathAsync(dto.LearningPathId);

        var entity = dto.ToEntity(current, createdByUserId);

        await _pathSkillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var savedSkill = await _pathSkillRepository.GetByIdAsync(entity.Id);
        return (savedSkill ?? entity).ToDto();
    }

    public async Task<IEnumerable<LearningPathSkillDto>> GetSkillsAsync(Guid learningPathId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pathSkillRepository.GetByLearningPathIdAsync(learningPathId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteSkillAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPathSkillAsync(id);

        await _pathSkillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Enrollment operations ─────────────────────────────────────────────────

    public async Task<EmployeeLearningPathDto> GetEnrollmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEnrollmentWithDetailsAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeLearningPathSummaryDto>> GetEnrollmentsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _enrollmentRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<EmployeeLearningPathDto> EnrollEmployeeAsync(EnrollEmployeeInLearningPathDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedPathWithDetailsAsync(dto.LearningPathId);

        var enrollment = dto.ToEntity(current, createdByUserId);

        // AssignedById is optional on the DTO and the mapper copies it straight through, so an
        // enrolment made from a screen recorded nobody as having assigned it. Default it to the
        // caller — whoever enrols someone IS the assigner — while still honouring an explicit id,
        // since HR can legitimately record an enrolment on a line manager's behalf.
        enrollment.AssignedById ??= createdByUserId;

        enrollment.EnrolledDate = DateTime.UtcNow;
        enrollment.ProgressPercentage = 0;
        enrollment.IsCompleted = false;

        await _enrollmentRepository.AddAsync(enrollment);

        // Create a step for each program in the learning path. Track the enrollment and all steps,
        // then persist them in a SINGLE SaveChanges so the enrollment can never be left step-less
        // (which would otherwise pin progress at 0% forever). enrollment.Id is assigned client-side.
        var programs = (await _pathProgramRepository.GetByLearningPathIdAsync(dto.LearningPathId))
            .Where(p => p.TenantId == current);

        foreach (var program in programs)
        {
            var step = new EmployeeLearningPathStep
            {
                Id = Guid.NewGuid(),
                TenantId = current,
                EmployeeLearningPathId = enrollment.Id,
                LearningPathProgramId = program.Id,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdByUserId.ToString()
            };

            await _stepRepository.AddAsync(step);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee {EmployeeId} enrolled in learning path {LearningPathId}", dto.EmployeeId, dto.LearningPathId);

        // Freshly written: no Employee/AssignedBy loaded, so the response named neither the learner
        // nor who assigned it — on the one response a caller is most likely to render directly.
        return await GetEnrollmentByIdAsync(enrollment.Id, cancellationToken);
    }

    public async Task<EmployeeLearningPathDto> RecalculateProgressAsync(Guid enrollmentId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId);
        var tenantId = enrollment.TenantId;

        // Rows belonging to another tenant would not merely leak: they would be counted into the
        // mandatory totals and silently corrupt the stored progress percentage.
        var steps = (await _stepRepository.GetByEmployeeLearningPathIdAsync(enrollmentId))
            .Where(s => s.TenantId == tenantId).ToList();
        var programs = (await _pathProgramRepository.GetByLearningPathIdAsync(enrollment.LearningPathId))
            .Where(p => p.TenantId == tenantId);
        var mandatoryProgramIds = programs.Where(p => p.IsMandatory).Select(p => p.Id).ToHashSet();

        var mandatorySteps = steps.Where(s => mandatoryProgramIds.Contains(s.LearningPathProgramId)).ToList();
        var completedMandatory = mandatorySteps.Count(s => s.IsCompleted);
        var totalMandatory = mandatorySteps.Count;

        enrollment.ProgressPercentage = totalMandatory > 0
            ? (int)Math.Round((double)completedMandatory / totalMandatory * 100)
            : 0;

        if (totalMandatory > 0 && completedMandatory == totalMandatory)
        {
            enrollment.IsCompleted = true;
            enrollment.ActualCompletionDate = DateTime.UtcNow;
        }

        enrollment.UpdatedAt = DateTime.UtcNow;
        enrollment.UpdatedBy = updatedByUserId.ToString();

        await _enrollmentRepository.UpdateAsync(enrollment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Progress recalculated for enrollment {EnrollmentId}: {Progress}%", enrollmentId, enrollment.ProgressPercentage);

        return enrollment.ToDto();
    }

    /// <summary>
    /// Completing or reopening one step of an enrolment.
    ///
    /// Three rules apply, in this order:
    ///   1. Only the learner or HR may touch the step at all. Tenant scope alone let any employee
    ///      complete a colleague's step.
    ///   2. A learner may only confirm a completion that evidence already supports.
    ///   3. HR may complete without evidence, but must say why — and that override is recorded.
    ///
    /// The gate is deliberately not absolute: self-paced e-learning, prior or external learning,
    /// migrated history and plain admin corrections are all legitimate completions with no
    /// attendance row behind them. Blocking them outright does not stop people recording them, it
    /// pushes them to fabricate an attendance record instead — which destroys the very evidence the
    /// rule exists to protect. So the override is allowed, named, and written down.
    /// </summary>
    public async Task<EmployeeLearningPathStepDto> UpdateStepAsync(UpdateLearningPathStepDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // The context load also gives us the nomination and completion record the evidence test needs.
        var entity = await _stepRepository.GetStepWithContextAsync(dto.Id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Learning path step with ID '{dto.Id}' not found.");

        var learnerId = entity.EmployeeLearningPath.EmployeeId;
        var isLearner = learnerId == actorEmployeeId;
        var isHr      = _currentUserProvider.HasRole(Constants.Roles.Hr)
                     || _currentUserProvider.HasRole(Constants.Roles.SuperAdmin)
                     || _currentUserProvider.HasRole(Constants.Roles.TenantAdmin);

        // Rule 1 — ownership. TrainingBusinessRulesAttribute turns this into a 403 carrying the message.
        if (!isLearner && !isHr)
            throw new UnauthorizedAccessException(
                "This step belongs to another employee. Only the learner or HR can update it.");

        var wasCompleted = entity.IsCompleted;
        var toStatus = LearningPathStepStatus.NotCompleted;
        string? overrideReason = null;

        if (dto.IsCompleted)
        {
            var hasEvidence = await HasCompletionEvidenceAsync(entity, learnerId, cancellationToken);

            if (hasEvidence)
            {
                toStatus = LearningPathStepStatus.Completed;
            }
            else if (!isHr)
            {
                // Rule 2 — a learner cannot self-certify. 422 with this message, via the filter.
                throw new InvalidOperationException(
                    "This step has no attendance or completion record behind it yet, so it cannot be marked "
                  + "complete here. Attend a scheduled run, or ask HR to record it with a reason.");
            }
            else
            {
                // Rule 3 — HR may override, but not silently.
                if (string.IsNullOrWhiteSpace(dto.Reason))
                    throw new InvalidOperationException(
                        "There is no attendance or completion record behind this step. Give a reason for "
                      + "recording it complete (for example prior learning, or self-paced study).");

                toStatus = LearningPathStepStatus.CompletedByOverride;
                overrideReason = dto.Reason.Trim();
            }
        }

        entity.UpdateEntity(dto, actorEmployeeId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = actorEmployeeId.ToString();

        await _stepRepository.UpdateAsync(entity);

        // Only a real transition is worth a row; re-saving an unchanged step should not pad the trail.
        if (wasCompleted != dto.IsCompleted)
        {
            await _statusHistoryService.RecordAsync(
                entityType:          StepHistoryEntityType,
                entityId:            entity.Id,
                entityReference:     entity.LearningPathProgram?.Program?.ProgramName,
                fromStatus:          (int)(wasCompleted ? LearningPathStepStatus.Completed : LearningPathStepStatus.NotCompleted),
                fromStatusName:      wasCompleted ? "Completed" : "Not completed",
                toStatus:            (int)toStatus,
                toStatusName:        toStatus switch
                                     {
                                         LearningPathStepStatus.Completed           => "Completed — evidenced",
                                         LearningPathStepStatus.CompletedByOverride => "Completed — recorded by HR",
                                         _                                          => "Reopened",
                                     },
                changedByEmployeeId: actorEmployeeId,
                reason:              overrideReason,
                cancellationToken:   cancellationToken);
        }

        // One SaveChanges commits the step and its audit row together.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (entity.IsCompleted)
            await RecalculateProgressAsync(entity.EmployeeLearningPathId, actorEmployeeId, cancellationToken);

        var savedStep = await _stepRepository.GetByIdWithNavigationsAsync(entity.Id);
        return (savedStep ?? entity).ToDto();
    }

    /// <summary>
    /// Whether anything on the record justifies calling this step complete: the learner was marked
    /// present on the nominated run, or a completion record exists for it.
    /// </summary>
    private async Task<bool> HasCompletionEvidenceAsync(
        EmployeeLearningPathStep step, Guid learnerId, CancellationToken cancellationToken)
    {
        if (step.Nomination?.CompletionRecord != null)
            return true;

        if (step.NominationId == null || step.Nomination == null)
            return false;

        var attendance = await _nominationService.GetAttendanceForScheduleAsync(
            step.Nomination.ScheduleId, cancellationToken);

        return attendance.Any(a => a.EmployeeId == learnerId && a.IsPresent);
    }

    public async Task<IEnumerable<EmployeeLearningPathSummaryDto>> GetEnrollmentsByPathIdAsync(Guid pathId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _enrollmentRepository.GetByLearningPathIdAsync(pathId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<bool> RemoveEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEnrollmentAsync(enrollmentId);

        await _enrollmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<EnrollmentListItemDto>> GetAllEnrollmentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _enrollmentRepository.GetAllWithDetailsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToEnrollmentListItemDto());
    }

    public async Task<EmployeeLearningPathDto> UpdateEnrollmentAsync(UpdateEnrollmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEnrollmentWithDetailsAsync(dto.Id);

        entity.TargetCompletionDate = dto.TargetCompletionDate;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _enrollmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetEnrollmentByIdAsync(entity.Id, cancellationToken);
    }

    // ── Step detail (employee self-service) ───────────────────────────────────

    public async Task<StepDetailPageDto> GetStepDetailAsync(Guid stepId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // A step owned by another tenant is reported as missing rather than forbidden, so the endpoint does
        // not confirm that the id exists elsewhere.
        var step = await _stepRepository.GetStepWithContextAsync(stepId);
        if (step == null || step.TenantId != tenantId)
            throw new ArgumentException($"Step '{stepId}' not found.");

        var lpp        = step.LearningPathProgram;
        var program    = lpp.Program;
        var enrollment = step.EmployeeLearningPath;

        // The records below belong to whoever is enrolled, not to whoever is looking. HR opening
        // someone else's step from the org-wide list would otherwise see its own attendance and
        // feedback presented as the learner's — and, worse, have canMarkComplete decided by it.
        var learnerId = enrollment.EmployeeId;

        // Sibling steps drive the displayed sequence and total, so foreign rows would not merely leak:
        // they would shift this step's position and inflate the step count.
        var allSteps = enrollment.Steps
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.LearningPathProgram?.SequenceOrder ?? 0)
            .ToList();

        var stepSeq   = allSteps.FindIndex(s => s.Id == stepId) + 1;
        var totalSteps = allSteps.Count;

        // Lock detection
        bool isLocked = false;
        string? prereqName = null;
        if (lpp.PrerequisitePathProgramId.HasValue)
        {
            var prereqStep = allSteps.FirstOrDefault(s => s.LearningPathProgramId == lpp.PrerequisitePathProgramId.Value);
            if (prereqStep is { IsCompleted: false })
            {
                isLocked  = true;
                prereqName = lpp.PrerequisitePathProgram?.Program?.ProgramName;
            }
        }

        // Available schedules
        var schedules = (await _scheduleService.GetByProgramIdAsync(program.Id, cancellationToken)).ToList();

        // Nomination / attendance / completion / feedback
        StepNominationDto?       myNomination = null;
        List<StepAttendanceDto>  myAttendance = new();
        StepCompletionDto?       myCompletion = null;
        StepFeedbackDto?         myFeedback   = null;

        if (step.NominationId.HasValue)
        {
            var nom = await _nominationService.GetByIdAsync(step.NominationId.Value, cancellationToken);

            myNomination = new StepNominationDto
            {
                Id                = nom.Id,
                NominationNumber  = nom.NominationNumber,
                ScheduleId        = nom.ScheduleId,
                ScheduleNumber    = nom.ScheduleNumber,
                TrainingStartDate = nom.TrainingStartDate,
                TrainingEndDate   = nom.TrainingEndDate,
                Status            = nom.Status,
                NominationDate    = nom.NominationDate,
                Justification     = nom.Justification
            };

            // Attendance records for this employee on that schedule
            var allAtt = await _nominationService.GetAttendanceForScheduleAsync(nom.ScheduleId, cancellationToken);
            myAttendance = allAtt
                .Where(a => a.EmployeeId == learnerId)
                .Select(a => new StepAttendanceDto
                {
                    Id             = a.Id,
                    AttendanceDate = a.AttendanceDate,
                    IsPresent      = a.IsPresent,
                    AbsenceReason  = a.AbsenceReason,
                    CheckInTime    = a.CheckInTime,
                    CheckOutTime   = a.CheckOutTime
                }).ToList();

            // Completion from the Include chain (avoids extra DB round-trip)
            var comp = step.Nomination?.CompletionRecord;
            if (comp != null)
            {
                myCompletion = new StepCompletionDto
                {
                    Id                  = comp.Id,
                    CompletionDate      = comp.CompletionDate,
                    FinalScore          = comp.FinalScore,
                    IsPassed            = comp.IsPassed,
                    Status              = comp.Status,
                    IsVerifiedByManager = comp.IsVerifiedByManager
                };
            }

            // Feedback for this employee on that schedule
            var allFb = await _nominationService.GetFeedbackForScheduleAsync(nom.ScheduleId, cancellationToken);
            var myFb  = allFb.FirstOrDefault(f => f.EmployeeId == learnerId);
            if (myFb != null)
            {
                myFeedback = new StepFeedbackDto
                {
                    Id                       = myFb.Id,
                    ContentRelevanceRating   = myFb.ContentRelevanceRating,
                    TrainerKnowledgeRating   = myFb.TrainerKnowledgeRating,
                    DeliveryMethodRating     = myFb.DeliveryMethodRating,
                    MaterialQualityRating    = myFb.MaterialQualityRating,
                    OverallSatisfactionRating = myFb.OverallSatisfactionRating,
                    StrengthsOfTraining      = myFb.StrengthsOfTraining,
                    AreasForImprovement      = myFb.AreasForImprovement,
                    SuggestionsForFuture     = myFb.SuggestionsForFuture,
                    WouldRecommend           = myFb.WouldRecommend,
                    FeedbackDate             = myFb.FeedbackDate
                };
            }
        }

        // Deliberately the same helper the write path enforces with, so the button's enabled state and
        // the rule that rejects the request cannot drift apart.
        bool canMarkComplete = await HasCompletionEvidenceAsync(step, learnerId, cancellationToken);

        return new StepDetailPageDto
        {
            StepId                  = step.Id,
            EnrollmentId            = enrollment.Id,
            LearningPathName        = enrollment.LearningPath?.Name ?? string.Empty,
            StepSequence            = stepSeq,
            TotalSteps              = totalSteps,
            IsCompleted             = step.IsCompleted,
            CompletedDate           = step.CompletedDate,
            IsLocked                = isLocked,
            PrerequisiteProgramName = prereqName,
            IsMandatory             = lpp.IsMandatory,
            CanMarkComplete         = canMarkComplete,
            LearnerId               = learnerId,
            LearnerName             = enrollment.Employee?.FullName ?? string.Empty,
            IsOwnStep               = learnerId == employeeId,
            ProgramId               = program.Id,
            ProgramCode             = program.ProgramCode,
            ProgramName             = program.ProgramName,
            Description             = program.Description,
            Level                   = program.Level,
            DurationDays            = program.DurationDays,
            DurationHours           = program.DurationHours,
            LearningObjectives      = program.LearningObjectives,
            Prerequisites           = program.Prerequisites,
            ProvidesCertificate     = program.ProvidesCertificate,
            CertificateName         = program.CertificateName,
            Materials = program.Materials.Select(m => new StepMaterialDto
            {
                Id           = m.Id,
                MaterialName = m.MaterialName,
                Type         = m.Type,
                FilePath     = m.FilePath,
                ExternalUrl  = m.ExternalUrl,
                IsPublic     = m.IsPublic
            }).ToList(),
            AvailableSchedules = schedules.Select(s => new StepScheduleSummaryDto
            {
                Id                         = s.Id,
                ScheduleNumber             = s.ScheduleNumber,
                StartDate                  = s.StartDate,
                EndDate                    = s.EndDate,
                Venue                      = s.Venue,
                TrainerName                = s.TrainerName,
                VendorName                 = s.VendorName,
                MaxParticipants            = s.MaxParticipants,
                ConfirmedParticipantsCount = s.ConfirmedParticipantsCount,
                RegistrationCloseDate      = s.RegistrationCloseDate,
                Status                     = s.Status,
                IsRegistrationOpen         = s.Status == ErpSystem.Core.Enums.ScheduleStatus.RegistrationOpen
                                             && s.RegistrationCloseDate >= DateTime.UtcNow
            }).ToList(),
            MyNomination = myNomination,
            MyAttendance = myAttendance,
            MyCompletion = myCompletion,
            MyFeedback   = myFeedback
        };
    }
}

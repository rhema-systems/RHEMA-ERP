using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LearningPathService> _logger;

    public LearningPathService(
        ILearningPathRepository pathRepository,
        ILearningPathProgramRepository pathProgramRepository,
        ILearningPathSkillRepository pathSkillRepository,
        IEmployeeLearningPathRepository enrollmentRepository,
        IEmployeeLearningPathStepRepository stepRepository,
        ITrainingScheduleService scheduleService,
        ITrainingNominationService nominationService,
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
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Learning path queries ─────────────────────────────────────────────────

    public async Task<LearningPathDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _pathRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Learning path with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<LearningPathSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _pathRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<LearningPathSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _pathRepository.GetActiveAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<LearningPathSummaryDto>> GetByStatusAsync(LearningPathStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _pathRepository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<LearningPathSummaryDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var entities = await _pathRepository.GetByPositionAsync(positionId);
        return entities.ToSummaryDtoList();
    }

    public async Task<LearningPathDto> CreateAsync(CreateLearningPathDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _pathRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Learning path created: {PathName}", dto.Name);

        return entity.ToDto();
    }

    public async Task<LearningPathDto> UpdateAsync(UpdateLearningPathDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _pathRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Learning path with ID '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _pathRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _pathRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Learning path with ID '{id}' not found.");

        await _pathRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Learning path {PathId} deleted", id);

        return true;
    }

    // ── Program sub-operations ────────────────────────────────────────────────

    public async Task<LearningPathProgramDto> AddProgramAsync(CreateLearningPathProgramDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var path = await _pathRepository.GetByIdAsync(dto.LearningPathId);

        if (path == null)
            throw new ArgumentException($"Learning path with ID '{dto.LearningPathId}' not found.");

        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _pathProgramRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<LearningPathProgramDto>> GetProgramsAsync(Guid learningPathId, CancellationToken cancellationToken = default)
    {
        var entities = await _pathProgramRepository.GetByLearningPathIdAsync(learningPathId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<LearningPathProgramDto> UpdateProgramAsync(UpdateLearningPathProgramDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _pathProgramRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Learning path program with ID '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _pathProgramRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _pathProgramRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Learning path program with ID '{id}' not found.");

        await _pathProgramRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Skill sub-operations ──────────────────────────────────────────────────

    public async Task<LearningPathSkillDto> AddSkillAsync(CreateLearningPathSkillDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var path = await _pathRepository.GetByIdAsync(dto.LearningPathId);

        if (path == null)
            throw new ArgumentException($"Learning path with ID '{dto.LearningPathId}' not found.");

        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _pathSkillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<LearningPathSkillDto>> GetSkillsAsync(Guid learningPathId, CancellationToken cancellationToken = default)
    {
        var entities = await _pathSkillRepository.GetByLearningPathIdAsync(learningPathId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteSkillAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _pathSkillRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Learning path skill with ID '{id}' not found.");

        await _pathSkillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Enrollment operations ─────────────────────────────────────────────────

    public async Task<EmployeeLearningPathDto> GetEnrollmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _enrollmentRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Learning path enrollment with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeLearningPathSummaryDto>> GetEnrollmentsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _enrollmentRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<EmployeeLearningPathDto> EnrollEmployeeAsync(EnrollEmployeeInLearningPathDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var path = await _pathRepository.GetWithFullDetailsAsync(dto.LearningPathId);

        if (path == null)
            throw new ArgumentException($"Learning path with ID '{dto.LearningPathId}' not found.");

        var enrollment = dto.ToEntity(tenantId, createdByUserId);
        enrollment.EnrolledDate = DateTime.UtcNow;
        enrollment.ProgressPercentage = 0;
        enrollment.IsCompleted = false;

        await _enrollmentRepository.AddAsync(enrollment);

        // Create a step for each program in the learning path. Track the enrollment and all steps,
        // then persist them in a SINGLE SaveChanges so the enrollment can never be left step-less
        // (which would otherwise pin progress at 0% forever). enrollment.Id is assigned client-side.
        var programs = await _pathProgramRepository.GetByLearningPathIdAsync(dto.LearningPathId);

        foreach (var program in programs)
        {
            var step = new EmployeeLearningPathStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
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

        return enrollment.ToDto();
    }

    public async Task<EmployeeLearningPathDto> RecalculateProgressAsync(Guid enrollmentId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(enrollmentId);

        if (enrollment == null)
            throw new ArgumentException($"Learning path enrollment with ID '{enrollmentId}' not found.");

        var steps = (await _stepRepository.GetByEmployeeLearningPathIdAsync(enrollmentId)).ToList();
        var programs = await _pathProgramRepository.GetByLearningPathIdAsync(enrollment.LearningPathId);
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

    public async Task<EmployeeLearningPathStepDto> UpdateStepAsync(UpdateLearningPathStepDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _stepRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Learning path step with ID '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _stepRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (entity.IsCompleted)
            await RecalculateProgressAsync(entity.EmployeeLearningPathId, updatedByUserId, cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeLearningPathSummaryDto>> GetEnrollmentsByPathIdAsync(Guid pathId, CancellationToken cancellationToken = default)
    {
        var entities = await _enrollmentRepository.GetByLearningPathIdAsync(pathId);
        return entities.ToSummaryDtoList();
    }

    public async Task<bool> RemoveEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _enrollmentRepository.GetByIdAsync(enrollmentId);
        if (entity == null)
            throw new ArgumentException($"Learning path enrollment with ID '{enrollmentId}' not found.");

        await _enrollmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<EnrollmentListItemDto>> GetAllEnrollmentsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _enrollmentRepository.GetAllWithDetailsAsync();
        return entities.Select(e => e.ToEnrollmentListItemDto());
    }

    public async Task<EmployeeLearningPathDto> UpdateEnrollmentAsync(UpdateEnrollmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _enrollmentRepository.GetWithFullDetailsAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Learning path enrollment with ID '{dto.Id}' not found.");

        entity.TargetCompletionDate = dto.TargetCompletionDate;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _enrollmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    // ── Step detail (employee self-service) ───────────────────────────────────

    public async Task<StepDetailPageDto> GetStepDetailAsync(Guid stepId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var step = await _stepRepository.GetStepWithContextAsync(stepId)
            ?? throw new ArgumentException($"Step '{stepId}' not found.");

        var lpp        = step.LearningPathProgram;
        var program    = lpp.Program;
        var enrollment = step.EmployeeLearningPath;

        var allSteps = enrollment.Steps
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
                .Where(a => a.EmployeeId == employeeId)
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
            var myFb  = allFb.FirstOrDefault(f => f.EmployeeId == employeeId);
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

        bool canMarkComplete = myAttendance.Any(a => a.IsPresent) || myCompletion != null;

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

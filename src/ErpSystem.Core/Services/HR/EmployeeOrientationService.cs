using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class EmployeeOrientationService : IEmployeeOrientationService
{
    private readonly IEmployeeOrientationRepository _enrollmentRepository;
    private readonly IOrientationProgramRepository _programRepository;
    private readonly IOrientationSessionRepository _sessionRepository;
    private readonly IOrientationContentItemRepository _contentItemRepository;
    private readonly IOrientationModuleRepository _moduleRepository;
    private readonly IOrientationContentProgressRepository _contentProgressRepository;
    private readonly IOrientationAssessmentQuestionRepository _questionRepository;
    private readonly IOrientationAssessmentResponseRepository _responseRepository;
    private readonly IOrientationAcknowledgementRepository _acknowledgementRepository;
    private readonly IOrientationFeedbackRepository _feedbackRepository;
    private readonly IOrientationCertificateRepository _certificateRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeOrientationService> _logger;

    public EmployeeOrientationService(
        IEmployeeOrientationRepository enrollmentRepository,
        IOrientationProgramRepository programRepository,
        IOrientationSessionRepository sessionRepository,
        IOrientationContentItemRepository contentItemRepository,
        IOrientationModuleRepository moduleRepository,
        IOrientationContentProgressRepository contentProgressRepository,
        IOrientationAssessmentQuestionRepository questionRepository,
        IOrientationAssessmentResponseRepository responseRepository,
        IOrientationAcknowledgementRepository acknowledgementRepository,
        IOrientationFeedbackRepository feedbackRepository,
        IOrientationCertificateRepository certificateRepository,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeOrientationService> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _programRepository = programRepository;
        _sessionRepository = sessionRepository;
        _contentItemRepository = contentItemRepository;
        _moduleRepository = moduleRepository;
        _contentProgressRepository = contentProgressRepository;
        _questionRepository = questionRepository;
        _responseRepository = responseRepository;
        _acknowledgementRepository = acknowledgementRepository;
        _feedbackRepository = feedbackRepository;
        _certificateRepository = certificateRepository;
        _currentUserProvider = currentUserProvider;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    /// <summary>
    /// True when the caller administers orientation rather than merely participating in it.
    /// Matches the <c>IsHr</c> convention the appraisal and check-in controllers already use.
    /// </summary>
    private bool IsHrActor =>
        _currentUserProvider.Roles.Any(r =>
            string.Equals(r, Constants.Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r, Constants.Roles.Hr, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r, Constants.Roles.LegacyHrUser, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Entitlement test for a record addressed by id: HR sees every enrollment, everyone else sees
    /// only their own.
    ///
    /// This lives on the ownership helper rather than in the controller deliberately — every
    /// id-addressed operation in this service (read, update, withdraw, track progress, sit the
    /// assessment, sign, give feedback, read certificates) already funnels through here, so the rule
    /// cannot be bypassed by a new endpoint that forgets to ask. Tenant scoping alone let any
    /// authenticated employee read, and submit an assessment against, a colleague's enrollment.
    ///
    /// Note this grants *access to the record*, not permission to perform HR-only acts on it —
    /// issuing and revoking certificates are gated separately at the controller.
    /// </summary>
    private void RequireEnrollmentAccess(EmployeeOrientation enrollment)
    {
        if (IsHrActor) return;
        if (_currentUser.EmployeeId is { } me && me != Guid.Empty && enrollment.EmployeeId == me) return;

        throw new UnauthorizedAccessException("You do not have access to this orientation enrollment.");
    }

    /// <summary>HR may read anyone's list; everyone else only their own.</summary>
    private void RequireEmployeeAccess(Guid employeeId)
    {
        if (IsHrActor) return;
        if (_currentUser.EmployeeId is { } me && me != Guid.Empty && employeeId == me) return;

        throw new UnauthorizedAccessException("You do not have access to this employee's orientation records.");
    }

    private async Task<EmployeeOrientation> GetOwnedEnrollmentAsync(Guid id)
    {
        var entity = await _enrollmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation enrollment with ID '{id}' not found.");
        RequireEnrollmentAccess(entity);
        return entity;
    }

    private async Task<EmployeeOrientation> GetOwnedEnrollmentWithDetailsAsync(Guid id)
    {
        var entity = await _enrollmentRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation enrollment with ID '{id}' not found.");
        RequireEnrollmentAccess(entity);
        return entity;
    }

    private async Task<OrientationAcknowledgement> GetOwnedAcknowledgementAsync(Guid id)
    {
        var entity = await _acknowledgementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation acknowledgement with ID '{id}' not found.");

        // Reached by id rather than through the enrollment, so the entitlement test has to be repeated
        // here: signing is a personal legal act — "I have read and understood" — and without this any
        // authenticated user could sign a declaration in someone else's name, hash and IP recorded.
        var enrollment = await _enrollmentRepository.GetByIdAsync(entity.EmployeeOrientationId);
        if (enrollment == null || enrollment.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation acknowledgement with ID '{id}' not found.");
        RequireEnrollmentAccess(enrollment);

        return entity;
    }

    private async Task<OrientationCertificate> GetOwnedCertificateAsync(Guid id)
    {
        var entity = await _certificateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation certificate with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationProgram> GetOwnedProgramAsync(Guid id)
    {
        var entity = await _programRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation program with ID '{id}' not found.");
        return entity;
    }

    // ====================================================================
    // QUERIES
    // ====================================================================

    public async Task<EmployeeOrientationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEnrollmentWithDetailsAsync(id);
        var dto = entity.ToDto();
        var map = await _unitOfWork.ResolveEmployeesAsync(GetTenantId(), dto.EmployeeIds());
        dto.FillNames(map);
        return dto;
    }

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        RequireEmployeeAccess(employeeId);
        return await HydrateSummariesAsync(
            (await _enrollmentRepository.GetByEmployeeIdAsync(employeeId)).Where(e => e.TenantId == tenantId));
    }

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetByProgramIdAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateSummariesAsync(
            (await _enrollmentRepository.GetByProgramIdAsync(programId)).Where(e => e.TenantId == tenantId));
    }

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateSummariesAsync(
            (await _enrollmentRepository.GetBySessionIdAsync(sessionId)).Where(e => e.TenantId == tenantId));
    }

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetByCompletionStatusAsync(OrientationCompletionStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateSummariesAsync(
            (await _enrollmentRepository.GetByCompletionStatusAsync(status)).Where(e => e.TenantId == tenantId));
    }

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateSummariesAsync(
            (await _enrollmentRepository.GetOverdueAsync()).Where(e => e.TenantId == tenantId));
    }

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetDueSoonAsync(int daysAhead = 7, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateSummariesAsync(
            (await _enrollmentRepository.GetDueSoonAsync(daysAhead)).Where(e => e.TenantId == tenantId));
    }

    public async Task<PagedResult<EmployeeOrientationSummaryDto>> GetPagedByProgramAsync(Guid programId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _enrollmentRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && e.ProgramId == programId)
            .Include(e => e.Program)
            .Include(e => e.Session);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.EnrolledAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<EmployeeOrientationSummaryDto>
        {
            Items = await HydrateSummariesAsync(items),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>Maps enrollments to summary DTOs and fills employee display names in one keyed lookup.</summary>
    private async Task<List<EmployeeOrientationSummaryDto>> HydrateSummariesAsync(IEnumerable<EmployeeOrientation> entities)
    {
        var list = entities.ToSummaryDtoList().ToList();
        var map = await _unitOfWork.ResolveEmployeesAsync(GetTenantId(), list.EmployeeIds());
        list.FillNames(map);
        return list;
    }

    /// <summary>
    /// Re-reads an enrollment through the full-details chain and fills employee names, so a write
    /// response carries the same fields the read does. Entities reference employees by id with no
    /// navigation, so without the hydration step every create/update response comes back nameless.
    /// </summary>
    private async Task<EmployeeOrientationDto> ReadEnrollmentAsync(Guid id)
    {
        var dto = (await _enrollmentRepository.GetWithFullDetailsAsync(id))!.ToDto();
        var map = await _unitOfWork.ResolveEmployeesAsync(GetTenantId(), dto.EmployeeIds());
        dto.FillNames(map);
        return dto;
    }

    // ====================================================================
    // ENROLLMENT
    // ====================================================================

    public async Task<EmployeeOrientationDto> EnrollAsync(CreateEmployeeOrientationDto createDto, Guid tenantId, Guid enrolledByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var program = await GetOwnedProgramAsync(createDto.ProgramId);

        var alreadyEnrolled = await _enrollmentRepository.GetQueryable()
            .AnyAsync(e => e.TenantId == tenantId && e.EmployeeId == createDto.EmployeeId && e.ProgramId == createDto.ProgramId && !e.IsDeleted, cancellationToken);
        if (alreadyEnrolled)
            throw new InvalidOperationException("This employee is already enrolled in the program.");

        var entity = createDto.ToEntity(tenantId, enrolledByUserId);
        await ApplySessionCapacityAsync(entity, createDto.SessionId, tenantId, cancellationToken);
        ApplyDueDate(entity, program);

        await _enrollmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Employee {EmployeeId} enrolled in program {ProgramId}", createDto.EmployeeId, createDto.ProgramId);

        return await ReadEnrollmentAsync(entity.Id);
    }

    public async Task<IEnumerable<EmployeeOrientationDto>> BulkEnrollAsync(BulkEnrollOrientationDto bulkDto, Guid tenantId, Guid enrolledByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var program = await GetOwnedProgramAsync(bulkDto.ProgramId);

        var created = new List<EmployeeOrientation>();

        foreach (var employeeId in bulkDto.EmployeeIds.Distinct())
        {
            var alreadyEnrolled = await _enrollmentRepository.GetQueryable()
                .AnyAsync(e => e.TenantId == tenantId && e.EmployeeId == employeeId && e.ProgramId == bulkDto.ProgramId && !e.IsDeleted, cancellationToken);
            if (alreadyEnrolled)
                continue;

            var entity = new EmployeeOrientation
            {
                TenantId = tenantId,
                ProgramId = bulkDto.ProgramId,
                SessionId = bulkDto.SessionId,
                EmployeeId = employeeId,
                EnrollmentStatus = OrientationEnrollmentStatus.Confirmed,
                EnrollmentSource = bulkDto.EnrollmentSource,
                EnrolledAt = DateTime.UtcNow,
                EnrolledByEmployeeId = bulkDto.EnrolledByEmployeeId ?? enrolledByUserId,
                CompletionStatus = OrientationCompletionStatus.NotStarted,
                CreatedBy = enrolledByUserId.ToString(),
            };
            await ApplySessionCapacityAsync(entity, bulkDto.SessionId, tenantId, cancellationToken);
            ApplyDueDate(entity, program);

            await _enrollmentRepository.AddAsync(entity);
            created.Add(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Bulk-enrolled {Count} employees in program {ProgramId}", created.Count, bulkDto.ProgramId);

        // Re-read each: the entities built here carry no program/session navigation and no employee
        // names, so returning them raw hands back rows with every display column blank.
        var results = new List<EmployeeOrientationDto>();
        foreach (var e in created)
            results.Add(await ReadEnrollmentAsync(e.Id));
        return results;
    }

    public async Task<EmployeeOrientationDto> UpdateAsync(UpdateEmployeeOrientationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEnrollmentAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _enrollmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReadEnrollmentAsync(entity.Id);
    }

    public async Task<bool> WithdrawAsync(WithdrawOrientationDto withdrawDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEnrollmentAsync(withdrawDto.EnrollmentId);

        entity.EnrollmentStatus = OrientationEnrollmentStatus.Withdrawn;
        entity.WithdrawalReason = withdrawDto.WithdrawalReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _enrollmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEnrollmentAsync(id);

        await _enrollmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // CONTENT PROGRESS
    // ====================================================================

    public async Task<OrientationContentProgressDto> TrackContentProgressAsync(TrackOrientationContentProgressDto trackDto, Guid tenantId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var enrollment = await GetOwnedEnrollmentAsync(trackDto.EmployeeOrientationId);

        var progress = await _contentProgressRepository.GetByEnrollmentAndContentAsync(trackDto.EmployeeOrientationId, trackDto.ContentItemId);
        if (progress != null && progress.TenantId != tenantId)
            progress = null;

        var now = DateTime.UtcNow;

        if (progress == null)
        {
            progress = new OrientationContentProgress
            {
                TenantId = tenantId,
                EmployeeOrientationId = trackDto.EmployeeOrientationId,
                ContentItemId = trackDto.ContentItemId,
                Status = OrientationContentProgressStatus.InProgress,
                FirstAccessedAt = now,
                CreatedBy = updatedByUserId.ToString(),
            };
            await _contentProgressRepository.AddAsync(progress);
        }
        else
        {
            progress.UpdatedAt = now;
            progress.UpdatedBy = updatedByUserId.ToString();
            await _contentProgressRepository.UpdateAsync(progress);
        }

        progress.LastAccessedAt = now;
        progress.AccessCount += 1;
        progress.TotalTimeSpentSeconds += Math.Max(0, trackDto.TimeSpentSecondsDelta);
        if (progress.FirstAccessedAt == null) progress.FirstAccessedAt = now;

        if (trackDto.Acknowledge)
            progress.IsAcknowledged = true;

        if (trackDto.MarkCompleted)
        {
            progress.Status = OrientationContentProgressStatus.Completed;
            progress.CompletedAt = now;
        }
        else if (progress.Status == OrientationContentProgressStatus.NotStarted)
        {
            progress.Status = OrientationContentProgressStatus.InProgress;
        }

        // Roll progress up to the enrollment.
        enrollment.LastActivityAt = now;
        if (enrollment.StartedAt == null) enrollment.StartedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeProgressPercentageAsync(enrollment, cancellationToken);

        // Working through the last content item is what finishes a program that has no assessment, so
        // completion has to be re-evaluated here and not only on the assessment path.
        var program = await GetOwnedProgramAsync(enrollment.ProgramId);
        await EvaluateCompletionAsync(enrollment, program, now, cancellationToken);
        await _enrollmentRepository.UpdateAsync(enrollment);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read: the content item was never loaded onto the tracked progress row, so returning it
        // as-is would give the player back a row that cannot name the item it just marked complete.
        return (await _contentProgressRepository.GetByEnrollmentAndContentAsync(
            trackDto.EmployeeOrientationId, trackDto.ContentItemId))!.ToDto();
    }

    public async Task<IEnumerable<OrientationContentProgressDto>> GetContentProgressAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEnrollmentAsync(enrollmentId);
        return (await _contentProgressRepository.GetByEnrollmentIdAsync(enrollmentId))
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.ToDto());
    }

    /// <summary>
    /// The program's live structure, for the participant working through it.
    ///
    /// Reached through the enrollment rather than the program so the ownership gate applies: the
    /// catalogue reads are HR-only, and a participant needs the content item ids to be able to track
    /// progress against them at all.
    /// </summary>
    public async Task<IEnumerable<OrientationModuleDto>> GetProgramContentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId);

        var modules = (await _moduleRepository.GetByProgramIdAsync(enrollment.ProgramId))
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .OrderBy(m => m.SequenceOrder);

        var result = new List<OrientationModuleDto>();
        foreach (var module in modules)
        {
            // The repository's Include pulls every content item on the module, soft-deleted and
            // retired alike. A participant is only ever shown what is live, so the collection is
            // rebuilt here rather than taken from the mapper — and the count with it, otherwise the
            // player would advertise more items than it lists.
            var live = module.ContentItems
                .Where(c => !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.SequenceOrder)
                .Select(c => c.ToDto())
                .ToList();

            var dto = module.ToDto();
            dto.ContentItems = live;
            dto.ContentItemCount = live.Count;
            result.Add(dto);
        }

        return result;
    }

    // ====================================================================
    // ASSESSMENT
    // ====================================================================

    /// <summary>
    /// The paper as the participant sees it. Options come back with <c>IsCorrect</c> stripped and
    /// <c>Explanation</c> withheld until the attempt has been graded — this endpoint is what the person
    /// about to sit the assessment calls, and it was handing them the answer key in the response body.
    /// The authoring view (<c>GET orientation-programs/{id}/questions</c>) is where the key belongs.
    /// </summary>
    public async Task<IEnumerable<OrientationAssessmentQuestionDto>> GetAssessmentForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId);

        var questions = (await _questionRepository.GetActiveByProgramIdAsync(enrollment.ProgramId))
            .Where(q => q.TenantId == tenantId);

        var graded = enrollment.AttemptCount > 0;
        return questions.Select(q => q.ToParticipantDto(revealAnswers: graded));
    }

    public async Task<OrientationAssessmentResultDto> SubmitAssessmentAsync(SubmitOrientationAssessmentDto submitDto, Guid tenantId, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var enrollment = await GetOwnedEnrollmentAsync(submitDto.EmployeeOrientationId);

        var program = await GetOwnedProgramAsync(enrollment.ProgramId);

        var questions = (await _questionRepository.GetActiveByProgramIdAsync(enrollment.ProgramId))
            .Where(q => q.TenantId == tenantId)
            .ToList();
        var questionsById = questions.ToDictionary(q => q.Id);

        // Clear any previous attempt's responses for this enrollment.
        var previous = (await _responseRepository.GetByEnrollmentIdAsync(enrollment.Id))
            .Where(r => r.TenantId == tenantId)
            .ToList();
        if (previous.Count > 0)
            await _responseRepository.DeleteRangeAsync(previous);

        var now = DateTime.UtcNow;
        decimal awardedPoints = 0;
        var correctCount = 0;
        var responses = new List<OrientationAssessmentResponse>();

        // The denominator is every gradable question on the paper, not just the ones that came back in
        // the payload. Accumulating it inside the answer loop meant an unanswered question left the
        // paper entirely: answering one of ten correctly and omitting the rest scored 100%, passed,
        // completed the enrollment and issued a certificate.
        var gradable = questions.Where(q => q.QuestionType != OrientationQuestionType.FreeText).ToList();
        var gradableCount = gradable.Count;
        var totalPoints = gradable.Sum(q => q.Points);

        foreach (var answer in submitDto.Answers)
        {
            if (!questionsById.TryGetValue(answer.QuestionId, out var question))
                continue;

            if (question.QuestionType == OrientationQuestionType.FreeText)
            {
                // Free-text answers are stored but not auto-graded.
                responses.Add(new OrientationAssessmentResponse
                {
                    TenantId = tenantId,
                    EmployeeOrientationId = enrollment.Id,
                    QuestionId = question.Id,
                    FreeTextAnswer = answer.FreeTextAnswer,
                    IsCorrect = false,
                    PointsAwarded = 0,
                    AnsweredAt = now,
                    CreatedBy = submittedByUserId.ToString(),
                });
                continue;
            }

            var correctIds = question.Options.Where(o => o.IsCorrect).Select(o => o.Id).OrderBy(x => x).ToList();
            var selectedIds = answer.SelectedOptionIds.Distinct().OrderBy(x => x).ToList();
            var isCorrect = selectedIds.Count > 0 && correctIds.SequenceEqual(selectedIds);

            if (isCorrect)
            {
                correctCount++;
                awardedPoints += question.Points;
            }

            if (selectedIds.Count == 0)
            {
                responses.Add(new OrientationAssessmentResponse
                {
                    TenantId = tenantId,
                    EmployeeOrientationId = enrollment.Id,
                    QuestionId = question.Id,
                    SelectedOptionId = null,
                    IsCorrect = false,
                    PointsAwarded = 0,
                    AnsweredAt = now,
                    CreatedBy = submittedByUserId.ToString(),
                });
            }
            else
            {
                var first = true;
                foreach (var optionId in selectedIds)
                {
                    responses.Add(new OrientationAssessmentResponse
                    {
                        TenantId = tenantId,
                        EmployeeOrientationId = enrollment.Id,
                        QuestionId = question.Id,
                        SelectedOptionId = optionId,
                        IsCorrect = isCorrect,
                        PointsAwarded = first && isCorrect ? question.Points : 0,
                        AnsweredAt = now,
                        CreatedBy = submittedByUserId.ToString(),
                    });
                    first = false;
                }
            }
        }

        if (responses.Count > 0)
            await _responseRepository.AddRangeAsync(responses);

        var scorePercent = totalPoints > 0 ? Math.Round(awardedPoints / totalPoints * 100m, 2) : 0m;
        var passed = !program.PassingScorePercent.HasValue || scorePercent >= program.PassingScorePercent.Value;

        enrollment.AttemptCount += 1;
        enrollment.FinalScore = scorePercent;
        enrollment.IsPassed = passed;
        enrollment.LastActivityAt = now;

        await EvaluateCompletionAsync(enrollment, program, now, cancellationToken);

        await _enrollmentRepository.UpdateAsync(enrollment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var persisted = (await _responseRepository.GetByEnrollmentIdAsync(enrollment.Id))
            .Where(r => r.TenantId == tenantId);

        return new OrientationAssessmentResultDto
        {
            EmployeeOrientationId = enrollment.Id,
            ScorePercent = scorePercent,
            PointsAwarded = awardedPoints,
            TotalPoints = totalPoints,
            CorrectCount = correctCount,
            TotalQuestions = gradableCount,
            Passed = passed,
            AttemptNumber = enrollment.AttemptCount,
            Responses = persisted.Select(r => r.ToDto()).ToList(),
        };
    }

    public async Task<IEnumerable<OrientationAssessmentResponseDto>> GetAssessmentResponsesAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEnrollmentAsync(enrollmentId);
        return (await _responseRepository.GetByEnrollmentIdAsync(enrollmentId))
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.ToDto());
    }

    // ====================================================================
    // ACKNOWLEDGEMENTS
    // ====================================================================

    public async Task<OrientationAcknowledgementDto> AddAcknowledgementAsync(CreateOrientationAcknowledgementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedEnrollmentAsync(createDto.EmployeeOrientationId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.Status = OrientationAcknowledgementStatus.Presented;
        entity.PresentedAt = DateTime.UtcNow;

        await _acknowledgementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationAcknowledgementDto>> GetAcknowledgementsAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEnrollmentAsync(enrollmentId);
        return (await _acknowledgementRepository.GetByEnrollmentIdAsync(enrollmentId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto());
    }

    public async Task<OrientationAcknowledgementDto> SignAcknowledgementAsync(SignOrientationAcknowledgementDto signDto, string? ipAddress, Guid signedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAcknowledgementAsync(signDto.AcknowledgementId);

        var now = DateTime.UtcNow;
        if (signDto.Accept)
        {
            entity.Status = OrientationAcknowledgementStatus.Signed;
            entity.SignedAt = now;
            entity.SignatureIpAddress = ipAddress;
            entity.SignatureHash = ComputeSignatureHash(entity.EmployeeOrientationId, entity.AcknowledgementText, now);

            var enrollment = await _enrollmentRepository.GetByIdAsync(entity.EmployeeOrientationId);
            if (enrollment != null && enrollment.TenantId == GetTenantId())
            {
                enrollment.AcknowledgementSigned = true;
                enrollment.LastActivityAt = now;

                // Re-evaluate against the program's gates rather than only promoting out of
                // PendingAcknowledgement — for an acknowledgement-only program nothing ever put the
                // enrollment into that state, so signing used to change nothing at all.
                var program = await GetOwnedProgramAsync(enrollment.ProgramId);
                await EvaluateCompletionAsync(enrollment, program, now, cancellationToken);

                await _enrollmentRepository.UpdateAsync(enrollment);
            }
        }
        else
        {
            entity.Status = OrientationAcknowledgementStatus.Declined;
            entity.DeclinedAt = now;
            entity.DeclineReason = signDto.DeclineReason;
        }

        entity.UpdatedAt = now;
        entity.UpdatedBy = signedByUserId.ToString();
        await _acknowledgementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    // ====================================================================
    // FEEDBACK
    // ====================================================================

    public async Task<OrientationFeedbackDto> SubmitFeedbackAsync(CreateOrientationFeedbackDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedEnrollmentAsync(createDto.EmployeeOrientationId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        // The submitter is the TOKEN's employee, never the payload's. The DTO used to carry
        // SubmittedByEmployeeId and the mapping stored it as sent -- the "id reaching the service is
        // not the actor being stored" shape area 9 met four times. IsAnonymous stays a display flag:
        // the row still knows who filed it, so it can be deduplicated and so the author can see
        // their own; the READ is what withholds the name from everyone else.
        entity.SubmittedByEmployeeId = _currentUser.EmployeeId is { } me && me != Guid.Empty ? me : null;

        // One submission per enrollment per person. Feedback is an opinion, and a second press of
        // the button must not become a second opinion -- the training side had exactly this defect
        // (a repeatable vote into a trainer's average) and this was the sibling nobody closed.
        if (entity.SubmittedByEmployeeId is { } submitter)
        {
            var already = await _feedbackRepository.GetQueryable().AnyAsync(f =>
                f.TenantId == tenantId && !f.IsDeleted
                && f.EmployeeOrientationId == createDto.EmployeeOrientationId
                && f.SubmittedByEmployeeId == submitter, cancellationToken);
            if (already)
                throw new InvalidOperationException(
                    "You have already given feedback on this orientation. It can be read back under "
                    + "your own feedback, but it cannot be filed twice.");
        }

        await _feedbackRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return WithholdIfAnonymous(entity.ToDto());
    }

    public async Task<IEnumerable<OrientationFeedbackDto>> GetMyFeedbackAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (_currentUser.EmployeeId is not { } me || me == Guid.Empty)
            throw new UnauthorizedAccessException("Your user account is not linked to an employee record.");

        var rows = await _feedbackRepository.GetQueryable()
            .Where(f => f.TenantId == tenantId && !f.IsDeleted && f.SubmittedByEmployeeId == me)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(cancellationToken);

        // Their own rows: nothing withheld, anonymous or not -- the author always sees their own name.
        return rows.Select(f => f.ToDto()).ToList();
    }

    /// <summary>
    /// Anonymous feedback names nobody except its author.
    /// </summary>
    /// <remarks>
    /// Until lane 6, IsAnonymous hid nothing: the read handed SubmittedByEmployeeId to every caller
    /// who could reach the enrollment, so HR saw exactly who had ticked "anonymous". A flag the
    /// reader can see through is a promise the product does not keep.
    /// </remarks>
    private OrientationFeedbackDto WithholdIfAnonymous(OrientationFeedbackDto dto)
    {
        if (!dto.IsAnonymous) return dto;
        var me = _currentUser.EmployeeId;
        if (me is { } id && id != Guid.Empty && dto.SubmittedByEmployeeId == id) return dto;
        dto.SubmittedByEmployeeId = null;
        dto.SubmittedByName = null;
        return dto;
    }

    public async Task<IEnumerable<OrientationFeedbackDto>> GetFeedbackAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEnrollmentAsync(enrollmentId);
        var list = (await _feedbackRepository.GetByEnrollmentIdAsync(enrollmentId))
            .Where(f => f.TenantId == tenantId)
            .Select(f => f.ToDto())
            .ToList();
        var map = await _unitOfWork.ResolveEmployeesAsync(tenantId, list.Select(f => f.SubmittedByEmployeeId));
        list.FillNames(map);
        // After the names are filled, so an anonymous row is stripped of the name too.
        return list.Select(WithholdIfAnonymous).ToList();
    }

    // ====================================================================
    // CERTIFICATES
    // ====================================================================

    public async Task<OrientationCertificateDto> IssueCertificateAsync(IssueOrientationCertificateDto issueDto, Guid tenantId, Guid issuedByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var enrollment = await GetOwnedEnrollmentAsync(issueDto.EmployeeOrientationId);

        var program = await _programRepository.GetByIdAsync(enrollment.ProgramId);
        if (program != null && program.TenantId != tenantId)
            program = null;

        var entity = issueDto.ToEntity(tenantId, issuedByUserId);
        entity.CertificateNumber = string.IsNullOrWhiteSpace(issueDto.CertificateNumber)
            ? await GenerateCertificateNumberAsync(tenantId, cancellationToken)
            : issueDto.CertificateNumber.Trim();

        var numberExists = await _certificateRepository.CertificateNumberExistsAsync(tenantId, entity.CertificateNumber);
        if (numberExists)
            throw new InvalidOperationException($"Certificate number '{entity.CertificateNumber}' is already in use.");

        if (entity.ExpiresAt == null && program?.CertificateValidityMonths is > 0)
            entity.ExpiresAt = entity.IssuedAt.AddMonths(program.CertificateValidityMonths.Value);

        await _certificateRepository.AddAsync(entity);

        enrollment.CertificateIssued = true;
        enrollment.CertificateSerialNumber = entity.CertificateNumber;
        enrollment.CertificateExpiresAt = entity.ExpiresAt;
        await _enrollmentRepository.UpdateAsync(enrollment);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation certificate issued: {Number}", entity.CertificateNumber);

        // Re-read: the new certificate carries no enrollment navigation, and the DTO reads EmployeeId
        // off it — which is also the key the name hydrator uses, so returning it raw blanks the holder.
        var issued = (await _certificateRepository.GetByIdAsync(entity.Id))!.ToDto();
        await HydrateCertificatesAsync(new List<OrientationCertificateDto> { issued });
        return issued;
    }

    public async Task<bool> RevokeCertificateAsync(RevokeOrientationCertificateDto revokeDto, Guid revokedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCertificateAsync(revokeDto.CertificateId);

        entity.Status = OrientationCertificateStatus.Revoked;
        entity.RevokedAt = DateTime.UtcNow;
        entity.RevocationReason = revokeDto.RevocationReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = revokedByUserId.ToString();
        await _certificateRepository.UpdateAsync(entity);

        var enrollment = await _enrollmentRepository.GetByIdAsync(entity.EmployeeOrientationId);
        if (enrollment != null && enrollment.TenantId == GetTenantId())
        {
            // Fall back to whichever certificate is still active rather than blanket-clearing the flag:
            // an enrollment can hold a reissued certificate, and clearing the flag while leaving the
            // serial and expiry in place left the record claiming a certificate it says it does not have.
            var remaining = (await _certificateRepository.GetByEnrollmentIdAsync(enrollment.Id))
                .FirstOrDefault(c => c.Id != entity.Id
                                     && c.TenantId == enrollment.TenantId
                                     && c.Status == OrientationCertificateStatus.Active);

            enrollment.CertificateIssued = remaining != null;
            enrollment.CertificateSerialNumber = remaining?.CertificateNumber;
            enrollment.CertificateExpiresAt = remaining?.ExpiresAt;
            await _enrollmentRepository.UpdateAsync(enrollment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<OrientationCertificateDto>> GetCertificatesForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEnrollmentAsync(enrollmentId);
        return await HydrateCertificatesAsync(
            (await _certificateRepository.GetByEnrollmentIdAsync(enrollmentId))
                .Where(c => c.TenantId == tenantId)
                .Select(c => c.ToDto())
                .ToList());
    }

    public async Task<IEnumerable<OrientationCertificateDto>> GetCertificatesForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        RequireEmployeeAccess(employeeId);
        return await HydrateCertificatesAsync(
            (await _certificateRepository.GetByEmployeeIdAsync(employeeId))
                .Where(c => c.TenantId == tenantId)
                .Select(c => c.ToDto())
                .ToList());
    }

    public async Task<OrientationCertificateDto?> GetCertificateByNumberAsync(string certificateNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _certificateRepository.GetByCertificateNumberAsync(certificateNumber);
        if (entity == null || entity.TenantId != tenantId) return null;
        var dto = entity.ToDto();
        await HydrateCertificatesAsync(new List<OrientationCertificateDto> { dto });
        return dto;
    }

    public async Task<IEnumerable<OrientationCertificateDto>> GetExpiringCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateCertificatesAsync(
            (await _certificateRepository.GetExpiringAsync(daysAhead))
                .Where(c => c.TenantId == tenantId)
                .Select(c => c.ToDto())
                .ToList());
    }

    private async Task<List<OrientationCertificateDto>> HydrateCertificatesAsync(List<OrientationCertificateDto> list)
    {
        var map = await _unitOfWork.ResolveEmployeesAsync(GetTenantId(), list.EmployeeIds());
        list.FillNames(map);
        return list;
    }

    // ====================================================================
    // HELPERS
    // ====================================================================

    private async Task ApplySessionCapacityAsync(EmployeeOrientation entity, Guid? sessionId, Guid tenantId, CancellationToken cancellationToken)
    {
        if (sessionId == null) return;

        var session = await _sessionRepository.GetByIdAsync(sessionId.Value);
        if (session == null || session.TenantId != tenantId)
            throw new ArgumentException($"Orientation session with ID '{sessionId}' not found.");

        if (session.MaxParticipants.HasValue)
        {
            var enrolled = await _sessionRepository.GetEnrolledCountAsync(session.Id);
            if (enrolled >= session.MaxParticipants.Value)
            {
                if (!session.AllowWaitlist)
                    throw new InvalidOperationException("The selected session is full and does not allow a waitlist.");

                var waitlisted = (await _enrollmentRepository.GetWaitlistedForSessionAsync(session.Id))
                    .Where(e => e.TenantId == tenantId);
                entity.EnrollmentStatus = OrientationEnrollmentStatus.Waitlisted;
                entity.WaitlistPosition = waitlisted.Count() + 1;
            }
        }
    }

    private static void ApplyDueDate(EmployeeOrientation entity, OrientationProgram program)
    {
        if (program.CompletionDeadlineDays is > 0 && entity.NextDueDate == null)
            entity.NextDueDate = entity.EnrolledAt.AddDays(program.CompletionDeadlineDays.Value);
    }

    /// <summary>
    /// The content items an enrollment is actually measured against: live items on live modules.
    ///
    /// The repository filter is <c>!IsDeleted</c> only, so it also returns retired content and content
    /// sitting on a retired module — neither of which the participant is shown by
    /// <see cref="GetProgramContentAsync"/>, and neither of which they can therefore ever complete.
    /// Counting them makes the completion gate unsatisfiable: retiring a single slide deck strands
    /// everyone already enrolled below 100% forever, in exactly the way the ProgressPercentage defect
    /// did. The definition lives here so the progress denominator and the gate cannot drift apart.
    /// </summary>
    private async Task<int> CountLiveContentItemsAsync(Guid programId, Guid tenantId)
    {
        return (await _contentItemRepository.GetByProgramIdAsync(programId))
            .Count(c => c.TenantId == tenantId && c.IsActive && c.Module.IsActive);
    }

    private async Task RecomputeProgressPercentageAsync(EmployeeOrientation enrollment, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var totalContent = await CountLiveContentItemsAsync(enrollment.ProgramId, tenantId);
        var completed = await _contentProgressRepository.CountCompletedForEnrollmentAsync(enrollment.Id);

        enrollment.ProgressPercentage = totalContent == 0
            ? 0
            : Math.Min(100, (int)Math.Round(completed * 100.0 / totalContent));

        await _enrollmentRepository.UpdateAsync(enrollment);
    }

    /// <summary>
    /// The single place that decides whether an enrollment is finished, evaluated against the program's
    /// own gates: content worked through, assessment passed where one is required, acknowledgement
    /// signed where one is required.
    ///
    /// Before this existed, only two writes could ever set <c>Completed</c>: submitting an assessment,
    /// and signing an acknowledgement — but the latter only fired when the status was already
    /// <c>PendingAcknowledgement</c>, which only the assessment path set. So a program with
    /// <c>RequiresAssessment = false</c> — the ordinary read-and-acknowledge policy briefing, which is
    /// most of them — could never be completed by any route: it sat at <c>InProgress</c> at 100%
    /// progress forever, no certificate, and permanently in the overdue queue.
    /// </summary>
    private async Task EvaluateCompletionAsync(
        EmployeeOrientation enrollment, OrientationProgram program, DateTime now, CancellationToken cancellationToken)
    {
        if (enrollment.CompletionStatus == OrientationCompletionStatus.Exempted)
            return;

        // A failed assessment is a terminal state for this attempt — a retake calls back through here.
        if (program.RequiresAssessment && enrollment.AttemptCount > 0 && !enrollment.IsPassed)
        {
            enrollment.CompletionStatus = OrientationCompletionStatus.Failed;
            return;
        }

        // A program with no content items has nothing to work through, so the content gate is
        // satisfied rather than unsatisfiable. Reading this off ProgressPercentage alone got it
        // wrong twice over: the percentage is only recomputed when content progress is tracked, and
        // it is deliberately 0 when there is no content — so an assessment-only program sat at 0%
        // forever and passing the assessment could never complete it.
        //
        // Counted as live content only — see CountLiveContentItemsAsync. A retired item, or one on a
        // retired module, is invisible to the participant, so including it here would reintroduce the
        // unsatisfiable gate by a different route.
        var tenantId = GetTenantId();
        var contentCount = await CountLiveContentItemsAsync(enrollment.ProgramId, tenantId);
        var contentDone = contentCount == 0 || enrollment.ProgressPercentage >= 100;

        var assessmentDone = !program.RequiresAssessment || (enrollment.AttemptCount > 0 && enrollment.IsPassed);
        var acknowledgementDone = !program.RequiresAcknowledgement || enrollment.AcknowledgementSigned;

        if (contentDone && assessmentDone && acknowledgementDone)
        {
            enrollment.CompletionStatus = OrientationCompletionStatus.Completed;
            enrollment.CompletedAt ??= now;
            enrollment.EnrollmentStatus = OrientationEnrollmentStatus.Completed;
            return;
        }

        // Everything the participant can do is done and only the signature is outstanding — surface that
        // as its own state so the "waiting on you to sign" queue is reachable.
        if (contentDone && assessmentDone && !acknowledgementDone)
        {
            enrollment.CompletionStatus = OrientationCompletionStatus.PendingAcknowledgement;
            return;
        }

        // Failed is included so a retake that is under way moves back out of it. Without that the
        // first failed attempt is terminal and no later pass can clear it.
        if (enrollment.CompletionStatus is OrientationCompletionStatus.NotStarted
            or OrientationCompletionStatus.Completed
            or OrientationCompletionStatus.PendingAcknowledgement
            or OrientationCompletionStatus.Failed)
        {
            enrollment.CompletionStatus = OrientationCompletionStatus.InProgress;
        }
    }

    /// <summary>
    /// Numbers off the highest serial ever issued, soft-deleted rows included.
    /// (TenantId, CertificateNumber) is UNIQUE and a soft delete does not release the value, so the
    /// previous count-of-live-rows approach handed back a number the database still held: deleting one
    /// certificate made the very next issue die on a duplicate key. A serial on an audit record is an
    /// identifier, not a slot — once issued it is spent.
    /// </summary>
    private async Task<string> GenerateCertificateNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"OCERT-{DateTime.UtcNow.Year}-";
        var issued = await _certificateRepository
            .GetQueryableIncludingDeleted(c => c.TenantId == tenantId && c.CertificateNumber.StartsWith(prefix))
            .Select(c => c.CertificateNumber)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var number in issued)
        {
            if (int.TryParse(number[prefix.Length..], out var n) && n > max) max = n;
        }

        return $"{prefix}{(max + 1):D5}";
    }

    private static string ComputeSignatureHash(Guid enrollmentId, string text, DateTime signedAt)
    {
        var raw = $"{enrollmentId:N}|{text}|{signedAt:O}";
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}

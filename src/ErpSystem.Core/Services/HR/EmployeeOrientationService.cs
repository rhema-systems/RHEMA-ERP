using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class EmployeeOrientationService : IEmployeeOrientationService
{
    private readonly IEmployeeOrientationRepository _enrollmentRepository;
    private readonly IOrientationProgramRepository _programRepository;
    private readonly IOrientationSessionRepository _sessionRepository;
    private readonly IOrientationContentItemRepository _contentItemRepository;
    private readonly IOrientationContentProgressRepository _contentProgressRepository;
    private readonly IOrientationAssessmentQuestionRepository _questionRepository;
    private readonly IOrientationAssessmentResponseRepository _responseRepository;
    private readonly IOrientationAcknowledgementRepository _acknowledgementRepository;
    private readonly IOrientationFeedbackRepository _feedbackRepository;
    private readonly IOrientationCertificateRepository _certificateRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeOrientationService> _logger;

    public EmployeeOrientationService(
        IEmployeeOrientationRepository enrollmentRepository,
        IOrientationProgramRepository programRepository,
        IOrientationSessionRepository sessionRepository,
        IOrientationContentItemRepository contentItemRepository,
        IOrientationContentProgressRepository contentProgressRepository,
        IOrientationAssessmentQuestionRepository questionRepository,
        IOrientationAssessmentResponseRepository responseRepository,
        IOrientationAcknowledgementRepository acknowledgementRepository,
        IOrientationFeedbackRepository feedbackRepository,
        IOrientationCertificateRepository certificateRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeOrientationService> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _programRepository = programRepository;
        _sessionRepository = sessionRepository;
        _contentItemRepository = contentItemRepository;
        _contentProgressRepository = contentProgressRepository;
        _questionRepository = questionRepository;
        _responseRepository = responseRepository;
        _acknowledgementRepository = acknowledgementRepository;
        _feedbackRepository = feedbackRepository;
        _certificateRepository = certificateRepository;
        _currentUserProvider = currentUserProvider;
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

    private async Task<EmployeeOrientation> GetOwnedEnrollmentAsync(Guid id)
    {
        var entity = await _enrollmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation enrollment with ID '{id}' not found.");
        return entity;
    }

    private async Task<EmployeeOrientation> GetOwnedEnrollmentWithDetailsAsync(Guid id)
    {
        var entity = await _enrollmentRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation enrollment with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationAcknowledgement> GetOwnedAcknowledgementAsync(Guid id)
    {
        var entity = await _acknowledgementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation acknowledgement with ID '{id}' not found.");
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

        return (await _enrollmentRepository.GetWithFullDetailsAsync(entity.Id))!.ToDto();
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

        return created.Select(e => e.ToDto());
    }

    public async Task<EmployeeOrientationDto> UpdateAsync(UpdateEmployeeOrientationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEnrollmentAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _enrollmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _enrollmentRepository.GetWithFullDetailsAsync(entity.Id))!.ToDto();
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
        if (enrollment.CompletionStatus == OrientationCompletionStatus.NotStarted)
            enrollment.CompletionStatus = OrientationCompletionStatus.InProgress;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeProgressPercentageAsync(enrollment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return progress.ToDto();
    }

    public async Task<IEnumerable<OrientationContentProgressDto>> GetContentProgressAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEnrollmentAsync(enrollmentId);
        return (await _contentProgressRepository.GetByEnrollmentIdAsync(enrollmentId))
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.ToDto());
    }

    // ====================================================================
    // ASSESSMENT
    // ====================================================================

    public async Task<IEnumerable<OrientationAssessmentQuestionDto>> GetAssessmentForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId);

        var questions = (await _questionRepository.GetActiveByProgramIdAsync(enrollment.ProgramId))
            .Where(q => q.TenantId == tenantId);
        return questions.Select(q => q.ToDto());
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
        decimal totalPoints = 0;
        decimal awardedPoints = 0;
        var correctCount = 0;
        var gradableCount = 0;
        var responses = new List<OrientationAssessmentResponse>();

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

            gradableCount++;
            totalPoints += question.Points;

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

        if (!passed)
        {
            enrollment.CompletionStatus = OrientationCompletionStatus.Failed;
        }
        else if (program.RequiresAcknowledgement && !enrollment.AcknowledgementSigned)
        {
            enrollment.CompletionStatus = OrientationCompletionStatus.PendingAcknowledgement;
        }
        else
        {
            enrollment.CompletionStatus = OrientationCompletionStatus.Completed;
            enrollment.CompletedAt = now;
            enrollment.EnrollmentStatus = OrientationEnrollmentStatus.Completed;
        }

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
                if (enrollment.CompletionStatus == OrientationCompletionStatus.PendingAcknowledgement)
                {
                    enrollment.CompletionStatus = OrientationCompletionStatus.Completed;
                    enrollment.CompletedAt = now;
                    enrollment.EnrollmentStatus = OrientationEnrollmentStatus.Completed;
                }
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
        await _feedbackRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
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
        return list;
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

        var numberExists = await _certificateRepository.GetQueryable()
            .AnyAsync(c => c.TenantId == tenantId && c.CertificateNumber == entity.CertificateNumber && !c.IsDeleted, cancellationToken);
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
        return entity.ToDto();
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
            enrollment.CertificateIssued = false;
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

    private async Task RecomputeProgressPercentageAsync(EmployeeOrientation enrollment, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var totalContent = (await _contentItemRepository.GetByProgramIdAsync(enrollment.ProgramId))
            .Count(c => c.TenantId == tenantId);
        var completed = await _contentProgressRepository.CountCompletedForEnrollmentAsync(enrollment.Id);

        enrollment.ProgressPercentage = totalContent == 0
            ? 0
            : Math.Min(100, (int)Math.Round(completed * 100.0 / totalContent));

        await _enrollmentRepository.UpdateAsync(enrollment);
    }

    private async Task<string> GenerateCertificateNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"OCERT-{DateTime.UtcNow.Year}-";
        var next = await _certificateRepository.GetQueryable()
            .CountAsync(c => c.TenantId == tenantId && c.CertificateNumber.StartsWith(prefix), cancellationToken) + 1;
        var number = $"{prefix}{next:D5}";
        while (await _certificateRepository.GetQueryable()
            .AnyAsync(c => c.TenantId == tenantId && c.CertificateNumber == number && !c.IsDeleted, cancellationToken))
        {
            next++;
            number = $"{prefix}{next:D5}";
        }
        return number;
    }

    private static string ComputeSignatureHash(Guid enrollmentId, string text, DateTime signedAt)
    {
        var raw = $"{enrollmentId:N}|{text}|{signedAt:O}";
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}

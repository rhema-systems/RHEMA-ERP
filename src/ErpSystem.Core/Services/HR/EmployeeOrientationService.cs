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
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ====================================================================
    // QUERIES
    // ====================================================================

    public async Task<EmployeeOrientationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _enrollmentRepository.GetWithFullDetailsAsync(id)
            ?? throw new ArgumentException($"Orientation enrollment with ID '{id}' not found.");
        var dto = entity.ToDto();
        var map = await _unitOfWork.ResolveEmployeesAsync(dto.EmployeeIds());
        dto.FillNames(map);
        return dto;
    }

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => await HydrateSummariesAsync(await _enrollmentRepository.GetByEmployeeIdAsync(employeeId));

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetByProgramIdAsync(Guid programId, CancellationToken cancellationToken = default)
        => await HydrateSummariesAsync(await _enrollmentRepository.GetByProgramIdAsync(programId));

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => await HydrateSummariesAsync(await _enrollmentRepository.GetBySessionIdAsync(sessionId));

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetByCompletionStatusAsync(OrientationCompletionStatus status, CancellationToken cancellationToken = default)
        => await HydrateSummariesAsync(await _enrollmentRepository.GetByCompletionStatusAsync(status));

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetOverdueAsync(CancellationToken cancellationToken = default)
        => await HydrateSummariesAsync(await _enrollmentRepository.GetOverdueAsync());

    public async Task<IEnumerable<EmployeeOrientationSummaryDto>> GetDueSoonAsync(int daysAhead = 7, CancellationToken cancellationToken = default)
        => await HydrateSummariesAsync(await _enrollmentRepository.GetDueSoonAsync(daysAhead));

    public async Task<PagedResult<EmployeeOrientationSummaryDto>> GetPagedByProgramAsync(Guid programId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _enrollmentRepository.GetQueryable()
            .Where(e => e.ProgramId == programId)
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
        var map = await _unitOfWork.ResolveEmployeesAsync(list.EmployeeIds());
        list.FillNames(map);
        return list;
    }

    // ====================================================================
    // ENROLLMENT
    // ====================================================================

    public async Task<EmployeeOrientationDto> EnrollAsync(CreateEmployeeOrientationDto createDto, Guid tenantId, Guid enrolledByUserId, CancellationToken cancellationToken = default)
    {
        var program = await _programRepository.GetByIdAsync(createDto.ProgramId)
            ?? throw new ArgumentException($"Orientation program with ID '{createDto.ProgramId}' not found.");

        if (await _enrollmentRepository.ExistsForEmployeeAndProgramAsync(createDto.EmployeeId, createDto.ProgramId))
            throw new InvalidOperationException("This employee is already enrolled in the program.");

        var entity = createDto.ToEntity(tenantId, enrolledByUserId);
        await ApplySessionCapacityAsync(entity, createDto.SessionId, cancellationToken);
        ApplyDueDate(entity, program);

        await _enrollmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Employee {EmployeeId} enrolled in program {ProgramId}", createDto.EmployeeId, createDto.ProgramId);

        return (await _enrollmentRepository.GetWithFullDetailsAsync(entity.Id))!.ToDto();
    }

    public async Task<IEnumerable<EmployeeOrientationDto>> BulkEnrollAsync(BulkEnrollOrientationDto bulkDto, Guid tenantId, Guid enrolledByUserId, CancellationToken cancellationToken = default)
    {
        var program = await _programRepository.GetByIdAsync(bulkDto.ProgramId)
            ?? throw new ArgumentException($"Orientation program with ID '{bulkDto.ProgramId}' not found.");

        var created = new List<EmployeeOrientation>();

        foreach (var employeeId in bulkDto.EmployeeIds.Distinct())
        {
            if (await _enrollmentRepository.ExistsForEmployeeAndProgramAsync(employeeId, bulkDto.ProgramId))
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
            await ApplySessionCapacityAsync(entity, bulkDto.SessionId, cancellationToken);
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
        var entity = await _enrollmentRepository.GetByIdAsync(updateDto.Id)
            ?? throw new ArgumentException($"Orientation enrollment with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _enrollmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _enrollmentRepository.GetWithFullDetailsAsync(entity.Id))!.ToDto();
    }

    public async Task<bool> WithdrawAsync(WithdrawOrientationDto withdrawDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _enrollmentRepository.GetByIdAsync(withdrawDto.EnrollmentId)
            ?? throw new ArgumentException($"Orientation enrollment with ID '{withdrawDto.EnrollmentId}' not found.");

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
        var entity = await _enrollmentRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Orientation enrollment with ID '{id}' not found.");

        await _enrollmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // CONTENT PROGRESS
    // ====================================================================

    public async Task<OrientationContentProgressDto> TrackContentProgressAsync(TrackOrientationContentProgressDto trackDto, Guid tenantId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(trackDto.EmployeeOrientationId)
            ?? throw new ArgumentException($"Orientation enrollment with ID '{trackDto.EmployeeOrientationId}' not found.");

        var progress = await _contentProgressRepository.GetByEnrollmentAndContentAsync(trackDto.EmployeeOrientationId, trackDto.ContentItemId);
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
        => (await _contentProgressRepository.GetByEnrollmentIdAsync(enrollmentId)).Select(p => p.ToDto());

    // ====================================================================
    // ASSESSMENT
    // ====================================================================

    public async Task<IEnumerable<OrientationAssessmentQuestionDto>> GetAssessmentForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(enrollmentId)
            ?? throw new ArgumentException($"Orientation enrollment with ID '{enrollmentId}' not found.");

        var questions = await _questionRepository.GetActiveByProgramIdAsync(enrollment.ProgramId);
        return questions.Select(q => q.ToDto());
    }

    public async Task<OrientationAssessmentResultDto> SubmitAssessmentAsync(SubmitOrientationAssessmentDto submitDto, Guid tenantId, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(submitDto.EmployeeOrientationId)
            ?? throw new ArgumentException($"Orientation enrollment with ID '{submitDto.EmployeeOrientationId}' not found.");

        var program = await _programRepository.GetByIdAsync(enrollment.ProgramId)
            ?? throw new ArgumentException($"Orientation program with ID '{enrollment.ProgramId}' not found.");

        var questions = (await _questionRepository.GetActiveByProgramIdAsync(enrollment.ProgramId)).ToList();
        var questionsById = questions.ToDictionary(q => q.Id);

        // Clear any previous attempt's responses for this enrollment.
        var previous = (await _responseRepository.GetByEnrollmentIdAsync(enrollment.Id)).ToList();
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

        var persisted = await _responseRepository.GetByEnrollmentIdAsync(enrollment.Id);

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
        => (await _responseRepository.GetByEnrollmentIdAsync(enrollmentId)).Select(r => r.ToDto());

    // ====================================================================
    // ACKNOWLEDGEMENTS
    // ====================================================================

    public async Task<OrientationAcknowledgementDto> AddAcknowledgementAsync(CreateOrientationAcknowledgementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        if (!await _enrollmentRepository.ExistsAsync(e => e.Id == createDto.EmployeeOrientationId && !e.IsDeleted))
            throw new ArgumentException($"Orientation enrollment with ID '{createDto.EmployeeOrientationId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.Status = OrientationAcknowledgementStatus.Presented;
        entity.PresentedAt = DateTime.UtcNow;

        await _acknowledgementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationAcknowledgementDto>> GetAcknowledgementsAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
        => (await _acknowledgementRepository.GetByEnrollmentIdAsync(enrollmentId)).Select(a => a.ToDto());

    public async Task<OrientationAcknowledgementDto> SignAcknowledgementAsync(SignOrientationAcknowledgementDto signDto, string? ipAddress, Guid signedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _acknowledgementRepository.GetByIdAsync(signDto.AcknowledgementId)
            ?? throw new ArgumentException($"Orientation acknowledgement with ID '{signDto.AcknowledgementId}' not found.");

        var now = DateTime.UtcNow;
        if (signDto.Accept)
        {
            entity.Status = OrientationAcknowledgementStatus.Signed;
            entity.SignedAt = now;
            entity.SignatureIpAddress = ipAddress;
            entity.SignatureHash = ComputeSignatureHash(entity.EmployeeOrientationId, entity.AcknowledgementText, now);

            var enrollment = await _enrollmentRepository.GetByIdAsync(entity.EmployeeOrientationId);
            if (enrollment != null)
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
        if (!await _enrollmentRepository.ExistsAsync(e => e.Id == createDto.EmployeeOrientationId && !e.IsDeleted))
            throw new ArgumentException($"Orientation enrollment with ID '{createDto.EmployeeOrientationId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _feedbackRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationFeedbackDto>> GetFeedbackAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var list = (await _feedbackRepository.GetByEnrollmentIdAsync(enrollmentId)).Select(f => f.ToDto()).ToList();
        var map = await _unitOfWork.ResolveEmployeesAsync(list.Select(f => f.SubmittedByEmployeeId));
        list.FillNames(map);
        return list;
    }

    // ====================================================================
    // CERTIFICATES
    // ====================================================================

    public async Task<OrientationCertificateDto> IssueCertificateAsync(IssueOrientationCertificateDto issueDto, Guid tenantId, Guid issuedByUserId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(issueDto.EmployeeOrientationId)
            ?? throw new ArgumentException($"Orientation enrollment with ID '{issueDto.EmployeeOrientationId}' not found.");

        var program = await _programRepository.GetByIdAsync(enrollment.ProgramId);

        var entity = issueDto.ToEntity(tenantId, issuedByUserId);
        entity.CertificateNumber = string.IsNullOrWhiteSpace(issueDto.CertificateNumber)
            ? await GenerateCertificateNumberAsync(cancellationToken)
            : issueDto.CertificateNumber.Trim();

        if (await _certificateRepository.CertificateNumberExistsAsync(entity.CertificateNumber))
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
        var entity = await _certificateRepository.GetByIdAsync(revokeDto.CertificateId)
            ?? throw new ArgumentException($"Orientation certificate with ID '{revokeDto.CertificateId}' not found.");

        entity.Status = OrientationCertificateStatus.Revoked;
        entity.RevokedAt = DateTime.UtcNow;
        entity.RevocationReason = revokeDto.RevocationReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = revokedByUserId.ToString();
        await _certificateRepository.UpdateAsync(entity);

        var enrollment = await _enrollmentRepository.GetByIdAsync(entity.EmployeeOrientationId);
        if (enrollment != null)
        {
            enrollment.CertificateIssued = false;
            await _enrollmentRepository.UpdateAsync(enrollment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<OrientationCertificateDto>> GetCertificatesForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
        => await HydrateCertificatesAsync((await _certificateRepository.GetByEnrollmentIdAsync(enrollmentId)).Select(c => c.ToDto()).ToList());

    public async Task<IEnumerable<OrientationCertificateDto>> GetCertificatesForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => await HydrateCertificatesAsync((await _certificateRepository.GetByEmployeeIdAsync(employeeId)).Select(c => c.ToDto()).ToList());

    public async Task<OrientationCertificateDto?> GetCertificateByNumberAsync(string certificateNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _certificateRepository.GetByCertificateNumberAsync(certificateNumber);
        if (entity == null) return null;
        var dto = entity.ToDto();
        await HydrateCertificatesAsync(new List<OrientationCertificateDto> { dto });
        return dto;
    }

    public async Task<IEnumerable<OrientationCertificateDto>> GetExpiringCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => await HydrateCertificatesAsync((await _certificateRepository.GetExpiringAsync(daysAhead)).Select(c => c.ToDto()).ToList());

    private async Task<List<OrientationCertificateDto>> HydrateCertificatesAsync(List<OrientationCertificateDto> list)
    {
        var map = await _unitOfWork.ResolveEmployeesAsync(list.EmployeeIds());
        list.FillNames(map);
        return list;
    }

    // ====================================================================
    // HELPERS
    // ====================================================================

    private async Task ApplySessionCapacityAsync(EmployeeOrientation entity, Guid? sessionId, CancellationToken cancellationToken)
    {
        if (sessionId == null) return;

        var session = await _sessionRepository.GetByIdAsync(sessionId.Value)
            ?? throw new ArgumentException($"Orientation session with ID '{sessionId}' not found.");

        if (session.MaxParticipants.HasValue)
        {
            var enrolled = await _sessionRepository.GetEnrolledCountAsync(session.Id);
            if (enrolled >= session.MaxParticipants.Value)
            {
                if (!session.AllowWaitlist)
                    throw new InvalidOperationException("The selected session is full and does not allow a waitlist.");

                var waitlisted = await _enrollmentRepository.GetWaitlistedForSessionAsync(session.Id);
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
        var totalContent = (await _contentItemRepository.GetByProgramIdAsync(enrollment.ProgramId)).Count();
        var completed = await _contentProgressRepository.CountCompletedForEnrollmentAsync(enrollment.Id);

        enrollment.ProgressPercentage = totalContent == 0
            ? 0
            : Math.Min(100, (int)Math.Round(completed * 100.0 / totalContent));

        await _enrollmentRepository.UpdateAsync(enrollment);
    }

    private async Task<string> GenerateCertificateNumberAsync(CancellationToken cancellationToken)
    {
        var prefix = $"OCERT-{DateTime.UtcNow.Year}-";
        var next = (await _certificateRepository.CountAsync()) + 1;
        var number = $"{prefix}{next:D5}";
        while (await _certificateRepository.CertificateNumberExistsAsync(number))
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

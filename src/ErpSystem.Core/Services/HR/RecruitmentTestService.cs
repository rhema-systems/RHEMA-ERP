using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="IRecruitmentTestService"/>
public class RecruitmentTestService : IRecruitmentTestService
{
    /// <summary>
    /// How long after <c>MustSubmitBy</c> a submission is still treated as on time.
    /// </summary>
    /// <remarks>
    /// ⚠ The submit round trip, the client's own tick interval and the difference between the
    /// browser's clock and the server's all land inside this. Without it a candidate who pressed
    /// Submit with four seconds left could be told their time had run out, which is the kind of
    /// thing that ends in a complaint nobody can disprove.
    /// </remarks>
    private static readonly TimeSpan SubmitGrace = TimeSpan.FromMinutes(2);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecruitmentTestRepository _testRepository;
    private readonly IJobApplicationService _applicationService;
    private readonly IApplicationPipelineService _pipelineService;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<RecruitmentTestService> _logger;

    public RecruitmentTestService(
        IUnitOfWork unitOfWork,
        IRecruitmentTestRepository testRepository,
        IJobApplicationService applicationService,
        IApplicationPipelineService pipelineService,
        ITemplatedEmailService templatedEmail,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserService currentUser,
        ILogger<RecruitmentTestService> logger)
    {
        _unitOfWork = unitOfWork;
        _testRepository = testRepository;
        _applicationService = applicationService;
        _pipelineService = pipelineService;
        _templatedEmail = templatedEmail;
        _currentUserProvider = currentUserProvider;
        _currentUser = currentUser;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global filter and TenantId
    // auto-stamp are inert. Every read and write here scopes to the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// The acting EMPLOYEE, or null when the caller is not one.
    /// </summary>
    /// <remarks>
    /// ⚠ Not interchangeable with <see cref="ActorUserId"/>. The applicant test ledger's
    /// <c>MarkedById</c> is a foreign key to <c>Employees</c> and the pipeline's
    /// <c>movedByEmployeeId</c> is another; putting a user id in either is the defect shape this
    /// module already carries once. A candidate submitting their own paper has neither.
    /// </remarks>
    private Guid? ActorEmployeeId =>
        _currentUser.EmployeeId is { } id && id != Guid.Empty ? (Guid?)id : null;

    /// <summary>The acting USER — the audit columns on the rows this service writes.</summary>
    private Guid? ActorUserId =>
        _currentUserProvider.UserId != Guid.Empty ? (Guid?)_currentUserProvider.UserId : null;

    // ═══════════════════════════════════════════════════════════════════════════
    //  E2 — AUTHORING
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<RecruitmentTestDto>> GetTestsAsync(
        bool? activeOnly = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = _unitOfWork.Repository<RecruitmentTest>().GetQueryable()
            .Where(t => t.TenantId == tenantId);

        if (activeOnly == true) query = query.Where(t => t.IsActive);
        if (activeOnly == false) query = query.Where(t => !t.IsActive);

        var tests = await query
            .Include(t => t.Questions)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        // One query for the whole page rather than one per row: a list of twenty papers would
        // otherwise be twenty round trips just to colour twenty "in use" badges.
        var ids = tests.Select(t => t.Id).ToList();
        var used = await _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
            .Where(s => s.TenantId == tenantId && ids.Contains(s.Assignment.RecruitmentTestId))
            .Select(s => s.Assignment.RecruitmentTestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return tests.Select(t => MapTestSummary(t, used.Contains(t.Id))).ToList();
    }

    public async Task<RecruitmentTestDto> GetTestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var test = await RequireOwnedPaperAsync(id, cancellationToken);
        return MapTestFull(test, await HasSittingsAsync(test.Id, cancellationToken));
    }

    public async Task<RecruitmentTestDto> CreateTestAsync(
        CreateRecruitmentTestDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        ValidateTestHeader(dto);

        var entity = new RecruitmentTest
        {
            TenantId = tenantId,
            TestCode = await _testRepository.GetNextTestCodeAsync(tenantId, cancellationToken),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Instructions = dto.Instructions?.Trim(),
            TestType = dto.TestType,
            DurationMinutes = dto.DurationMinutes,
            PassMarkPercent = dto.PassMarkPercent,
            MaxAttempts = dto.MaxAttempts,
            ShuffleQuestions = dto.ShuffleQuestions,
            ShuffleOptions = dto.ShuffleOptions,

            // ⚠ A new paper is NEVER active. It has no questions yet, and an assignable paper with
            // no questions is a sitting that can only score zero.
            IsActive = false,
            CreatedById = ActorUserId,
        };

        await _unitOfWork.Repository<RecruitmentTest>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recruitment test {Code} ({Name}) created.", entity.TestCode, entity.Name);
        return MapTestFull(entity, hasSittings: false);
    }

    public async Task<RecruitmentTestDto> UpdateTestAsync(
        UpdateRecruitmentTestDto dto, CancellationToken cancellationToken = default)
    {
        var test = await RequireOwnedPaperAsync(dto.Id, cancellationToken);
        await RequireNotSatAsync(test, "change its settings", cancellationToken);
        ValidateTestHeader(dto);

        test.Name = dto.Name.Trim();
        test.Description = dto.Description?.Trim();
        test.Instructions = dto.Instructions?.Trim();
        test.TestType = dto.TestType;
        test.DurationMinutes = dto.DurationMinutes;
        test.PassMarkPercent = dto.PassMarkPercent;
        test.MaxAttempts = dto.MaxAttempts;
        test.ShuffleQuestions = dto.ShuffleQuestions;
        test.ShuffleOptions = dto.ShuffleOptions;
        Touch(test);

        await _unitOfWork.Repository<RecruitmentTest>().UpdateAsync(test);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapTestFull(test, hasSittings: false);
    }

    public async Task<bool> DeleteTestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var test = await RequireOwnedPaperAsync(id, cancellationToken);
        await RequireNotSatAsync(test, "delete it", cancellationToken);

        // An assignment pointing at a deleted paper would offer candidates a test that cannot be
        // opened, so the assignments go too. Nobody has sat any of them — that was just checked.
        var assignments = await _unitOfWork.Repository<RecruitmentTestAssignment>().GetQueryable()
            .Where(a => a.TenantId == test.TenantId && a.RecruitmentTestId == test.Id)
            .ToListAsync(cancellationToken);

        foreach (var assignment in assignments)
            await _unitOfWork.Repository<RecruitmentTestAssignment>().DeleteAsync(assignment);

        await _unitOfWork.Repository<RecruitmentTest>().DeleteAsync(test);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RecruitmentTestDto> SetTestActiveAsync(
        Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var test = await RequireOwnedPaperAsync(id, cancellationToken);

        if (isActive) ValidatePaperIsMarkable(test);

        test.IsActive = isActive;
        Touch(test);

        await _unitOfWork.Repository<RecruitmentTest>().UpdateAsync(test);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recruitment test {Code} is now {State}.",
            test.TestCode, isActive ? "active" : "retired");

        return MapTestFull(test, await HasSittingsAsync(test.Id, cancellationToken));
    }

    public async Task<RecruitmentTestSectionDto> AddSectionAsync(
        CreateRecruitmentTestSectionDto dto, CancellationToken cancellationToken = default)
    {
        var test = await RequireOwnedPaperAsync(dto.RecruitmentTestId, cancellationToken);
        await RequireNotSatAsync(test, "add a section", cancellationToken);

        var entity = new RecruitmentTestSection
        {
            TenantId = test.TenantId,
            RecruitmentTestId = test.Id,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            DisplayOrder = dto.DisplayOrder,
            CreatedById = ActorUserId,
        };

        await _unitOfWork.Repository<RecruitmentTestSection>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapSection(entity);
    }

    public async Task<RecruitmentTestSectionDto> UpdateSectionAsync(
        UpdateRecruitmentTestSectionDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<RecruitmentTestSection>().GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == dto.Id && s.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Test section '{dto.Id}' not found.");

        var test = await RequireOwnedPaperAsync(entity.RecruitmentTestId, cancellationToken);
        await RequireNotSatAsync(test, "rename a section", cancellationToken);

        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description?.Trim();
        entity.DisplayOrder = dto.DisplayOrder;
        Touch(entity);

        await _unitOfWork.Repository<RecruitmentTestSection>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapSection(entity);
    }

    public async Task<bool> DeleteSectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<RecruitmentTestSection>().GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Test section '{id}' not found.");

        var test = await RequireOwnedPaperAsync(entity.RecruitmentTestId, cancellationToken);
        await RequireNotSatAsync(test, "delete a section", cancellationToken);

        // ⚠ The questions are UNGROUPED, not deleted. The FK is Restrict precisely so that a
        // regrouping of the paper cannot destroy the questions — deleting a heading is not deleting
        // what was under it, and a cascade here would have made it so.
        var questions = await _unitOfWork.Repository<RecruitmentTestQuestion>().GetQueryable()
            .Where(q => q.TenantId == tenantId && q.RecruitmentTestSectionId == id)
            .ToListAsync(cancellationToken);

        foreach (var question in questions)
        {
            question.RecruitmentTestSectionId = null;
            await _unitOfWork.Repository<RecruitmentTestQuestion>().UpdateAsync(question);
        }

        await _unitOfWork.Repository<RecruitmentTestSection>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RecruitmentTestQuestionDto> AddQuestionAsync(
        CreateRecruitmentTestQuestionDto dto, CancellationToken cancellationToken = default)
    {
        var test = await RequireOwnedPaperAsync(dto.RecruitmentTestId, cancellationToken);
        await RequireNotSatAsync(test, "add a question", cancellationToken);
        await RequireSectionBelongsAsync(dto.RecruitmentTestSectionId, test, cancellationToken);
        ValidateQuestionShape(dto);

        var entity = new RecruitmentTestQuestion
        {
            TenantId = test.TenantId,
            RecruitmentTestId = test.Id,
            RecruitmentTestSectionId = dto.RecruitmentTestSectionId,
            QuestionText = dto.QuestionText.Trim(),
            QuestionType = dto.QuestionType,
            Points = dto.Points,
            ExpectedAnswer = dto.ExpectedAnswer?.Trim(),
            Explanation = dto.Explanation?.Trim(),
            DisplayOrder = dto.DisplayOrder > 0
                ? dto.DisplayOrder
                : test.Questions.Count + 1,
            CreatedById = ActorUserId,
        };

        await _unitOfWork.Repository<RecruitmentTestQuestion>().AddAsync(entity);

        foreach (var option in BuildOptions(dto, entity))
            await _unitOfWork.Repository<RecruitmentTestQuestionOption>().AddAsync(option);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await RequireOwnedQuestionAsync(entity.Id, cancellationToken);
        return MapQuestion(saved);
    }

    public async Task<RecruitmentTestQuestionDto> UpdateQuestionAsync(
        UpdateRecruitmentTestQuestionDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await RequireOwnedQuestionAsync(dto.Id, cancellationToken);
        var test = await RequireOwnedPaperAsync(entity.RecruitmentTestId, cancellationToken);
        await RequireNotSatAsync(test, "change a question", cancellationToken);
        await RequireSectionBelongsAsync(dto.RecruitmentTestSectionId, test, cancellationToken);
        ValidateQuestionShape(dto);

        entity.RecruitmentTestSectionId = dto.RecruitmentTestSectionId;
        entity.QuestionText = dto.QuestionText.Trim();
        entity.QuestionType = dto.QuestionType;
        entity.Points = dto.Points;
        entity.ExpectedAnswer = dto.ExpectedAnswer?.Trim();
        entity.Explanation = dto.Explanation?.Trim();
        if (dto.DisplayOrder > 0) entity.DisplayOrder = dto.DisplayOrder;
        Touch(entity);

        await _unitOfWork.Repository<RecruitmentTestQuestion>().UpdateAsync(entity);

        // ⚠ REPLACE-SET. The payload carries the whole list of options and one left out is removed —
        // the same convention a vacancy's shortlisting criterion values follow. A caller that sends
        // an empty list on a closed question is refused by ValidateQuestionShape rather than having
        // its choices silently deleted.
        var existing = await _unitOfWork.Repository<RecruitmentTestQuestionOption>().GetQueryable()
            .Where(o => o.TenantId == entity.TenantId && o.RecruitmentTestQuestionId == entity.Id)
            .ToListAsync(cancellationToken);

        foreach (var option in existing)
            await _unitOfWork.Repository<RecruitmentTestQuestionOption>().DeleteAsync(option);

        foreach (var option in BuildOptions(dto, entity))
            await _unitOfWork.Repository<RecruitmentTestQuestionOption>().AddAsync(option);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await RequireOwnedQuestionAsync(entity.Id, cancellationToken);
        return MapQuestion(saved);
    }

    public async Task<bool> DeleteQuestionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireOwnedQuestionAsync(id, cancellationToken);
        var test = await RequireOwnedPaperAsync(entity.RecruitmentTestId, cancellationToken);
        await RequireNotSatAsync(test, "delete a question", cancellationToken);

        foreach (var option in entity.Options.ToList())
            await _unitOfWork.Repository<RecruitmentTestQuestionOption>().DeleteAsync(option);

        await _unitOfWork.Repository<RecruitmentTestQuestion>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CandidateSittingDto> PreviewTestAsync(
        Guid testId, CancellationToken cancellationToken = default)
    {
        var test = await RequireOwnedPaperAsync(testId, cancellationToken);

        // ⚠ Through the candidate projection, unshuffled. The author needs to see the paper in the
        // order they wrote it; what they need the preview to PROVE is that the answers are not in it,
        // and that is proved by using the same projection the candidate is served.
        return new CandidateSittingDto
        {
            SittingId = Guid.Empty,
            TestName = test.Name,
            Instructions = test.Instructions,
            DurationMinutes = test.DurationMinutes,
            AttemptNumber = 0,
            Status = RecruitmentSittingStatus.NotStarted,
            Questions = ProjectForCandidate(test, shuffle: false),
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  E3 — ASSIGNMENT AND INVITATION
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<RecruitmentTestAssignmentDto> AssignAsync(
        CreateRecruitmentTestAssignmentDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var test = await RequireOwnedPaperAsync(dto.RecruitmentTestId, cancellationToken);

        if (!test.IsActive)
            throw new InvalidOperationException(
                $"'{test.Name}' is not active and cannot be assigned. Activate it first — activation " +
                "is where the paper is checked for questions that cannot be marked.");

        var hasVacancy = dto.JobVacancyId is { } v && v != Guid.Empty;
        var hasApplication = dto.JobApplicationId is { } a && a != Guid.Empty;

        if (hasVacancy == hasApplication)
            throw new InvalidOperationException(
                "Assign the test either to a vacancy or to one application, not both and not neither.");

        if (dto.OpensAt is { } opens && dto.ClosesAt is { } closes && closes <= opens)
            throw new InvalidOperationException("The closing date must be after the opening date.");

        if (hasVacancy)
        {
            var vacancy = await _unitOfWork.Repository<JobVacancy>().GetQueryable()
                .FirstOrDefaultAsync(x => x.Id == dto.JobVacancyId && x.TenantId == tenantId, cancellationToken)
                ?? throw new ArgumentException($"Vacancy '{dto.JobVacancyId}' not found.");

            var duplicate = await _unitOfWork.Repository<RecruitmentTestAssignment>().GetQueryable()
                .AnyAsync(x => x.TenantId == tenantId
                            && x.RecruitmentTestId == test.Id
                            && x.JobVacancyId == vacancy.Id, cancellationToken);

            if (duplicate)
                throw new InvalidOperationException(
                    $"'{test.Name}' is already assigned to this vacancy. Open that assignment to change " +
                    "its window, or grant an extra attempt to a candidate who needs to re-sit.");
        }
        else
        {
            var application = await _unitOfWork.Repository<JobApplication>().GetQueryable()
                .FirstOrDefaultAsync(x => x.Id == dto.JobApplicationId && x.TenantId == tenantId, cancellationToken)
                ?? throw new ArgumentException($"Application '{dto.JobApplicationId}' not found.");

            var duplicate = await _unitOfWork.Repository<RecruitmentTestAssignment>().GetQueryable()
                .AnyAsync(x => x.TenantId == tenantId
                            && x.RecruitmentTestId == test.Id
                            && x.JobApplicationId == application.Id, cancellationToken);

            if (duplicate)
                throw new InvalidOperationException(
                    $"'{test.Name}' is already assigned to this candidate.");
        }

        var entity = new RecruitmentTestAssignment
        {
            TenantId = tenantId,
            RecruitmentTestId = test.Id,
            JobVacancyId = hasVacancy ? dto.JobVacancyId : null,
            JobApplicationId = hasApplication ? dto.JobApplicationId : null,
            OpensAt = dto.OpensAt,
            ClosesAt = dto.ClosesAt,
            IsRequired = dto.IsRequired,
            CreatedById = ActorUserId,
        };

        await _unitOfWork.Repository<RecruitmentTestAssignment>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapAssignmentAsync(entity.Id, cancellationToken);
    }

    public async Task<IEnumerable<RecruitmentTestAssignmentDto>> GetAssignmentsAsync(
        Guid? testId = null, Guid? vacancyId = null, Guid? applicationId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = AssignmentsWithNavigations()
            .Where(a => a.TenantId == tenantId);

        if (testId is { } t) query = query.Where(a => a.RecruitmentTestId == t);
        if (vacancyId is { } v) query = query.Where(a => a.JobVacancyId == v);
        if (applicationId is { } ap)
            // An application is reached by its OWN assignment and by its vacancy's — the second is
            // how a vacancy-wide test appears on one candidate's record, and a filter that missed it
            // would show an empty assessments tab for a candidate who has one to sit.
            query = query.Where(a => a.JobApplicationId == ap
                                  || (a.JobVacancyId != null
                                      && a.JobVacancy!.JobApplications.Any(j => j.Id == ap)));

        var rows = await query.OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();

        var counts = await _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
            .Where(s => s.TenantId == tenantId && ids.Contains(s.RecruitmentTestAssignmentId))
            .GroupBy(s => s.RecruitmentTestAssignmentId)
            .Select(g => new { AssignmentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.Select(r => MapAssignment(
            r, counts.FirstOrDefault(c => c.AssignmentId == r.Id)?.Count ?? 0)).ToList();
    }

    public async Task<bool> DeleteAssignmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<RecruitmentTestAssignment>().GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Test assignment '{id}' not found.");

        var sat = await _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId && s.RecruitmentTestAssignmentId == id, cancellationToken);

        if (sat)
            throw new InvalidOperationException(
                "Candidates have already sat this assignment, so it cannot be withdrawn. Close the " +
                "window instead — that stops new attempts and leaves the marks where they are.");

        await _unitOfWork.Repository<RecruitmentTestAssignment>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> InviteAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var assignment = await AssignmentsWithNavigations()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Test assignment '{assignmentId}' not found.");

        var targets = await ResolveAssignmentApplicationsAsync(assignment, cancellationToken);

        if (targets.Count == 0)
            throw new InvalidOperationException(
                "There is nobody to invite. A vacancy-wide test reaches applications that are still " +
                "live — not the withdrawn, rejected or already-hired ones.");

        var sent = 0;
        foreach (var application in targets)
        {
            var email = application.JobCandidate?.Email;
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning(
                    "Application {Number} has no email address — no test invitation could be sent.",
                    application.ApplicationNumber);
                continue;
            }

            var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CandidateName"] = application.JobCandidate?.FullName,
                ["JobTitle"] = application.JobVacancy?.JobTitle,
                ["ApplicationNumber"] = application.ApplicationNumber,
                ["TestName"] = assignment.Test.Name,
                ["TestInstructions"] = assignment.Test.Instructions,
                ["DurationText"] = assignment.Test.DurationMinutes is { } minutes
                    ? $"{minutes} minutes"
                    : "no time limit",
                ["QuestionCount"] = assignment.Test.Questions.Count(q => !q.IsDeleted).ToString(),
                ["OpensAt"] = assignment.OpensAt?.ToString("dddd, d MMMM yyyy 'at' HH:mm"),
                ["ClosesAt"] = assignment.ClosesAt?.ToString("dddd, d MMMM yyyy 'at' HH:mm"),
                ["AttemptsAllowed"] = (assignment.Test.MaxAttempts + assignment.ExtraAttemptsGranted).ToString(),
            };

            try
            {
                var send = _templatedEmail.SendAsync(
                    RecruitmentEmailCatalog.Module,
                    RecruitmentEmailCatalog.Events.TestInvitation,
                    email,
                    tokens);

                if (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken)) == send)
                {
                    await send;
                    sent++;
                }
                else
                {
                    _logger.LogWarning(
                        "Test invitation to {Email} timed out after 10 s — the assignment still stands.",
                        email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send the test invitation to {Email} — the assignment still stands.", email);
            }
        }

        assignment.InvitedAt = DateTime.UtcNow;
        Touch(assignment);
        await _unitOfWork.Repository<RecruitmentTestAssignment>().UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Test '{Test}': {Sent} of {Total} candidate(s) invited.",
            assignment.Test.Name, sent, targets.Count);

        return sent;
    }

    public async Task<RecruitmentTestAssignmentDto> GrantExtraAttemptAsync(
        GrantExtraAttemptDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException(
                "A re-sit is granted with a reason on record. Say what happened.");

        var assignment = await _unitOfWork.Repository<RecruitmentTestAssignment>().GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == dto.AssignmentId && a.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Test assignment '{dto.AssignmentId}' not found.");

        assignment.ExtraAttemptsGranted += dto.ExtraAttempts;

        // Appended, not replaced: a second grant for a different reason must not erase the first.
        var stamp = $"{DateTime.UtcNow:yyyy-MM-dd}: +{dto.ExtraAttempts} — {dto.Reason.Trim()}";
        assignment.ExtraAttemptReason = string.IsNullOrWhiteSpace(assignment.ExtraAttemptReason)
            ? stamp
            : $"{assignment.ExtraAttemptReason}\n{stamp}";

        if (assignment.ExtraAttemptReason.Length > 1000)
            assignment.ExtraAttemptReason = assignment.ExtraAttemptReason[^1000..];

        Touch(assignment);
        await _unitOfWork.Repository<RecruitmentTestAssignment>().UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Assignment {AssignmentId}: {Extra} extra attempt(s) granted — {Reason}",
            assignment.Id, dto.ExtraAttempts, dto.Reason);

        return await MapAssignmentAsync(assignment.Id, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  E5 — HR'S SIDE OF A SITTING
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<RecruitmentTestSittingDto>> GetSittingsAsync(
        Guid? testId = null, Guid? assignmentId = null, Guid? applicationId = null,
        Guid? vacancyId = null, RecruitmentSittingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = SittingsWithNavigations().Where(s => s.TenantId == tenantId);

        if (testId is { } t) query = query.Where(s => s.Assignment.RecruitmentTestId == t);
        if (assignmentId is { } a) query = query.Where(s => s.RecruitmentTestAssignmentId == a);
        if (applicationId is { } ap) query = query.Where(s => s.JobApplicationId == ap);
        if (vacancyId is { } v) query = query.Where(s => s.JobApplication.JobVacancyId == v);
        if (status is { } st) query = query.Where(s => s.Status == st);

        var rows = await query
            .OrderByDescending(s => s.SubmittedAt ?? s.StartedAt ?? s.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(MapSitting).ToList();
    }

    public async Task<RecruitmentTestSittingDto> GetSittingAsync(
        Guid id, CancellationToken cancellationToken = default)
        => MapSitting(await RequireOwnedSittingAsync(id, cancellationToken));

    public async Task<RecruitmentTestSittingDto> MarkAnswerAsync(
        MarkFreeTextAnswerDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var answer = await _unitOfWork.Repository<RecruitmentTestAnswer>().GetQueryable()
            .Include(x => x.Question)
            .FirstOrDefaultAsync(x => x.Id == dto.AnswerId && x.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Answer '{dto.AnswerId}' not found.");

        if (answer.Question.QuestionType != RecruitmentQuestionType.FreeText)
            throw new InvalidOperationException(
                "Only a written answer is marked by hand. The closed questions were marked when the " +
                "candidate submitted, and overwriting one would mean the paper no longer says what " +
                "the marking key says.");

        if (dto.PointsAwarded > answer.Question.Points)
            throw new InvalidOperationException(
                $"This question is worth {answer.Question.Points:0.##} mark(s); " +
                $"{dto.PointsAwarded:0.##} cannot be awarded for it.");

        var sitting = await RequireOwnedSittingAsync(answer.RecruitmentTestSittingId, cancellationToken);

        if (sitting.Status is not (RecruitmentSittingStatus.AwaitingMarking or RecruitmentSittingStatus.Expired))
            throw new InvalidOperationException(
                $"This sitting is {sitting.Status} and is not open for marking.");

        answer.PointsAwarded = dto.PointsAwarded;
        answer.IsCorrect = dto.PointsAwarded > 0;
        answer.IsManuallyMarked = true;
        answer.MarkerComment = dto.MarkerComment?.Trim();
        Touch(answer);

        await _unitOfWork.Repository<RecruitmentTestAnswer>().UpdateAsync(answer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapSitting(await RequireOwnedSittingAsync(sitting.Id, cancellationToken));
    }

    public async Task<RecruitmentTestSittingDto> FinaliseSittingAsync(
        FinaliseSittingDto dto, CancellationToken cancellationToken = default)
    {
        var sitting = await RequireOwnedSittingAsync(dto.SittingId, cancellationToken);

        if (sitting.Status is RecruitmentSittingStatus.NotStarted or RecruitmentSittingStatus.InProgress)
            throw new InvalidOperationException(
                "This sitting has not been submitted yet, so there is nothing to finalise.");

        if (sitting.Status == RecruitmentSittingStatus.Cancelled)
            throw new InvalidOperationException("A cancelled sitting cannot be finalised.");

        var unmarked = sitting.Answers.Count(a =>
            !a.IsDeleted
            && a.Question.QuestionType == RecruitmentQuestionType.FreeText
            && !a.IsManuallyMarked);

        if (unmarked > 0)
            throw new InvalidOperationException(
                $"{unmarked} written answer(s) still need a mark. A paper finalised with an essay " +
                "unmarked publishes a score that is missing the essay.");

        await FinaliseCoreAsync(sitting, dto.MarkerNotes, ActorEmployeeId, cancellationToken);
        return MapSitting(await RequireOwnedSittingAsync(sitting.Id, cancellationToken));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  E6 — OFFLINE: WHO AN ASSIGNMENT REACHES, AND A PAPER SITTING ENTERED BY HR
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<RecruitmentTestAssignmentCandidateDto>> GetAssignmentCandidatesAsync(
        Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;

        var assignment = await AssignmentsWithNavigations()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Test assignment '{assignmentId}' not found.");

        var applications = await ResolveAssignmentApplicationsAsync(assignment, cancellationToken);
        var applicationIds = applications.Select(a => a.Id).ToList();

        var sittings = await _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
            .Where(x => x.TenantId == tenantId
                     && x.RecruitmentTestAssignmentId == assignment.Id
                     && applicationIds.Contains(x.JobApplicationId)
                     && x.Status != RecruitmentSittingStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var allowed = assignment.Test.MaxAttempts + assignment.ExtraAttemptsGranted;
        var untimed = assignment.Test.DurationMinutes is null;

        return applications
            .OrderBy(a => a.JobCandidate?.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.JobCandidate?.FirstName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(a =>
            {
                var mine = sittings.Where(x => x.JobApplicationId == a.Id).OrderBy(x => x.AttemptNumber).ToList();
                var last = mine.LastOrDefault();
                var open = mine.Any(x => IsOpenOnline(x, now));

                // ⚠ Only a FINALISED attempt has a figure. One still waiting on its essay is not a
                // score, and the column would otherwise show a number the ledger does not hold.
                var finalised = last is { JobApplicantTestResultId: not null };

                var blocked = open
                    ? untimed
                        ? "An online attempt is open, and this paper has no time limit, so it will not close by itself. Ask the candidate to submit it first."
                        : "An online attempt is still running. It must be submitted, or run out of time, before a paper sitting can be recorded."
                    : mine.Count >= allowed
                        ? (allowed == 1 ? "Has already sat this test." : $"Has used all {allowed} attempts.")
                          + " Grant a re-sit to record another."
                        : null;

                return new RecruitmentTestAssignmentCandidateDto
                {
                    JobApplicationId = a.Id,
                    ApplicationNumber = a.ApplicationNumber,
                    CandidateName = a.JobCandidate?.FullName ?? string.Empty,
                    AttemptsUsed = mine.Count,
                    AttemptsAllowed = allowed,
                    LastSittingId = last?.Id,
                    LastStatus = last?.Status,
                    LastMode = last?.Mode,
                    LastScorePercent = finalised ? last!.ScorePercent : null,
                    Passed = finalised ? last!.Passed : null,
                    HasOpenOnlineAttempt = open,
                    CanRecordPaperSitting = blocked is null,
                    BlockedReason = blocked,
                };
            })
            .ToList();
    }

    public async Task<RecruitmentTestSittingDto> RecordPaperSittingAsync(
        RecordPaperSittingDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;

        var assignment = await AssignmentsWithNavigations()
            .FirstOrDefaultAsync(a => a.Id == dto.AssignmentId && a.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Test assignment '{dto.AssignmentId}' not found.");

        var paper = await _testRepository.GetWithFullPaperAsync(assignment.RecruitmentTestId, cancellationToken)
            ?? throw new ArgumentException("The paper behind that assignment could not be found.");

        var application = await _unitOfWork.Repository<JobApplication>().GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == dto.JobApplicationId && a.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Application '{dto.JobApplicationId}' not found.");

        // ⚠ The SAME reach rule the portal uses to let a candidate start. A result is not recorded
        // against an application that is withdrawn, rejected or already hired.
        if (!RecruitmentTestReach.Reaches(assignment, application))
            throw new InvalidOperationException(
                "This test is not assigned to that candidate, or their application is no longer live "
                + "(withdrawn, rejected or hired), and a result is not recorded against a closed application.");

        // ── when: inside the window, and not in the future ────────────────────
        //
        // ⚠ Checked against the date it was SAT, not today. A paper sat on the last day of the window
        // and typed in the week after is on time; one sat after the window closed is not, whenever it
        // is entered.
        var satOn = AsUtc(dto.SatOn)!.Value;

        if (satOn > now.AddMinutes(5))
            throw new InvalidOperationException(
                "A sitting cannot be recorded for a date that has not happened yet.");

        if (assignment.OpensAt is { } opens && satOn < opens)
            throw new InvalidOperationException(
                $"This test opened on {opens:d MMMM yyyy}; a paper sat before then cannot be recorded against it.");

        if (assignment.ClosesAt is { } closes && satOn > closes)
            throw new InvalidOperationException(
                $"This test closed on {closes:d MMMM yyyy}; a paper sat after that cannot be recorded against it.");

        // ── attempts: a paper attempt is an attempt ───────────────────────────
        var sittings = await _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
            .Where(x => x.TenantId == tenantId
                     && x.RecruitmentTestAssignmentId == assignment.Id
                     && x.JobApplicationId == application.Id
                     && x.Status != RecruitmentSittingStatus.Cancelled)
            .ToListAsync(cancellationToken);

        // ⚠ Not over a running online attempt. Two live attempts at one paper would be finalised in
        // whichever order they finished, and the ledger row belongs to the LAST one finalised — so the
        // result shown could silently be the attempt HR did not mean.
        if (sittings.Any(x => IsOpenOnline(x, now)))
            throw new InvalidOperationException(
                paper.DurationMinutes is null
                    ? "This candidate has an online attempt open, and the paper has no time limit, so it will not "
                      + "close by itself. Ask them to submit it before a paper sitting is recorded."
                    : "This candidate has an online attempt still running. Let it be submitted, or run out of "
                      + "time, before a paper sitting is recorded.");

        var allowed = paper.MaxAttempts + assignment.ExtraAttemptsGranted;
        if (sittings.Count >= allowed)
            throw new InvalidOperationException(
                (allowed == 1 ? "This candidate has already sat this test." : $"This candidate has used all {allowed} attempts.")
                + " Grant a re-sit, with the reason, before recording another.");

        // ── what was ticked, and the written marks ────────────────────────────
        var questions = paper.Questions.Where(q => !q.IsDeleted).ToDictionary(q => q.Id);
        ValidatePaperAnswers(dto.Answers ?? new List<PaperAnswerDto>(), questions);

        if (dto.InvigilatedById is { } invigilatorId && invigilatorId != Guid.Empty)
        {
            // ⚠ The ledger's InvigilatedById is a foreign key to Employees. Checked here so a bad id
            // is a sentence, not a constraint violation after the sitting has been written.
            var known = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Employee>().GetQueryable()
                .AnyAsync(e => e.Id == invigilatorId && e.TenantId == tenantId, cancellationToken);
            if (!known)
                throw new ArgumentException("That invigilator is not an employee of this organisation.");
        }

        var sitting = new RecruitmentTestSitting
        {
            TenantId = tenantId,
            RecruitmentTestAssignmentId = assignment.Id,
            Assignment = assignment,
            JobApplicationId = application.Id,
            AttemptNumber = sittings.Count + 1,
            Mode = RecruitmentSittingMode.Paper,
            StartedAt = satOn,
            SubmittedAt = satOn,
            Status = RecruitmentSittingStatus.AwaitingMarking,
            CreatedById = ActorUserId,
        };

        await _unitOfWork.Repository<RecruitmentTestSitting>().AddAsync(sitting);

        // ⚠ The closed questions are marked by the SAME marker the portal uses, against the whole
        // paper — the denominator rule, exact set equality, numbers compared as numbers. A paper
        // script and an online one cannot be marked two different ways.
        var submitted = (dto.Answers ?? new List<PaperAnswerDto>()).ToDictionary(
            a => a.QuestionId,
            a => new SubmittedAnswer(
                a.QuestionId,
                (a.SelectedOptionIds ?? new List<Guid>()).Distinct().ToList(),
                string.IsNullOrWhiteSpace(a.FreeTextAnswer) ? null : a.FreeTextAnswer.Trim(),
                string.IsNullOrWhiteSpace(a.NumericAnswer) ? null : a.NumericAnswer.Trim()));

        await ApplyMarksAsync(sitting, paper, submitted, cancellationToken);

        // The written answers, as the marker marked them on the script.
        foreach (var answer in sitting.Answers.Where(a =>
                     !a.IsDeleted && a.Question.QuestionType == RecruitmentQuestionType.FreeText))
        {
            var entry = dto.Answers!.First(x => x.QuestionId == answer.RecruitmentTestQuestionId);
            answer.PointsAwarded = entry.PointsAwarded!.Value;
            answer.IsCorrect = entry.PointsAwarded > 0;
            answer.IsManuallyMarked = true;
            answer.MarkerComment = string.IsNullOrWhiteSpace(entry.MarkerComment) ? null : entry.MarkerComment.Trim();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await FinaliseCoreAsync(sitting, dto.MarkerNotes, ActorEmployeeId, cancellationToken,
            venue: string.IsNullOrWhiteSpace(dto.Venue) ? null : dto.Venue.Trim(),
            invigilatedById: dto.InvigilatedById is { } i && i != Guid.Empty ? (Guid?)i : null);

        _logger.LogInformation(
            "Paper sitting recorded for application {Number}: attempt {Attempt} of '{Test}', {Score}%.",
            application.ApplicationNumber, sitting.AttemptNumber, paper.Name, sitting.ScorePercent);

        return MapSitting(await RequireOwnedSittingAsync(sitting.Id, cancellationToken));
    }

    /// <summary>
    /// Refuses a paper entry that could not have come off a real script — naming the question by the
    /// number PRINTED on the paper, since that is what the person typing is looking at.
    /// </summary>
    /// <remarks>
    /// ⚠ What it deliberately does NOT refuse: two boxes ticked on a one-answer question, or words in a
    /// numeric answer. A candidate can do both on paper. The entry records what they did, and the key
    /// marks it wrong — refusing it would force HR to "correct" the script before entering it.
    /// </remarks>
    private static void ValidatePaperAnswers(
        IReadOnlyList<PaperAnswerDto> answers,
        IReadOnlyDictionary<Guid, RecruitmentTestQuestion> questions)
    {
        // Numbered as the printed paper numbers them: authoring order, from 1.
        var printed = questions.Values
            .OrderBy(q => q.DisplayOrder).ThenBy(q => q.CreatedAt)
            .Select((q, index) => (q.Id, Number: index + 1))
            .ToDictionary(x => x.Id, x => x.Number);

        var seen = new HashSet<Guid>();

        foreach (var answer in answers)
        {
            if (!questions.TryGetValue(answer.QuestionId, out var q))
                throw new InvalidOperationException("One of the answers is for a question that is not on this paper.");

            var n = printed[q.Id];
            if (!seen.Add(q.Id))
                throw new InvalidOperationException($"Question {n} is entered twice. Enter each question once.");

            var chosen = answer.SelectedOptionIds ?? new List<Guid>();

            if (q.QuestionType == RecruitmentQuestionType.FreeText)
            {
                if (chosen.Count > 0 || !string.IsNullOrWhiteSpace(answer.NumericAnswer))
                    throw new InvalidOperationException($"Question {n} is a written answer; it has no boxes and no number.");

                if (answer.PointsAwarded is { } awarded && (awarded < 0 || awarded > q.Points))
                    throw new InvalidOperationException(
                        $"Question {n} is worth {q.Points:0.##} mark(s); {awarded:0.##} cannot be awarded for it.");
                continue;
            }

            // ⚠ The one refusal that protects the key: a closed question is marked BY the key.
            if (answer.PointsAwarded is not null)
                throw new InvalidOperationException(
                    $"Question {n} is marked by the key, not by hand. Enter what the candidate ticked and the key "
                    + "will mark it — a typed mark there would be the marker overriding the key.");

            if (q.QuestionType == RecruitmentQuestionType.Numeric)
            {
                if (chosen.Count > 0)
                    throw new InvalidOperationException($"Question {n} asks for a number; it has no boxes to tick.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(answer.NumericAnswer))
                throw new InvalidOperationException($"Question {n} is answered by ticking boxes, not with a number.");

            var valid = q.Options.Where(o => !o.IsDeleted).Select(o => o.Id).ToHashSet();
            if (chosen.Any(id => !valid.Contains(id)))
                throw new InvalidOperationException($"A box entered for question {n} is not one of its choices.");
        }

        var unmarked = questions.Values
            .Where(q => q.QuestionType == RecruitmentQuestionType.FreeText
                     && !answers.Any(a => a.QuestionId == q.Id && a.PointsAwarded is not null))
            .Select(q => printed[q.Id])
            .OrderBy(x => x)
            .ToList();

        if (unmarked.Count > 0)
            throw new InvalidOperationException(
                (unmarked.Count == 1
                    ? $"The written answer to question {unmarked[0]} has no mark."
                    : $"The written answers to questions {string.Join(", ", unmarked)} have no mark.")
                + " A paper sitting is entered from a marked script — give every written answer its mark, "
                + "0 where nothing was written.");
    }

    public async Task<int> ExpireOverdueSittingsAsync(
        Guid tenantId, DateTime now, Guid? actingUserId, CancellationToken cancellationToken = default)
    {
        var overdue = await SittingsWithNavigations()
            .Where(s => s.TenantId == tenantId
                     && s.Status == RecruitmentSittingStatus.InProgress
                     && s.MustSubmitBy != null
                     && s.MustSubmitBy < now)
            .ToListAsync(cancellationToken);

        foreach (var sitting in overdue)
        {
            var paper = await _testRepository.GetWithFullPaperAsync(
                sitting.Assignment.RecruitmentTestId, cancellationToken);
            if (paper is null) continue;

            // Marked on what was SAVED before the clock ran out, which is why progress is stored on
            // the server. A sitting expired with no saved answers scores zero on the closed
            // questions — that is what an abandoned paper is worth, and it is not the same thing as
            // never having started, which stays NotStarted and is never scored at all.
            await ApplyMarksAsync(sitting, paper, CollectSavedAnswers(sitting), cancellationToken);
            sitting.Status = RecruitmentSittingStatus.Expired;
            sitting.SubmittedAt ??= sitting.MustSubmitBy;
            sitting.LastModifiedById = actingUserId;
            sitting.UpdatedAt = now;

            await _unitOfWork.Repository<RecruitmentTestSitting>().UpdateAsync(sitting);
        }

        if (overdue.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "{Count} recruitment test sitting(s) expired for tenant {TenantId}.", overdue.Count, tenantId);
        }

        return overdue.Count;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  E4 — THE CANDIDATE'S SIDE
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<CandidateAssessmentSummaryDto>> GetMyAssessmentsAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var candidateId = await RequireCandidateAsync(userId, tenantId, cancellationToken);
        var now = DateTime.UtcNow;

        var applications = await _unitOfWork.Repository<JobApplication>().GetQueryable()
            .Include(a => a.JobVacancy).ThenInclude(v => v.Requisition).ThenInclude(r => r.JobDescription)
            .Where(a => a.TenantId == tenantId && a.JobCandidateId == candidateId)
            .ToListAsync(cancellationToken);

        if (applications.Count == 0) return Array.Empty<CandidateAssessmentSummaryDto>();

        var applicationIds = applications.Select(a => a.Id).ToList();
        var vacancyIds = applications.Select(a => a.JobVacancyId).Distinct().ToList();

        var assignments = await AssignmentsWithNavigations()
            .Where(a => a.TenantId == tenantId
                     && ((a.JobApplicationId != null && applicationIds.Contains(a.JobApplicationId.Value))
                      || (a.JobVacancyId != null && vacancyIds.Contains(a.JobVacancyId.Value))))
            .ToListAsync(cancellationToken);

        var assignmentIds = assignments.Select(a => a.Id).ToList();

        var sittings = await _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
            .Where(s => s.TenantId == tenantId
                     && assignmentIds.Contains(s.RecruitmentTestAssignmentId)
                     && applicationIds.Contains(s.JobApplicationId))
            .ToListAsync(cancellationToken);

        var result = new List<CandidateAssessmentSummaryDto>();

        foreach (var assignment in assignments)
        {
            foreach (var application in applications)
            {
                if (!RecruitmentTestReach.Reaches(assignment, application)) continue;

                var mine = sittings
                    .Where(s => s.RecruitmentTestAssignmentId == assignment.Id
                             && s.JobApplicationId == application.Id
                             && s.Status != RecruitmentSittingStatus.Cancelled)
                    .OrderBy(s => s.AttemptNumber)
                    .ToList();

                var allowed = assignment.Test.MaxAttempts + assignment.ExtraAttemptsGranted;
                var inProgress = mine.FirstOrDefault(s => s.Status == RecruitmentSittingStatus.InProgress
                                                       && (s.MustSubmitBy is null || s.MustSubmitBy > now));
                var last = mine.LastOrDefault();

                var summary = new CandidateAssessmentSummaryDto
                {
                    AssignmentId = assignment.Id,
                    JobApplicationId = application.Id,
                    ApplicationNumber = application.ApplicationNumber,
                    JobTitle = application.JobVacancy?.JobTitle ?? string.Empty,
                    TestName = assignment.Test.Name,
                    Description = assignment.Test.Description,
                    DurationMinutes = assignment.Test.DurationMinutes,
                    QuestionCount = assignment.Test.Questions.Count(q => !q.IsDeleted),
                    IsRequired = assignment.IsRequired,
                    OpensAt = AsUtc(assignment.OpensAt),
                    ClosesAt = AsUtc(assignment.ClosesAt),
                    AttemptsUsed = mine.Count,
                    AttemptsAllowed = allowed,
                    InProgressSittingId = inProgress?.Id,
                    LastStatus = last?.Status,
                    LastSubmittedAt = AsUtc(last?.SubmittedAt),
                };

                // ⚠ Only a FINALISED sitting shows the candidate a number. A mark that is still being
                // worked on is not a mark, and one shown and then corrected is a mark the
                // organisation will be held to.
                if (last is { Status: RecruitmentSittingStatus.Marked, JobApplicantTestResultId: not null })
                {
                    summary.ReleasedScorePercent = last.ScorePercent;
                    summary.Passed = last.Passed;
                }

                summary.BlockedReason = BlockedReason(assignment, mine.Count, allowed, inProgress, now);
                summary.CanStart = summary.BlockedReason is null;

                result.Add(summary);
            }
        }

        return result
            .OrderBy(r => r.CanStart ? 0 : 1)
            .ThenBy(r => r.ClosesAt ?? DateTime.MaxValue)
            .ToList();
    }

    public async Task<CandidateSittingDto> StartSittingAsync(
        Guid userId, Guid tenantId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var candidateId = await RequireCandidateAsync(userId, tenantId, cancellationToken);
        var now = DateTime.UtcNow;

        var assignment = await AssignmentsWithNavigations()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("That assessment could not be found.");

        var applications = await _unitOfWork.Repository<JobApplication>().GetQueryable()
            .Where(a => a.TenantId == tenantId && a.JobCandidateId == candidateId)
            .ToListAsync(cancellationToken);

        var mine = applications.FirstOrDefault(a => RecruitmentTestReach.Reaches(assignment, a))
            ?? throw new ArgumentException("That assessment could not be found.");

        var paper = await _testRepository.GetWithFullPaperAsync(assignment.RecruitmentTestId, cancellationToken)
            ?? throw new ArgumentException("That assessment could not be found.");

        var sittings = await _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
            .Where(s => s.TenantId == tenantId
                     && s.RecruitmentTestAssignmentId == assignment.Id
                     && s.JobApplicationId == mine.Id
                     && s.Status != RecruitmentSittingStatus.Cancelled)
            .OrderBy(s => s.AttemptNumber)
            .ToListAsync(cancellationToken);

        var allowed = assignment.Test.MaxAttempts + assignment.ExtraAttemptsGranted;
        var live = sittings.FirstOrDefault(s => s.Status == RecruitmentSittingStatus.InProgress
                                             && (s.MustSubmitBy is null || s.MustSubmitBy > now));

        // Re-opening the attempt that is already running is not a new attempt, and must not consume
        // one. It re-issues the session token — see CandidateSittingDto.AccessToken.
        if (live is not null)
            return await ReopenAsync(live, paper, cancellationToken);

        var blocked = BlockedReason(assignment, sittings.Count, allowed, inProgress: null, now);
        if (blocked is not null) throw new InvalidOperationException(blocked);

        var token = GenerateAccessToken();
        var sitting = new RecruitmentTestSitting
        {
            TenantId = tenantId,
            RecruitmentTestAssignmentId = assignment.Id,
            JobApplicationId = mine.Id,
            AttemptNumber = sittings.Count + 1,
            StartedAt = now,
            Status = RecruitmentSittingStatus.InProgress,
            AccessTokenHash = HashAccessToken(token),
            AccessTokenLast4 = token[^4..],
            CreatedById = userId,

            // ⚠ FIXED HERE, ONCE. Recomputing this at submit from "now minus duration" would let
            // somebody close the tab and come back tomorrow with a full clock.
            MustSubmitBy = assignment.Test.DurationMinutes is { } minutes
                ? now.AddMinutes(minutes)
                : null,
        };

        // A test that closes before the candidate's own clock runs out closes at the WINDOW. Without
        // this, starting a 60-minute paper five minutes before the window shuts would hand out an
        // hour that the assignment does not have.
        if (assignment.ClosesAt is { } closes && (sitting.MustSubmitBy is null || closes < sitting.MustSubmitBy))
            sitting.MustSubmitBy = closes;

        await _unitOfWork.Repository<RecruitmentTestSitting>().AddAsync(sitting);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Application {Number} started attempt {Attempt} of '{Test}'.",
            mine.ApplicationNumber, sitting.AttemptNumber, assignment.Test.Name);

        var dto = ProjectSittingForCandidate(sitting, paper);
        dto.AccessToken = token;
        return dto;
    }

    public async Task<CandidateSittingDto> GetMySittingAsync(
        Guid userId, Guid tenantId, Guid sittingId, CancellationToken cancellationToken = default)
    {
        var (sitting, paper) = await RequireOwnSittingAsync(userId, tenantId, sittingId, cancellationToken);

        // A finished attempt is read-only: no new token, and the answers are shown as submitted.
        if (sitting.Status != RecruitmentSittingStatus.InProgress)
            return ProjectSittingForCandidate(sitting, paper);

        return await ReopenAsync(sitting, paper, cancellationToken);
    }

    public async Task<CandidateSittingDto> SaveProgressAsync(
        Guid userId, Guid tenantId, SubmitSittingDto dto, CancellationToken cancellationToken = default)
    {
        var (sitting, paper) = await RequireOwnSittingAsync(userId, tenantId, dto.SittingId, cancellationToken);
        RequireLiveSession(sitting, dto.AccessToken);

        if (sitting.Status != RecruitmentSittingStatus.InProgress)
            throw new InvalidOperationException("This assessment has already been submitted.");

        // ⚠ Saved UNMARKED. IsCorrect and PointsAwarded stay false and zero until the paper is
        // submitted; writing a verdict here would mean the answers table held a mark for work the
        // candidate is still changing.
        await WriteAnswersAsync(sitting, paper, ToSubmitted(dto.Answers), marks: null, cancellationToken);
        Touch(sitting);

        await _unitOfWork.Repository<RecruitmentTestSitting>().UpdateAsync(sitting);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var result = ProjectSittingForCandidate(sitting, paper);
        result.AccessToken = dto.AccessToken;
        return result;
    }

    public async Task<CandidateSittingResultDto> SubmitSittingAsync(
        Guid userId, Guid tenantId, SubmitSittingDto dto, CancellationToken cancellationToken = default)
    {
        var (sitting, paper) = await RequireOwnSittingAsync(userId, tenantId, dto.SittingId, cancellationToken);
        RequireLiveSession(sitting, dto.AccessToken);

        if (sitting.Status != RecruitmentSittingStatus.InProgress)
            throw new InvalidOperationException("This assessment has already been submitted.");

        var now = DateTime.UtcNow;
        var timedOut = sitting.MustSubmitBy is { } deadline && now > deadline + SubmitGrace;

        // ⚠ THE TIMER IS ENFORCED HERE. Past the deadline the late payload is ignored and the sitting
        // is marked on what was saved before it. It does not refuse: refusing would lose work done in
        // time, and accepting late answers would make the clock decorative.
        var answers = timedOut ? CollectSavedAnswers(sitting) : ToSubmitted(dto.Answers);

        var marking = await ApplyMarksAsync(sitting, paper, answers, cancellationToken);
        sitting.SubmittedAt = timedOut ? sitting.MustSubmitBy : now;
        sitting.Status = timedOut
            ? RecruitmentSittingStatus.Expired
            : RecruitmentSittingStatus.AwaitingMarking;
        sitting.LastModifiedById = userId;
        sitting.UpdatedAt = now;

        await _unitOfWork.Repository<RecruitmentTestSitting>().UpdateAsync(sitting);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ⚠ A paper the machine settled outright is FINALISED NOW — the ledger row is written and the
        // application re-scored, which is the whole point of the lane. A paper with written answers
        // waits for a human; there is no honest score to publish until somebody has read them.
        var settled = !marking.AwaitingManualMarking;
        if (settled)
        {
            await FinaliseCoreAsync(sitting, markerNotes: null, markedByEmployeeId: null, cancellationToken);
        }

        _logger.LogInformation(
            "Sitting {SittingId} submitted ({Status}); {Correct}/{Gradable} closed question(s) correct.",
            sitting.Id, sitting.Status, marking.CorrectCount, marking.GradableCount);

        return new CandidateSittingResultDto
        {
            SittingId = sitting.Id,
            Status = sitting.Status,
            SubmittedAt = AsUtc(sitting.SubmittedAt),
            AwaitingMarking = !settled,
            TimedOut = timedOut,
            ScorePercent = settled ? sitting.ScorePercent : null,
            Passed = settled ? sitting.Passed : null,
            Message = BuildSubmitMessage(timedOut, settled, sitting),
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MARKING AND FINALISATION
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks the closed questions and writes the answer rows.
    /// </summary>
    /// <remarks>
    /// ⚠ <paramref name="paper"/> is the WHOLE paper, and that is the load-bearing argument. The
    /// denominator is every gradable question on it, not the ones that came back — see
    /// <see cref="RecruitmentTestMarker"/> for what happened when orientation got this wrong.
    /// </remarks>
    private async Task<MarkingResult> ApplyMarksAsync(
        RecruitmentTestSitting sitting,
        RecruitmentTest paper,
        IReadOnlyDictionary<Guid, SubmittedAnswer> answers,
        CancellationToken cancellationToken)
    {
        var questions = paper.Questions.Where(q => !q.IsDeleted).OrderBy(q => q.DisplayOrder).ToList();
        var marking = RecruitmentTestMarker.Mark(questions, answers);

        await WriteAnswersAsync(sitting, paper, answers, marking, cancellationToken);

        sitting.AutoScore = marking.AwardedPoints;
        sitting.TotalPoints = marking.TotalPoints;

        // ⚠ Neither ScorePercent nor Passed is written here. The machine's part is not the mark: a
        // paper with an unmarked essay has no percentage yet, and publishing the auto score as one
        // would be a mark that is missing the essay. FinaliseCoreAsync writes both.
        return marking;
    }

    /// <summary>
    /// Closes the marking, writes the ledger row, and re-scores the application.
    /// </summary>
    /// <remarks>
    /// Shared by HR's explicit finalise and the automatic one a fully-closed paper gets at submit,
    /// so the two cannot drift. The two things that follow — a <c>JobApplicantTestResult</c> row and
    /// a re-score — are the reason <c>TestScoreWeight</c> has an input at last.
    /// </remarks>
    private async Task FinaliseCoreAsync(
        RecruitmentTestSitting sitting,
        string? markerNotes,
        Guid? markedByEmployeeId,
        CancellationToken cancellationToken,
        string? venue = null,
        Guid? invigilatedById = null)
    {
        var now = DateTime.UtcNow;

        var manual = sitting.Answers
            .Where(a => !a.IsDeleted && a.Question.QuestionType == RecruitmentQuestionType.FreeText)
            .Sum(a => a.PointsAwarded);

        var total = sitting.TotalPoints ?? 0m;
        var final = (sitting.AutoScore ?? 0m) + manual;

        sitting.ManualScore = manual;
        sitting.FinalScore = final;
        sitting.ScorePercent = total > 0 ? Math.Round(final / total * 100m, 2) : 0m;
        sitting.MarkedAt = now;
        sitting.MarkedById = markedByEmployeeId;
        if (!string.IsNullOrWhiteSpace(markerNotes)) sitting.MarkerNotes = markerNotes.Trim();

        // The paper is already on the sitting — SittingsWithNavigations includes Assignment.Test —
        // so this is not another round trip, and it cannot disagree with the one that was marked.
        var test = sitting.Assignment.Test;

        // ⚠ Null, not false, when the paper has no pass mark. A paper that does not pass or fail has
        // no verdict, and recording "did not pass" for one would be a judgement nobody made.
        sitting.Passed = test.PassMarkPercent is { } passMark
            ? sitting.ScorePercent >= passMark
            : null;

        // An expired sitting KEEPS its status. It was marked, but it was marked on an incomplete
        // paper, and a reader must be able to see that from the status alone.
        if (sitting.Status != RecruitmentSittingStatus.Expired)
            sitting.Status = RecruitmentSittingStatus.Marked;

        await WriteLedgerRowAsync(sitting, test, markedByEmployeeId, now, venue, invigilatedById, cancellationToken);

        await _unitOfWork.Repository<RecruitmentTestSitting>().UpdateAsync(sitting);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RescoreApplicationAsync(sitting.JobApplicationId, markedByEmployeeId, cancellationToken);
    }

    /// <summary>
    /// Writes — or updates — the <c>JobApplicantTestResult</c> this sitting produced.
    /// </summary>
    /// <remarks>
    /// <para>⚠ The ledger is what the shortlisting blend reads, and it is KEPT rather than replaced
    /// because offline tests are real. The sitting carries the id of the row it wrote, so finalising
    /// twice updates one row instead of blending the same test into the score twice.</para>
    ///
    /// <para>⚠ <b>ONE ROW PER TEST PER CANDIDATE, and a re-sit REPLACES it.</b> The blend
    /// <i>averages</i> every scored row on the application, so a second attempt writing a second row
    /// would average the two — and the commonest reason to grant a re-sit is that something went
    /// wrong with the first (decision Q4: a power cut at the test centre). Averaging the aborted
    /// attempt with the good one penalises the candidate for the interruption HR granted the re-sit
    /// over. The attempt history is not lost: it lives in the sittings, each with its own number,
    /// score and answers. This row is the summary, and the latest finalised attempt owns it.</para>
    /// </remarks>
    private async Task WriteLedgerRowAsync(
        RecruitmentTestSitting sitting,
        RecruitmentTest test,
        Guid? markedByEmployeeId,
        DateTime now,
        string? venue,
        Guid? invigilatedById,
        CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<JobApplicantTestResult>();

        var existing = sitting.JobApplicantTestResultId is { } resultId
            ? await repo.GetQueryable()
                .FirstOrDefaultAsync(r => r.Id == resultId && r.TenantId == sitting.TenantId, cancellationToken)
            : null;

        // ⚠ An earlier ATTEMPT's row is this sitting's row too — see the remarks. Without this the
        // second attempt mints a second ledger entry and the blend quietly averages the two.
        if (existing is null)
        {
            var priorResultId = await _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
                .Where(x => x.TenantId == sitting.TenantId
                         && x.RecruitmentTestAssignmentId == sitting.RecruitmentTestAssignmentId
                         && x.JobApplicationId == sitting.JobApplicationId
                         && x.Id != sitting.Id
                         && x.JobApplicantTestResultId != null)
                .OrderByDescending(x => x.AttemptNumber)
                .Select(x => x.JobApplicantTestResultId)
                .FirstOrDefaultAsync(cancellationToken);

            if (priorResultId is { } prior)
                existing = await repo.GetQueryable()
                    .FirstOrDefaultAsync(r => r.Id == prior && r.TenantId == sitting.TenantId, cancellationToken);
        }

        // ⚠ The remark says HOW the result was produced — a candidate's own submission and a script
        // typed in by HR are different kinds of evidence, and this row is what a challenge reads.
        var remarks = sitting.Mode == RecruitmentSittingMode.Paper
            ? $"Sat on paper (attempt {sitting.AttemptNumber}) and entered by HR; the closed questions " +
              "were marked against the key."
            : sitting.Status == RecruitmentSittingStatus.Expired
                ? $"Sat online (attempt {sitting.AttemptNumber}) — the time allowed elapsed and the paper " +
                  "was marked on the answers saved before it."
                : $"Sat online (attempt {sitting.AttemptNumber}).";

        if (!string.IsNullOrWhiteSpace(sitting.MarkerNotes))
            remarks = $"{remarks} {sitting.MarkerNotes}";

        // Remarks is nvarchar(2000) and MarkerNotes is allowed all 2000 of them on its own, so the
        // two concatenated can overflow the column and fail the save with a truncation error.
        if (remarks.Length > 2000) remarks = remarks[..2000];

        var isNew = existing is null;

        existing ??= new JobApplicantTestResult
        {
            TenantId = sitting.TenantId,
            JobApplicationId = sitting.JobApplicationId,
            TestType = test.TestType,
            TestName = test.Name,
            TestDate = sitting.SubmittedAt ?? now,
            CreatedById = ActorUserId,
        };

        // ⚠ Score and MaxScore, not the percentage. JobApplicationService's blend computes
        // Score / MaxScore × 100 itself, and storing a percentage in Score with a MaxScore of 100
        // would happen to work until somebody read the column.
        existing.Score = sitting.FinalScore;
        existing.MaxScore = sitting.TotalPoints;
        existing.Passed = sitting.Passed;
        existing.Remarks = remarks;

        // The row stands for the latest finalised attempt, so its date moves with it — otherwise a
        // re-sit in October would still read as having been taken in March.
        existing.TestDate = sitting.SubmittedAt ?? now;

        // ⚠ An EMPLOYEE id, not a user id — MarkedById on the ledger is a foreign key to Employees.
        // Null when the marker is not linked to an employee record, and null when the machine marked
        // it, which is the honest answer to "who marked this".
        existing.MarkedById = markedByEmployeeId;
        existing.MarkedDate = now;

        // Where and under whom, for a paper sitting — the ledger has always had the columns, because
        // offline tests are what it was built for. Assigned on every write, not only when supplied:
        // the row stands for the LATEST attempt, and an online re-sit of a paper attempt was not sat
        // in the room the paper was.
        existing.Venue = venue;
        existing.InvigilatedById = invigilatedById;

        // ⚠ Add OR update, never both. Calling Update on a row that has not been inserted puts the
        // tracker into Modified and EF then issues an UPDATE against a row that does not exist.
        if (isNew)
        {
            await repo.AddAsync(existing);
        }
        else
        {
            existing.UpdatedAt = now;
            existing.LastModifiedById = ActorUserId;
            await repo.UpdateAsync(existing);
        }

        sitting.JobApplicantTestResultId = existing.Id;
    }

    /// <summary>
    /// Re-scores the application so the <c>TestScoreWeight</c> blend runs.
    /// </summary>
    /// <remarks>
    /// ⚠ Never allowed to fail the marking. A vacancy whose criteria cannot be evaluated, or a
    /// scoring bug, must not roll back a mark somebody earned — the application is flagged stale
    /// instead, and the recruiter's own re-score picks it up.
    /// </remarks>
    private async Task RescoreApplicationAsync(
        Guid applicationId, Guid? actorEmployeeId, CancellationToken cancellationToken)
    {
        try
        {
            await _applicationService.EvaluateApplicationScoreAsync(applicationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Application {ApplicationId} could not be re-scored after marking — flagged stale.",
                applicationId);

            try
            {
                var repo = _unitOfWork.Repository<JobApplication>();
                var application = await repo.GetByIdAsync(applicationId);
                if (application is not null)
                {
                    application.ScoreIsStale = true;
                    await repo.UpdateAsync(application);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception inner)
            {
                _logger.LogWarning(inner,
                    "Application {ApplicationId} could not even be flagged stale.", applicationId);
            }
        }

        // The pipeline advance is skipped when the actor is not an employee — a candidate's own
        // submission has no employee behind it, and movedByEmployeeId is a foreign key to Employees.
        // Passing a user id there is the defect shape this module already carries once.
        if (actorEmployeeId is { } employeeId)
        {
            try
            {
                await _pipelineService.AutoAdvanceToStageTypeAsync(
                    applicationId, RecruitmentPipelineStageType.Assessment, employeeId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Application {ApplicationId} could not be advanced to the assessment stage.",
                    applicationId);
            }
        }
    }

    /// <summary>
    /// Brings the sitting's answer rows into line with what the candidate has now.
    /// </summary>
    /// <remarks>
    /// <para>⚠ A DIFF, not a delete-and-rewrite, and that is not a micro-optimisation. Autosave runs
    /// whenever the candidate touches an answer; a paper of forty questions rewritten on each save
    /// would leave thousands of rows behind over an hour. Rows are touched only where something
    /// actually changed.</para>
    ///
    /// <para>⚠ The removals are SOFT deletes and the rows stay in the tracked collection. A hard
    /// delete through raw SQL leaves EF still tracking a row that no longer exists, and the next
    /// <c>SaveChanges</c> then fails on "expected 1 row, got 0". Every read of
    /// <c>sitting.Answers</c> in this file filters <c>!IsDeleted</c> for that reason.</para>
    /// </remarks>
    private async Task WriteAnswersAsync(
        RecruitmentTestSitting sitting,
        RecruitmentTest paper,
        IReadOnlyDictionary<Guid, SubmittedAnswer> answers,
        MarkingResult? marks,
        CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<RecruitmentTestAnswer>();
        var questions = paper.Questions.Where(q => !q.IsDeleted).ToDictionary(q => q.Id);
        var now = DateTime.UtcNow;

        var desired = marks is not null
            ? BuildMarkedRows(marks, questions)
            : BuildUnmarkedRows(answers, questions);

        var live = sitting.Answers.Where(a => !a.IsDeleted).ToList();
        var matched = new HashSet<Guid>();

        foreach (var want in desired)
        {
            var existing = live.FirstOrDefault(a =>
                !matched.Contains(a.Id)
                && a.RecruitmentTestQuestionId == want.QuestionId
                && (a.SelectedOptionId ?? Guid.Empty) == (want.SelectedOptionId ?? Guid.Empty));

            if (existing is not null)
            {
                matched.Add(existing.Id);

                var unchanged = existing.FreeTextAnswer == want.FreeTextAnswer
                             && existing.NumericAnswer == want.NumericAnswer
                             && existing.IsCorrect == want.IsCorrect
                             && existing.PointsAwarded == want.PointsAwarded;

                if (unchanged) continue;

                existing.FreeTextAnswer = want.FreeTextAnswer;
                existing.NumericAnswer = want.NumericAnswer;
                existing.IsCorrect = want.IsCorrect;
                existing.PointsAwarded = want.PointsAwarded;
                existing.AnsweredAt = now;
                existing.UpdatedAt = now;
                await repo.UpdateAsync(existing);
                continue;
            }

            var added = new RecruitmentTestAnswer
            {
                TenantId = sitting.TenantId,
                RecruitmentTestSittingId = sitting.Id,
                RecruitmentTestQuestionId = want.QuestionId,
                Question = questions[want.QuestionId],
                SelectedOptionId = want.SelectedOptionId,
                FreeTextAnswer = want.FreeTextAnswer,
                NumericAnswer = want.NumericAnswer,
                IsCorrect = want.IsCorrect,
                PointsAwarded = want.PointsAwarded,
                AnsweredAt = now,
                CreatedById = sitting.CreatedById,
            };

            sitting.Answers.Add(added);
            await repo.AddAsync(added);
        }

        foreach (var stale in live.Where(a => !matched.Contains(a.Id)))
            await repo.DeleteAsync(stale);
    }

    /// <summary>One answer row as it ought to be after this write.</summary>
    private sealed record DesiredAnswer(
        Guid QuestionId,
        Guid? SelectedOptionId,
        string? FreeTextAnswer,
        string? NumericAnswer,
        bool IsCorrect,
        decimal PointsAwarded);

    /// <summary>The marked set — one row per option chosen, the points on the first only.</summary>
    private static List<DesiredAnswer> BuildMarkedRows(
        MarkingResult marks, IReadOnlyDictionary<Guid, RecruitmentTestQuestion> questions)
        => marks.Marks
            .Where(m => questions.ContainsKey(m.QuestionId))
            .Select(m => new DesiredAnswer(
                m.QuestionId, m.SelectedOptionId, m.FreeTextAnswer, m.NumericAnswer,
                m.IsCorrect, m.PointsAwarded))
            .ToList();

    /// <summary>
    /// The saved set — exactly what came in, with no verdict on it.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>IsCorrect</c> stays false and <c>PointsAwarded</c> zero until the paper is submitted.
    /// Writing a verdict here would mean the answers table held a mark for work the candidate is
    /// still changing — and a marker screen opened mid-test would show it.
    /// </remarks>
    private static List<DesiredAnswer> BuildUnmarkedRows(
        IReadOnlyDictionary<Guid, SubmittedAnswer> answers,
        IReadOnlyDictionary<Guid, RecruitmentTestQuestion> questions)
    {
        var rows = new List<DesiredAnswer>();

        foreach (var (questionId, answer) in answers)
        {
            if (!questions.ContainsKey(questionId)) continue;

            var chosen = answer.SelectedOptionIds?.Distinct().ToList() ?? new List<Guid>();
            if (chosen.Count == 0)
            {
                // An answer with nothing in it is not stored. A blank row would read as "answered"
                // on the resume, and would leave a marker screen listing questions nobody touched.
                if (string.IsNullOrWhiteSpace(answer.FreeTextAnswer)
                    && string.IsNullOrWhiteSpace(answer.NumericAnswer)) continue;

                rows.Add(new DesiredAnswer(
                    questionId, null, answer.FreeTextAnswer, answer.NumericAnswer, false, 0m));
            }
            else
            {
                rows.AddRange(chosen.Select(optionId =>
                    new DesiredAnswer(questionId, optionId, null, null, false, 0m)));
            }
        }

        return rows;
    }

    /// <summary>Rebuilds what was saved into the shape the marker takes.</summary>
    private static Dictionary<Guid, SubmittedAnswer> CollectSavedAnswers(RecruitmentTestSitting sitting)
        => sitting.Answers
            .Where(a => !a.IsDeleted)
            .GroupBy(a => a.RecruitmentTestQuestionId)
            .ToDictionary(
                g => g.Key,
                g => new SubmittedAnswer(
                    g.Key,
                    g.Where(a => a.SelectedOptionId.HasValue).Select(a => a.SelectedOptionId!.Value).ToList(),
                    g.Select(a => a.FreeTextAnswer).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    g.Select(a => a.NumericAnswer).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))));

    private static Dictionary<Guid, SubmittedAnswer> ToSubmitted(IEnumerable<SubmitAnswerDto> answers)
        => answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(
                g => g.Key,
                g => new SubmittedAnswer(
                    g.Key,
                    g.SelectMany(a => a.SelectedOptionIds ?? new List<Guid>()).Distinct().ToList(),
                    g.Select(a => a.FreeTextAnswer).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    g.Select(a => a.NumericAnswer).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))));

    private static string BuildSubmitMessage(bool timedOut, bool settled, RecruitmentTestSitting sitting)
    {
        if (timedOut)
            return "The time allowed for this assessment had elapsed, so it was marked on the answers " +
                   "saved before the deadline.";

        if (!settled)
            return "Your responses have been received. This assessment includes written answers, which " +
                   "a member of the recruitment team will read before your result is released.";

        var marked = $"Your responses have been received and marked: {sitting.ScorePercent:0.##}%.";

        // ⚠ Only a PASS is stated. A candidate is not told by an automated response that they fell
        // short — that is a conversation the recruitment team has, in their own words, after they
        // have looked at the whole application. The percentage is shown either way.
        return sitting.Passed == true ? $"{marked} You met the pass mark." : marked;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  THE SESSION TOKEN
    // ═══════════════════════════════════════════════════════════════════════════

    private static string GenerateAccessToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static string HashAccessToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>
    /// Refuses a write from a session that is no longer the live one.
    /// </summary>
    /// <remarks>
    /// ⚠ The case this exists for: the candidate opens the test in a second tab, answers there for
    /// twenty minutes, and the FIRST tab autosaves. Without this the first tab's twenty-minute-old
    /// answers replace the fresh ones and the candidate never knows. The stale tab is refused and
    /// told what happened instead.
    /// </remarks>
    private static void RequireLiveSession(RecruitmentTestSitting sitting, string? presented)
    {
        if (string.IsNullOrWhiteSpace(sitting.AccessTokenHash)) return;

        if (string.IsNullOrWhiteSpace(presented)
            || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(HashAccessToken(presented)),
                    Encoding.UTF8.GetBytes(sitting.AccessTokenHash)))
        {
            throw new InvalidOperationException(
                "This assessment was opened in another window, which is now the one being marked. " +
                "Reload this page to continue there.");
        }
    }

    /// <summary>Re-issues the session token and hands back the paper with saved progress.</summary>
    private async Task<CandidateSittingDto> ReopenAsync(
        RecruitmentTestSitting sitting, RecruitmentTest paper, CancellationToken cancellationToken)
    {
        var token = GenerateAccessToken();
        sitting.AccessTokenHash = HashAccessToken(token);
        sitting.AccessTokenLast4 = token[^4..];
        sitting.AccessTokenExpiresAt = sitting.MustSubmitBy;
        Touch(sitting);

        await _unitOfWork.Repository<RecruitmentTestSitting>().UpdateAsync(sitting);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = ProjectSittingForCandidate(sitting, paper);
        dto.AccessToken = token;
        return dto;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  GUARDS
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<RecruitmentTest> RequireOwnedPaperAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var test = await _testRepository.GetWithFullPaperAsync(id, cancellationToken);

        // A paper owned by another tenant is reported as missing rather than forbidden, so the
        // endpoints do not confirm that the id exists elsewhere.
        if (test is null || test.TenantId != tenantId)
            throw new ArgumentException($"Recruitment test '{id}' not found.");

        return test;
    }

    private async Task<RecruitmentTestQuestion> RequireOwnedQuestionAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<RecruitmentTestQuestion>().GetQueryable()
            .Include(q => q.Options)
            .Include(q => q.Section)
            .FirstOrDefaultAsync(q => q.Id == id && q.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Test question '{id}' not found.");
    }

    private async Task<RecruitmentTestSitting> RequireOwnedSittingAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        return await SittingsWithNavigations()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Test sitting '{id}' not found.");
    }

    /// <summary>
    /// The candidate's own sitting, and the paper behind it.
    /// </summary>
    /// <remarks>
    /// ⚠ Ownership runs sitting → application → candidate → <c>JobCandidate.UserId</c>. A sitting
    /// belonging to somebody else is reported as missing, not forbidden: a 403 on a guessed id
    /// confirms the id exists.
    /// </remarks>
    private async Task<(RecruitmentTestSitting Sitting, RecruitmentTest Paper)> RequireOwnSittingAsync(
        Guid userId, Guid tenantId, Guid sittingId, CancellationToken cancellationToken)
    {
        var candidateId = await RequireCandidateAsync(userId, tenantId, cancellationToken);

        var sitting = await SittingsWithNavigations()
            .FirstOrDefaultAsync(s => s.Id == sittingId && s.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("That assessment could not be found.");

        if (sitting.JobApplication.JobCandidateId != candidateId)
            throw new ArgumentException("That assessment could not be found.");

        var paper = await _testRepository.GetWithFullPaperAsync(
                        sitting.Assignment.RecruitmentTestId, cancellationToken)
            ?? throw new ArgumentException("That assessment could not be found.");

        return (sitting, paper);
    }

    private async Task<Guid> RequireCandidateAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var candidate = await _unitOfWork.Repository<JobCandidate>().GetQueryable()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.TenantId == tenantId, cancellationToken);

        return candidate?.Id
            ?? throw new InvalidOperationException(
                "Your candidate profile could not be found. Complete your profile before opening an assessment.");
    }

    private async Task<bool> HasSittingsAsync(Guid testId, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
            .AnyAsync(s => s.Assignment.RecruitmentTestId == testId, cancellationToken);

    /// <summary>
    /// ⚠ A paper somebody has sat is a RECORD, not a draft. Editing a question after it has been
    /// answered rewrites what that person was marked on, and the marking key on file would stop
    /// matching the script in the drawer.
    /// </summary>
    private async Task RequireNotSatAsync(
        RecruitmentTest test, string action, CancellationToken cancellationToken)
    {
        if (!await HasSittingsAsync(test.Id, cancellationToken)) return;

        throw new InvalidOperationException(
            $"'{test.Name}' has already been sat, so you cannot {action}. Retire it and copy it into a " +
            "new paper if it needs to change — the marks already given must keep matching the questions " +
            "that were asked.");
    }

    private async Task RequireSectionBelongsAsync(
        Guid? sectionId, RecruitmentTest test, CancellationToken cancellationToken)
    {
        if (sectionId is not { } id || id == Guid.Empty) return;

        var belongs = await _unitOfWork.Repository<RecruitmentTestSection>().GetQueryable()
            .AnyAsync(s => s.Id == id && s.RecruitmentTestId == test.Id && s.TenantId == test.TenantId,
                      cancellationToken);

        if (!belongs)
            throw new InvalidOperationException("That section belongs to a different test paper.");
    }

    private static void ValidateTestHeader(CreateRecruitmentTestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("The test needs a name.");

        if (dto.DurationMinutes is { } minutes && minutes <= 0)
            throw new InvalidOperationException(
                "A duration of zero is not 'untimed' — leave it blank for a paper with no time limit.");

        if (dto.PassMarkPercent is { } pass && (pass < 0 || pass > 100))
            throw new InvalidOperationException("The pass mark is a percentage between 0 and 100.");
    }

    /// <summary>
    /// Refuses a question the machine could never mark.
    /// </summary>
    /// <remarks>
    /// ⚠ Every one of these is a paper that renders, accepts twenty minutes of a candidate's work and
    /// can only ever score zero on that question. They are caught at authoring — the only point at
    /// which somebody is in a position to fix them.
    /// </remarks>
    private static void ValidateQuestionShape(CreateRecruitmentTestQuestionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.QuestionText))
            throw new InvalidOperationException("The question needs text.");

        if (dto.Points <= 0 && dto.QuestionType != RecruitmentQuestionType.FreeText)
            throw new InvalidOperationException(
                "A question worth nothing cannot be marked. Give it at least one mark, or remove it.");

        var options = dto.Options ?? new List<CreateRecruitmentTestQuestionOptionDto>();
        var live = options.Where(o => !string.IsNullOrWhiteSpace(o.OptionText)).ToList();

        switch (dto.QuestionType)
        {
            case RecruitmentQuestionType.FreeText:
                if (live.Count > 0)
                    throw new InvalidOperationException(
                        "A written answer has no choices. Remove them, or change the question type.");
                break;

            case RecruitmentQuestionType.Numeric:
                if (string.IsNullOrWhiteSpace(dto.ExpectedAnswer))
                    throw new InvalidOperationException(
                        "A numeric question needs the expected answer — without it nothing can be right.");
                if (!decimal.TryParse(dto.ExpectedAnswer, out _))
                    throw new InvalidOperationException(
                        $"'{dto.ExpectedAnswer}' is not a number, so a numeric answer could never match it.");
                break;

            case RecruitmentQuestionType.TrueFalse:
                if (live.Count != 2)
                    throw new InvalidOperationException(
                        "A true/false question has exactly two choices.");
                if (live.Count(o => o.IsCorrect) != 1)
                    throw new InvalidOperationException(
                        "Mark exactly one of the two as correct.");
                break;

            case RecruitmentQuestionType.SingleChoice:
                if (live.Count < 2)
                    throw new InvalidOperationException(
                        "A multiple-choice question needs at least two choices.");
                if (live.Count(o => o.IsCorrect) != 1)
                    throw new InvalidOperationException(
                        "Mark exactly one choice as correct. For a question with several right " +
                        "answers, use the multiple-answer type — it is marked on getting every one " +
                        "of them and no wrong one.");
                break;

            case RecruitmentQuestionType.MultiSelect:
                if (live.Count < 2)
                    throw new InvalidOperationException(
                        "A multiple-answer question needs at least two choices.");
                if (live.Count(o => o.IsCorrect) == 0)
                    throw new InvalidOperationException(
                        "Mark at least one choice as correct — a question where nothing is right " +
                        "cannot be answered.");
                if (live.All(o => o.IsCorrect))
                    throw new InvalidOperationException(
                        "Every choice is marked correct, so ticking all of them scores full marks. " +
                        "Add a wrong choice, or use the single-choice type.");
                break;
        }
    }

    /// <summary>
    /// The check that runs at ACTIVATION, over the whole paper.
    /// </summary>
    /// <remarks>
    /// ⚠ Per-question validation is not enough on its own: a question can be saved correctly and the
    /// paper still be unusable — no questions at all, or every question a written one on a test
    /// nobody has a marker for. Activation is the last moment before a candidate sees it.
    /// </remarks>
    private static void ValidatePaperIsMarkable(RecruitmentTest test)
    {
        var questions = test.Questions.Where(q => !q.IsDeleted).ToList();

        if (questions.Count == 0)
            throw new InvalidOperationException(
                $"'{test.Name}' has no questions. A paper with nothing on it can only score zero.");

        if (questions.Sum(q => q.Points) <= 0)
            throw new InvalidOperationException(
                $"'{test.Name}' is worth no marks in total, so no score could be computed from it.");

        foreach (var question in questions.OrderBy(q => q.DisplayOrder))
        {
            var options = question.Options.Where(o => !o.IsDeleted).ToList();

            switch (question.QuestionType)
            {
                case RecruitmentQuestionType.FreeText:
                    continue;

                case RecruitmentQuestionType.Numeric:
                    if (string.IsNullOrWhiteSpace(question.ExpectedAnswer)
                        || !decimal.TryParse(question.ExpectedAnswer, out _))
                        throw new InvalidOperationException(
                            $"Question {question.DisplayOrder} has no usable expected answer, so nothing " +
                            "a candidate types could be right.");
                    continue;

                default:
                    if (options.Count < 2)
                        throw new InvalidOperationException(
                            $"Question {question.DisplayOrder} has fewer than two choices.");
                    if (!options.Any(o => o.IsCorrect))
                        throw new InvalidOperationException(
                            $"Question {question.DisplayOrder} has no correct choice, so it can only " +
                            "ever score zero.");
                    if (question.QuestionType != RecruitmentQuestionType.MultiSelect
                        && options.Count(o => o.IsCorrect) != 1)
                        throw new InvalidOperationException(
                            $"Question {question.DisplayOrder} has more than one correct choice, but it " +
                            "is marked as a single-answer question.");
                    continue;
            }
        }
    }

    /// <summary>Why the candidate cannot start, in the words the screen shows them.</summary>
    private static string? BlockedReason(
        RecruitmentTestAssignment assignment, int attemptsUsed, int allowed,
        RecruitmentTestSitting? inProgress, DateTime now)
    {
        if (inProgress is not null) return null;

        if (!assignment.Test.IsActive)
            return "This assessment is not currently available.";

        if (assignment.OpensAt is { } opens && now < opens)
            return $"This assessment opens on {opens:dddd, d MMMM yyyy} at {opens:HH:mm}.";

        if (assignment.ClosesAt is { } closes && now > closes)
            return $"This assessment closed on {closes:dddd, d MMMM yyyy} at {closes:HH:mm}.";

        if (attemptsUsed >= allowed)
            return allowed == 1
                ? "You have already sat this assessment."
                : $"You have used all {allowed} attempts at this assessment.";

        return null;
    }

    // Which applications an assignment reaches lives in RecruitmentTestReach, shared with the
    // printed paper — two copies of "which statuses are live" is how a withdrawn candidate gets
    // printed a paper the portal would refuse them.

    /// <summary>An online attempt still running: in progress, and inside its clock if it has one.</summary>
    private static bool IsOpenOnline(RecruitmentTestSitting sitting, DateTime now)
        => sitting.Status == RecruitmentSittingStatus.InProgress
        && (sitting.MustSubmitBy is null || sitting.MustSubmitBy > now);

    private async Task<List<JobApplication>> ResolveAssignmentApplicationsAsync(
        RecruitmentTestAssignment assignment, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Repository<JobApplication>().GetQueryable()
            .Include(a => a.JobCandidate)
            .Include(a => a.JobVacancy).ThenInclude(v => v.Requisition).ThenInclude(r => r.JobDescription)
            .Where(a => a.TenantId == assignment.TenantId);

        query = assignment.JobApplicationId is { } applicationId
            ? query.Where(a => a.Id == applicationId)
            : query.Where(a => a.JobVacancyId == assignment.JobVacancyId);

        var rows = await query.ToListAsync(cancellationToken);

        return assignment.JobApplicationId is not null
            ? rows
            : rows.Where(a => RecruitmentTestReach.IsLive(a.Status)).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  QUERIES AND PROJECTIONS
    // ═══════════════════════════════════════════════════════════════════════════

    private IQueryable<RecruitmentTestAssignment> AssignmentsWithNavigations()
        => _unitOfWork.Repository<RecruitmentTestAssignment>().GetQueryable()
            .Include(a => a.Test).ThenInclude(t => t.Questions)
            .Include(a => a.JobVacancy).ThenInclude(v => v!.Requisition).ThenInclude(r => r.JobDescription)
            .Include(a => a.JobApplication).ThenInclude(j => j!.JobCandidate)
            // ⚠ The per-application branch needs its OWN vacancy chain. JobVacancy.JobTitle is
            // [NotMapped] and resolves through Requisition.JobDescription, so without this a
            // per-candidate assignment lists with a blank job title — the column the list exists for.
            .Include(a => a.JobApplication).ThenInclude(j => j!.JobVacancy)
                .ThenInclude(v => v.Requisition).ThenInclude(r => r.JobDescription);

    private IQueryable<RecruitmentTestSitting> SittingsWithNavigations()
        => _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
            .Include(s => s.Assignment).ThenInclude(a => a.Test)
            .Include(s => s.JobApplication).ThenInclude(a => a.JobCandidate)
            .Include(s => s.JobApplication).ThenInclude(a => a.JobVacancy)
            .Include(s => s.Answers).ThenInclude(a => a.Question)
            .Include(s => s.Answers).ThenInclude(a => a.SelectedOption);

    /// <summary>
    /// The paper, stripped of everything a candidate must not see.
    /// </summary>
    /// <remarks>
    /// ⚠ The ONE projection every candidate-facing read goes through, including the author's preview.
    /// <c>IsCorrect</c>, <c>ExpectedAnswer</c> and <c>Explanation</c> are not merely left unset — the
    /// types they would go in do not have the fields.
    /// </remarks>
    private static List<CandidateTestQuestionDto> ProjectForCandidate(RecruitmentTest test, bool shuffle)
    {
        var questions = test.Questions.Where(q => !q.IsDeleted).ToList();

        // ⚠ Seeded on the paper's id so the order is STABLE for a given paper. A fresh Random on
        // every read would reshuffle the page under the candidate each time they refreshed, which
        // is not "shuffled questions" — it is a paper that will not hold still.
        var rng = new Random(test.Id.GetHashCode());

        var ordered = shuffle && test.ShuffleQuestions
            ? questions.OrderBy(_ => rng.Next()).ToList()
            : questions.OrderBy(q => q.DisplayOrder).ThenBy(q => q.CreatedAt).ToList();

        return ordered.Select(q =>
        {
            var options = q.Options.Where(o => !o.IsDeleted).ToList();
            var optionOrder = shuffle && test.ShuffleOptions
                ? options.OrderBy(_ => rng.Next()).ToList()
                : options.OrderBy(o => o.DisplayOrder).ThenBy(o => o.CreatedAt).ToList();

            return new CandidateTestQuestionDto
            {
                Id = q.Id,
                SectionId = q.RecruitmentTestSectionId,
                SectionName = q.Section?.Name,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                Points = q.Points,
                DisplayOrder = q.DisplayOrder,
                Options = optionOrder.Select(o => new CandidateTestOptionDto
                {
                    Id = o.Id,
                    OptionText = o.OptionText,
                    DisplayOrder = o.DisplayOrder,
                }).ToList(),
            };
        }).ToList();
    }

    private static CandidateSittingDto ProjectSittingForCandidate(
        RecruitmentTestSitting sitting, RecruitmentTest paper)
        => new()
        {
            SittingId = sitting.Id,
            TestName = paper.Name,
            Instructions = paper.Instructions,
            DurationMinutes = paper.DurationMinutes,
            AttemptNumber = sitting.AttemptNumber,
            Status = sitting.Status,
            StartedAt = AsUtc(sitting.StartedAt),
            MustSubmitBy = AsUtc(sitting.MustSubmitBy),
            SubmittedAt = AsUtc(sitting.SubmittedAt),
            Questions = ProjectForCandidate(paper, shuffle: true),
            SavedAnswers = sitting.Answers
                .Where(a => !a.IsDeleted)
                .GroupBy(a => a.RecruitmentTestQuestionId)
                .Select(g => new CandidateSavedAnswerDto
                {
                    QuestionId = g.Key,
                    SelectedOptionIds = g.Where(a => a.SelectedOptionId.HasValue)
                        .Select(a => a.SelectedOptionId!.Value).ToList(),
                    FreeTextAnswer = g.Select(a => a.FreeTextAnswer)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    NumericAnswer = g.Select(a => a.NumericAnswer)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                }).ToList(),
        };

    private static RecruitmentTestDto MapTestSummary(RecruitmentTest t, bool hasSittings)
        => new()
        {
            Id = t.Id,
            TenantId = t.TenantId,
            TestCode = t.TestCode,
            Name = t.Name,
            Description = t.Description,
            Instructions = t.Instructions,
            TestType = t.TestType,
            DurationMinutes = t.DurationMinutes,
            PassMarkPercent = t.PassMarkPercent,
            MaxAttempts = t.MaxAttempts,
            ShuffleQuestions = t.ShuffleQuestions,
            ShuffleOptions = t.ShuffleOptions,
            IsActive = t.IsActive,
            QuestionCount = t.Questions.Count(q => !q.IsDeleted),
            TotalPoints = t.Questions.Where(q => !q.IsDeleted).Sum(q => q.Points),
            HasSittings = hasSittings,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy ?? string.Empty,
            UpdatedAt = t.UpdatedAt,
            UpdatedBy = t.UpdatedBy,
        };

    private static RecruitmentTestDto MapTestFull(RecruitmentTest t, bool hasSittings)
    {
        var dto = MapTestSummary(t, hasSittings);
        dto.Sections = t.Sections.Where(s => !s.IsDeleted)
            .OrderBy(s => s.DisplayOrder).Select(MapSection).ToList();
        dto.Questions = t.Questions.Where(q => !q.IsDeleted)
            .OrderBy(q => q.DisplayOrder).Select(MapQuestion).ToList();
        return dto;
    }

    private static RecruitmentTestSectionDto MapSection(RecruitmentTestSection s)
        => new()
        {
            Id = s.Id,
            RecruitmentTestId = s.RecruitmentTestId,
            Name = s.Name,
            Description = s.Description,
            DisplayOrder = s.DisplayOrder,
            CreatedAt = s.CreatedAt,
            CreatedBy = s.CreatedBy ?? string.Empty,
            UpdatedAt = s.UpdatedAt,
            UpdatedBy = s.UpdatedBy,
        };

    private static RecruitmentTestQuestionDto MapQuestion(RecruitmentTestQuestion q)
        => new()
        {
            Id = q.Id,
            RecruitmentTestId = q.RecruitmentTestId,
            RecruitmentTestSectionId = q.RecruitmentTestSectionId,
            SectionName = q.Section?.Name,
            QuestionText = q.QuestionText,
            QuestionType = q.QuestionType,
            Points = q.Points,
            ExpectedAnswer = q.ExpectedAnswer,
            Explanation = q.Explanation,
            DisplayOrder = q.DisplayOrder,
            CreatedAt = q.CreatedAt,
            CreatedBy = q.CreatedBy ?? string.Empty,
            UpdatedAt = q.UpdatedAt,
            UpdatedBy = q.UpdatedBy,
            Options = q.Options.Where(o => !o.IsDeleted)
                .OrderBy(o => o.DisplayOrder)
                .Select(o => new RecruitmentTestQuestionOptionDto
                {
                    Id = o.Id,
                    RecruitmentTestQuestionId = o.RecruitmentTestQuestionId,
                    OptionText = o.OptionText,
                    IsCorrect = o.IsCorrect,
                    DisplayOrder = o.DisplayOrder,
                    CreatedAt = o.CreatedAt,
                    CreatedBy = o.CreatedBy ?? string.Empty,
                }).ToList(),
        };

    private async Task<RecruitmentTestAssignmentDto> MapAssignmentAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var entity = await AssignmentsWithNavigations()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new ArgumentException($"Test assignment '{id}' not found.");

        var count = await _unitOfWork.Repository<RecruitmentTestSitting>().GetQueryable()
            .CountAsync(s => s.RecruitmentTestAssignmentId == id, cancellationToken);

        return MapAssignment(entity, count);
    }

    private static RecruitmentTestAssignmentDto MapAssignment(RecruitmentTestAssignment a, int sittingCount)
        => new()
        {
            Id = a.Id,
            RecruitmentTestId = a.RecruitmentTestId,
            TestName = a.Test.Name,
            TestCode = a.Test.TestCode,
            DurationMinutes = a.Test.DurationMinutes,
            JobVacancyId = a.JobVacancyId,
            VacancyNumber = a.JobVacancy?.VacancyNumber,
            JobTitle = a.JobVacancy?.JobTitle ?? a.JobApplication?.JobVacancy?.JobTitle,
            JobApplicationId = a.JobApplicationId,
            ApplicationNumber = a.JobApplication?.ApplicationNumber,
            CandidateName = a.JobApplication?.JobCandidate?.FullName,
            OpensAt = AsUtc(a.OpensAt),
            ClosesAt = AsUtc(a.ClosesAt),
            IsRequired = a.IsRequired,
            ExtraAttemptsGranted = a.ExtraAttemptsGranted,
            ExtraAttemptReason = a.ExtraAttemptReason,
            InvitedAt = AsUtc(a.InvitedAt),
            SittingCount = sittingCount,
            CreatedAt = a.CreatedAt,
            CreatedBy = a.CreatedBy ?? string.Empty,
            UpdatedAt = a.UpdatedAt,
            UpdatedBy = a.UpdatedBy,
        };

    private static RecruitmentTestSittingDto MapSitting(RecruitmentTestSitting s)
    {
        var answers = s.Answers.Where(a => !a.IsDeleted).ToList();

        return new RecruitmentTestSittingDto
        {
            Id = s.Id,
            RecruitmentTestAssignmentId = s.RecruitmentTestAssignmentId,
            JobApplicationId = s.JobApplicationId,
            ApplicationNumber = s.JobApplication?.ApplicationNumber ?? string.Empty,
            CandidateName = s.JobApplication?.JobCandidate?.FullName ?? string.Empty,
            TestName = s.Assignment?.Test?.Name ?? string.Empty,
            AttemptNumber = s.AttemptNumber,
            Status = s.Status,
            Mode = s.Mode,
            StartedAt = AsUtc(s.StartedAt),
            SubmittedAt = AsUtc(s.SubmittedAt),
            MustSubmitBy = AsUtc(s.MustSubmitBy),
            AutoScore = s.AutoScore,
            ManualScore = s.ManualScore,
            FinalScore = s.FinalScore,
            TotalPoints = s.TotalPoints,
            ScorePercent = s.ScorePercent,
            Passed = s.Passed,
            MarkedAt = AsUtc(s.MarkedAt),
            MarkerNotes = s.MarkerNotes,
            JobApplicantTestResultId = s.JobApplicantTestResultId,
            AwaitingManualMarking = answers.Any(a =>
                a.Question.QuestionType == RecruitmentQuestionType.FreeText && !a.IsManuallyMarked),

            CreatedAt = s.CreatedAt,
            CreatedBy = s.CreatedBy ?? string.Empty,
            UpdatedAt = s.UpdatedAt,
            UpdatedBy = s.UpdatedBy,
            Answers = answers
                .OrderBy(a => a.Question.DisplayOrder)
                .Select(a => new SittingAnswerDto
                {
                    Id = a.Id,
                    QuestionId = a.RecruitmentTestQuestionId,
                    QuestionText = a.Question.QuestionText,
                    QuestionType = a.Question.QuestionType,
                    QuestionPoints = a.Question.Points,
                    SelectedOptionId = a.SelectedOptionId,
                    SelectedOptionText = a.SelectedOption?.OptionText,
                    FreeTextAnswer = a.FreeTextAnswer,
                    NumericAnswer = a.NumericAnswer,
                    IsCorrect = a.IsCorrect,
                    PointsAwarded = a.PointsAwarded,
                    IsManuallyMarked = a.IsManuallyMarked,
                    MarkerComment = a.MarkerComment,
                }).ToList(),
        };
    }

    private static IEnumerable<RecruitmentTestQuestionOption> BuildOptions(
        CreateRecruitmentTestQuestionDto dto, RecruitmentTestQuestion question)
    {
        var options = dto.Options ?? new List<CreateRecruitmentTestQuestionOptionDto>();
        var order = 1;

        foreach (var option in options.Where(o => !string.IsNullOrWhiteSpace(o.OptionText)))
        {
            yield return new RecruitmentTestQuestionOption
            {
                TenantId = question.TenantId,
                RecruitmentTestQuestionId = question.Id,
                OptionText = option.OptionText.Trim(),
                IsCorrect = option.IsCorrect,
                DisplayOrder = option.DisplayOrder > 0 ? option.DisplayOrder : order,
            };
            order++;
        }
    }

    /// <summary>
    /// Stamps UTC on an instant before it is serialised.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Found by the lane E harness, and it is a real defect, not a formatting nicety.</b>
    /// The columns are <c>datetime2</c>, which carries no offset, so EF hands every value back with
    /// <c>DateTimeKind.Unspecified</c> and <c>System.Text.Json</c> then writes it WITHOUT a trailing
    /// <c>Z</c>. The same field therefore left the API two different ways: <c>…3090023Z</c> straight
    /// after the attempt was opened (computed from <c>DateTime.UtcNow</c>, so <c>Kind = Utc</c>) and
    /// <c>…3090023</c> on the very next read.</para>
    ///
    /// <para>⚠ <b>Why it matters here more than anywhere else.</b> A browser reading
    /// <c>new Date("2026-09-22T19:03:57")</c> — no offset — parses it as LOCAL time. The candidate's
    /// countdown is therefore out by their whole UTC offset the moment they refresh the page. In
    /// Ghana (UTC+0) nothing shows; for a candidate an hour east it reads an hour long, and the
    /// server cuts them off while their own timer still says they have time. A clock is the one
    /// field where "close enough" is not.</para>
    /// </remarks>
    private static DateTime? AsUtc(DateTime? value)
    {
        if (value is not { } instant) return null;

        return instant.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(instant, DateTimeKind.Utc)
            : instant.ToUniversalTime();
    }

    private void Touch(BaseEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.LastModifiedById = ActorUserId;
    }
}

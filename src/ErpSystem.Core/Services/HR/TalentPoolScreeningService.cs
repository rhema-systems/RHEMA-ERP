using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="ITalentPoolScreeningService"/>
public class TalentPoolScreeningService : ITalentPoolScreeningService
{
    private readonly IJobCandidateRepository _candidateRepository;
    private readonly IJobVacancyRepository _vacancyRepository;
    private readonly IJobShortlistingCriteriaRepository _criteriaRepository;
    private readonly IJobApplicationRepository _applicationRepository;
    private readonly IJobInterviewRepository _interviewRepository;
    private readonly IJobApplicationService _applicationService;
    private readonly IJobInterviewService _interviewService;
    private readonly ICandidateEngagementEventService _engagementService;
    private readonly IShortlistingCriteriaResolver _criteriaResolver;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TalentPoolScreeningService> _logger;

    public TalentPoolScreeningService(
        IJobCandidateRepository candidateRepository,
        IJobVacancyRepository vacancyRepository,
        IJobShortlistingCriteriaRepository criteriaRepository,
        IJobApplicationRepository applicationRepository,
        IJobInterviewRepository interviewRepository,
        IJobApplicationService applicationService,
        IJobInterviewService interviewService,
        ICandidateEngagementEventService engagementService,
        IShortlistingCriteriaResolver criteriaResolver,
        ITemplatedEmailService templatedEmail,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TalentPoolScreeningService> logger)
    {
        _unitOfWork = unitOfWork;
        _candidateRepository = candidateRepository;
        _vacancyRepository = vacancyRepository;
        _criteriaRepository = criteriaRepository;
        _applicationRepository = applicationRepository;
        _interviewRepository = interviewRepository;
        _applicationService = applicationService;
        _interviewService = interviewService;
        _engagementService = engagementService;
        _criteriaResolver = criteriaResolver;
        _templatedEmail = templatedEmail;
        _currentUserProvider = currentUserProvider;
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

    // ═════════════════════════════════════════════════════════════════════════
    //  B1 — SCREENING
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<TalentPoolScreenResultDto> ScreenAgainstVacancyAsync(
        Guid vacancyId, TalentPoolScreenRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var vacancy = await _vacancyRepository.GetWithFullDetailsAsync(vacancyId);
        if (vacancy is null || vacancy.TenantId != tenantId || vacancy.IsDeleted)
            throw new ArgumentException($"Job vacancy '{vacancyId}' not found.");

        var criteria = (await _criteriaRepository.GetByVacancyIdAsync(vacancyId))
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .ToList();

        // ⚠ A vacancy with no criteria is refused, not screened into a column of nulls.
        //
        // The engine would answer "nothing could be measured" for every single pool member, and the
        // screen would render a full table of dashes — which reads as "the pool is useless" rather
        // than "this vacancy has not said what it wants yet". Saying so is the useful answer, and
        // it names the screen that fixes it.
        if (criteria.Count == 0)
            throw new InvalidOperationException(
                $"Vacancy {vacancy.VacancyNumber} has no shortlisting criteria, so there is nothing to "
                + "screen the pool against. Add criteria on the vacancy's Shortlisting criteria tab first.");

        // Which pool members already hold an application here — so the screen can say "already
        // applied" instead of offering to invite somebody who is in the pipeline already.
        var existingApplicantIds = (await _applicationRepository.GetByVacancyIdAsync(vacancyId))
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Select(a => a.JobCandidateId)
            .ToHashSet();

        var result = await ScreenAsync(criteria, request, existingApplicantIds, cancellationToken);
        result.VacancyId = vacancy.Id;
        result.VacancyNumber = vacancy.VacancyNumber;
        result.JobTitle = vacancy.JobTitle;
        return result;
    }

    public async Task<TalentPoolScreenResultDto> ScreenAdHocAsync(
        TalentPoolScreenRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var inputs = request.Criteria ?? new List<AdHocScreeningCriterionDto>();
        if (inputs.Count == 0)
            throw new InvalidOperationException(
                "An ad-hoc screen needs at least one criterion — with none, every candidate scores the "
                + "same and the answer is just the pool list.");

        var criteria = new List<JobShortlistingCriteria>(inputs.Count);
        foreach (var input in inputs)
        {
            // The SAME resolver a saved criterion goes through: it refuses a mandatory Gender or
            // Age, a numeric criterion with no bound, a list criterion with no values, and a
            // catalogue id or area that is not the tenant's. An ad-hoc screen that accepted what a
            // vacancy refuses would be a second, laxer set of rules wearing the same name.
            var values = await _criteriaResolver.ResolveValuesAsync(
                input.Type, input.IsMandatory, input.Values, input.RequiredValue,
                input.MinValue, input.MaxValue, null, null, tenantId);

            criteria.Add(new JobShortlistingCriteria
            {
                Id = Guid.Empty,          // never persisted; the breakdown reports Guid.Empty
                TenantId = tenantId,
                CriteriaName = input.CriteriaName,
                Type = input.Type,
                RequiredValue = ShortlistingCriteriaResolver.MirrorLabels(
                    input.Type, values, input.RequiredValue, legacyShape: input.Values is null),
                MinValue = input.MinValue,
                MaxValue = input.MaxValue,
                IsMandatory = input.IsMandatory,
                MatchMode = input.MatchMode,
                MatchStrategy = input.MatchStrategy,
                Weight = input.Weight,
                ComparisonOperator = input.ComparisonOperator,
                Values = values,
            });
        }

        return await ScreenAsync(criteria, request, new HashSet<Guid>(), cancellationToken);
    }

    /// <summary>
    /// The screen itself: read the filtered pool with everything the engine needs, score each
    /// member through <see cref="ShortlistingEvaluator"/>, and rank.
    /// </summary>
    /// <remarks>
    /// <para>Ranking puts a mandatory failure below everybody who passed, then orders by score, then
    /// by experience — so a near miss the recruiter might waive is visible at the bottom rather than
    /// absent. A candidate nothing could be measured about (null score) sorts last of all: unknown
    /// is not a recommendation.</para>
    /// </remarks>
    private async Task<TalentPoolScreenResultDto> ScreenAsync(
        List<JobShortlistingCriteria> criteria,
        TalentPoolScreenRequestDto request,
        HashSet<Guid> existingApplicantIds,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var filter = request.Filter ?? new TalentPoolFilterDto();
        var members = await _candidateRepository.GetTalentPoolForScreeningAsync(filter, tenantId, cancellationToken);
        // Once per screen: an "Education level" criterion compares ranks on it (round 4, lane Q).
        var ladder = await QualificationLadder.LoadAsync(_unitOfWork, tenantId, cancellationToken);

        var rows = new List<TalentPoolScreenRowDto>(members.Count);
        foreach (var member in members)
        {
            var view = ScoringCandidateView.FromCandidate(member);
            var scored = ShortlistingEvaluator.Score(criteria, view, ladder);

            rows.Add(new TalentPoolScreenRowDto
            {
                CandidateId = member.Id,
                CandidateName = member.FullName,
                CandidateNumber = member.CandidateNumber,
                Email = member.Email,
                Headline = member.Headline,
                City = member.City,
                GeoAreaId = member.GeoAreaId,
                TotalYearsExperience = member.TotalYearsExperience,
                PreferredWorkArrangementName = member.PreferredWorkArrangement.ToString(),
                AvailableFrom = member.AvailableFrom,
                HasPhoto = member.HasPhotoOnFile(),
                IsInTalentPool = member.IsInTalentPool,
                CriteriaScore = scored.Score,
                CriteriaScoreMax = 100m,
                AllMandatoryPassed = scored.AllMandatoryPassed,
                TotalWeight = scored.TotalWeight,
                AlreadyApplied = existingApplicantIds.Contains(member.Id),
                Breakdown = scored.Breakdown,
            });
        }

        var result = new TalentPoolScreenResultDto
        {
            ScreenedCount = rows.Count,
            ScoredCount = rows.Count(r => r.CriteriaScore.HasValue),
            QualifiedCount = rows.Count(r => r.AllMandatoryPassed && r.CriteriaScore.HasValue),
            Criteria = criteria.Select(c => new ScreeningCriterionSummaryDto
            {
                CriteriaId = c.Id == Guid.Empty ? null : c.Id,
                CriteriaName = c.CriteriaName,
                Type = c.Type,
                IsMandatory = c.IsMandatory,
                Weight = c.Weight,
                AcceptedValues = c.Values.Count > 0
                    ? string.Join(", ", c.Values.OrderBy(v => v.SortOrder).Select(v => v.Label))
                    : c.RequiredValue,
            }).ToList(),
        };

        var ranked = rows.AsEnumerable();
        if (!request.IncludeNonMatching)
            ranked = ranked.Where(r => r.AllMandatoryPassed && r.CriteriaScore.HasValue);

        result.Rows = ranked
            .OrderByDescending(r => r.AllMandatoryPassed)
            .ThenByDescending(r => r.CriteriaScore ?? -1m)
            .ThenByDescending(r => r.TotalYearsExperience ?? -1)
            .ThenBy(r => r.CandidateName)
            .Take(request.TopN)
            .ToList();

        _logger.LogInformation(
            "Talent pool screened: {Screened} member(s) against {Criteria} criteria, {Qualified} met every mandatory one.",
            result.ScreenedCount, criteria.Count, result.QualifiedCount);

        return result;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  B3 — ACTING ON THE RESULT
    // ═════════════════════════════════════════════════════════════════════════

    /// <remarks>
    /// <para>Each candidate is handled on its own so one bad row cannot lose the rest — the partial
    /// convention the pool's bulk screen already uses. The application goes through
    /// <c>IJobApplicationService.CreateAsync</c> rather than being written here, so the vacancy
    /// ownership check, the application number sequence and the initial status are the ones every
    /// other application gets.</para>
    ///
    /// <para>⚠ The engagement event is written AFTER the application exists. Logging the invitation
    /// first would leave a candidate's history claiming they were invited to a vacancy whose
    /// application creation then failed — a record of something that did not happen.</para>
    /// </remarks>
    public async Task<RecruitmentBulkOperationResultDto> InviteToApplyAsync(
        TalentPoolInviteToApplyDto dto, Guid actingEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var vacancy = await _vacancyRepository.GetWithFullDetailsAsync(dto.JobVacancyId);
        if (vacancy is null || vacancy.TenantId != tenantId || vacancy.IsDeleted)
            throw new ArgumentException($"Job vacancy '{dto.JobVacancyId}' not found.");

        // ⚠ The vacancy must still be taking applications. CreateAsync does not check this — it is
        // the door HR records a walk-in through, and a late walk-in is a judgement call. An
        // INVITATION is not: writing to somebody asking them to apply for a post that is filled, or
        // closed, or not yet approved, is the organisation embarrassing itself in the candidate's
        // inbox. Refused for the whole request, since it is a property of the vacancy, not the row.
        // ⚠ The property is VacancyStatus, not Status — JobVacancy carries several status-shaped
        // columns (ShortlistApprovalStatus is the other) and the one that says whether the post is
        // open is named for the vacancy.
        if (vacancy.VacancyStatus is not (JobVacancyStatus.Approved or JobVacancyStatus.Published
                                       or JobVacancyStatus.Shortlisting))
            throw new InvalidOperationException(
                $"Vacancy {vacancy.VacancyNumber} is {vacancy.VacancyStatus} and is not taking applications, "
                + "so candidates cannot be invited to apply for it.");

        var alreadyApplied = (await _applicationRepository.GetByVacancyIdAsync(dto.JobVacancyId))
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .ToDictionary(a => a.JobCandidateId, a => a.ApplicationNumber);

        var result = new RecruitmentBulkOperationResultDto();
        foreach (var candidateId in dto.CandidateIds.Distinct())
        {
            try
            {
                var candidate = await _candidateRepository.GetByIdAsync(candidateId);
                if (candidate is null || candidate.TenantId != tenantId || candidate.IsDeleted)
                {
                    Skip(result, candidateId, "Candidate not found.");
                    continue;
                }

                if (alreadyApplied.TryGetValue(candidateId, out var existingNumber))
                {
                    Skip(result, candidateId,
                        $"{candidate.FullName} already has an application for this vacancy ({existingNumber}).");
                    continue;
                }

                var application = await _applicationService.CreateAsync(
                    new CreateJobApplicationDto
                    {
                        JobVacancyId = dto.JobVacancyId,
                        JobCandidateId = candidateId,
                        Source = ApplicationSource.TalentPool,
                        YearsOfExperience = candidate.TotalYearsExperience,
                        AvailableFrom = candidate.AvailableFrom,
                    },
                    tenantId, actingEmployeeId, cancellationToken);

                await _engagementService.LogEventAsync(
                    new CreateCandidateEngagementEventDto
                    {
                        JobCandidateId = candidateId,
                        EventType = CandidateEngagementEventType.InvitedToApply,
                        EventDate = DateTime.UtcNow,
                        Subject = $"Invited to apply: {vacancy.JobTitle} ({vacancy.VacancyNumber})",
                        Notes = dto.Notes,
                        IsInternal = false,
                    },
                    tenantId, actingEmployeeId, cancellationToken);

                if (dto.SendEmail)
                    await SendInvitationEmailAsync(candidate, vacancy, application.ApplicationNumber, dto.Notes);

                result.Succeeded++;
                result.Results.Add(new RecruitmentBulkOperationItemResult
                {
                    ApplicationId = application.Id,
                    CandidateId = candidateId,
                    Success = true,
                    Message = application.ApplicationNumber,
                });
            }
            catch (Exception ex)
            {
                Skip(result, candidateId, ex.Message);
            }
        }

        _logger.LogInformation(
            "Talent pool: {Succeeded} candidate(s) invited to apply for {VacancyNumber}, {Skipped} skipped.",
            result.Succeeded, vacancy.VacancyNumber, result.Skipped);
        return result;
    }

    /// <remarks>
    /// ⚠ Booking does NOT manufacture an application. Decision Q2: the shortlist, the interview and
    /// the offer all hang off an application, and inventing one here would leave no record of who
    /// decided this person should be considered. A candidate with no application is skipped with a
    /// reason that says to invite them first — which is the button immediately above.
    /// </remarks>
    public async Task<RecruitmentBulkOperationResultDto> BookForInterviewAsync(
        TalentPoolBookInterviewDto dto, Guid actingEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var interview = await _interviewRepository.GetByIdAsync(dto.JobInterviewId);
        if (interview is null || interview.TenantId != tenantId || interview.IsDeleted)
            throw new ArgumentException($"Job interview '{dto.JobInterviewId}' not found.");

        if (interview.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("That interview has been cancelled, so nobody can be booked into it.");

        var vacancyApplications = (await _applicationRepository.GetByVacancyIdAsync(interview.JobVacancyId))
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .ToList();

        var result = new RecruitmentBulkOperationResultDto();
        foreach (var candidateId in dto.CandidateIds.Distinct())
        {
            try
            {
                var candidate = await _candidateRepository.GetByIdAsync(candidateId);
                if (candidate is null || candidate.TenantId != tenantId || candidate.IsDeleted)
                {
                    Skip(result, candidateId, "Candidate not found.");
                    continue;
                }

                // The most recent application, when somebody has applied more than once for the
                // same post over time — a re-advertised vacancy is one row, not several.
                var application = vacancyApplications
                    .Where(a => a.JobCandidateId == candidateId)
                    .OrderByDescending(a => a.ApplicationDate)
                    .FirstOrDefault();

                if (application is null)
                {
                    Skip(result, candidateId,
                        $"{candidate.FullName} has not applied for this vacancy. Invite them to apply first — "
                        + "an interview is booked against an application.");
                    continue;
                }

                if (application.Status is ApplicationStatus.Withdrawn or ApplicationStatus.Rejected)
                {
                    Skip(result, candidateId,
                        $"{candidate.FullName}'s application is {application.Status} and cannot be interviewed.");
                    continue;
                }

                // AddIntervieweeAsync owns the rest: it re-checks the application belongs to this
                // interview's vacancy, refuses a duplicate booking, and auto-advances the pipeline
                // stage. All of that would have to be repeated here otherwise, and one copy would
                // fall behind.
                var interviewee = await _interviewService.AddIntervieweeAsync(
                    new AddJobIntervieweeDto
                    {
                        JobInterviewId = dto.JobInterviewId,
                        JobApplicationId = application.Id,
                    },
                    tenantId, actingEmployeeId, cancellationToken);

                await _engagementService.LogEventAsync(
                    new CreateCandidateEngagementEventDto
                    {
                        JobCandidateId = candidateId,
                        EventType = CandidateEngagementEventType.StatusUpdate,
                        EventDate = DateTime.UtcNow,
                        Subject = $"Booked for interview {interview.InterviewNumber}",
                        Notes = dto.Notes,
                        IsInternal = true,
                    },
                    tenantId, actingEmployeeId, cancellationToken);

                result.Succeeded++;
                result.Results.Add(new RecruitmentBulkOperationItemResult
                {
                    ApplicationId = application.Id,
                    CandidateId = candidateId,
                    Success = true,
                    Message = interviewee.Id.ToString(),
                });
            }
            catch (Exception ex)
            {
                Skip(result, candidateId, ex.Message);
            }
        }

        _logger.LogInformation(
            "Talent pool: {Succeeded} candidate(s) booked into interview {InterviewNumber}, {Skipped} skipped.",
            result.Succeeded, interview.InterviewNumber, result.Skipped);
        return result;
    }

    private static void Skip(RecruitmentBulkOperationResultDto result, Guid candidateId, string message)
    {
        result.Skipped++;
        result.Results.Add(new RecruitmentBulkOperationItemResult
        {
            CandidateId = candidateId,
            Success = false,
            Message = message,
        });
    }

    /// <summary>
    /// Best-effort, exactly as every other candidate-facing recruitment email is: the application
    /// is already committed, and a broken SMTP server must not undo it or hold the response open.
    /// </summary>
    private async Task SendInvitationEmailAsync(
        JobCandidate candidate, JobVacancy vacancy, string applicationNumber, string? note)
    {
        if (string.IsNullOrWhiteSpace(candidate.Email)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"]     = candidate.FullName,
            ["JobTitle"]          = vacancy.JobTitle,
            ["VacancyNumber"]     = vacancy.VacancyNumber,
            ["ApplicationNumber"] = applicationNumber,
            ["ClosingDate"]       = vacancy.ApplicationDeadline?.ToString("dddd, d MMMM yyyy"),
            ["InvitationNote"]    = note,
        };

        try
        {
            var send = _templatedEmail.SendAsync(
                RecruitmentEmailCatalog.Module,
                RecruitmentEmailCatalog.Events.TalentPoolInvitation,
                candidate.Email,
                tokens);

            if (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(10))) == send)
                await send;
            else
                _logger.LogWarning(
                    "Talent-pool invitation email timed out after 10 s for {Email} — the application was still created.",
                    candidate.Email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send the talent-pool invitation to {Email} — the application was still created.",
                candidate.Email);
        }
    }
}

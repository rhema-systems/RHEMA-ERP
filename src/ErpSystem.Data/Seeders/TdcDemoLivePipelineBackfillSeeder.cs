using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Finishes off the records the LIVE pipeline leaves half-built.
///
/// <para><b>Why this is needed.</b> <c>scenarios/050-recruitment.mjs</c> §16–20 create ten extra
/// vacancies and ten extra applicants purely so the registers show every state a recruitment officer
/// works in — Draft through OfferStage, offers Sent and Negotiating and Declined. Those records exist
/// to be counted and filtered, so the scenario does not build them out: the vacancies carry no
/// shortlisting criteria and the applicants get no correspondence. That is invisible on a list screen
/// and obvious the moment somebody opens one, which is exactly the complaint this whole piece of work
/// began with. Two runbook claims assert it:</para>
///
/// <list type="bullet">
///   <item>"every advertised vacancy carries shortlisting criteria (0 = none without)" — found 6.</item>
///   <item>"every application has an acknowledgement and an outcome on file (0 = none without)" — found 10.</item>
/// </list>
///
/// <para>The historical cycles already carry both, written by
/// <see cref="TdcDemoRecruitmentHistorySeeder"/>. This only fills the gaps the API scenario leaves.</para>
///
/// <para><b>Its orchestrator step always runs</b> rather than declaring a probe, because the work is
/// defined by absence — "whatever currently has none" — and doing nothing is the normal outcome. Each
/// query already filters to records that lack the rows, so a second run writes nothing.</para>
/// </summary>
public class TdcDemoLivePipelineBackfillSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoLivePipelineBackfillSeeder> _logger;

    private const string By = "TdcDemoLivePipelineBackfillSeeder";

    /// <summary>Statuses at which a vacancy has been advertised and should therefore have criteria.</summary>
    private static readonly JobVacancyStatus[] Advertised =
    {
        JobVacancyStatus.Published, JobVacancyStatus.ClosedForApplications, JobVacancyStatus.Shortlisting,
        JobVacancyStatus.Interviewing, JobVacancyStatus.OfferStage, JobVacancyStatus.Filled,
    };

    public TdcDemoLivePipelineBackfillSeeder(
        ApplicationDbContext context, ILogger<TdcDemoLivePipelineBackfillSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot finish the live pipeline records.");
            return;
        }

        var tenantId = tenant.Id;
        var now = DateTime.UtcNow;

        var criteriaAdded = await AddMissingCriteriaAsync(tenantId, now, ct);
        var lettersAdded = await AddMissingCorrespondenceAsync(tenantId, ct);

        if (criteriaAdded + lettersAdded > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Live pipeline finished off: {Criteria} shortlisting criteria added to advertised vacancies "
                + "that had none, and {Letters} letters added to applications with no correspondence.",
                criteriaAdded, lettersAdded);
        }
        else
        {
            _logger.LogInformation("Nothing to finish — every advertised vacancy and application is complete.");
        }
    }

    /// <summary>
    /// Gives an advertised vacancy the criteria it was supposedly advertised against. The weights add
    /// to 100 and the experience threshold comes off the establishment, so the screening screen scores
    /// on the post's real requirement rather than a made-up number.
    /// </summary>
    private async Task<int> AddMissingCriteriaAsync(Guid tenantId, DateTime now, CancellationToken ct)
    {
        var withCriteria = await _context.Set<JobShortlistingCriteria>().IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => c.JobVacancyId)
            .Distinct()
            .ToListAsync(ct);

        var bare = await _context.Set<JobVacancy>().IgnoreQueryFilters()
            .Where(v => v.TenantId == tenantId && !v.IsDeleted
                     && Advertised.Contains(v.VacancyStatus)
                     && !withCriteria.Contains(v.Id))
            .ToListAsync(ct);

        if (bare.Count == 0) return 0;

        var positions = await _context.Set<EmployeePosition>().IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id, p => p, ct);

        // Round 4, lane Q. "A relevant first degree" was a Qualification criterion asking for the
        // WORD "Degree", matched by containment, which no Bachelor of Science contains, so every
        // applicant on every live vacancy failed it (recruitment guide R4-5.2). It is now an
        // Education level criterion, at least the Bachelor's rung, compared by rank. The ladder
        // is built by scenario 005, which runs before this second pass. Without it the criterion
        // is left out, not guessed.
        var bachelors = await _context.Set<QualificationLevel>().IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.IsActive && l.Code == "BDEG")
            .FirstOrDefaultAsync(ct);
        if (bachelors is null)
            _logger.LogWarning(
                "No active 'BDEG' rung on the qualification ladder: the live vacancies get no first-degree criterion. "
                + "Run scenario 005, which builds the ladder, then this pass again.");

        var added = 0;
        foreach (var vacancy in bare)
        {
            positions.TryGetValue(vacancy.PositionId, out var position);
            var title = position?.Title ?? vacancy.CustomAdvertTitle ?? "the post";
            var minYears = position?.MinimumExperienceYears is int my && my > 0 ? my : 4;

            if (bachelors is not null)
            {
                var degree = new JobShortlistingCriteria
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, JobVacancyId = vacancy.Id,
                    CriteriaName = "A relevant first degree",
                    Description = $"A first degree in a discipline relevant to the post of {title}.",
                    Type = JobShortlistingCriteriaType.EducationLevel,
                    RequiredValue = bachelors.Name, IsMandatory = true, Weight = 40,
                    CreatedAt = now, CreatedBy = By,
                };
                _context.Set<JobShortlistingCriteria>().Add(degree);
                _context.Set<JobShortlistingCriteriaValue>().Add(new JobShortlistingCriteriaValue
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, JobShortlistingCriteriaId = degree.Id,
                    Kind = ShortlistingValueKind.QualificationLevel, ReferenceId = bachelors.Id,
                    Label = bachelors.Name, SortOrder = 0, CreatedAt = now, CreatedBy = By,
                });
                added++;
            }

            var criteria = new[]
            {
                new JobShortlistingCriteria
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, JobVacancyId = vacancy.Id,
                    CriteriaName = $"{minYears} years' post-qualification experience",
                    Type = JobShortlistingCriteriaType.YearsOfExperience,
                    MinValue = minYears, IsMandatory = true, Weight = 35,
                    ComparisonOperator = ShortlistingComparisonOperator.GreaterThanOrEqual,
                    CreatedAt = now, CreatedBy = By,
                },
                new JobShortlistingCriteria
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, JobVacancyId = vacancy.Id,
                    CriteriaName = "Written communication and reporting",
                    Type = JobShortlistingCriteriaType.Skill,
                    RequiredValue = "Report writing", IsMandatory = false, Weight = 25,
                    MatchStrategy = ValueMatchStrategy.Contains,
                    CreatedAt = now, CreatedBy = By,
                },
            };

            _context.Set<JobShortlistingCriteria>().AddRange(criteria);
            added += criteria.Length;
        }

        return added;
    }

    /// <summary>
    /// Every applicant hears twice: an acknowledgement when the application arrives, and the outcome
    /// once the shortlist is settled. The correspondence tab is the screen an HR officer is asked to
    /// defend in a complaint, so no application should be without one.
    /// </summary>
    private async Task<int> AddMissingCorrespondenceAsync(Guid tenantId, CancellationToken ct)
    {
        var withLetters = await _context.Set<JobApplicantCommunication>().IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted)
            .Select(m => m.JobApplicationId)
            .Distinct()
            .ToListAsync(ct);

        var bare = await _context.Set<JobApplication>().IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && !withLetters.Contains(a.Id))
            .ToListAsync(ct);

        if (bare.Count == 0) return 0;

        var vacancyTitles = await _context.Set<JobVacancy>().IgnoreQueryFilters()
            .Where(v => v.TenantId == tenantId && !v.IsDeleted)
            .ToDictionaryAsync(v => v.Id, v => v.CustomAdvertTitle ?? v.VacancyNumber, ct);

        var added = 0;
        foreach (var application in bare)
        {
            var title = vacancyTitles.TryGetValue(application.JobVacancyId, out var t) ? t : "the advertised post";
            var shortlisted = application.ShortlistedDate is not null;

            _context.Set<JobApplicantCommunication>().Add(new JobApplicantCommunication
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JobApplicationId = application.Id,
                Type = JobApplicantCommunicationType.Email,
                Direction = JobApplicantCommunicationDirection.System,
                Subject = $"Application received — {title}",
                Body = "We acknowledge receipt of your application. Your application reference is "
                     + $"{application.ApplicationNumber}. Only shortlisted applicants will be contacted.",
                SentAt = application.ApplicationDate.AddMinutes(4),
                CreatedAt = application.ApplicationDate.AddMinutes(4),
                CreatedBy = By,
            });

            _context.Set<JobApplicantCommunication>().Add(new JobApplicantCommunication
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JobApplicationId = application.Id,
                Type = shortlisted ? JobApplicantCommunicationType.Email : JobApplicantCommunicationType.Letter,
                Direction = JobApplicantCommunicationDirection.Outbound,
                Subject = shortlisted ? $"Invitation to interview — {title}" : $"Outcome of your application — {title}",
                Body = shortlisted
                    ? "You have been shortlisted for interview. Details of the date, time and venue follow "
                    + "separately. Please bring original certificates, a valid national ID and two passport photographs."
                    : "Thank you for your interest in Tema Development Corporation. On this occasion your "
                    + "application has not been successful. Your details will be retained for twelve months and "
                    + "considered against future vacancies.",
                SentAt = (application.ShortlistedDate ?? application.ApplicationDate).AddDays(1),
                CreatedAt = (application.ShortlistedDate ?? application.ApplicationDate).AddDays(1),
                CreatedBy = By,
            });

            added += 2;
        }

        return added;
    }
}

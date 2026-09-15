using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Writes the recruitment year that has <b>already closed</b> — twelve complete cycles between
/// January and July 2026, each carried from requisition to hire (or to a documented failure) —
/// onto the DEFAULT tenant's demonstration data.
///
/// <para><b>Why this exists.</b> The demo pack's coverage rule is "at least one row in every
/// required table", and recruitment satisfied it: 71 of its 73 tables had a row. A stakeholder
/// walkthrough on 2026-09-11 still found the module hollow, because one row proves a table is
/// wired and proves nothing about a screen. The whole module rested on two vacancies, seven
/// candidates, eight applications, two interviews, two offers and one hire — and the dashboard and
/// analytics pages, which aggregate rather than list, therefore read <b>zero</b>.</para>
///
/// <para><b>Why a seeder and not a scenario.</b> Every other recruitment row is created through the
/// real API by <c>scenarios/050-recruitment.mjs</c>, and that stays true for the live pipeline.
/// History cannot be: <c>RecruitmentAnalyticsService</c> measures time-to-fill from
/// <c>StaffRequisition.RequestDate</c> to <c>JobHireRecord.ActualStartDate</c>, groups applications
/// by <c>ApplicationDate.Year</c>, and reads <c>JobVacancy.ShortlistCompletedAt</c> — all of which
/// the API stamps itself at the moment of the call. Driven through the doors, every record would
/// carry the build date, and every trend chart would be a single point above "today". Dated history
/// has to be written directly. Nothing here makes a broken door work; the live pipeline still goes
/// through the API.</para>
///
/// <para><b>What it is careful about.</b></para>
/// <list type="bullet">
///   <item><b>Number sequences are advanced.</b> Sixty-odd candidates and a dozen vacancies minted
///   behind <c>INumberSequenceService</c>'s back would leave its counter stale, and the first
///   candidate created <i>live on stage</i> would collide on a duplicate number. Every key this
///   seeder consumes (REQ, VAC, CAND, APP, OFR, HIR, INT) is read, minted from and written back —
///   in the <i>same save</i> as the rows it numbered, so that no later failure can commit the one
///   without the other. That is not hypothetical: see the note on <c>Counters.Stage</c>.</item>
///   <item><b>It runs in the SECOND seed pass</b>, after the scenarios, so the live records the
///   runbook quotes by number — REQ-2026-00001…4, VAC-000001, VAC-000002 — keep those numbers.
///   History therefore carries numbers <i>above</i> the live records despite being older; that is a
///   deliberate trade against renumbering Book 1 and the cheat sheet.</item>
///   <item><b>It is deterministic.</b> A demo record must be identical on every rebuild, so names,
///   dates, salaries and phone numbers come from index arithmetic and a fixed-seed LCG — never from
///   <c>Random</c>, whose sequence is not stable across .NET versions.</item>
/// </list>
///
/// <para>Idempotent: skipped when any vacancy has already been filled, which the live scenario
/// never does.</para>
/// </summary>
public class TdcDemoRecruitmentHistorySeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoRecruitmentHistorySeeder> _logger;

    private const string By = "TdcDemoRecruitmentHistorySeeder";

    /// <summary>The calendar year the history is written into — the year the analytics page defaults to.</summary>
    private const int Year = 2026;

    /// <summary>
    /// The establishment post whose live vacancy is pushed past its deadline to light the
    /// dashboard's SLA panel. Scenario 050 (<c>liveRuns</c>) publishes this one for the purpose —
    /// keep the two in step if either changes.
    /// </summary>
    private const string OverdueVacancyPositionCode = "DV-STP";

    public TdcDemoRecruitmentHistorySeeder(
        ApplicationDbContext context, ILogger<TdcDemoRecruitmentHistorySeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // THE TWELVE CYCLES
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    private enum Outcome { Filled, NoSuitableCandidates, Cancelled }

    /// <param name="PositionCode">A post on the TDC establishment. All twelve are currently staffed,
    /// so "recruited for in March, filled in May" agrees with what the org chart shows today.</param>
    /// <param name="ReqDay">Day-of-year the requisition was raised. Spreading these across the year
    /// is what gives the applications-per-month chart a shape.</param>
    /// <param name="DeclinedFirstOffer">When true the first offer is declined and a second is made
    /// to the runner-up — which is the only way the offer acceptance-rate chart gets a denominator.</param>
    private sealed record Cycle(
        string PositionCode,
        int ReqDay,
        StaffRequisitionType Type,
        StaffRequisitionPriority Priority,
        StaffReplacementReason? ReplacementReason,
        int Applicants,
        Outcome Outcome,
        string RecruiterPositionCode,
        JobPostingChannel Channel,
        decimal Salary,
        int DaysToFill,
        bool DeclinedFirstOffer,
        string Justification);

    private static readonly Cycle[] Cycles =
    {
        new("FN-AO",  12, StaffRequisitionType.Replacement, StaffRequisitionPriority.Medium, StaffReplacementReason.Resignation,   7, Outcome.Filled, "HR-HRO", JobPostingChannel.Newspaper,      4800m, 74, false,
            "The post fell vacant when the incumbent resigned at the end of December. Expenditure accounts cannot close a month without it and the Financial Accountant is carrying the reconciliation personally."),
        new("MS-SA",  19, StaffRequisitionType.NewPosition, StaffRequisitionPriority.High,   null,                                  6, Outcome.Filled, "HR-HRM", JobPostingChannel.LinkedIn,       7200m, 88, false,
            "Approved in the 2026 establishment review. The MIS Unit has run without a dedicated systems administrator since the ERP rollout began; server administration is currently an unofficial addition to the Database Administrator's duties."),
        new("ES-EOL", 40, StaffRequisitionType.Replacement, StaffRequisitionPriority.Medium, StaffReplacementReason.Retirement,     5, Outcome.Filled, "HR-HRO", JobPostingChannel.CompanyWebsite, 5100m, 69, false,
            "The incumbent reached compulsory retirement age on 31 January. The Lands section carries 1,400 active leases and cannot be left with one officer."),
        new("DV-QS",  47, StaffRequisitionType.Replacement, StaffRequisitionPriority.High,   StaffReplacementReason.Promotion,      8, Outcome.Filled, "HR-HRM", JobPostingChannel.JobBoard,       6900m, 95, true,
            "Vacated on the incumbent's promotion to Supervising Quantity Surveyor. Three estate projects are in measurement and the valuations cannot wait for an internal acting arrangement."),
        new("HR-HRO", 72, StaffRequisitionType.NewPosition, StaffRequisitionPriority.Medium, null,                                  6, Outcome.Filled, "HHR",    JobPostingChannel.InternalPortal, 5400m, 61, false,
            "Second HR Officer post approved in the establishment review to carry the recruitment and records workload that has grown with the ERP implementation."),
        new("LG-LO", 104, StaffRequisitionType.Replacement, StaffRequisitionPriority.Urgent, StaffReplacementReason.Resignation,    5, Outcome.Filled, "HR-HRM", JobPostingChannel.Agency,         7600m, 57, false,
            "Resignation with one month's notice. Twenty-two land litigation files are active and the Head of Legal is the only qualified officer remaining."),
        new("CP-CRS",111, StaffRequisitionType.Backfill,    StaffRequisitionPriority.Medium, StaffReplacementReason.LongTermLeave,  7, Outcome.Filled, "HR-HRO", JobPostingChannel.Indeed,         4600m, 66, false,
            "The substantive holder is on twelve months' study leave. The four client-relations desks need a supervisor for the duration."),
        new("IA-AAS",141, StaffRequisitionType.Replacement, StaffRequisitionPriority.Low,    StaffReplacementReason.Transfer,       6, Outcome.Filled, "HR-HRO", JobPostingChannel.Glassdoor,      4200m, 82, false,
            "The incumbent transferred to the Compliance desk. The audit programme for the second half of the year assumes two assistants."),
        new("PR-PO", 165, StaffRequisitionType.Contract,    StaffRequisitionPriority.High,   null,                                  4, Outcome.Filled, "HR-HRM", JobPostingChannel.CompanyWebsite, 5900m, 48, false,
            "Two-year contract appointment to run the procurement plan for the Community 25 development. Funded from the project budget, not the establishment."),
        new("MK-SMO",172, StaffRequisitionType.Replacement, StaffRequisitionPriority.Medium, StaffReplacementReason.Termination,    5, Outcome.Filled, "HR-HRO", JobPostingChannel.LinkedIn,       5200m, 71, true,
            "The post fell vacant following the conclusion of a disciplinary process. Sales coverage for the Tema and Ashaiman estates has been split between two officers since March."),
        new("DC-BI", 196, StaffRequisitionType.Other,       StaffRequisitionPriority.Medium, null,                                  4, Outcome.NoSuitableCandidates, "HR-HRO", JobPostingChannel.Newspaper, 4400m, 0, false,
            "Development Control requires a fourth inspector to cover the Community 22 corridor, where unauthorised development has risen sharply."),
        new("MS-CA", 203, StaffRequisitionType.Internship,  StaffRequisitionPriority.Low,    null,                                  3, Outcome.Cancelled, "HR-HRO", JobPostingChannel.InternalPortal, 1800m, 0, false,
            "Twelve-month industrial attachment for a KNUST computer science undergraduate, supporting the MIS Unit's help desk."),
    };

    /// <summary>
    /// Three requisitions that never reached a vacancy. They exist so the requisitions register shows
    /// the states a recruitment officer actually works through — a request sitting with the
    /// reviewer, one that was refused, and one partly filled — rather than only the three statuses
    /// the happy path produces.
    /// </summary>
    private static readonly (string Code, int Day, StaffRequisitionType Type, StaffRequisitionStatus Status, string Justification, string Note)[] StandaloneRequisitions =
    {
        ("DV-ART", 228, StaffRequisitionType.NewPosition, StaffRequisitionStatus.UnderReview,
            "Building Maintenance has one artisan against an establishment of three. Planned maintenance on the Community 1 flats is six months behind as a result.",
            "With the General Manager, Finance & Administration for establishment confirmation."),
        ("DC-GS",  214, StaffRequisitionType.Replacement, StaffRequisitionStatus.Rejected,
            "The Guardsmen Supervisor post has been vacant since the incumbent's transfer. Sixteen guardsmen report directly to the Head of Development Control in the interim.",
            "Refused for this financial year. The Directorate is to propose an acting arrangement from within the existing establishment and re-submit in the 2027 estimates."),
        ("CP-CRV", 189, StaffRequisitionType.Replacement, StaffRequisitionStatus.PartiallyFulfilled,
            "Two VIP client-relations assistants were approved. The desk cannot run a full day with one officer.",
            "One of the two posts filled in July; the second re-advertised after the preferred candidate withdrew."),
    };

    // ═════════════════════════════════════════════════════════════════════════════════════════════

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed the recruitment history.");
            return;
        }

        var tenantId = tenant.Id;

        // Keyed on a FILLED vacancy. The live scenario walks a vacancy as far as Published and never
        // fills one, so this distinguishes "history already written" from "the scenario ran".
        if (await _context.Set<JobVacancy>().IgnoreQueryFilters()
                .AnyAsync(v => v.TenantId == tenantId && !v.IsDeleted
                            && v.VacancyStatus == JobVacancyStatus.Filled, ct))
        {
            _logger.LogInformation("Recruitment history is already present for the DEFAULT tenant. Skipping.");
            return;
        }

        // ── Establishment the history hangs off ──────────────────────────────────────────────────
        var positionList = await _context.Set<EmployeePosition>().IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.Code != "")
            .ToListAsync(ct);

        // Two posts can share a code only through bad data; first-wins keeps the seeder running
        // rather than throwing a duplicate-key out of ToDictionary halfway through a rebuild.
        var positions = positionList
            .GroupBy(p => p.Code)
            .ToDictionary(g => g.Key, g => g.First());
        var positionsById = positionList.ToDictionary(p => p.Id, p => p);

        var unitNames = await _context.Set<OrganizationUnit>().IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var employeesByPosition = await _context.Employees.IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.EmployeeNumber.StartsWith("TDC/"))
            .GroupBy(e => e.PositionId)
            .Select(g => new { PositionId = g.Key, Employee = g.OrderBy(e => e.EmployeeNumber).First() })
            .ToDictionaryAsync(x => x.PositionId, x => x.Employee, ct);

        Employee? AtPosition(string code) =>
            positions.TryGetValue(code, out var p) && p.Id != Guid.Empty
            && employeesByPosition.TryGetValue(p.Id, out var e) ? e : null;

        var hrHead = AtPosition("HHR");
        if (hrHead is null)
        {
            _logger.LogWarning(
                "No employee holds the Head of HR & Administration post (HHR). The demo workforce has "
                + "not been seeded — run 'seed-hr-all' then 'seed-hr-demo' before this step. Skipping.");
            return;
        }

        var missing = Cycles.Select(c => c.PositionCode)
            .Concat(StandaloneRequisitions.Select(r => r.Code))
            .Distinct()
            .Where(code => !positions.ContainsKey(code))
            .ToList();

        if (missing.Count > 0)
        {
            _logger.LogWarning(
                "These establishment posts are missing, so their cycles are skipped: {Codes}.",
                string.Join(", ", missing));
        }

        // Country.Code is ISO alpha-3 — the alpha-2 "GH" is a separate column.
        var ghana = await _context.Set<Country>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Code == "GHA", ct);

        var jobDescriptions = await _context.Set<JobDescription>().IgnoreQueryFilters()
            .Where(j => j.TenantId == tenantId && !j.IsDeleted)
            .GroupBy(j => j.PositionId)
            .Select(g => new { PositionId = g.Key, Id = g.First().Id })
            .ToDictionaryAsync(x => x.PositionId, x => x.Id, ct);

        var pipelineId = await _context.Set<RecruitmentPipeline>().IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        var hqLocationId = await _context.Set<Location>().IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted)
            .OrderBy(l => l.Name)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(ct);

        // ── Reference data the dossiers and scorecards hang off ─────────────────────────────────
        // The pipeline stages, the interview question bank and the talent segments are created
        // through the API by scenarios/050-recruitment.mjs, which runs BEFORE this second seed pass.
        // Each is optional: a missing catalogue thins that part of the dossier rather than failing.
        var pipelineStages = await _context.Set<RecruitmentPipelineStage>().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Order).ToListAsync(ct);

        var questionBank = await _context.Set<JobInterviewQuestionDetail>().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive).ToListAsync(ct);

        var associates = await _context.Set<ExternalAssociate>().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted).Take(6).ToListAsync(ct);

        var relationshipTypes = await _context.Set<RelationshipType>().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted).ToListAsync(ct);

        var languageCatalogue = await _context.Set<Language>().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted).ToListAsync(ct);

        var qualificationCatalogue = await _context.Set<Qualification>().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted).ToListAsync(ct);

        var segments = await _context.Set<CandidateTalentSegment>().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted).ToListAsync(ct);

        var refs = new Refs(tenantId, pipelineStages, questionBank, associates, relationshipTypes,
                            languageCatalogue, qualificationCatalogue, segments, hrHead);

        if (questionBank.Count == 0)
        {
            _logger.LogWarning(
                "The interview question bank is empty, so the historical interviews will carry a panel "
                + "but no scorecards. Run scenarios/050-recruitment.mjs before this seeder.");
        }

        // ── Counters, carried forward from whatever the scenarios already issued ─────────────────
        var seq = await Counters.LoadAsync(_context, tenantId, ct);
        var rng = new DeterministicRng(20260914u);
        var names = new NamePool();

        var now = DateTime.UtcNow;
        DateTime Day(int dayOfYear) => new DateTime(Year, 1, 1).AddDays(dayOfYear - 1);

        var vacancies = new List<JobVacancy>();
        var refereesByCandidate = new Dictionary<Guid, List<JobCandidateReferee>>();
        var hires = 0;
        var panelIndex = 0;

        // ⚠ SECOND-LEVEL DEPENDENTS ARE DEFERRED, AND THIS IS NOT A STYLE CHOICE.
        // A row whose required foreign key points at a principal that is itself still in the Added
        // state cannot simply be Add()ed: EF's NavigationFixer runs ConditionallyNullForeignKeyProperties
        // over the pending graph and throws "The association between entity types 'X' and 'Y' has been
        // severed, but the relationship is ... required". Measured on 2026-09-14 with
        // PreEmploymentCheckItem → ReferenceCheckResponse and again with
        // JobCandidate → CandidateEngagementEvent.
        // After the first SaveChangesAsync every principal above is Unchanged and persisted, so the
        // same Add() is unambiguous. These closures capture the principals and run there.
        var deferred = new List<Action>();

        foreach (var cycle in Cycles)
        {
            if (!positions.TryGetValue(cycle.PositionCode, out var position)) continue;

            var recruiter = AtPosition(cycle.RecruiterPositionCode) ?? hrHead;
            var hiringManager = position.ReportsToPositionId is Guid rp && employeesByPosition.TryGetValue(rp, out var hm)
                ? hm
                : hrHead;

            // The offer letter names the post the appointee reports to, not the person — Employee
            // carries no job title of its own (its Title is a salutation), so it comes off the
            // establishment.
            var reportsToTitle = position.ReportsToPositionId is Guid rpt && positionsById.TryGetValue(rpt, out var rposition)
                ? rposition.Title
                : "Head of Department";

            var unitName = unitNames.TryGetValue(position.OrganizationUnitId, out var un) ? un : "Tema Development Corporation";

            var requestDate = Day(cycle.ReqDay);
            var approvedDate = requestDate.AddDays(rng.Next(4, 12));
            var publishDate = approvedDate.AddDays(rng.Next(2, 7));
            var deadline = publishDate.AddDays(21);
            var shortlistDate = deadline.AddDays(rng.Next(3, 10));

            // ── Requisition ──────────────────────────────────────────────────────────────────────
            var requisition = new StaffRequisition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RequisitionNumber = seq.Next(Counters.Req),
                RequisitionTitle = $"{position.Title} — {unitName}",
                PositionId = position.Id,
                OrganizationUnitId = position.OrganizationUnitId,
                OrganizationLevelId = position.OrganizationLevelId,
                JobDescriptionId = jobDescriptions.TryGetValue(position.Id, out var jd) ? (Guid?)jd : null,
                Type = cycle.Type,
                Priority = cycle.Priority,
                Status = cycle.Outcome switch
                {
                    Outcome.Filled => StaffRequisitionStatus.Fulfilled,
                    Outcome.NoSuitableCandidates => StaffRequisitionStatus.Cancelled,
                    _ => StaffRequisitionStatus.OnHold,
                },
                NumberOfPositions = 1,
                PositionsFilled = cycle.Outcome == Outcome.Filled ? 1 : 0,
                ReplacementReason = cycle.ReplacementReason,
                RequestDate = requestDate,
                DesiredStartDate = requestDate.AddDays(60),
                TargetFillDate = requestDate.AddDays(75),
                BusinessJustification = cycle.Justification,
                IsBudgeted = true,
                AllowInternalCandidates = true,
                AllowExternalCandidates = cycle.Channel != JobPostingChannel.InternalPortal,
                RequestedById = hiringManager.Id,
                IsFulfilled = cycle.Outcome == Outcome.Filled,
                FulfilledDate = cycle.Outcome == Outcome.Filled ? (DateTime?)requestDate.AddDays(cycle.DaysToFill) : null,
                CancelledById = cycle.Outcome == Outcome.NoSuitableCandidates ? (Guid?)hrHead.Id : null,
                CancelledDate = cycle.Outcome == Outcome.NoSuitableCandidates ? (DateTime?)shortlistDate.AddDays(30) : null,
                CancellationReason = cycle.Outcome == Outcome.NoSuitableCandidates
                    ? "Two advertising rounds produced no applicant meeting the minimum qualification. The post is to be re-scoped and re-advertised in the next financial year."
                    : null,
                Notes = cycle.Outcome switch
                {
                    Outcome.Filled => "Closed on the successful candidate's assumption of duty.",
                    Outcome.NoSuitableCandidates => "Closed without a hire.",
                    _ => "Held pending the outcome of the establishment review.",
                },
                CreatedAt = requestDate,
                CreatedBy = By,
            };
            _context.Set<StaffRequisition>().Add(requisition);

            AddCosts(tenantId, requisition, cycle, recruiter.Id, rng);

            // ── Vacancy ──────────────────────────────────────────────────────────────────────────
            var vacancy = new JobVacancy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                VacancyNumber = seq.Next(Counters.Vac),
                CustomAdvertTitle = position.Title,
                StaffRequisitionId = requisition.Id,
                PositionId = position.Id,
                NumberOfPositions = 1,
                HiringManagerId = hiringManager.Id,
                RecruiterId = recruiter.Id,
                VacancyStatus = cycle.Outcome switch
                {
                    Outcome.Filled => JobVacancyStatus.Filled,
                    Outcome.NoSuitableCandidates => JobVacancyStatus.ClosedForApplications,
                    _ => JobVacancyStatus.Cancelled,
                },
                PublishDate = publishDate,
                ActualPublishDate = publishDate,
                ApplicationDeadline = deadline,
                ShortlistingDeadline = deadline.AddDays(10),
                ShortlistApprovalStatus = cycle.Outcome == Outcome.Cancelled
                    ? ShortlistApprovalStatus.NotSubmitted
                    : ShortlistApprovalStatus.Approved,
                ShortlistSubmittedAt = cycle.Outcome == Outcome.Cancelled ? null : (DateTime?)shortlistDate,
                ShortlistSubmittedById = cycle.Outcome == Outcome.Cancelled ? null : (Guid?)recruiter.Id,
                ShortlistApprovedAt = cycle.Outcome == Outcome.Cancelled ? null : (DateTime?)shortlistDate.AddDays(2),
                ShortlistApprovedById = cycle.Outcome == Outcome.Cancelled ? null : (Guid?)hrHead.Id,
                ShortlistCompletedAt = cycle.Outcome == Outcome.Cancelled ? null : (DateTime?)shortlistDate,
                // What the time-to-shortlist chart actually plots — days from advert to a settled list.
                TimeToShortlistDays = cycle.Outcome == Outcome.Cancelled
                    ? null
                    : (int?)(shortlistDate - publishDate).TotalDays,
                ShortlistingSlaBreached = cycle.Outcome != Outcome.Cancelled
                                       && (shortlistDate - deadline).TotalDays > 7,
                NumberOfInterviewRounds = cycle.Outcome == Outcome.Filled ? 2 : 1,
                ClosedDate = cycle.Outcome == Outcome.Filled
                    ? requestDate.AddDays(cycle.DaysToFill)
                    : shortlistDate.AddDays(30),
                FilledDate = cycle.Outcome == Outcome.Filled ? (DateTime?)requestDate.AddDays(cycle.DaysToFill) : null,
                ClosureReason = cycle.Outcome switch
                {
                    Outcome.Filled => JobVacancyClosureReason.PositionFilled,
                    Outcome.NoSuitableCandidates => JobVacancyClosureReason.NoSuitableCandidates,
                    _ => JobVacancyClosureReason.HiringFreeze,
                },
                ClosureNotes = cycle.Outcome == Outcome.Cancelled
                    ? "Withdrawn on the instruction of the Managing Director pending the 2027 establishment review."
                    : null,
                IsSalaryVisible = cycle.Channel is JobPostingChannel.InternalPortal or JobPostingChannel.CompanyWebsite,
                EmploymentType = cycle.Type switch
                {
                    StaffRequisitionType.Contract => EmploymentType.Contract,
                    StaffRequisitionType.Internship => EmploymentType.Internship,
                    _ => EmploymentType.Permanent,
                },
                WorkMode = WorkMode.OnSite,
                SalaryRangeMin = decimal.Round(cycle.Salary * 0.9m, 0),
                SalaryRangeMax = decimal.Round(cycle.Salary * 1.15m, 0),
                SalaryCurrencyCode = "GHS",
                RequiredMinExperienceYears = position.MinimumExperienceYears > 0 ? position.MinimumExperienceYears : 3,
                TargetStartDate = DateOnly.FromDateTime(requestDate.AddDays(60)),
                RequiresWrittenTest = cycle.Applicants >= 6,
                RecruitmentPipelineId = pipelineId,
                TestScoreWeight = cycle.Applicants >= 6 ? 20 : 0,
                AllowInternalCandidates = true,
                AllowExternalCandidates = cycle.Channel != JobPostingChannel.InternalPortal,
                CreatedAt = approvedDate,
                CreatedBy = By,
            };
            _context.Set<JobVacancy>().Add(vacancy);
            vacancies.Add(vacancy);

            // ⚠ THE TWO RECORDS POINT AT EACH OTHER: JobVacancy.StaffRequisitionId names the
            // requisition, and StaffRequisition.JobVacancyId names the vacancy back. Setting both
            // while both are still Added makes EF refuse the entire save with "Unable to save
            // changes because a circular dependency was detected in the data to be saved:
            // 'JobVacancy [Added] <- ... StaffRequisition [Added] <- ... JobVacancy [Added]'" — it
            // cannot order the two INSERTs. The vacancy's link is the one that must exist at insert
            // time; the requisition's back-pointer is a convenience and is set after the first save,
            // by which point both rows exist and it is a plain UPDATE.
            var backPointerReq = requisition;
            var backPointerVac = vacancy;
            deferred.Add(() => backPointerReq.JobVacancyId = backPointerVac.Id);

            // ── The advert ───────────────────────────────────────────────────────────────────────
            _context.Set<JobPosting>().Add(new JobPosting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JobVacancyId = vacancy.Id,
                Channel = cycle.Channel,
                Title = $"{position.Title} — Tema Development Corporation",
                Description =
                    $"Applications are invited from suitably qualified persons for appointment to the post of {position.Title}. "
                    + "The successful applicant will be based at the Tema Head Office and will report to the head of the unit.",
                AdvertHeadline = $"TDC is recruiting: {position.Title}",
                HowToApply = "Apply online at careers.tdcghana.com, or deliver a sealed application to the Head of HR & Administration, TDC Head Office, Tema.",
                ClosingDateText = deadline.ToString("dddd, d MMMM yyyy"),
                ContactDetails = "recruitment@tdcghana.com · 0303 202 xxx",
                ShowSalaryInAdvert = vacancy.IsSalaryVisible,
                PublishDate = publishDate,
                ActualPublishDate = publishDate,
                ExpiryDate = deadline,
                Status = JobPostingStatus.Expired,
                IsActive = false,
                ApplicationCount = cycle.Applicants,
                PostedById = recruiter.Id,
                CreatedAt = publishDate,
                CreatedBy = By,
            });

            // A second advert on a different channel for the harder-to-fill posts, so the
            // source-effectiveness chart can attribute the same vacancy to two routes.
            if (cycle.Applicants >= 6)
            {
                _context.Set<JobPosting>().Add(new JobPosting
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    JobVacancyId = vacancy.Id,
                    Channel = JobPostingChannel.CompanyWebsite,
                    Title = $"{position.Title} (Tema)",
                    Description = $"Vacancy for a {position.Title} at Tema Development Corporation.",
                    PublishDate = publishDate,
                    ActualPublishDate = publishDate,
                    ExpiryDate = deadline,
                    Status = JobPostingStatus.Closed,
                    IsActive = false,
                    ApplicationCount = 0,
                    PostedById = recruiter.Id,
                    CreatedAt = publishDate,
                    CreatedBy = By,
                });
            }

            // ── Candidates and applications ──────────────────────────────────────────────────────
            var applications = new List<JobApplication>();
            var candidatesByApplication = new Dictionary<Guid, JobCandidate>();

            for (var i = 0; i < cycle.Applicants; i++)
            {
                var candidate = names.NextCandidate(tenantId, seq, ghana?.Id, rng, publishDate);
                _context.Set<JobCandidate>().Add(candidate);

                // NextCandidate has already incremented, so Issued - 1 is this candidate's index —
                // the seat number every deterministic choice in the dossier is derived from.
                var seat = names.Issued - 1;
                refereesByCandidate[candidate.Id] = AddDossier(refs, candidate, cycle.PositionCode, seat, rng, publishDate);
                deferred.Add(() => AddSegmentAndEngagement(refs, candidate, recruiter, publishDate, seat));

                var appliedOn = publishDate.AddDays(rng.Next(1, 21)).AddHours(rng.Next(8, 19));
                var isWinner = cycle.Outcome == Outcome.Filled && i == 0;
                var isRunnerUp = cycle.Outcome == Outcome.Filled && i == 1;
                var shortlisted = i < Math.Min(3, cycle.Applicants) && cycle.Outcome != Outcome.Cancelled;

                var application = new JobApplication
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ApplicationNumber = seq.Next(Counters.App),
                    JobVacancyId = vacancy.Id,
                    JobCandidateId = candidate.Id,
                    ApplicationDate = appliedOn,
                    // Every application carries a different route to TDC. This one field is the whole
                    // source-effectiveness chart, and the demo data had it on one value for all eight rows.
                    Source = SourceFor(i, cycle),
                    Status = StatusFor(cycle, i, isWinner, isRunnerUp, shortlisted),
                    YearsOfExperience = 2 + rng.Next(12),
                    AvailableFrom = appliedOn.AddDays(30),
                    AutoScore = decimal.Round(45m + rng.Next(0, 5000) / 100m, 2),
                    ScoredAt = appliedOn.AddMinutes(2),
                    AggregatedReviewScore = shortlisted ? (decimal?)decimal.Round(60m + rng.Next(0, 3500) / 100m, 2) : null,
                    DecisionSource = shortlisted ? (ShortlistDecisionSource?)ShortlistDecisionSource.Manual : null,
                    ShortlistedDate = shortlisted ? (DateTime?)shortlistDate : null,
                    ShortlistedById = shortlisted ? (Guid?)recruiter.Id : null,
                    ShortlistingNotes = shortlisted
                        ? "Meets the minimum qualification and the experience threshold; referees supplied."
                        : null,
                    RejectedDate = !shortlisted && cycle.Outcome != Outcome.Cancelled ? (DateTime?)shortlistDate.AddDays(1) : null,
                    RejectedById = !shortlisted && cycle.Outcome != Outcome.Cancelled ? (Guid?)recruiter.Id : null,
                    RejectionReason = !shortlisted && cycle.Outcome != Outcome.Cancelled
                        ? "Did not meet the minimum qualification stated in the advertisement."
                        : null,
                    CreatedAt = appliedOn,
                    CreatedBy = By,
                };
                _context.Set<JobApplication>().Add(application);
                applications.Add(application);
                candidatesByApplication[application.Id] = candidate;

            }

            vacancy.ApplicationCount = applications.Count;
            vacancy.ShortlistedCount = applications.Count(a => a.ShortlistedDate != null);

            // Everything a stakeholder opens ON these records, as opposed to the records themselves.
            AddScreening(refs, vacancy, position, applications, recruiter, cycle, publishDate, shortlistDate, rng);
            AddCommunications(refs, vacancy, applications, recruiter, position, shortlistDate);
            AddTrails(refs, vacancy, requisition, applications, recruiter, hiringManager, cycle,
                      requestDate, approvedDate, publishDate, shortlistDate);
            AddStageAssignments(refs, vacancy, recruiter, hiringManager, cycle, publishDate);

            if (cycle.Outcome == Outcome.Cancelled)
            {
                continue;   // withdrawn before shortlisting — no interview, no offer, no hire
            }

            // ── Interviews ───────────────────────────────────────────────────────────────────────
            var shortlist = applications.Where(a => a.ShortlistedDate != null).ToList();
            var rounds = cycle.Outcome == Outcome.Filled ? 2 : 1;

            for (var round = 1; round <= rounds && shortlist.Count > 0; round++)
            {
                var interviewDate = shortlistDate.AddDays(7 * round + rng.Next(0, 4));
                var interview = new JobInterview
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InterviewNumber = seq.Next(Counters.Int),
                    JobVacancyId = vacancy.Id,
                    Round = round,
                    Type = round == 1 ? FirstRoundTypeFor(cycle) : JobInterviewType.Final,
                    Mode = round == 1 && cycle.Channel == JobPostingChannel.LinkedIn
                        ? InterviewMode.Video
                        : InterviewMode.InPerson,
                    Status = JobInterviewStatus.Completed,
                    ScheduledDate = DateOnly.FromDateTime(interviewDate),
                    StartTime = new TimeSpan(9, 0, 0),
                    EndTime = new TimeSpan(round == 1 ? 13 : 12, 0, 0),
                    LocationOrLink = round == 1 && cycle.Channel == JobPostingChannel.LinkedIn
                        ? "Microsoft Teams — link issued with the invitation"
                        : "Boardroom, TDC Head Office, Tema",
                    Instructions = "Bring original certificates, a valid national ID and two passport photographs.",
                    CreatedAt = interviewDate.AddDays(-5),
                    CreatedBy = By,
                };
                _context.Set<JobInterview>().Add(interview);

                // The second round is the final two only — which is what makes the funnel narrow.
                var attending = round == 1 ? shortlist : shortlist.Take(2).ToList();
                var slot = new TimeSpan(9, 0, 0);
                var seats = new List<JobInterviewee>();

                foreach (var application in attending)
                {
                    var isWinner = application == applications[0];
                    var seatRow = new JobInterviewee
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        JobInterviewId = interview.Id,
                        JobApplicationId = application.Id,
                        SlotStartTime = slot,
                        SlotEndTime = slot.Add(TimeSpan.FromMinutes(45)),
                        InvitationSentDate = interviewDate.AddDays(-5),
                        ConfirmedAttendance = true,
                        ConfirmationDate = interviewDate.AddDays(-3),
                        CandidateAttended = true,
                        Outcome = isWinner
                            ? (round == rounds ? JobInterviewOutcome.HighlyRecommended : JobInterviewOutcome.ProceedToNextRound)
                            : (round == rounds ? JobInterviewOutcome.NotRecommended : JobInterviewOutcome.Acceptable),
                        CreatedAt = interviewDate.AddDays(-5),
                        CreatedBy = By,
                    };
                    _context.Set<JobInterviewee>().Add(seatRow);
                    seats.Add(seatRow);
                    slot = slot.Add(TimeSpan.FromMinutes(45));
                }

                // The panel, the questions they chose from the bank, and a scorecard per candidate
                // per panel member. Without these the interview screen is a date and a room.
                AddPanel(refs, interview, seats, hiringManager, hrHead, recruiter, interviewDate,
                         panelIndex++, rng);

                vacancy.InterviewCount++;
            }

            if (cycle.Outcome != Outcome.Filled)
            {
                continue;   // interviewed, nobody appointable
            }

            // ── Offers ───────────────────────────────────────────────────────────────────────────
            var offerDate = shortlistDate.AddDays(21 + rng.Next(0, 7));
            var startDate = requestDate.AddDays(cycle.DaysToFill);

            if (cycle.DeclinedFirstOffer && applications.Count > 1)
            {
                // Made to the first choice, declined on salary, re-made to the runner-up. Without a
                // declined offer the acceptance-rate gauge reads 100% on a single data point.
                var declined = BuildOffer(tenantId, seq, applications[0], vacancy, position, reportsToTitle,
                    unitName, hrHead, hqLocationId, cycle, offerDate, startDate, JobOfferStatus.Declined);
                declined.DeclinedDate = offerDate.AddDays(6);
                declined.DeclineReason = "Accepted an offer elsewhere at a higher entry notch.";
                declined.CandidateResponseNotes = "Asked whether the entry notch could be revisited; the grade minimum could not be exceeded.";
                _context.Set<JobOffer>().Add(declined);
                AddOfferExtras(refs, declined, cycle, position, offerDate);

                applications[0].Status = ApplicationStatus.OfferDeclined;
                applications[1].Status = ApplicationStatus.Hired;
                (applications[0], applications[1]) = (applications[1], applications[0]);
                offerDate = offerDate.AddDays(9);
                vacancy.OfferCount++;
            }

            var accepted = BuildOffer(tenantId, seq, applications[0], vacancy, position, reportsToTitle,
                unitName, hrHead, hqLocationId, cycle, offerDate, startDate, JobOfferStatus.Accepted);
            accepted.AcceptedDate = offerDate.AddDays(4);
            _context.Set<JobOffer>().Add(accepted);
            AddOfferExtras(refs, accepted, cycle, position, offerDate);
            vacancy.OfferCount++;

            // Only the appointee leaves the pool, and only once the accepted offer is known — on a
            // declined-offer cycle that is the runner-up, not the first choice.
            if (candidatesByApplication.TryGetValue(applications[0].Id, out var appointee))
            {
                appointee.TalentPoolStatus = TalentPoolCandidateStatus.Converted;
                appointee.IsInTalentPool = false;
                appointee.TalentPoolRemovedDate = offerDate.AddDays(4);
                appointee.TalentPoolRemovalReason = "Appointed to the post applied for.";
            }

            // ── Pre-employment checks, and what the referees actually said ───────────────────────
            // The referees are resolved FIRST: the checklist raises one reference item per referee,
            // because a check item carries exactly one response. See the note on AddChecks.
            var appointeeReferees = new List<JobCandidateReferee>();
            if (candidatesByApplication.TryGetValue(applications[0].Id, out var appointeeForRefs)
                && refereesByCandidate.TryGetValue(appointeeForRefs.Id, out var onFile))
            {
                appointeeReferees = onFile;
            }

            var referenceItems = AddChecks(tenantId, accepted, hrHead.Id, offerDate, hires, rng, appointeeReferees);

            for (var ri = 0; ri < referenceItems.Count && ri < appointeeReferees.Count; ri++)
            {
                var item = referenceItems[ri];
                var referee = appointeeReferees[ri];
                var order = ri;
                var when = offerDate;
                deferred.Add(() => AddReferenceResponse(refs, item, referee, when, order));
            }

            // ── The hire ─────────────────────────────────────────────────────────────────────────
            _context.Set<JobHireRecord>().Add(new JobHireRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                HireNumber = seq.Next(Counters.Hir),
                ApplicationId = applications[0].Id,
                OfferId = accepted.Id,
                Status = JobHireStatus.Active,
                ExpectedStartDate = DateOnly.FromDateTime(startDate),
                // ActualStartDate is what makes the hire "timed" — every speed metric in
                // RecruitmentAnalyticsService discards a hire without one.
                ActualStartDate = DateOnly.FromDateTime(startDate.AddDays(hires % 3 == 0 ? 0 : hires % 3)),
                ConfirmedById = hrHead.Id,
                ConfirmedDate = startDate.AddDays(1),
                Notes = "Assumption of duty recorded; personal file opened and the appointment letter filed.",
                CreatedAt = offerDate.AddDays(5),
                CreatedBy = By,
            });

            vacancy.HireCount = 1;
            hires++;
        }

        // ── The three requisitions that never became a vacancy ───────────────────────────────────
        foreach (var (code, day, type, status, justification, note) in StandaloneRequisitions)
        {
            if (!positions.TryGetValue(code, out var position)) continue;

            var requestDate = Day(day);
            var requester = (position.ReportsToPositionId is Guid rp2 && employeesByPosition.TryGetValue(rp2, out var mgr))
                ? mgr
                : hrHead;

            _context.Set<StaffRequisition>().Add(new StaffRequisition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RequisitionNumber = seq.Next(Counters.Req),
                RequisitionTitle = $"{position.Title} — {(unitNames.TryGetValue(position.OrganizationUnitId, out var sun) ? sun : "Tema Development Corporation")}",
                PositionId = position.Id,
                OrganizationUnitId = position.OrganizationUnitId,
                OrganizationLevelId = position.OrganizationLevelId,
                JobDescriptionId = jobDescriptions.TryGetValue(position.Id, out var jd2) ? (Guid?)jd2 : null,
                Type = type,
                Priority = StaffRequisitionPriority.Medium,
                Status = status,
                NumberOfPositions = status == StaffRequisitionStatus.PartiallyFulfilled ? 2 : 1,
                PositionsFilled = status == StaffRequisitionStatus.PartiallyFulfilled ? 1 : 0,
                RequestDate = requestDate,
                DesiredStartDate = requestDate.AddDays(60),
                BusinessJustification = justification,
                IsBudgeted = status != StaffRequisitionStatus.Rejected,
                AllowInternalCandidates = true,
                AllowExternalCandidates = true,
                RequestedById = requester.Id,
                Notes = note,
                CreatedAt = requestDate,
                CreatedBy = By,
            });
        }

        // ── Repair the live vacancies the scenario leaves unassigned ─────────────────────────────
        //
        // Vacancy ageing and recruiter load read OPEN vacancies only, so the history above cannot
        // fill them — those charts are fed by the live pipeline, and the API scenario has no
        // recruiter to name. Both charts label a null RecruiterId "Unassigned", which is what the
        // 2026-09-11 walkthrough showed.
        var unassigned = await _context.Set<JobVacancy>().IgnoreQueryFilters()
            .Where(v => v.TenantId == tenantId && !v.IsDeleted && v.RecruiterId == null)
            .ToListAsync(ct);

        var recruiterPool = new[] { "HR-HRM", "HR-HRO", "HHR" }
            .Select(AtPosition)
            .Where(e => e is not null)
            .Select(e => e!.Id)
            .ToList();

        if (recruiterPool.Count > 0)
        {
            for (var i = 0; i < unassigned.Count; i++)
            {
                unassigned[i].RecruiterId = recruiterPool[i % recruiterPool.Count];
                unassigned[i].UpdatedAt = now;
                unassigned[i].UpdatedBy = By;
            }
        }

        // ── One overdue advert, so the dashboard's SLA panel has something to alert on ───────────
        //
        // The panel lists ACTIVE vacancies (Published or Approved — JobVacancyRepository) whose
        // application deadline has passed. The API cannot produce one: a deadline is only ever set
        // forward, and by the time it passes in real life the vacancy has moved on. So exactly one
        // published vacancy is pushed six days past its deadline here. Everything else keeps a
        // future deadline, including the one the "deadline approaching" panel is meant to show.
        //
        // ⚠ It has to be THIS vacancy, not simply the newest. The public careers board filters on
        // `ApplicationDeadline == null || ApplicationDeadline >= now`, so a past deadline takes a
        // vacancy off /careers — and Book 1 §5.2 step 3 has the audience looking at VAC-000001 and
        // VAC-000002 there. Scenario 050 publishes the Supervising Town Planner post for exactly
        // this purpose and nothing in the runbook cites it.
        var overdueTargetPositionId = positions.TryGetValue(OverdueVacancyPositionCode, out var overduePosition)
            ? overduePosition.Id
            : (Guid?)null;

        var publishedLive = await _context.Set<JobVacancy>().IgnoreQueryFilters()
            .Where(v => v.TenantId == tenantId && !v.IsDeleted
                     && v.VacancyStatus == JobVacancyStatus.Published
                     && v.ApplicationDeadline != null)
            .ToListAsync(ct);

        // Fall back to the most recently created published vacancy only when that post is missing,
        // and never to the two the runbook names.
        var overdue = publishedLive.FirstOrDefault(v => v.PositionId == overdueTargetPositionId)
                   ?? publishedLive.Where(v => v.VacancyNumber is not ("VAC-000001" or "VAC-000002"))
                                   .OrderByDescending(v => v.CreatedAt)
                                   .FirstOrDefault();

        if (overdue is not null)
        {
            overdue.ApplicationDeadline = now.Date.AddDays(-6);
            overdue.ShortlistingDeadline = now.Date.AddDays(4);
            overdue.ShortlistingSlaBreached = true;
            overdue.UpdatedAt = now;
            overdue.UpdatedBy = By;
        }

        // Candidates the API scenario left bare — give them a dossier too.
        var backfilled = await BackfillBareDossiersAsync(
            refs, tenantId, AtPosition("HR-HRO") ?? hrHead, positionsById, names, rng, now, deferred, ct);

        await SpreadLifecycleStatesAsync(tenantId, hrHead, now, ct);

        // First save: every principal above lands in the database and becomes Unchanged — together
        // with the counters that numbered them, so the two cannot be separated by a later failure.
        // See the note on Counters.Stage.
        seq.Stage(_context);
        await _context.SaveChangesAsync(ct);

        // Second save: the rows that hang off those principals. See the note on `deferred`.
        foreach (var add in deferred) add();
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Recruitment history written: {Vacancies} vacancies ({Hires} filled), {Candidates} candidates "
            + "with full dossiers, {Requisitions} requisitions, {Backfilled} further candidates given a "
            + "dossier they were missing, and a recruiter assigned to {Repaired} live vacancies that had none. "
            + "⚠ Written directly rather than through the API — the doors stamp their own dates, "
            + "which would put the whole year on the build date.",
            vacancies.Count, hires, names.Issued, Cycles.Length + StandaloneRequisitions.Length,
            backfilled, unassigned.Count);
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // BUILDERS
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spreads applications across every <see cref="ApplicationSource"/> the model defines. The first
    /// applicant on each cycle is deliberately a referral or an internal candidate — the two routes
    /// that convert best — so the source-effectiveness chart shows a hire rate that varies by route
    /// rather than a flat bar.
    /// </summary>
    private static ApplicationSource SourceFor(int index, Cycle cycle) => (index, cycle.Channel) switch
    {
        (0, JobPostingChannel.InternalPortal) => ApplicationSource.InternalPortal,
        (0, _) => ApplicationSource.EmployeeReferral,
        (1, JobPostingChannel.LinkedIn) => ApplicationSource.LinkedIn,
        (1, JobPostingChannel.Agency) => ApplicationSource.RecruitmentAgency,
        (1, JobPostingChannel.Newspaper) => ApplicationSource.NewspaperAd,
        (1, _) => ApplicationSource.CompanyWebsite,
        (2, _) => ApplicationSource.JobBoard,
        (3, _) => ApplicationSource.SocialMedia,
        (4, _) => ApplicationSource.CareerFair,
        (5, _) => ApplicationSource.WalkIn,
        (6, _) => ApplicationSource.Other,
        _ => ApplicationSource.CompanyWebsite,
    };

    private static ApplicationStatus StatusFor(Cycle cycle, int index, bool isWinner, bool isRunnerUp, bool shortlisted)
    {
        if (cycle.Outcome == Outcome.Cancelled) return ApplicationStatus.Rejected;
        if (isWinner) return ApplicationStatus.Hired;
        if (isRunnerUp) return ApplicationStatus.Waitlisted;
        if (cycle.Outcome == Outcome.NoSuitableCandidates)
            return shortlisted ? ApplicationStatus.InterviewCompleted : ApplicationStatus.Rejected;
        return index == cycle.Applicants - 1 ? ApplicationStatus.Withdrawn : ApplicationStatus.Rejected;
    }

    private static JobInterviewType FirstRoundTypeFor(Cycle cycle) => cycle.PositionCode switch
    {
        "MS-SA" or "MS-CA" or "DV-QS" => JobInterviewType.Technical,
        "LG-LO" or "IA-AAS" => JobInterviewType.CompetencyBased,
        "CP-CRS" or "MK-SMO" => JobInterviewType.Presentation,
        "FN-AO" or "PR-PO" => JobInterviewType.Panel,
        _ => JobInterviewType.Screening,
    };

    private JobOffer BuildOffer(
        Guid tenantId, Counters seq, JobApplication application, JobVacancy vacancy,
        EmployeePosition position, string reportsToTitle, string departmentName, Employee hrHead,
        Guid? locationId, Cycle cycle, DateTime offerDate, DateTime startDate, JobOfferStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        OfferNumber = seq.Next(Counters.Ofr),
        JobApplicationId = application.Id,
        OfferStatus = status,
        PositionId = position.Id,
        PositionTitle = position.Title,
        ReportsToTitle = reportsToTitle,
        GradeTitle = position.Level > 0 ? $"Grade {position.Level}" : "Grade M3",
        DepartmentName = departmentName,
        WorkMode = WorkMode.OnSite,
        LocationId = locationId,
        EmploymentType = vacancy.EmploymentType,
        ContractDurationMonths = cycle.Type == StaffRequisitionType.Contract ? 24
                               : cycle.Type == StaffRequisitionType.Internship ? 12 : (int?)null,
        ProbationPeriodMonths = position.ProbationPeriodMonths ?? 6,
        NoticePeriodMonths = position.NoticePeriodMonths > 0 ? position.NoticePeriodMonths : 1,
        AnnualLeaveDays = 21,
        WeeklyHours = 40m,
        NdaRequired = true,
        BaseSalary = cycle.Salary,
        SalaryGradeMin = vacancy.SalaryRangeMin,
        SalaryGradeMax = vacancy.SalaryRangeMax,
        CurrencyCode = "GHS",
        ProposedStartDate = DateOnly.FromDateTime(startDate),
        AdditionalTerms = "Appointment is subject to satisfactory pre-employment checks and to confirmation at the end of the probationary period.",
        PreparedById = hrHead.Id,
        ApprovedById = hrHead.Id,
        ApprovedDate = offerDate.AddDays(-1),
        // OfferDate is the year filter for the offer-outcomes chart. Left null, the row falls back to
        // CreatedAt, which on a direct write is whatever the seeder happened to set.
        OfferDate = offerDate,
        ExpiryDate = offerDate.AddDays(14),
        Version = 1,
        IsLatestVersion = true,
        IsConditional = true,
        CreatedAt = offerDate,
        CreatedBy = By,
    };

    /// <summary>
    /// Recruitment spend on a cycle — the whole input to the cost-per-hire chart, which had three
    /// rows on one requisition to work from.
    /// </summary>
    private void AddCosts(Guid tenantId, StaffRequisition requisition, Cycle cycle, Guid recordedById, DeterministicRng rng)
    {
        var recorded = requisition.RequestDate.AddDays(rng.Next(20, 50));

        // ⚠ A cost line must name who was paid — the API refuses one that does not (round 3,
        // "Say who was paid: choose a supplier, or name the payee."). These are written directly, so
        // nothing would stop a nameless row; it is set anyway, because a cost the screen cannot
        // attribute is worse than no cost at all.
        var payeeFor = new Dictionary<StaffRequisitionCostCategory, string>
        {
            [StaffRequisitionCostCategory.JobAdvertising] = cycle.Channel switch
            {
                JobPostingChannel.Newspaper => "Graphic Communications Group Ltd",
                JobPostingChannel.LinkedIn => "LinkedIn Ireland Unlimited Company",
                JobPostingChannel.Indeed => "Indeed Ireland Operations Ltd",
                JobPostingChannel.Glassdoor => "Glassdoor Inc.",
                _ => "Jobberman Ghana Ltd",
            },
            [StaffRequisitionCostCategory.RecruitmentAgencyFee] = "Talent Bridge Recruitment Ghana Ltd",
            [StaffRequisitionCostCategory.BackgroundCheck] = "Sentinel Verification Services Ltd",
            [StaffRequisitionCostCategory.MedicalExamination] = "Tema Diagnostic & Occupational Health Centre",
            [StaffRequisitionCostCategory.Assessment] = "Dr Kwabena Asare (external assessor)",
            [StaffRequisitionCostCategory.TravelAndInterview] = "TDC Imprest Account",
        };

        var lines = new List<(StaffRequisitionCostCategory Category, string Purpose, decimal Amount)>
        {
            (StaffRequisitionCostCategory.JobAdvertising,
                $"Advertisement — {cycle.Channel}", cycle.Channel switch
                {
                    JobPostingChannel.Newspaper => 4200m,
                    JobPostingChannel.Agency => 0m,
                    JobPostingChannel.LinkedIn => 1850m,
                    JobPostingChannel.Indeed or JobPostingChannel.Glassdoor => 1200m,
                    JobPostingChannel.JobBoard => 950m,
                    _ => 0m,
                }),
        };

        if (cycle.Channel == JobPostingChannel.Agency)
            lines.Add((StaffRequisitionCostCategory.RecruitmentAgencyFee, "Agency placement fee — 12% of first-year salary", decimal.Round(cycle.Salary * 12 * 0.12m, 0)));

        if (cycle.Outcome == Outcome.Filled)
        {
            lines.Add((StaffRequisitionCostCategory.BackgroundCheck, "Police clearance and academic verification", 650m));
            lines.Add((StaffRequisitionCostCategory.MedicalExamination, "Pre-employment medical — TDC Clinic", 480m));
        }

        if (cycle.Applicants >= 6)
            lines.Add((StaffRequisitionCostCategory.Assessment, "Written assessment — printing, invigilation and marking", 780m));

        if (cycle.Applicants >= 5)
            lines.Add((StaffRequisitionCostCategory.TravelAndInterview, "Interview panel refreshments and candidate transport reimbursement", 540m));

        var i = 0;
        foreach (var (category, purpose, amount) in lines.Where(l => l.Amount > 0))
        {
            var costDate = recorded.AddDays(i * 3);
            _context.Set<StaffRequisitionCost>().Add(new StaffRequisitionCost
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RequisitionId = requisition.Id,
                Category = category,
                Purpose = purpose,
                Amount = amount,
                PayeeName = payeeFor.TryGetValue(category, out var payee) ? payee : "TDC Imprest Account",
                Currency = "GHS",
                ExchangeRate = 1m,
                AmountBaseCurrency = amount,
                CostDate = DateOnly.FromDateTime(costDate),
                Status = StaffRequisitionCostStatus.Approved,
                ApprovedById = recordedById,
                ApprovedOn = costDate.AddDays(2),
                RecordedById = recordedById,
                // RecordedDate carries the year filter for the cost chart.
                RecordedDate = costDate,
                CreatedAt = costDate,
                CreatedBy = By,
            });
            i++;
        }
    }

    /// <summary>
    /// A pre-employment check per hire. The statuses rotate so the checks register shows the five
    /// outcomes a coordinator actually deals with, not ten rows of "Pending".
    /// </summary>
    /// <summary>
    /// The appointee's pre-employment checks, and the reference-check items the referees' answers
    /// hang off. Returns those reference items, in the same order as <paramref name="referees"/>.
    ///
    /// <para>⚠ ONE REFERENCE ITEM PER REFEREE, BECAUSE A CHECK ITEM CARRIES EXACTLY ONE RESPONSE.
    /// <c>PreEmploymentCheckItem</c> → <c>ReferenceCheckResponse</c> is modelled <c>HasOne…WithOne</c>,
    /// which puts a UNIQUE index on <c>ReferenceCheckResponses.CheckItemId</c>
    /// (<c>IX_RefCheckResponse_CheckItemId</c>), and <c>PreEmploymentCheckService.AddReferenceResponseAsync</c>
    /// upserts on that basis. This seeder used to raise a single item called "Two professional
    /// references" and then add one response per referee against it; on 2026-09-14 the second
    /// referee's response collided with the first and took the whole second save down with it,
    /// including the counter flush. Each referee now gets an item of their own — which is also what
    /// the API can actually produce, so the demonstration shows a reachable state.</para>
    /// </summary>
    private List<PreEmploymentCheckItem> AddChecks(
        Guid tenantId,
        JobOffer offer,
        Guid coordinatorId,
        DateTime offerDate,
        int index,
        DeterministicRng rng,
        IReadOnlyList<JobCandidateReferee> referees)
    {
        var overall = (index % 8) switch
        {
            5 => PreEmploymentCheckStatus.CompletedWithCaution,
            6 => PreEmploymentCheckStatus.Waived,
            7 => PreEmploymentCheckStatus.InProgress,
            _ => PreEmploymentCheckStatus.Completed,
        };

        var check = new PreEmploymentCheck
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JobOfferId = offer.Id,
            OverallStatus = overall,
            CoordinatedById = coordinatorId,
            CompletedDate = overall == PreEmploymentCheckStatus.InProgress ? null : (DateTime?)offerDate.AddDays(12),
            Notes = overall switch
            {
                PreEmploymentCheckStatus.CompletedWithCaution =>
                    "One referee could not be reached after three attempts; a substitute referee was accepted on the Head of HR's authority.",
                PreEmploymentCheckStatus.Waived =>
                    "Internal appointment — the personal file already carries a current police clearance and the academic records were verified at first appointment.",
                PreEmploymentCheckStatus.InProgress =>
                    "Awaiting the Ghana Police Service clearance certificate; all other items are verified.",
                _ => "All items verified before assumption of duty.",
            },
            CreatedAt = offerDate.AddDays(1),
            CreatedBy = By,
        };
        _context.Set<PreEmploymentCheck>().Add(check);

        var items = new List<(PreEmploymentCheckType Type, string Name, string Provider, bool Mandatory)>
        {
            (PreEmploymentCheckType.MedicalExamination, "Pre-employment medical examination", "TDC Staff Clinic", true),
            (PreEmploymentCheckType.PoliceClearance, "Police criminal record clearance", "Ghana Police Service — CID", true),
            (PreEmploymentCheckType.AcademicVerification, "Verification of academic certificates", "Issuing institutions", true),
        };

        // One per referee — see the note on this method. A candidate with no referees on file still
        // gets the item, so the checklist is not silently short of a mandatory check.
        if (referees.Count == 0)
        {
            items.Add((PreEmploymentCheckType.ReferenceCheck, "Professional reference", "Named referee", true));
        }
        else
        {
            foreach (var referee in referees)
            {
                items.Add((
                    PreEmploymentCheckType.ReferenceCheck,
                    $"Professional reference — {referee.FullName}",
                    string.IsNullOrWhiteSpace(referee.Organization) ? "Named referee" : referee.Organization,
                    true));
            }
        }

        items.Add((PreEmploymentCheckType.BackgroundCheck, "Employment history verification", "Previous employer", false));

        var referenceItems = new List<PreEmploymentCheckItem>();
        var i = 0;
        foreach (var (type, name, provider, mandatory) in items)
        {
            var itemStatus = overall switch
            {
                PreEmploymentCheckStatus.Waived => CheckItemStatus.Waived,
                PreEmploymentCheckStatus.InProgress => type == PreEmploymentCheckType.PoliceClearance
                    ? CheckItemStatus.Requested
                    : CheckItemStatus.Verified,
                PreEmploymentCheckStatus.CompletedWithCaution => type == PreEmploymentCheckType.ReferenceCheck
                    ? CheckItemStatus.Received
                    : CheckItemStatus.Verified,
                _ => CheckItemStatus.Verified,
            };

            var requested = offerDate.AddDays(2 + i);
            var item = new PreEmploymentCheckItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PreEmploymentCheckId = check.Id,
                CheckType = type,
                Name = name,
                ServiceProviderName = provider,
                Status = itemStatus,
                RequestedDate = requested,
                ReceivedDate = itemStatus is CheckItemStatus.Received or CheckItemStatus.Verified
                    ? requested.AddDays(rng.Next(3, 11))
                    : null,
                Passed = itemStatus == CheckItemStatus.Verified ? true : (bool?)null,
                ExpectedDays = 7,
                IsMandatory = mandatory,
                IsBlockingOnFail = mandatory,
                ReviewedById = itemStatus == CheckItemStatus.Verified ? (Guid?)coordinatorId : null,
                ReviewedDate = itemStatus == CheckItemStatus.Verified ? (DateTime?)requested.AddDays(11) : null,
                CreatedAt = requested,
                CreatedBy = By,
            };
            _context.Set<PreEmploymentCheckItem>().Add(item);

            // Handed back so the referee's actual answer can be attached to it.
            if (type == PreEmploymentCheckType.ReferenceCheck) referenceItems.Add(item);
            i++;
        }

        return referenceItems;
    }


    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // FLESH ON THE SPINE
    //
    // The block above builds requisition → vacancy → candidate → application → interview → offer →
    // check → hire, which is what the analytics charts measure. On its own that is a spine: sixty-six
    // candidates with a name and a telephone number and nothing behind them, interviews with no panel
    // and no scorecard, and vacancies advertised without criteria. Every one of those is a screen a
    // stakeholder opens. What follows fills them.
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reference data the dossiers and scorecards hang off. The question bank, the talent segments
    /// and the pipeline stages are created through the API by <c>scenarios/050-recruitment.mjs</c>;
    /// this seeder runs after it, so they are loaded rather than invented. Each one is optional —
    /// a missing catalogue degrades that part of the dossier rather than failing the run.
    /// </summary>
    private sealed record Refs(
        Guid TenantId,
        List<RecruitmentPipelineStage> Stages,
        List<JobInterviewQuestionDetail> Bank,
        List<ExternalAssociate> Associates,
        List<RelationshipType> RelationshipTypes,
        List<Language> Languages,
        List<Qualification> Qualifications,
        List<CandidateTalentSegment> Segments,
        Employee HrHead);

    /// <summary>
    /// What a credible applicant for a given post looks like. Without this every dossier would read
    /// the same, and a panel member opening two candidates would see the same degree from the same
    /// university — which is worse than an empty screen, because it is visibly fabricated.
    /// </summary>
    private sealed record PostProfile(
        string Degree,
        string[] Institutions,
        string[] PriorTitles,
        string[] Employers,
        string[] Skills,
        string? Certification,
        string? CertifyingBody);

    private static readonly PostProfile DefaultProfile = new(
        "BA Social Sciences",
        new[] { "University of Ghana, Legon", "University of Cape Coast", "Ghana Institute of Management and Public Administration" },
        new[] { "Officer", "Senior Officer", "Administrative Officer" },
        new[] { "Ghana Revenue Authority", "Tema Metropolitan Assembly", "Ghana Water Company" },
        new[] { "Records management", "Report writing", "Microsoft Office" },
        null, null);

    private static readonly Dictionary<string, PostProfile> Profiles = new()
    {
        ["FN-AO"] = new("BSc Administration (Accounting)",
            new[] { "University of Ghana Business School", "Kwame Nkrumah University of Science and Technology", "University of Professional Studies, Accra" },
            new[] { "Accounts Officer", "Assistant Accountant", "Accounts Clerk" },
            new[] { "Ghana Revenue Authority", "Ecobank Ghana", "Tema Oil Refinery", "Ghana Ports and Harbours Authority" },
            new[] { "Bank and ledger reconciliation", "Expenditure control", "Sage and Tally", "IFRS reporting" },
            "ICAG Part 2", "Institute of Chartered Accountants, Ghana"),

        ["MS-SA"] = new("BSc Computer Science",
            new[] { "Kwame Nkrumah University of Science and Technology", "University of Ghana", "Ashesi University" },
            new[] { "Systems Administrator", "Network Engineer", "IT Support Officer" },
            new[] { "Vodafone Ghana", "Ghana Commercial Bank", "MTN Ghana", "Ghana Water Company" },
            new[] { "Windows Server administration", "Active Directory", "Virtualisation (VMware)", "Backup and disaster recovery", "Network administration" },
            "Microsoft Certified: Azure Administrator Associate", "Microsoft"),

        ["ES-EOL"] = new("BSc Land Economy",
            new[] { "Kwame Nkrumah University of Science and Technology", "University of Ghana" },
            new[] { "Estates Officer", "Lands Officer", "Valuation Officer" },
            new[] { "Lands Commission", "SSNIT", "State Housing Company", "Tema Metropolitan Assembly" },
            new[] { "Lease administration", "Property valuation", "Land title documentation", "Ground rent assessment" },
            "Probationary Member, GhIS", "Ghana Institution of Surveyors"),

        ["DV-QS"] = new("BSc Quantity Surveying and Construction Economics",
            new[] { "Kwame Nkrumah University of Science and Technology", "Takoradi Technical University" },
            new[] { "Quantity Surveyor", "Assistant Quantity Surveyor", "Site Measurement Officer" },
            new[] { "Sethi Construction Ltd", "Consar Ltd", "Justmoh Construction", "Department of Urban Roads" },
            new[] { "Measurement and bills of quantities", "Interim valuations", "Final accounts", "CESMM and SMM7", "CostX" },
            "Probationary Member, GhIS", "Ghana Institution of Surveyors"),

        ["HR-HRO"] = new("BA Human Resource Management",
            new[] { "University of Ghana Business School", "Ghana Institute of Management and Public Administration", "University of Cape Coast" },
            new[] { "Human Resource Officer", "HR Assistant", "Recruitment Officer" },
            new[] { "Ghana Grid Company", "Unilever Ghana", "Zoomlion Ghana", "Ghana Cocoa Board" },
            new[] { "Recruitment and selection", "Payroll input and records", "Industrial relations", "HR information systems" },
            "Member, IHRMP", "Institute of Human Resource Management Practitioners"),

        ["LG-LO"] = new("LLB",
            new[] { "University of Ghana School of Law", "KNUST Faculty of Law", "Ghana School of Law" },
            new[] { "Legal Officer", "State Attorney", "Associate Counsel" },
            new[] { "Attorney-General's Department", "Bentsi-Enchill, Letsa & Ankomah", "Lands Commission", "SSNIT" },
            new[] { "Land litigation", "Conveyancing and lease drafting", "Contract review", "Statutory interpretation" },
            "Called to the Ghana Bar", "General Legal Council"),

        ["CP-CRS"] = new("BA Communication Studies",
            new[] { "University of Ghana", "Ghana Institute of Journalism", "Central University" },
            new[] { "Client Relations Supervisor", "Customer Service Officer", "Front Desk Supervisor" },
            new[] { "Vodafone Ghana", "Ecobank Ghana", "Ghana Water Company", "Melcom Group" },
            new[] { "Complaint resolution", "Customer service supervision", "CRM systems", "Public speaking" },
            null, null),

        ["IA-AAS"] = new("BSc Accounting",
            new[] { "University of Professional Studies, Accra", "University of Cape Coast", "University of Ghana Business School" },
            new[] { "Audit Assistant", "Internal Auditor", "Compliance Assistant" },
            new[] { "Ghana Audit Service", "KPMG Ghana", "PwC Ghana", "Ghana Revenue Authority" },
            new[] { "Internal audit testing", "Risk assessment", "Audit working papers", "Public financial management" },
            "ACCA Part 2", "Association of Chartered Certified Accountants"),

        ["PR-PO"] = new("BSc Procurement and Supply Chain Management",
            new[] { "Kwame Nkrumah University of Science and Technology", "University of Professional Studies, Accra" },
            new[] { "Procurement Officer", "Supply Chain Officer", "Stores Officer" },
            new[] { "Ghana Health Service", "Ghana Ports and Harbours Authority", "Ghana Cocoa Board", "Guinness Ghana" },
            new[] { "Public Procurement Act compliance", "Tender evaluation", "Supplier management", "Contract administration" },
            "Member, CIPS", "Chartered Institute of Procurement and Supply"),

        ["MK-SMO"] = new("BSc Marketing",
            new[] { "University of Ghana Business School", "Central University", "University of Professional Studies, Accra" },
            new[] { "Sales and Marketing Officer", "Business Development Officer", "Sales Executive" },
            new[] { "Devtraco Plus", "Regimanuel Gray", "Ecobank Ghana", "Unilever Ghana" },
            new[] { "Property sales", "Client prospecting", "Market research", "Digital marketing" },
            null, null),

        ["DC-BI"] = new("HND Building Technology",
            new[] { "Accra Technical University", "Takoradi Technical University", "Ho Technical University" },
            new[] { "Building Inspector", "Works Superintendent", "Site Supervisor" },
            new[] { "Tema Metropolitan Assembly", "Ashaiman Municipal Assembly", "Department of Urban Roads" },
            new[] { "Building regulation enforcement", "Structural inspection", "Development permit assessment" },
            null, null),

        ["MS-CA"] = new("BSc Information Technology",
            new[] { "Kwame Nkrumah University of Science and Technology", "Ghana Communication Technology University" },
            new[] { "National Service Personnel", "IT Intern", "Help Desk Assistant" },
            new[] { "Ghana Water Company", "Ghana Revenue Authority", "Ashesi University" },
            new[] { "Hardware troubleshooting", "Help desk support", "Microsoft Office" },
            null, null),
    };

    private static PostProfile ProfileFor(string positionCode) =>
        Profiles.TryGetValue(positionCode, out var p) ? p : DefaultProfile;

    /// <summary>
    /// The candidate's own record: what they studied, where they worked, who will speak for them,
    /// what they can do, and the recruiter's note. Seven tables, and the reason the candidate detail
    /// page is worth opening.
    /// </summary>
    private List<JobCandidateReferee> AddDossier(
        Refs r, JobCandidate c, string positionCode, int seat, DeterministicRng rng, DateTime asOf)
    {
        var p = ProfileFor(positionCode);
        var years = c.TotalYearsExperience ?? 5;
        var graduated = asOf.AddYears(-(years + 1));

        // ── Qualifications ───────────────────────────────────────────────────────────────────────
        var catalogueQualification = r.Qualifications
            .FirstOrDefault(q => p.Degree.Contains(q.Name, StringComparison.OrdinalIgnoreCase)
                              || q.Name.Contains(p.Degree, StringComparison.OrdinalIgnoreCase));

        _context.Set<JobCandidateQualification>().Add(new JobCandidateQualification
        {
            Id = Guid.NewGuid(),
            TenantId = r.TenantId,
            JobCandidateId = c.Id,
            QualificationType = QualificationType.Education,
            QualificationId = catalogueQualification?.Id,
            QualificationFreeText = p.Degree,
            Institution = p.Institutions[seat % p.Institutions.Length],
            DateAwarded = DateOnly.FromDateTime(graduated),
            Grade = (seat % 4) switch
            {
                0 => "First Class",
                1 => "Second Class Upper",
                2 => "Second Class Lower",
                _ => "Credit",
            },
        });

        // The more experienced third of applicants carry a second, postgraduate qualification.
        if (years >= 8)
        {
            _context.Set<JobCandidateQualification>().Add(new JobCandidateQualification
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobCandidateId = c.Id,
                QualificationType = QualificationType.Education,
                QualificationFreeText = "MSc/MBA (part-time)",
                Institution = "University of Ghana Business School",
                DateAwarded = DateOnly.FromDateTime(asOf.AddYears(-2)),
                Grade = "Merit",
            });
        }

        if (p.Certification is not null && seat % 3 != 2)
        {
            _context.Set<JobCandidateQualification>().Add(new JobCandidateQualification
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobCandidateId = c.Id,
                QualificationType = QualificationType.Certification,
                QualificationFreeText = p.Certification,
                Institution = p.CertifyingBody ?? "Professional body",
                DateAwarded = DateOnly.FromDateTime(asOf.AddYears(-3)),
            });
        }

        // ── Work history ─────────────────────────────────────────────────────────────────────────
        var currentFrom = asOf.AddYears(-Math.Max(2, years / 2));
        _context.Set<JobCandidateWorkHistory>().Add(new JobCandidateWorkHistory
        {
            Id = Guid.NewGuid(),
            TenantId = r.TenantId,
            JobCandidateId = c.Id,
            InstitutionName = c.CurrentEmployer ?? p.Employers[seat % p.Employers.Length],
            PositionHeld = p.PriorTitles[seat % p.PriorTitles.Length],
            StartDate = DateOnly.FromDateTime(currentFrom),
            Responsibilities = $"{p.Skills[0]}; {p.Skills[Math.Min(1, p.Skills.Length - 1)]}.",
        });

        if (years >= 5)
        {
            _context.Set<JobCandidateWorkHistory>().Add(new JobCandidateWorkHistory
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobCandidateId = c.Id,
                InstitutionName = p.Employers[(seat + 1) % p.Employers.Length],
                PositionHeld = p.PriorTitles[(seat + 2) % p.PriorTitles.Length],
                StartDate = DateOnly.FromDateTime(graduated.AddMonths(6)),
                EndDate = DateOnly.FromDateTime(currentFrom.AddDays(-1)),
                Responsibilities = $"{p.Skills[Math.Min(2, p.Skills.Length - 1)]}.",
                ReasonForLeaving = (seat % 3) switch
                {
                    0 => "Appointment to a senior grade elsewhere.",
                    1 => "End of a fixed-term contract.",
                    _ => "Relocation to the Greater Accra region.",
                },
            });
        }

        // ── Referees ─────────────────────────────────────────────────────────────────────────────
        // ⚠ RelationshipTypeId is the catalogue link JobCandidateReferee consumes as an
        // IRelationshipTypeConsumer. The free-text Relationship stays populated so the screen reads
        // correctly even where the catalogue has no matching row.
        RelationshipType? Relation(params string[] names) =>
            r.RelationshipTypes.FirstOrDefault(t => names.Any(n => t.Name.Contains(n, StringComparison.OrdinalIgnoreCase)));

        var supervisor = Relation("Supervisor", "Manager", "Employer");
        var colleague = Relation("Colleague", "Friend", "Other");

        var referees = new List<JobCandidateReferee>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobCandidateId = c.Id,
                FullName = RefereeNames[seat % RefereeNames.Length],
                Position = "Head of Department",
                Organization = c.CurrentEmployer ?? p.Employers[seat % p.Employers.Length],
                Email = $"hod{seat}@{Slug(c.CurrentEmployer ?? p.Employers[seat % p.Employers.Length])}.com",
                Phone = $"030{2 + seat % 7}{(200000 + seat * 4177) % 900000:D6}",
                Relationship = "Direct supervisor",
                RelationshipTypeId = supervisor?.Id,
                YearsKnown = Math.Max(2, years / 2),
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobCandidateId = c.Id,
                FullName = RefereeNames[(seat + 5) % RefereeNames.Length],
                Position = "Senior Lecturer",
                Organization = p.Institutions[seat % p.Institutions.Length],
                Email = $"lecturer{seat}@university.edu.gh",
                Phone = $"032{2 + seat % 6}{(300000 + seat * 3391) % 900000:D6}",
                Relationship = "Academic referee",
                RelationshipTypeId = colleague?.Id,
                YearsKnown = years + 2,
            },
        };
        _context.Set<JobCandidateReferee>().AddRange(referees);

        // ── Skills ───────────────────────────────────────────────────────────────────────────────
        for (var s = 0; s < Math.Min(4, p.Skills.Length); s++)
        {
            var certified = s == 0 && p.Certification is not null && seat % 3 != 2;
            _context.Set<JobCandidateSkill>().Add(new JobCandidateSkill
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobCandidateId = c.Id,
                SkillName = p.Skills[(seat + s) % p.Skills.Length],
                Proficiency = (ProficiencyLevel)(1 + ((seat + s + years) % 5)),
                YearsOfExperience = Math.Max(1, years - s),
                IsCertified = certified,
                CertificationName = certified ? p.Certification : null,
                CertifyingBody = certified ? p.CertifyingBody : null,
            });
        }

        // ── Languages ────────────────────────────────────────────────────────────────────────────
        // ⚠ JobCandidateLanguages has exactly one API writer — the careers-portal profile PUT — so
        // outside that one candidate the table can only be filled here.
        var spoken = new[] { ("English", LanguageProficiency.Native), (GhanaianLanguages[seat % GhanaianLanguages.Length], LanguageProficiency.Fluent), ("French", LanguageProficiency.Conversational) };
        for (var l = 0; l < (years >= 6 ? 3 : 2); l++)
        {
            var (name, proficiency) = spoken[l];
            _context.Set<JobCandidateLanguage>().Add(new JobCandidateLanguage
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobCandidateId = c.Id,
                LanguageId = r.Languages.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Id,
                LanguageName = name,
                Proficiency = proficiency,
            });
        }

        // ── Interests and the recruiter's note ───────────────────────────────────────────────────
        _context.Set<JobCandidateInterest>().Add(new JobCandidateInterest
        {
            Id = Guid.NewGuid(),
            TenantId = r.TenantId,
            JobCandidateId = c.Id,
            Detail = Interests[seat % Interests.Length],
        });

        _context.Set<JobCandidateNote>().Add(new JobCandidateNote
        {
            Id = Guid.NewGuid(),
            TenantId = r.TenantId,
            JobCandidateId = c.Id,
            NoteText = RecruiterNotes[seat % RecruiterNotes.Length],
            IsPrivate = seat % 4 == 0,
            CreatedAt = asOf.AddDays(3),
            CreatedBy = By,
        });

        return referees;
    }

    private static readonly string[] RefereeNames =
    {
        "Mr Isaac Osei", "Mrs Comfort Ansah", "Dr Mariam Yeboah", "Ing. Kofi Amoah", "Mr Emmanuel Baidoo",
        "Mrs Georgina Aidoo", "Mr Daniel Nkrumah", "Dr Samuel Antwi", "Mrs Vida Amankwaa", "Mr Godwin Agbeko",
        "Mrs Patience Dogbe", "Mr Charles Nyame",
    };

    private static readonly string[] GhanaianLanguages = { "Twi", "Ga", "Ewe", "Dagbani", "Fante", "Hausa" };

    private static readonly string[] Interests =
    {
        "Coaches a colts football side in Tema Community 9 at weekends.",
        "Serves on the finance committee of a local church.",
        "Mentors final-year students through a professional-body scheme.",
        "Volunteers with a Tema beach clean-up group.",
        "Sings with the Tema Choral Society.",
        "Runs a weekend coding club for senior high school students.",
        "Member of a Tema running club; completed the Accra half marathon.",
        "Keeps a small poultry farm at Kasoa.",
    };

    private static readonly string[] RecruiterNotes =
    {
        "Strong paper application. Confirm the professional membership is current at reference stage.",
        "Experience is a close match for the advertised duties; presentation at interview will decide it.",
        "Below the advertised experience threshold but a good academic record — retain in the pool for a junior grade.",
        "Currently on a notice period of three months, which would delay assumption of duty.",
        "Referee could not be reached on the number given; a second number was obtained at screening.",
        "Applied for a similar post last year and was waitlisted. Worth a second look.",
    };

    private static string Slug(string s) =>
        new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>
    /// The advertised criteria, the reviewers' scores against them, the decision log and — where the
    /// post called for one — the written assessment. This is the screening screen.
    /// </summary>
    private void AddScreening(
        Refs r, JobVacancy vacancy, EmployeePosition position, List<JobApplication> applications,
        Employee recruiter, Cycle cycle, DateTime publishDate, DateTime shortlistDate, DeterministicRng rng)
    {
        var p = ProfileFor(cycle.PositionCode);
        var minYears = position.MinimumExperienceYears is int my && my > 0 ? my : 4;

        var criteria = new List<JobShortlistingCriteria>
        {
            new()
            {
                Id = Guid.NewGuid(), TenantId = r.TenantId, JobVacancyId = vacancy.Id,
                CriteriaName = p.Degree,
                Description = $"A first degree in a relevant discipline. The advertised requirement is {p.Degree}.",
                Type = JobShortlistingCriteriaType.Qualification,
                RequiredValue = p.Degree, IsMandatory = true, Weight = 35,
                MatchStrategy = ValueMatchStrategy.Contains, MatchMode = MandatoryMatchMode.AllRequired,
                RequiredQualificationId = r.Qualifications
                    .FirstOrDefault(q => p.Degree.Contains(q.Name, StringComparison.OrdinalIgnoreCase))?.Id,
                CreatedAt = publishDate, CreatedBy = By,
            },
            new()
            {
                Id = Guid.NewGuid(), TenantId = r.TenantId, JobVacancyId = vacancy.Id,
                CriteriaName = $"{minYears} years' post-qualification experience",
                Type = JobShortlistingCriteriaType.YearsOfExperience,
                MinValue = minYears, IsMandatory = true, Weight = 30,
                ComparisonOperator = ShortlistingComparisonOperator.GreaterThanOrEqual,
                CreatedAt = publishDate, CreatedBy = By,
            },
            new()
            {
                Id = Guid.NewGuid(), TenantId = r.TenantId, JobVacancyId = vacancy.Id,
                CriteriaName = p.Skills[0],
                Type = JobShortlistingCriteriaType.Skill,
                RequiredValue = p.Skills[0], IsMandatory = false, Weight = 20,
                MatchStrategy = ValueMatchStrategy.Contains,
                CreatedAt = publishDate, CreatedBy = By,
            },
        };

        if (p.Certification is not null)
        {
            criteria.Add(new JobShortlistingCriteria
            {
                Id = Guid.NewGuid(), TenantId = r.TenantId, JobVacancyId = vacancy.Id,
                CriteriaName = p.Certification,
                Description = $"Membership of, or progress towards, {p.CertifyingBody}.",
                Type = JobShortlistingCriteriaType.Certification,
                RequiredValue = p.Certification, IsMandatory = false, Weight = 15,
                MatchStrategy = ValueMatchStrategy.Contains,
                CreatedAt = publishDate, CreatedBy = By,
            });
        }

        _context.Set<JobShortlistingCriteria>().AddRange(criteria);

        // ⚠ A criterion the screen presents as a LIST carries one accepted value per row. Numeric
        // criteria carry none — which is why JobShortlistingCriteriaValues is manifest-optional.
        foreach (var criterion in criteria.Where(x => x.Type is JobShortlistingCriteriaType.Qualification
                                                              or JobShortlistingCriteriaType.Skill
                                                              or JobShortlistingCriteriaType.Certification))
        {
            var accepted = criterion.Type switch
            {
                JobShortlistingCriteriaType.Qualification => new[] { p.Degree, "Equivalent professional qualification" },
                JobShortlistingCriteriaType.Skill => p.Skills.Take(3).ToArray(),
                _ => new[] { p.Certification! },
            };

            var order = 1;
            foreach (var label in accepted)
            {
                _context.Set<JobShortlistingCriteriaValue>().Add(new JobShortlistingCriteriaValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = r.TenantId,
                    JobShortlistingCriteriaId = criterion.Id,
                    Kind = criterion.Type switch
                    {
                        JobShortlistingCriteriaType.Qualification => ShortlistingValueKind.Qualification,
                        JobShortlistingCriteriaType.Skill => ShortlistingValueKind.Skill,
                        _ => ShortlistingValueKind.Certification,
                    },
                    Label = label,
                    SortOrder = order++,
                    CreatedAt = publishDate,
                    CreatedBy = By,
                });
            }
        }

        // ── Reviews, decisions and the written assessment ────────────────────────────────────────
        // A vacancy withdrawn before shortlisting keeps its advertised criteria — they were
        // published — but nobody scored anybody, so there is nothing below to write.
        if (cycle.Outcome == Outcome.Cancelled) return;

        var reviewers = new[] { recruiter.Id, r.HrHead.Id }.Distinct().ToArray();

        foreach (var application in applications)
        {
            var shortlisted = application.ShortlistedDate is not null;

            foreach (var reviewerId in reviewers)
            {
                _context.Set<ShortlistReview>().Add(new ShortlistReview
                {
                    Id = Guid.NewGuid(),
                    TenantId = r.TenantId,
                    JobApplicationId = application.Id,
                    ReviewerId = reviewerId,
                    Score = shortlisted
                        ? decimal.Round(68m + rng.Next(0, 2800) / 100m, 2)
                        : decimal.Round(31m + rng.Next(0, 2600) / 100m, 2),
                    Notes = shortlisted
                        ? "Qualification and experience both evidenced on the application; recommend interview."
                        : "Does not evidence the mandatory qualification.",
                    ReviewedAt = shortlistDate.AddHours(-6),
                    IsFinalized = true,
                    FinalizedAt = shortlistDate,
                    CreatedAt = shortlistDate.AddHours(-6),
                    CreatedBy = By,
                });
            }

            _context.Set<ShortlistDecisionLog>().Add(new ShortlistDecisionLog
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobApplicationId = application.Id,
                ApplicationNumber = application.ApplicationNumber,
                DecisionType = application.Status switch
                {
                    ApplicationStatus.Waitlisted => ShortlistDecisionType.Waitlisted,
                    ApplicationStatus.Rejected => ShortlistDecisionType.Rejected,
                    _ => ShortlistDecisionType.Shortlisted,
                },
                DecisionById = recruiter.Id,
                DecisionAt = shortlistDate,
                AutoScoreAtDecision = application.AutoScore,
                IsAutoDecision = false,
                Notes = shortlisted
                    ? "Shortlisted by the panel after moderation of the two reviewers' scores."
                    : "Not carried forward.",
                CreatedAt = shortlistDate,
                CreatedBy = By,
            });

            if (vacancy.RequiresWrittenTest && shortlisted)
            {
                var score = decimal.Round(52m + rng.Next(0, 4200) / 100m, 1);
                _context.Set<JobApplicantTestResult>().Add(new JobApplicantTestResult
                {
                    Id = Guid.NewGuid(),
                    TenantId = r.TenantId,
                    JobApplicationId = application.Id,
                    TestType = JobApplicantTestType.Written,
                    TestName = $"{position.Title} — written assessment",
                    TestDate = shortlistDate.AddDays(5),
                    Venue = "Conference room, TDC Head Office, Tema",
                    Score = score,
                    MaxScore = 100m,
                    Passed = score >= 50m,
                    Remarks = score >= 70m
                        ? "Strong technical section; the written communication section was the weakest part."
                        : "Adequate. Technical section carried the mark.",
                    InvigilatedById = recruiter.Id,
                    MarkedById = r.HrHead.Id,
                    MarkedDate = shortlistDate.AddDays(8),
                    CreatedAt = shortlistDate.AddDays(5),
                    CreatedBy = By,
                });
            }
        }
    }

    /// <summary>
    /// What was actually sent to each applicant. The correspondence tab on an application is the
    /// screen an HR officer is asked to defend in a complaint, so it must not be empty.
    /// </summary>
    private void AddCommunications(
        Refs r, JobVacancy vacancy, List<JobApplication> applications, Employee recruiter,
        EmployeePosition position, DateTime shortlistDate)
    {
        foreach (var application in applications)
        {
            var shortlisted = application.ShortlistedDate is not null;

            _context.Set<JobApplicantCommunication>().Add(new JobApplicantCommunication
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobApplicationId = application.Id,
                Type = JobApplicantCommunicationType.Email,
                Direction = JobApplicantCommunicationDirection.System,
                Subject = $"Application received — {position.Title} ({vacancy.VacancyNumber})",
                Body = $"We acknowledge receipt of your application for the post of {position.Title}. "
                     + $"Your application reference is {application.ApplicationNumber}. Only shortlisted applicants will be contacted.",
                SentAt = application.ApplicationDate.AddMinutes(4),
                SentById = recruiter.Id,
                CreatedAt = application.ApplicationDate.AddMinutes(4),
                CreatedBy = By,
            });

            _context.Set<JobApplicantCommunication>().Add(new JobApplicantCommunication
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobApplicationId = application.Id,
                Type = shortlisted ? JobApplicantCommunicationType.Email : JobApplicantCommunicationType.Letter,
                Direction = JobApplicantCommunicationDirection.Outbound,
                Subject = shortlisted
                    ? $"Invitation to interview — {position.Title}"
                    : $"Outcome of your application — {position.Title}",
                Body = shortlisted
                    ? "You have been shortlisted for interview. Details of the date, time and venue follow separately. "
                    + "Please bring original certificates, a valid national ID and two passport photographs."
                    : "Thank you for your interest in Tema Development Corporation. On this occasion your application "
                    + "has not been successful. Your details will be retained for twelve months and considered against future vacancies.",
                SentAt = shortlistDate.AddDays(1),
                SentById = recruiter.Id,
                CreatedAt = shortlistDate.AddDays(1),
                CreatedBy = By,
            });
        }
    }

    /// <summary>
    /// The stage timeline on an application, and the status trail on the vacancy and its requisition.
    ///
    /// <para>⚠ These three are marked <c>excluded</c> in <c>demo-coverage-manifest.csv</c> as
    /// "system-generated log/history; not fabricated" — a rule written for a database whose
    /// transactions all went through the API, where inventing a log would be a lie about what
    /// happened. This seeder fabricates the whole year, so a year with no trail would be the less
    /// honest of the two: the screens that read these tables would show a record that appeared from
    /// nowhere. They are written to agree exactly with the rows above and nothing else.</para>
    /// </summary>
    private void AddTrails(
        Refs r, JobVacancy vacancy, StaffRequisition requisition, List<JobApplication> applications,
        Employee recruiter, Employee hiringManager, Cycle cycle,
        DateTime requestDate, DateTime approvedDate, DateTime publishDate, DateTime shortlistDate)
    {
        // ── The requisition's trail ──────────────────────────────────────────────────────────────
        var requisitionSteps = new (StaffRequisitionStatus From, StaffRequisitionStatus To, DateTime At, string Note)[]
        {
            (StaffRequisitionStatus.Draft, StaffRequisitionStatus.Submitted, requestDate.AddDays(1), "Submitted for approval."),
            (StaffRequisitionStatus.Submitted, StaffRequisitionStatus.UnderReview, requestDate.AddDays(2), "With the General Manager for establishment confirmation."),
            (StaffRequisitionStatus.UnderReview, StaffRequisitionStatus.Approved, approvedDate, "Approved — within the establishment for the year."),
        };

        foreach (var (from, to, at, note) in requisitionSteps)
        {
            _context.Set<StaffRequisitionHistory>().Add(new StaffRequisitionHistory
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                RequisitionId = requisition.Id,
                FromStatus = from,
                ToStatus = to,
                ChangedById = hiringManager.Id,
                ActionDate = at,
                Comments = note,
                CreatedAt = at,
                CreatedBy = By,
            });
        }

        // ── The vacancy's trail, ending where the vacancy ended ──────────────────────────────────
        var walk = new List<(JobVacancyStatus From, JobVacancyStatus To, DateTime At)>
        {
            (JobVacancyStatus.Draft, JobVacancyStatus.PendingApproval, approvedDate.AddDays(-1)),
            (JobVacancyStatus.PendingApproval, JobVacancyStatus.Approved, approvedDate),
            (JobVacancyStatus.Approved, JobVacancyStatus.Published, publishDate),
        };

        if (cycle.Outcome != Outcome.Cancelled)
        {
            walk.Add((JobVacancyStatus.Published, JobVacancyStatus.ClosedForApplications, vacancy.ApplicationDeadline ?? shortlistDate));
            walk.Add((JobVacancyStatus.ClosedForApplications, JobVacancyStatus.Shortlisting, shortlistDate));
        }

        if (cycle.Outcome == Outcome.Filled)
        {
            walk.Add((JobVacancyStatus.Shortlisting, JobVacancyStatus.Interviewing, shortlistDate.AddDays(7)));
            walk.Add((JobVacancyStatus.Interviewing, JobVacancyStatus.OfferStage, shortlistDate.AddDays(21)));
            walk.Add((JobVacancyStatus.OfferStage, JobVacancyStatus.Filled, vacancy.FilledDate ?? shortlistDate.AddDays(40)));
        }
        else if (cycle.Outcome == Outcome.Cancelled)
        {
            walk.Add((JobVacancyStatus.Published, JobVacancyStatus.Cancelled, vacancy.ClosedDate ?? publishDate.AddDays(20)));
        }

        foreach (var (from, to, at) in walk)
        {
            _context.Set<JobVacancyStatusHistory>().Add(new JobVacancyStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobVacancyId = vacancy.Id,
                FromStatus = from,
                ToStatus = to,
                ChangedDate = at,
                ChangedById = recruiter.Id,
                Comments = to == JobVacancyStatus.Cancelled
                    ? "Withdrawn pending the establishment review."
                    : null,
                CreatedAt = at,
                CreatedBy = By,
            });
        }

        // ── Each application's journey through the pipeline ──────────────────────────────────────
        if (r.Stages.Count == 0) return;

        var review = r.Stages.FirstOrDefault(s => s.StageType == RecruitmentPipelineStageType.ApplicationReview) ?? r.Stages[0];
        var screening = r.Stages.FirstOrDefault(s => s.StageType == RecruitmentPipelineStageType.Screening) ?? review;
        var interviewStage = r.Stages.FirstOrDefault(s => s.StageType == RecruitmentPipelineStageType.Interview);
        var offerStage = r.Stages.FirstOrDefault(s => s.StageType == RecruitmentPipelineStageType.Offer);
        var hiredStage = r.Stages.FirstOrDefault(s => s.IsFinalStage);

        foreach (var application in applications)
        {
            var shortlisted = application.ShortlistedDate is not null;
            var hired = application.Status == ApplicationStatus.Hired;

            var path = new List<(RecruitmentPipelineStage Stage, DateTime In, DateTime? Out, JobApplicationStageExitReason? Why)>
            {
                (review, application.ApplicationDate, shortlistDate.AddDays(-2), JobApplicationStageExitReason.Progressed),
                (screening, shortlistDate.AddDays(-2), shortlisted ? shortlistDate : shortlistDate,
                    shortlisted ? JobApplicationStageExitReason.Progressed : JobApplicationStageExitReason.Rejected),
            };

            if (shortlisted && interviewStage is not null)
            {
                path.Add((interviewStage, shortlistDate, hired ? shortlistDate.AddDays(21) : shortlistDate.AddDays(21),
                    hired ? JobApplicationStageExitReason.Progressed : JobApplicationStageExitReason.Rejected));
            }

            if (hired && offerStage is not null)
                path.Add((offerStage, shortlistDate.AddDays(21), shortlistDate.AddDays(28), JobApplicationStageExitReason.Progressed));
            if (hired && hiredStage is not null)
                path.Add((hiredStage, shortlistDate.AddDays(28), null, null));

            for (var i = 0; i < path.Count; i++)
            {
                var (stage, enteredAt, exitedAt, why) = path[i];
                _context.Set<JobApplicationStageHistory>().Add(new JobApplicationStageHistory
                {
                    Id = Guid.NewGuid(),
                    TenantId = r.TenantId,
                    JobApplicationId = application.Id,
                    PipelineStageId = stage.Id,
                    EnteredAt = enteredAt,
                    ExitedAt = exitedAt,
                    ExitReason = why,
                    MovedById = recruiter.Id,
                    IsCurrent = i == path.Count - 1,
                    CreatedAt = enteredAt,
                    CreatedBy = By,
                });
            }
        }
    }

    /// <summary>Who owned each stage of the vacancy, and whether they finished on time.</summary>
    private void AddStageAssignments(
        Refs r, JobVacancy vacancy, Employee recruiter, Employee hiringManager, Cycle cycle, DateTime publishDate)
    {
        if (r.Stages.Count == 0) return;

        var order = 0;
        foreach (var stage in r.Stages.OrderBy(s => s.Order))
        {
            var due = publishDate.AddDays(7 * ++order);
            var reached = cycle.Outcome == Outcome.Filled
                          || order <= (cycle.Outcome == Outcome.Cancelled ? 1 : 3);

            _context.Set<VacancyPipelineStageAssignment>().Add(new VacancyPipelineStageAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobVacancyId = vacancy.Id,
                PipelineStageId = stage.Id,
                AssignedToId = order % 2 == 0 ? hiringManager.Id : recruiter.Id,
                AssignedById = r.HrHead.Id,
                AssignedAt = publishDate,
                DueDate = due,
                // The states a recruitment officer actually sees on the board, rather than one value.
                Status = !reached ? VacancyStageAssignmentStatus.Skipped
                       : order == 3 && cycle.Applicants < 6 ? VacancyStageAssignmentStatus.Skipped
                       : order == 2 && cycle.DaysToFill > 85 ? VacancyStageAssignmentStatus.Overdue
                       : VacancyStageAssignmentStatus.Completed,
                CompletedAt = reached ? (DateTime?)due.AddDays(-1) : null,
                CompletedById = reached ? (Guid?)(order % 2 == 0 ? hiringManager.Id : recruiter.Id) : null,
                CompletionNotes = reached ? "Completed and handed to the next stage owner." : null,
                EscalationEnabled = true,
                EscalationDaysAfterDue = 3,
                EscalateToId = r.HrHead.Id,
                CreatedAt = publishDate,
                CreatedBy = By,
            });
        }
    }

    /// <summary>
    /// The panel, the questions they asked and the marks they gave. Six tables, and without them the
    /// interview screen opens on a date and a room.
    /// </summary>
    private void AddPanel(
        Refs r, JobInterview interview, List<JobInterviewee> seats,
        Employee chair, Employee member, Employee assessor, DateTime interviewDate,
        int interviewIndex, DeterministicRng rng)
    {
        // Distinct people — the hiring manager, recruiter and Head of HR collapse to the same
        // employee on the HR posts, and a panel cannot seat the same person twice.
        var panelEmployees = new[] { chair, member, assessor }
            .GroupBy(e => e.Id).Select(g => g.First()).ToList();

        var panelists = new List<JobInterviewPanelist>();
        for (var i = 0; i < panelEmployees.Count; i++)
        {
            var panelist = new JobInterviewPanelist
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobInterviewId = interview.Id,
                EmployeeId = panelEmployees[i].Id,
                Role = i switch
                {
                    0 => JobInterviewPanelistRole.Chair,
                    1 => JobInterviewPanelistRole.Member,
                    _ => JobInterviewPanelistRole.TechnicalAssessor,
                },
                IsRequired = i < 2,
                Attended = true,
                InvitationSentDate = interviewDate.AddDays(-7),
                IsConfirmed = true,
                ConfirmationDate = interviewDate.AddDays(-5),
                CreatedAt = interviewDate.AddDays(-7),
                CreatedBy = By,
            };
            panelists.Add(panelist);
            _context.Set<JobInterviewPanelist>().Add(panelist);
        }

        // Every third panel co-opts an outside assessor — a professional body's nominee sitting on a
        // public-sector appointment panel is the norm, and it is the only writer of this table.
        if (r.Associates.Count > 0 && interviewIndex % 3 == 0)
        {
            _context.Set<JobInterviewExternalPanelist>().Add(new JobInterviewExternalPanelist
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobInterviewId = interview.Id,
                AssociateId = r.Associates[interviewIndex % r.Associates.Count].Id,
                Role = JobInterviewPanelistRole.TechnicalAssessor,
                IsRequired = false,
                Attended = true,
                InvitationSentDate = interviewDate.AddDays(-7),
                IsConfirmed = true,
                ConfirmationDate = interviewDate.AddDays(-4),
                CreatedAt = interviewDate.AddDays(-7),
                CreatedBy = By,
            });
        }

        // ── The question plan, drawn from the bank the API scenario created ──────────────────────
        if (r.Bank.Count == 0 || seats.Count == 0) return;

        var chosen = new List<JobInterviewQuestionDetail>();
        var display = 0;

        foreach (var group in r.Bank.GroupBy(q => q.QuestionTypeId).Take(3))
        {
            var pool = group.ToList();
            var plan = new JobInterviewQuestion
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobInterviewId = interview.Id,
                QuestionTypeId = group.Key,
                RequiredQuestionCount = Math.Min(2, pool.Count),
                AllowedPoolSize = pool.Count,
                DisplayOrder = ++display,
                CreatedAt = interviewDate.AddDays(-3),
                CreatedBy = By,
            };
            _context.Set<JobInterviewQuestion>().Add(plan);

            var order = 0;
            foreach (var q in pool.Skip(interviewIndex % Math.Max(1, pool.Count)).Take(2).DefaultIfEmpty(pool[0]))
            {
                if (chosen.Any(c => c.Id == q.Id)) continue;
                chosen.Add(q);
                _context.Set<JobInterviewSelectedQuestion>().Add(new JobInterviewSelectedQuestion
                {
                    Id = Guid.NewGuid(),
                    TenantId = r.TenantId,
                    JobInterviewQuestionId = plan.Id,
                    QuestionDetailId = q.Id,
                    DisplayOrder = ++order,
                    CreatedAt = interviewDate.AddDays(-3),
                    CreatedBy = By,
                });
            }
        }

        if (chosen.Count == 0) return;

        // ── The scorecards ───────────────────────────────────────────────────────────────────────
        // One per candidate per panel member — which is what the scoring screen saves, and what the
        // weighted average behind the recommendation is computed from.
        for (var s = 0; s < seats.Count; s++)
        {
            var seat = seats[s];
            // The candidate the panel preferred is the one who was appointed, so the marks have to
            // agree with the outcome already written above.
            var favoured = seat.Outcome is JobInterviewOutcome.HighlyRecommended or JobInterviewOutcome.ProceedToNextRound;

            foreach (var panelist in panelists)
            {
                decimal raw = 0, weighted = 0;
                var summary = new JobInterviewScoreSummary
                {
                    Id = Guid.NewGuid(),
                    TenantId = r.TenantId,
                    JobIntervieweeId = seat.Id,
                    InternalPanelistId = panelist.Id,
                    Recommendation = favoured
                        ? (panelist.Role == JobInterviewPanelistRole.Chair ? JobInterviewRecommendation.StrongHire : JobInterviewRecommendation.Hire)
                        : (s == seats.Count - 1 ? JobInterviewRecommendation.NoHire : JobInterviewRecommendation.Neutral),
                    Comments = favoured
                        ? "Answered the technical questions with worked examples from their own practice. Clear on the statutory framework and comfortable with the reporting lines."
                        : "Answered in general terms; could not take the technical question beyond the textbook position.",
                    EvaluationDate = interviewDate.AddHours(14),
                    IsFinalized = true,
                    FinalizedDate = interviewDate.AddHours(15),
                    CreatedAt = interviewDate.AddHours(14),
                    CreatedBy = By,
                };
                _context.Set<JobInterviewScoreSummary>().Add(summary);

                foreach (var q in chosen)
                {
                    var span = Math.Max(1, q.MaxScore - q.MinScore);
                    var baseline = favoured ? 0.72 : 0.42;
                    var score = (decimal)Math.Round(q.MinScore + span * (baseline + rng.Next(0, 22) / 100.0), 1);
                    if (score > q.MaxScore) score = q.MaxScore;

                    var weightedScore = decimal.Round(score * q.Weight / 100m, 2);
                    raw += score;
                    weighted += weightedScore;

                    _context.Set<JobInterviewScoreEntry>().Add(new JobInterviewScoreEntry
                    {
                        Id = Guid.NewGuid(),
                        TenantId = r.TenantId,
                        ScoreSummaryId = summary.Id,
                        QuestionDetailId = q.Id,
                        RawScore = score,
                        WeightedScore = weightedScore,
                        CreatedAt = interviewDate.AddHours(14),
                        CreatedBy = By,
                    });
                }

                summary.TotalRawScore = raw;
                summary.TotalWeightedScore = weighted;
            }
        }
    }

    /// <summary>The package attached to an offer, and the file note explaining how it was settled.</summary>
    private void AddOfferExtras(Refs r, JobOffer offer, Cycle cycle, EmployeePosition position, DateTime offerDate)
    {
        var benefits = new (string Name, string Description, decimal? Value, bool Monetary)[]
        {
            ("Medical cover", "Staff and registered dependants on the corporation's scheme with the TDC Staff Clinic and the panel hospitals.", null, false),
            ("Transport allowance", "Monthly transport allowance paid with salary.", decimal.Round(cycle.Salary * 0.10m, 0), true),
            ("Rent allowance", "Monthly rent allowance at the rate for the grade.", decimal.Round(cycle.Salary * 0.20m, 0), true),
            ("Provident fund", "Corporation contributes 5% of basic salary to the staff provident fund, in addition to statutory SSNIT.", decimal.Round(cycle.Salary * 0.05m, 0), true),
            ("Annual leave", "Twenty-one working days a year, rising with length of service.", null, false),
        };

        var order = 0;
        foreach (var (name, description, value, monetary) in benefits)
        {
            _context.Set<JobOfferBenefit>().Add(new JobOfferBenefit
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobOfferId = offer.Id,
                BenefitName = name,
                Description = description,
                MonetaryValue = value,
                CurrencyCode = monetary ? "GHS" : null,
                IsMonetary = monetary,
                DisplayOrder = ++order,
                CreatedAt = offerDate,
                CreatedBy = By,
            });
        }

        _context.Set<JobOfferNote>().Add(new JobOfferNote
        {
            Id = Guid.NewGuid(),
            TenantId = r.TenantId,
            JobOfferId = offer.Id,
            Body = offer.OfferStatus == JobOfferStatus.Declined
                ? "Candidate asked for entry above the grade minimum. The Head of HR advised that the notch cannot be exceeded on first appointment and the offer was allowed to lapse."
                : $"Entry notch set at the minimum for the grade of {position.Title} plus one, in recognition of experience beyond the advertised threshold. Cleared with the General Manager, Finance & Administration.",
            AuthorName = "Head of HR & Administration",
            CreatedAt = offerDate.AddDays(1),
            CreatedBy = By,
        });
    }

    /// <summary>
    /// What the referees actually said. Hangs off the reference item of the appointee's
    /// pre-employment check, so the check screen has something to open.
    /// </summary>
    /// <summary>
    /// What one referee said, against the check item raised for them. One response per item — see
    /// the note on <see cref="AddChecks"/> for why that is not negotiable.
    /// </summary>
    private void AddReferenceResponse(
        Refs r, PreEmploymentCheckItem referenceItem, JobCandidateReferee referee, DateTime offerDate, int order)
    {
        var answered = offerDate.AddDays(6 + order * 2);

        _context.Set<ReferenceCheckResponse>().Add(new ReferenceCheckResponse
        {
            Id = Guid.NewGuid(),
            TenantId = r.TenantId,
            CheckItemId = referenceItem.Id,
            RefereeId = referee.Id,
            RefereeName = referee.FullName,
            RefereeOrganisation = referee.Organization,
            RefereePosition = referee.Position,
            RefereeEmail = referee.Email,
            RefereePhone = referee.Phone,
            ResponseDate = answered,
            ResponseMethod = order == 0 ? ReferenceResponseMethod.Email : ReferenceResponseMethod.Phone,
            OverallRating = order == 0 ? ReferenceRating.Excellent : ReferenceRating.Good,
            Comments = order == 0
                ? "Confirms the dates and the post held. Describes the applicant as reliable and technically sound; left on good terms and would be re-employed."
                : "Spoke well of the applicant's conduct and attendance. Noted that they worked with limited supervision.",
            WouldRehire = true,
            ConfirmedDatesOfEmployment = true,
            ConfirmedPositionHeld = true,
            ConfirmedReasonForLeaving = order == 0,
            CreatedAt = answered,
            CreatedBy = By,
        });
    }

    /// <summary>
    /// The recruitment CRM: which segment a candidate belongs to and every time somebody spoke to
    /// them. Without it the talent-pool screen is a list of names with no history.
    /// </summary>
    private void AddSegmentAndEngagement(
        Refs r, JobCandidate candidate, Employee recruiter, DateTime asOf, int seat)
    {
        if (r.Segments.Count > 0 && seat % 2 == 0)
        {
            _context.Set<CandidateSegmentMembership>().Add(new CandidateSegmentMembership
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobCandidateId = candidate.Id,
                SegmentId = r.Segments[seat % r.Segments.Count].Id,
                AddedByEmployeeId = recruiter.Id,
                AddedDate = asOf.AddDays(30),
                Notes = "Added at the close of the recruitment round.",
                CreatedAt = asOf.AddDays(30),
                CreatedBy = By,
            });
        }

        var events = new (CandidateEngagementEventType Type, string Subject, string Note, int Day)[]
        {
            (CandidateEngagementEventType.ProfileReview, "Application screened", "Application read against the advertised criteria.", 3),
            (CandidateEngagementEventType.StatusUpdate, "Outcome communicated", "Told the outcome of the round by the recruitment desk.", 32),
        };

        foreach (var (type, subject, note, day) in events.Take(seat % 3 == 0 ? 2 : 1))
        {
            _context.Set<CandidateEngagementEvent>().Add(new CandidateEngagementEvent
            {
                Id = Guid.NewGuid(),
                TenantId = r.TenantId,
                JobCandidateId = candidate.Id,
                EventType = type,
                EventDate = asOf.AddDays(day),
                Subject = subject,
                Notes = note,
                RecordedByEmployeeId = recruiter.Id,
                IsInternal = true,
                CreatedAt = asOf.AddDays(day),
                CreatedBy = By,
            });
        }
    }



    /// <summary>
    /// Gives a dossier to any candidate who has none.
    ///
    /// <para>The twelve cycles above build their own applicants complete. The API scenario does not:
    /// <c>050-recruitment.mjs</c> writes full dossiers for the six applicants on VAC-000001 and for
    /// the one careers-site registration, but the applicants it creates for the live pipeline
    /// (sections 17 onwards) carry a name, a telephone number and an employer and nothing else.
    /// Opening one of those on stage shows a candidate page with every tab empty, which is exactly
    /// the complaint this whole piece of work started from.</para>
    ///
    /// <para>The probe is "has no qualification row", and it runs against the DATABASE — the
    /// candidates created earlier in this same run are not saved yet, so they are correctly not
    /// considered. The post each candidate applied for is resolved through their application so the
    /// dossier matches the job rather than being generic.</para>
    /// </summary>
    private async Task<int> BackfillBareDossiersAsync(
        Refs refs, Guid tenantId, Employee recruiter, Dictionary<Guid, EmployeePosition> positionsById,
        NamePool names, DeterministicRng rng, DateTime now, List<Action> deferred, CancellationToken ct)
    {
        var haveQualifications = await _context.Set<JobCandidateQualification>().IgnoreQueryFilters()
            .Where(q => q.TenantId == tenantId && !q.IsDeleted)
            .Select(q => q.JobCandidateId)
            .Distinct()
            .ToListAsync(ct);

        var bare = await _context.Set<JobCandidate>().IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && !haveQualifications.Contains(c.Id))
            .ToListAsync(ct);

        if (bare.Count == 0) return 0;

        // Which post each of them applied for, so the dossier is about the right job.
        var appliedFor = await _context.Set<JobApplication>().IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Select(a => new { a.JobCandidateId, a.JobVacancy.PositionId })
            .ToListAsync(ct);

        var positionByCandidate = appliedFor
            .GroupBy(x => x.JobCandidateId)
            .ToDictionary(g => g.Key, g => g.First().PositionId);

        foreach (var candidate in bare)
        {
            var code = positionByCandidate.TryGetValue(candidate.Id, out var positionId)
                    && positionsById.TryGetValue(positionId, out var position)
                ? position.Code
                : string.Empty;

            var seat = names.Issued + bare.IndexOf(candidate);
            var asOf = candidate.CreatedAt == default ? now : candidate.CreatedAt;
            AddDossier(refs, candidate, code, seat, rng, asOf);
            deferred.Add(() => AddSegmentAndEngagement(refs, candidate, recruiter, asOf, seat));
        }

        _logger.LogInformation(
            "Filled in dossiers for {Count} candidates who had none — the live-pipeline applicants the "
            + "API scenario creates without qualifications, referees, skills or languages.",
            bare.Count);

        return bare.Count;
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // ONBOARDING AND PROBATION
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spreads the ONBOARDING register across the states it can actually hold.
    ///
    /// <para><b>⚠ Why this UPDATES rows rather than creating them for the ten historical hires.</b>
    /// <c>OnboardingPlan.EmployeeId</c> is non-nullable and <c>ProbationPeriod</c> needs both an
    /// <c>EmployeeId</c> and a <c>ContractDetailId</c>. The appointees invented above are candidates,
    /// not staff — they have no employee record and no contract. Giving them one would add ten people
    /// to a register already staffed to the establishment by <see cref="TdcDemoWorkforceSeeder"/>, so
    /// the organogram, the headcount and the salary-grade counts would all disagree with the org
    /// chart. Linking them to the staff who really hold those posts is no better: the workforce seeder
    /// gives those people years of service reaching back years, and a probation period starting in
    /// March 2026 for someone employed in 2019 is a record that contradicts itself.
    ///
    /// <para>So the honest thing is left alone: onboarding and probation stay attached to the real
    /// employees the <c>145-onboarding</c> and <c>065-probation</c> scenarios create through the API.
    /// What was wrong with those registers was never the row count — it was that every plan sat at
    /// NotStarted and every probation at Active, so neither screen showed the states an HR officer
    /// actually works in. That is what this fixes.</para></para>
    ///
    /// <para>⚠ Two probation periods are deliberately untouched: <b>TDC/00063</b> (Kojo Ansah), whose
    /// first review Book 1 §7 walks live, and <b>TDC/00018</b> (Patrick Appiah), because
    /// <see cref="TdcDemoProbationExtensionSeeder"/> runs after this step and looks for an
    /// <c>Active</c> probation — move him and that seeder silently writes nothing.</para>
    /// </summary>
    private async Task SpreadLifecycleStatesAsync(Guid tenantId, Employee hrHead, DateTime now, CancellationToken ct)
    {
        // ⚠ PROBATION IS DELIBERATELY NOT TOUCHED. An earlier version of this method moved three of
        // the six periods to Completed / ConfirmationApproved / PendingConfirmation, on the reasoning
        // that a register sitting entirely on Active shows none of the states an HR officer works in.
        // That was wrong here, for three reasons found by reading the books rather than the schema:
        //
        //   1. Confirmation is not a status. ProbationService writes a confirmation date onto the
        //      EMPLOYEE and releases the benefits withheld until then. Setting the status directly
        //      leaves a period that says "confirmed" beside an employee record that says otherwise.
        //   2. Book 1 §7's aside and Book 2 §6 both rest on all six starters showing as "withheld"
        //      on the benefits screen — that refusal is a live demo moment.
        //   3. Book 1 §7 step 2 has head.dev conduct a review on stage, and the section's opening
        //      line is "six probation periods, each with a SCHEDULED first review".
        //
        // Six people currently serving probation is not a collapsed status dimension; it is the
        // story the books tell. The probation register's other states are demonstrated by conducting
        // a review live, not by pre-seeding an outcome.

        // ── Onboarding ───────────────────────────────────────────────────────────────────────────
        var plans = await _context.Set<OnboardingPlan>().IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .OrderBy(p => p.StartDate)
            .ToListAsync(ct);

        var planStates = new[] { OnboardingStatus.Completed, OnboardingStatus.Completed, OnboardingStatus.Overdue, OnboardingStatus.InProgress };

        for (var i = 0; i < plans.Count; i++)
        {
            var plan = plans[i];
            var state = i < planStates.Length ? planStates[i] : OnboardingStatus.NotStarted;

            plan.Status = state;
            plan.OnboardingCoordinatorId ??= hrHead.Id;
            plan.ActualCompletionDate = state == OnboardingStatus.Completed
                ? (DateOnly?)(plan.TargetCompletionDate ?? DateOnly.FromDateTime(plan.StartDate.ToDateTime(TimeOnly.MinValue).AddDays(30)))
                : null;
            plan.Notes = state switch
            {
                OnboardingStatus.Completed => "All mandatory tasks completed and verified; the plan was signed off by the Head of HR & Administration.",
                OnboardingStatus.Overdue => "Two tasks are past their due date — the IT account sits with MIS and the bank mandate with Finance.",
                OnboardingStatus.InProgress => "Documentation and system access complete; induction and the policy acknowledgements are outstanding.",
                _ => plan.Notes,
            };
            plan.UpdatedAt = now;
            plan.UpdatedBy = By;

            var tasks = await _context.Set<OnboardingTask>().IgnoreQueryFilters()
                .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.OnboardingPlanId == plan.Id)
                .OrderBy(t => t.DisplayOrder)
                .ToListAsync(ct);

            for (var t = 0; t < tasks.Count; t++)
            {
                var task = tasks[t];

                // Rotated so the unit queues show work in every condition rather than one column of
                // Pending — Waived, Blocked and PendingVerification are states the queue screens have
                // columns for and had no rows in.
                task.Status = state switch
                {
                    OnboardingStatus.Completed => t % 7 == 6 ? OnboardingTaskStatus.Waived : OnboardingTaskStatus.Completed,
                    OnboardingStatus.Overdue => (t % 4) switch
                    {
                        0 => OnboardingTaskStatus.Completed,
                        1 => OnboardingTaskStatus.Overdue,
                        2 => OnboardingTaskStatus.Blocked,
                        _ => OnboardingTaskStatus.Pending,
                    },
                    OnboardingStatus.InProgress => (t % 4) switch
                    {
                        0 => OnboardingTaskStatus.Completed,
                        1 => OnboardingTaskStatus.PendingVerification,
                        2 => OnboardingTaskStatus.InProgress,
                        _ => OnboardingTaskStatus.Pending,
                    },
                    _ => task.Status,
                };

                if (task.Status == OnboardingTaskStatus.Completed)
                {
                    task.CompletedDate = task.DueDate;
                    task.CompletedById ??= hrHead.Id;
                    task.CompletionNotes ??= "Done and evidenced on the personal file.";
                }
                else if (task.Status == OnboardingTaskStatus.Blocked)
                {
                    task.CompletionNotes = "Blocked: the bank mandate cannot be raised until the staff number is issued.";
                }
                else if (task.Status == OnboardingTaskStatus.Waived)
                {
                    task.CompletionNotes = "Waived — the appointee transferred internally and already holds this.";
                }

                task.UpdatedAt = now;
                task.UpdatedBy = By;
            }
        }

        _logger.LogInformation(
            "Onboarding spread across its real states over {Plans} plans. Probation deliberately left "
            + "untouched — confirmation writes to the employee record and two books rest on all six "
            + "starters still serving.",
            plans.Count);
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // NUMBER SEQUENCES
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Mints reference numbers in the same shape <c>NumberSequenceService</c> does, and writes the
    /// counters back so the first record created live on stage does not collide with one of these.
    /// <c>NextValue</c> holds the <b>last</b> value handed out, so minting is increment-then-format.
    /// </summary>
    private sealed class Counters
    {
        public const string Req = "REQ";
        public const string Vac = "VAC";
        public const string Cand = "CAND";
        public const string App = "APP";
        public const string Ofr = "OFR";
        public const string Hir = "HIR";
        public const string Int = "INT";

        // Key → (year the counter is scoped to, zero-padded width). REQ is the one key in recruitment
        // whose counter resets per year; the rest run unbroken from 1.
        private static readonly Dictionary<string, (int Year, int Width)> Shapes = new()
        {
            [Req] = (Year, 5),
            [Vac] = (0, 6),
            [Cand] = (0, 6),
            [App] = (0, 7),
            [Ofr] = (0, 6),
            [Hir] = (0, 6),
            [Int] = (0, 6),
        };

        private readonly Dictionary<string, NumberSequence> _rows = new();
        private readonly Dictionary<string, long> _values = new();
        private Guid _tenantId;

        public static async Task<Counters> LoadAsync(ApplicationDbContext context, Guid tenantId, CancellationToken ct)
        {
            var counters = new Counters { _tenantId = tenantId };
            var keys = Shapes.Keys.ToList();

            var rows = await context.Set<NumberSequence>().IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId && !s.IsDeleted && keys.Contains(s.SequenceKey))
                .ToListAsync(ct);

            // ⚠ THE SEQUENCE TABLE IS NOT THE WHOLE TRUTH, AND TRUSTING IT ALONE MINTS DUPLICATES.
            // Measured on 2026-09-14: NumberSequences carried rows for APP, CAND, REQ and VAC but
            // NOT for OFR, HIR or INT — yet OFR-000001…000009 and INT-000006 already existed,
            // because those three are numbered by a different mechanism. Starting from a missing
            // row means starting at zero, and the first offer minted here collided:
            //     Cannot insert duplicate key row in object 'dbo.JobOffers' with unique index
            //     'IX_JobOffer_Tenant_Number'. The duplicate key value is (…, OFR-000003).
            // So every counter starts at whichever is HIGHER: the sequence row, or the largest
            // number already issued in the table it numbers.
            var highest = await HighestIssuedAsync(context, tenantId, ct);

            foreach (var (key, (year, _)) in Shapes)
            {
                var row = rows.FirstOrDefault(r => r.SequenceKey == key && r.Year == year);
                var fromSequence = row?.NextValue ?? 0;
                var fromTable = highest.TryGetValue(key, out var h) ? h : 0;

                if (row is not null) counters._rows[key] = row;
                counters._values[key] = Math.Max(fromSequence, fromTable);
            }

            return counters;
        }

        /// <summary>
        /// The largest number already issued for each key, read from the table that carries it. The
        /// numbers are "PREFIX-000123" or "PREFIX-2026-00123", so the digits after the last dash are
        /// the counter. Anything that does not parse is ignored rather than throwing — a hand-made
        /// reference number must not stop the seed.
        /// </summary>
        private static async Task<Dictionary<string, long>> HighestIssuedAsync(
            ApplicationDbContext context, Guid tenantId, CancellationToken ct)
        {
            static long Parse(IEnumerable<string> numbers)
            {
                long max = 0;
                foreach (var n in numbers)
                {
                    if (string.IsNullOrWhiteSpace(n)) continue;
                    var tail = n[(n.LastIndexOf('-') + 1)..];
                    if (long.TryParse(tail, out var value) && value > max) max = value;
                }
                return max;
            }

            return new Dictionary<string, long>
            {
                [Req] = Parse(await context.Set<StaffRequisition>().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId).Select(x => x.RequisitionNumber).ToListAsync(ct)),
                [Vac] = Parse(await context.Set<JobVacancy>().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId).Select(x => x.VacancyNumber).ToListAsync(ct)),
                [Cand] = Parse(await context.Set<JobCandidate>().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId).Select(x => x.CandidateNumber).ToListAsync(ct)),
                [App] = Parse(await context.Set<JobApplication>().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId).Select(x => x.ApplicationNumber).ToListAsync(ct)),
                [Ofr] = Parse(await context.Set<JobOffer>().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId).Select(x => x.OfferNumber).ToListAsync(ct)),
                [Hir] = Parse(await context.Set<JobHireRecord>().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId).Select(x => x.HireNumber).ToListAsync(ct)),
                [Int] = Parse(await context.Set<JobInterview>().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId).Select(x => x.InterviewNumber).ToListAsync(ct)),
            };
        }

        public string Next(string key)
        {
            var (year, width) = Shapes[key];
            var value = ++_values[key];
            return year > 0
                ? $"{key}-{year}-{value.ToString().PadLeft(width, '0')}"
                : $"{key}-{value.ToString().PadLeft(width, '0')}";
        }

        /// <summary>
        /// Writes the counters back into the change tracker, WITHOUT saving.
        ///
        /// <para>⚠ IT HAS TO BE STAGED, NOT FLUSHED AT THE END, AND THIS IS NOT A STYLE CHOICE.
        /// This class used to save the counters in a step of its own after every other save. On
        /// 2026-09-14 the second save — the deferred dependants — threw, so the twelve vacancies,
        /// twenty-nine requisitions and eighty-three candidates above were committed while the
        /// counters that numbered them were not. The counter then stood twelve behind the register,
        /// and the first vacancy anybody created through the real door died on the unique index:</para>
        /// <code>
        /// Cannot insert duplicate key row in object 'dbo.JobVacancies' with unique index
        /// 'IX_JobVacancy_Tenant_Number'. The duplicate key value is (…, VAC-000013).
        /// </code>
        /// <para>Staged into the same <c>SaveChangesAsync</c> as the rows themselves, the counters
        /// and the numbers they issued commit or roll back together, and no later failure can
        /// separate them. Every number is minted before that first save, so nothing is missed.</para>
        /// </summary>
        public void Stage(ApplicationDbContext context)
        {
            var now = DateTime.UtcNow;

            foreach (var (key, (year, _)) in Shapes)
            {
                var value = _values[key];
                if (_rows.TryGetValue(key, out var row))
                {
                    if (row.NextValue >= value) continue;
                    row.NextValue = value;
                    row.UpdatedAt = now;
                    row.UpdatedBy = By;
                }
                else if (value > 0)
                {
                    context.Set<NumberSequence>().Add(new NumberSequence
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        SequenceKey = key,
                        Year = year,
                        NextValue = value,
                        CreatedAt = now,
                        CreatedBy = By,
                    });
                }
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // DETERMINISTIC PEOPLE
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Issues candidates with distinct names, e-mail addresses and telephone numbers, without a
    /// hand-written list of sixty-six people.
    ///
    /// <para>Pairing is <c>i → (first[i mod 24], last[(7i) mod 30])</c>. Seven is coprime with
    /// thirty, so the pair determines <c>i mod 24</c> and <c>i mod 30</c>, and therefore
    /// <c>i mod 120</c> — every name is unique for the first 120 candidates, which is comfortably
    /// more than the cycles issue. Uniqueness of the name is what makes the e-mail unique.</para>
    /// </summary>
    private sealed class NamePool
    {
        private static readonly (string Name, Gender Gender)[] First =
        {
            ("Kwabena", Gender.Male),   ("Ama", Gender.Female),     ("Yaw", Gender.Male),
            ("Akosua", Gender.Female),  ("Kofi", Gender.Male),      ("Efua", Gender.Female),
            ("Kwame", Gender.Male),     ("Adjoa", Gender.Female),   ("Kojo", Gender.Male),
            ("Abena", Gender.Female),   ("Kwaku", Gender.Male),     ("Esi", Gender.Female),
            ("Yaa", Gender.Female),     ("Fiifi", Gender.Male),     ("Afia", Gender.Female),
            ("Kwesi", Gender.Male),     ("Akua", Gender.Female),    ("Nii", Gender.Male),
            ("Naa", Gender.Female),     ("Selorm", Gender.Male),    ("Elikem", Gender.Male),
            ("Sena", Gender.Female),    ("Mawuli", Gender.Male),    ("Dzifa", Gender.Female),
        };

        private static readonly string[] Last =
        {
            "Mensah", "Boateng", "Owusu", "Asante", "Adjei", "Agyeman", "Danso", "Frimpong",
            "Nyarko", "Gyasi", "Baidoo", "Quartey", "Lartey", "Tetteh", "Nortey", "Ankrah",
            "Aryee", "Addo", "Amoako", "Bediako", "Darko", "Opoku", "Sarpong", "Yeboah",
            "Acquah", "Essien", "Koomson", "Tagoe", "Ocansey", "Hammond",
        };

        private static readonly string[] Domains = { "gmail.com", "yahoo.com", "outlook.com", "hotmail.com" };
        private static readonly string[] Prefixes = { "24", "20", "55", "27", "26", "54", "59" };

        private static readonly string[] Cities =
        {
            "Tema", "Accra", "Ashaiman", "Tema", "Madina", "Kasoa", "Accra", "Nungua", "Teshie", "Dansoman",
        };

        private static readonly string[] Employers =
        {
            "Ghana Ports and Harbours Authority", "Ecobank Ghana", "Tema Oil Refinery",
            "Ghana Water Company", "Volta River Authority", "KPMG Ghana", "Unilever Ghana",
            "Ghana Revenue Authority", "Melcom Group", "Zoomlion Ghana",
        };

        private int _index;

        public int Issued => _index;

        public JobCandidate NextCandidate(
            Guid tenantId, Counters seq, Guid? countryId, DeterministicRng rng, DateTime registeredOn)
        {
            var i = _index++;
            var (first, gender) = First[i % First.Length];
            var last = Last[(i * 7) % Last.Length];

            var experience = 2 + (i % 14);
            var birthYear = 1996 - experience - (i % 5);

            return new JobCandidate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CandidateNumber = seq.Next(Counters.Cand),
                FirstName = first,
                LastName = last,
                DateOfBirth = new DateTime(birthYear, (i % 12) + 1, (i % 27) + 1),
                Gender = gender,
                Email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@{Domains[i % Domains.Length]}",
                Phone = $"0{Prefixes[i % Prefixes.Length]}{(1000000 + (i * 374761) % 9000000)}",
                City = Cities[i % Cities.Length],
                CountryId = countryId,
                Nationality = "Ghanaian",
                CurrentEmployer = Employers[i % Employers.Length],
                CurrentJobTitle = "Officer",
                TotalYearsExperience = experience,
                NoticePeriodDays = new[] { 30, 30, 60, 14, 90 }[i % 5],
                PreferredWorkArrangement = PreferredWorkArrangement.OnSite,
                WorkAuthorizationStatus = WorkAuthorizationStatus.Citizen,
                ExpectedSalaryMin = 3000m + (i % 9) * 500m,
                ExpectedSalaryMax = 4500m + (i % 9) * 500m,
                ExpectedSalaryCurrency = "GHS",
                IsInTalentPool = true,
                TalentPoolAddedDate = registeredOn,
                TalentPoolSource = TalentPoolEntrySource.AppliedAndRetained,
                // Rotated so the talent-pool screen shows the six states a recruiter triages by,
                // rather than sixty-six rows of "Active".
                TalentPoolStatus = (i % 6) switch
                {
                    0 => TalentPoolCandidateStatus.Active,
                    1 => TalentPoolCandidateStatus.Passive,
                    2 => TalentPoolCandidateStatus.Active,
                    3 => TalentPoolCandidateStatus.Dormant,
                    4 => TalentPoolCandidateStatus.OnHold,
                    _ => TalentPoolCandidateStatus.Expired,
                },
                LastEngagedDate = registeredOn.AddDays(rng.Next(1, 60)),
                CreatedAt = registeredOn,
                CreatedBy = By,
            };
        }
    }

    /// <summary>
    /// A linear congruential generator with a fixed seed. <c>Random</c> is deliberately not used:
    /// its sequence is not stable across .NET versions, and a demo record that changes shape between
    /// rebuilds cannot be named in a runbook. Same constants as
    /// <see cref="TdcDemoWorkforceSeeder"/> uses, kept local because that one is private to it.
    /// </summary>
    private sealed class DeterministicRng
    {
        private uint _state;

        public DeterministicRng(uint seed) => _state = seed == 0 ? 1u : seed;

        private uint NextUInt()
        {
            _state = unchecked(1664525u * _state + 1013904223u);
            return _state;
        }

        public int Next(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0) return 0;
            return (int)(NextUInt() % (uint)exclusiveUpperBound);
        }

        public int Next(int inclusiveLower, int exclusiveUpper)
        {
            if (exclusiveUpper <= inclusiveLower) return inclusiveLower;
            return inclusiveLower + Next(exclusiveUpper - inclusiveLower);
        }
    }
}

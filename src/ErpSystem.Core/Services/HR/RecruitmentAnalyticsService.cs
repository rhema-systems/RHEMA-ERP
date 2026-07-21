using System.Globalization;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Read-only aggregation over the recruitment domain for the analytics dashboard.
/// Every query runs through the repositories' <c>GetQueryable()</c>, so the global
/// tenant filter applies — these figures are always scoped to the caller's tenant.
/// </summary>
public class RecruitmentAnalyticsService : IRecruitmentAnalyticsService
{
    private readonly IJobVacancyRepository          _vacancyRepository;
    private readonly IJobApplicationRepository      _applicationRepository;
    private readonly IJobOfferRepository            _offerRepository;
    private readonly IJobHireRecordRepository       _hireRepository;
    private readonly IStaffRequisitionCostRepository _costRepository;
    private readonly IPositionVacancyRepository     _positionVacancyRepository;
    private readonly ILogger<RecruitmentAnalyticsService> _logger;

    /// <summary>Vacancy statuses that represent live recruitment work — used for ageing and recruiter load.</summary>
    private static readonly JobVacancyStatus[] OpenVacancyStatuses =
    {
        JobVacancyStatus.Approved,
        JobVacancyStatus.Published,
        JobVacancyStatus.ClosedForApplications,
        JobVacancyStatus.Shortlisting,
        JobVacancyStatus.Interviewing,
        JobVacancyStatus.OfferStage,
    };

    /// <summary>Position-vacancy statuses that represent a seat still standing empty.</summary>
    private static readonly PositionVacancyStatus[] OpenPositionVacancyStatuses =
    {
        PositionVacancyStatus.Open,
        PositionVacancyStatus.UnderReview,
        PositionVacancyStatus.RequisitionRaised,
    };

    public RecruitmentAnalyticsService(
        IJobVacancyRepository           vacancyRepository,
        IJobApplicationRepository       applicationRepository,
        IJobOfferRepository             offerRepository,
        IJobHireRecordRepository        hireRepository,
        IStaffRequisitionCostRepository costRepository,
        IPositionVacancyRepository      positionVacancyRepository,
        ILogger<RecruitmentAnalyticsService> logger)
    {
        _vacancyRepository         = vacancyRepository;
        _applicationRepository     = applicationRepository;
        _offerRepository           = offerRepository;
        _hireRepository            = hireRepository;
        _costRepository            = costRepository;
        _positionVacancyRepository = positionVacancyRepository;
        _logger                    = logger;
    }

    public async Task<RecruitmentAnalyticsDto> GetAnalyticsAsync(
        int? year = null, CancellationToken cancellationToken = default)
    {
        var y     = year ?? DateTime.UtcNow.Year;
        var today = DateTime.UtcNow.Date;

        // Queries run sequentially — the scoped DbContext is not thread-safe.

        // ── Applications received this year ───────────────────────────────────
        var apps = await _applicationRepository.GetQueryable()
            .Where(a => a.ApplicationDate.Year == y)
            .Select(a => new
            {
                a.ApplicationDate,
                a.Source,
                RecruiterId = a.JobVacancy.RecruiterId,
                Shortlisted = a.ShortlistedDate != null,
                Interviewed = a.InterviewSlots.Any(),
                Offered     = a.Offer != null,
                Hired       = a.HireRecord != null,
            })
            .ToListAsync(cancellationToken);

        // ── Offers issued this year (latest version only — revisions must not double-count) ──
        var offers = await _offerRepository.GetQueryable()
            .Where(o => o.IsLatestVersion
                     && ((o.OfferDate.HasValue && o.OfferDate.Value.Year == y)
                      || (!o.OfferDate.HasValue && o.CreatedAt.Year == y)))
            .Select(o => new { o.OfferStatus, o.OfferDate, o.CreatedAt })
            .ToListAsync(cancellationToken);

        // ── Hires (all non-cancelled; filtered to the year in memory on effective start date) ──
        var hireRaw = await _hireRepository.GetQueryable()
            .Where(h => h.Status != JobHireStatus.Cancelled)
            .Select(h => new
            {
                h.ExpectedStartDate,
                h.ActualStartDate,
                h.ConfirmedDate,
                ApplicationDate = h.Application.ApplicationDate,
                Source          = h.Application.Source,
                RequisitionDate = (DateTime?)h.Application.JobVacancy.Requisition.RequestDate,
                RecruiterId     = h.Application.JobVacancy.RecruiterId,
            })
            .ToListAsync(cancellationToken);

        var hires = hireRaw
            .Select(h => new
            {
                h.ApplicationDate,
                h.RequisitionDate,
                h.Source,
                h.RecruiterId,
                // Prefer what actually happened; fall back to the plan so the hire still counts.
                Start = h.ActualStartDate.HasValue
                    ? h.ActualStartDate.Value.ToDateTime(TimeOnly.MinValue)
                    : (h.ConfirmedDate ?? h.ExpectedStartDate.ToDateTime(TimeOnly.MinValue)),
                // Only confirmed/actual starts are trustworthy enough for speed metrics.
                IsTimed = h.ActualStartDate.HasValue || h.ConfirmedDate.HasValue,
            })
            .Where(h => h.Start.Year == y)
            .ToList();

        // ── Requisition costs recorded this year ──────────────────────────────
        var costs = await _costRepository.GetQueryable()
            .Where(c => c.RecordedDate.Year == y)
            .Select(c => new { c.Category, c.Amount, c.ExchangeRate, c.Currency })
            .ToListAsync(cancellationToken);

        // ── Time-to-shortlist (vacancies whose shortlist completed this year) ─
        var shortlistDays = await _vacancyRepository.GetQueryable()
            .Where(v => v.TimeToShortlistDays.HasValue
                     && v.ShortlistCompletedAt.HasValue
                     && v.ShortlistCompletedAt.Value.Year == y)
            .Select(v => v.TimeToShortlistDays!.Value)
            .ToListAsync(cancellationToken);

        // ── Open vacancies (point-in-time) ────────────────────────────────────
        var openVacancies = await _vacancyRepository.GetQueryable()
            .Where(v => OpenVacancyStatuses.Contains(v.VacancyStatus))
            .Select(v => new
            {
                v.Id,
                v.VacancyNumber,
                v.VacancyStatus,
                v.ApplicationCount,
                v.ActualPublishDate,
                v.PublishDate,
                v.CreatedAt,
                v.CustomAdvertTitle,
                JdTitle        = v.Requisition.JobDescription != null ? v.Requisition.JobDescription.JobTitle : null,
                v.RecruiterId,
                RecruiterFirst = v.Recruiter != null ? v.Recruiter.FirstName : null,
                RecruiterLast  = v.Recruiter != null ? v.Recruiter.LastName  : null,
            })
            .ToListAsync(cancellationToken);

        // ── Open position vacancies — empty seats (point-in-time) ─────────────
        var openSeats = await _positionVacancyRepository.GetQueryable()
            .Where(p => OpenPositionVacancyStatuses.Contains(p.Status))
            .Select(p => p.VacatedDate)
            .ToListAsync(cancellationToken);

        // ════════════════════════════════════════════════════════════════════
        // AGGREGATION
        // ════════════════════════════════════════════════════════════════════

        // ── Speed ─────────────────────────────────────────────────────────────
        var timed = hires.Where(h => h.IsTimed).ToList();

        var timeToFill = timed
            .Where(h => h.RequisitionDate.HasValue && h.Start >= h.RequisitionDate.Value.Date)
            .Select(h => (h.Start - h.RequisitionDate!.Value.Date).TotalDays)
            .ToList();

        var timeToHire = timed
            .Where(h => h.Start >= h.ApplicationDate.Date)
            .Select(h => (h.Start - h.ApplicationDate.Date).TotalDays)
            .ToList();

        // ── Cost ──────────────────────────────────────────────────────────────
        var totalCost = costs.Sum(c => c.Amount * c.ExchangeRate);

        var costByCategory = costs
            .GroupBy(c => c.Category)
            .Select(g => new RecruitmentCostCategoryDto
            {
                Category     = (int)g.Key,
                CategoryName = Humanize(g.Key.ToString()),
                Amount       = Math.Round(g.Sum(c => c.Amount * c.ExchangeRate), 2),
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        // ── Source effectiveness ──────────────────────────────────────────────
        var sourceEffectiveness = apps
            .GroupBy(a => a.Source)
            .Select(g =>
            {
                var applications = g.Count();
                var sourceHires  = g.Count(a => a.Hired);
                return new RecruitmentSourceEffectivenessDto
                {
                    Source       = (int)g.Key,
                    SourceName   = Humanize(g.Key.ToString()),
                    Applications = applications,
                    Shortlisted  = g.Count(a => a.Shortlisted),
                    Hires        = sourceHires,
                    HireRate     = applications > 0
                        ? Math.Round((decimal)sourceHires / applications * 100, 1)
                        : 0m,
                };
            })
            .OrderByDescending(s => s.Applications)
            .ToList();

        // ── Offer outcomes ────────────────────────────────────────────────────
        var accepted = offers.Count(o => o.OfferStatus is JobOfferStatus.Accepted
                                                       or JobOfferStatus.ConditionallyAccepted
                                                       or JobOfferStatus.ChecksCleared);
        var declined = offers.Count(o => o.OfferStatus == JobOfferStatus.Declined);
        var responded = accepted + declined;

        var offerOutcomes = new RecruitmentOfferOutcomeDto
        {
            TotalOffers    = offers.Count,
            Accepted       = accepted,
            Declined       = declined,
            Expired        = offers.Count(o => o.OfferStatus == JobOfferStatus.Expired),
            Withdrawn      = offers.Count(o => o.OfferStatus == JobOfferStatus.Withdrawn),
            Pending        = offers.Count(o => o.OfferStatus is JobOfferStatus.Sent or JobOfferStatus.Negotiating),
            AcceptanceRate = responded > 0 ? Math.Round((decimal)accepted / responded * 100, 1) : 0m,
            DeclineRate    = responded > 0 ? Math.Round((decimal)declined / responded * 100, 1) : 0m,
        };

        // ── Vacancy ageing ────────────────────────────────────────────────────
        var aged = openVacancies
            .Select(v => new
            {
                v.Id,
                v.VacancyNumber,
                v.VacancyStatus,
                v.ApplicationCount,
                v.RecruiterId,
                Title = v.CustomAdvertTitle ?? v.JdTitle ?? "Untitled vacancy",
                Recruiter = BuildName(v.RecruiterFirst, v.RecruiterLast),
                AgeDays = Math.Max(0, (today - (v.ActualPublishDate ?? v.PublishDate ?? v.CreatedAt).Date).Days),
            })
            .ToList();

        var ageing = new List<VacancyAgeingBucketDto>
        {
            new() { Label = "0–30 days",  Count = aged.Count(v => v.AgeDays <= 30) },
            new() { Label = "31–60 days", Count = aged.Count(v => v.AgeDays is > 30 and <= 60) },
            new() { Label = "61–90 days", Count = aged.Count(v => v.AgeDays is > 60 and <= 90) },
            new() { Label = "90+ days",   Count = aged.Count(v => v.AgeDays > 90) },
        };

        var oldest = aged
            .OrderByDescending(v => v.AgeDays)
            .Take(8)
            .Select(v => new AgeingVacancyDto
            {
                VacancyId        = v.Id,
                VacancyNumber    = v.VacancyNumber,
                JobTitle         = v.Title,
                RecruiterName    = v.Recruiter,
                StatusName       = Humanize(v.VacancyStatus.ToString()),
                ApplicationCount = v.ApplicationCount,
                AgeDays          = v.AgeDays,
            })
            .ToList();

        var seatAges = openSeats.Select(d => (double)Math.Max(0, (today - d.Date).Days)).ToList();

        // ── Recruiter load ────────────────────────────────────────────────────
        var hiresByRecruiter = hires
            .Where(h => h.RecruiterId.HasValue)
            .GroupBy(h => h.RecruiterId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Counted from live application rows rather than JobVacancy.ApplicationCount —
        // that denormalised counter is only maintained by the service layer, so it reads
        // zero on seeded/imported data and would understate the recruiter's real load.
        var appsByRecruiter = apps
            .Where(a => a.RecruiterId.HasValue)
            .GroupBy(a => a.RecruiterId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var recruiterLoad = aged
            .Where(v => v.RecruiterId.HasValue)
            .GroupBy(v => v.RecruiterId!.Value)
            .Select(g => new RecruiterLoadDto
            {
                RecruiterId   = g.Key,
                RecruiterName = g.Select(v => v.Recruiter).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                                ?? "Unnamed recruiter",
                OpenVacancies = g.Count(),
                Applications  = appsByRecruiter.TryGetValue(g.Key, out var ac) ? ac : 0,
                HiresYtd      = hiresByRecruiter.TryGetValue(g.Key, out var hc) ? hc : 0,
            })
            .OrderByDescending(r => r.OpenVacancies)
            .ThenByDescending(r => r.Applications)
            .Take(10)
            .ToList();

        // ── Monthly trend ─────────────────────────────────────────────────────
        var monthly = Enumerable.Range(1, 12).Select(m => new MonthlyRecruitmentPointDto
        {
            Month        = m,
            MonthName    = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
            Applications = apps.Count(a => a.ApplicationDate.Month == m),
            Offers       = offers.Count(o => (o.OfferDate ?? o.CreatedAt).Month == m),
            Hires        = hires.Count(h => h.Start.Month == m),
        }).ToList();

        return new RecruitmentAnalyticsDto
        {
            Year     = y,
            Currency = costs.Select(c => c.Currency).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "GHS",

            OpenVacanciesCount = openVacancies.Count,
            ApplicationsYtd    = apps.Count,
            OffersYtd          = offers.Count,
            HiresYtd           = hires.Count,

            AvgTimeToFillDays      = Average(timeToFill),
            MedianTimeToFillDays   = Median(timeToFill),
            AvgTimeToHireDays      = Average(timeToHire),
            AvgTimeToShortlistDays = shortlistDays.Count > 0
                ? Math.Round((decimal)shortlistDays.Average(), 1)
                : null,
            TimedHiresSampleSize   = timed.Count,

            TotalRecruitmentCost = Math.Round(totalCost, 2),
            CostPerHire          = hires.Count > 0 ? Math.Round(totalCost / hires.Count, 2) : null,
            CostByCategory       = costByCategory,

            Funnel = new RecruitmentFunnelDto
            {
                Applied     = apps.Count,
                Shortlisted = apps.Count(a => a.Shortlisted),
                Interviewed = apps.Count(a => a.Interviewed),
                Offered     = apps.Count(a => a.Offered),
                Hired       = apps.Count(a => a.Hired),
            },

            SourceEffectiveness = sourceEffectiveness,
            OfferOutcomes       = offerOutcomes,

            VacancyAgeing       = ageing,
            OldestOpenVacancies = oldest,

            OpenPositionVacanciesCount = openSeats.Count,
            AvgPositionVacancyAgeDays  = seatAges.Count > 0 ? Math.Round((decimal)seatAges.Average(), 1) : null,

            RecruiterLoad = recruiterLoad,
            MonthlyTrend  = monthly,
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static decimal? Average(List<double> values)
        => values.Count > 0 ? Math.Round((decimal)values.Average(), 1) : null;

    private static decimal? Median(List<double> values)
    {
        if (values.Count == 0) return null;

        var sorted = values.OrderBy(v => v).ToList();
        var mid    = sorted.Count / 2;
        var median = sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;

        return Math.Round((decimal)median, 1);
    }

    private static string? BuildName(string? first, string? last)
    {
        var name = $"{first} {last}".Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>"CompanyWebsite" → "Company Website", so enum names read as labels on a chart.</summary>
    private static string Humanize(string pascalCase)
        => string.IsNullOrEmpty(pascalCase)
            ? pascalCase
            : Regex.Replace(pascalCase, "(?<=[a-z0-9])(?=[A-Z])", " ");
}

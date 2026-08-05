namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// RECRUITMENT ANALYTICS (api/recruitment-dashboard/analytics?year=)
// Combined operational analytics: speed, cost, funnel, source effectiveness,
// offer outcomes, vacancy ageing and recruiter load.
// ============================================================================

public sealed class RecruitmentAnalyticsDto
{
    public int    Year     { get; set; }
    public string Currency { get; set; } = "GHS";

    // ── Headline KPIs ────────────────────────────────────────────────────────
    public int OpenVacanciesCount { get; set; }
    public int ApplicationsYtd    { get; set; }
    public int OffersYtd          { get; set; }
    public int HiresYtd           { get; set; }

    // ── Speed ────────────────────────────────────────────────────────────────
    /// <summary>Requisition raised → new hire's actual start. Averaged over hires that started this year.</summary>
    public decimal? AvgTimeToFillDays { get; set; }

    /// <summary>Median of the same series — resistant to a single pathological vacancy.</summary>
    public decimal? MedianTimeToFillDays { get; set; }

    /// <summary>Application received → that candidate's actual start.</summary>
    public decimal? AvgTimeToHireDays { get; set; }

    /// <summary>Vacancy published → shortlist completed (from JobVacancy.TimeToShortlistDays).</summary>
    public decimal? AvgTimeToShortlistDays { get; set; }

    /// <summary>Number of hires that carried a confirmed/actual start date, i.e. the sample behind the speed figures.</summary>
    public int TimedHiresSampleSize { get; set; }

    // ── Cost ─────────────────────────────────────────────────────────────────
    /// <summary>All requisition costs recorded this year, converted to base currency via ExchangeRate.</summary>
    public decimal  TotalRecruitmentCost { get; set; }
    public decimal? CostPerHire          { get; set; }
    public List<RecruitmentCostCategoryDto> CostByCategory { get; set; } = new();

    // ── Funnel ───────────────────────────────────────────────────────────────
    public RecruitmentFunnelDto Funnel { get; set; } = new();

    // ── Source effectiveness ─────────────────────────────────────────────────
    public List<RecruitmentSourceEffectivenessDto> SourceEffectiveness { get; set; } = new();

    // ── Offer outcomes ───────────────────────────────────────────────────────
    public RecruitmentOfferOutcomeDto OfferOutcomes { get; set; } = new();

    // ── Vacancy ageing (point-in-time, not year-filtered) ────────────────────
    public List<VacancyAgeingBucketDto> VacancyAgeing        { get; set; } = new();
    public List<AgeingVacancyDto>       OldestOpenVacancies  { get; set; } = new();

    /// <summary>Open <c>PositionVacancy</c> rows — empty seats, whether or not a requisition was raised.</summary>
    public int      OpenPositionVacanciesCount { get; set; }
    public decimal? AvgPositionVacancyAgeDays  { get; set; }

    // ── Recruiter load (point-in-time) ───────────────────────────────────────
    public List<RecruiterLoadDto> RecruiterLoad { get; set; } = new();

    // ── Trend ────────────────────────────────────────────────────────────────
    public List<MonthlyRecruitmentPointDto> MonthlyTrend { get; set; } = new();
}

/// <summary>Applied → shortlisted → interviewed → offered → hired, for applications received this year.</summary>
public sealed class RecruitmentFunnelDto
{
    public int Applied     { get; set; }
    public int Shortlisted { get; set; }
    public int Interviewed { get; set; }
    public int Offered     { get; set; }
    public int Hired       { get; set; }
}

public sealed class RecruitmentSourceEffectivenessDto
{
    public int    Source       { get; set; }
    public string SourceName   { get; set; } = string.Empty;
    public int    Applications { get; set; }
    public int    Shortlisted  { get; set; }
    public int    Hires        { get; set; }

    /// <summary>Hires as a percentage of applications from this source.</summary>
    public decimal HireRate { get; set; }
}

public sealed class RecruitmentOfferOutcomeDto
{
    public int TotalOffers { get; set; }
    public int Accepted    { get; set; }
    public int Declined    { get; set; }
    public int Expired     { get; set; }
    public int Withdrawn   { get; set; }

    /// <summary>Sent / negotiating — still awaiting a candidate decision.</summary>
    public int Pending { get; set; }

    /// <summary>Accepted as a percentage of offers the candidate actually responded to (accepted + declined).</summary>
    public decimal AcceptanceRate { get; set; }
    public decimal DeclineRate    { get; set; }
}

public sealed class VacancyAgeingBucketDto
{
    public string Label { get; set; } = string.Empty;
    public int    Count { get; set; }
}

public sealed class AgeingVacancyDto
{
    public Guid    VacancyId        { get; set; }
    public string  VacancyNumber    { get; set; } = string.Empty;
    public string  JobTitle         { get; set; } = string.Empty;
    public string? RecruiterName    { get; set; }
    public string  StatusName       { get; set; } = string.Empty;
    public int     ApplicationCount { get; set; }
    public int     AgeDays          { get; set; }
}

public sealed class RecruiterLoadDto
{
    public Guid   RecruiterId    { get; set; }
    public string RecruiterName  { get; set; } = string.Empty;
    public int    OpenVacancies  { get; set; }
    public int    Applications   { get; set; }
    public int    HiresYtd       { get; set; }
}

public sealed class RecruitmentCostCategoryDto
{
    public int     Category     { get; set; }
    public string  CategoryName { get; set; } = string.Empty;
    public decimal Amount       { get; set; }
}

public sealed class MonthlyRecruitmentPointDto
{
    public int    Month        { get; set; }
    public string MonthName    { get; set; } = string.Empty;
    public int    Applications { get; set; }
    public int    Offers       { get; set; }
    public int    Hires        { get; set; }
}

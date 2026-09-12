using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffGrievance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Employee-relations analytics — area 9c slice 8.
/// </summary>
/// <remarks>
/// <para><b>⚠ Two rules, both learned the hard way in area 7, and both structural here rather than
/// remembered.</b></para>
///
/// <para><b>1. Every number comes from ONE materialised set.</b> Area 7 shipped a dashboard and an
/// analytics page that disagreed about the same figure, because each ran its own query over a
/// slightly different set — one counted completions, the other counted passes. This service loads
/// the window ONCE, into memory, and computes every figure from that list. Slower in principle;
/// impossible to make internally inconsistent, which matters more on a page whose whole purpose is
/// being believed.</para>
///
/// <para><b>2. No rate without its denominator.</b> Area 7 shipped a compliance rate that read 0%
/// whether nobody complied or nobody was asked. Here a rate is an <see cref="ErRateDto"/> carrying
/// both numbers, and its percentage is <b>null</b> — never 0 — when the denominator is zero. There
/// is deliberately no way to construct a bare percentage.</para>
///
/// <para><b>Concerns are counted, never cross-tabbed.</b> No breakdown by unit, reporter or
/// anything else that narrows the field: in a unit of four, "one fraud concern this quarter" is an
/// identification rather than a statistic.</para>
/// </remarks>
public class EmployeeRelationsAnalyticsService : IEmployeeRelationsAnalyticsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public EmployeeRelationsAnalyticsService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>A case, flattened to exactly the fields every figure on the page is computed from.</summary>
    private sealed record CaseRow(
        Guid Id,
        EmployeeRelationsCaseType CaseType,
        GrievanceStatus Status,
        GrievanceEscalationLevel CurrentLevel,
        DateTime FiledDate,
        DateTime? ResolvedDate,
        Guid? OrganizationUnitId,
        string? OrganizationUnitName,
        int StepCount,
        bool CurrentStepUnanswered,
        DateTime CurrentStepReachedDate,
        Guid? CurrentStepAssignedToId,
        GrievanceResolutionOutcome? Outcome,
        bool HasSignedAgreement);

    public async Task<EmployeeRelationsAnalyticsDto> GetAsync(
        DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;

        var query = _unitOfWork.Repository<StaffGrievance>()
            .GetQueryable()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted);

        if (from.HasValue) query = query.Where(g => g.FiledDate >= from.Value);
        if (to.HasValue) query = query.Where(g => g.FiledDate <= to.Value);

        // ⚠ ONE projection, ONE materialisation. Every figure below is computed from `cases`; no
        // figure on this page runs its own query, so no two figures can disagree.
        var cases = await query
            .Select(g => new CaseRow(
                g.Id,
                g.CaseType,
                g.Status,
                g.CurrentLevel,
                g.FiledDate,
                g.ResolvedDate,
                g.Employee.OrganizationUnitId,
                g.Employee.OrganizationUnit != null ? g.Employee.OrganizationUnit.Name : null,
                g.Steps.Count(s => !s.IsDeleted),
                g.Steps.OrderByDescending(s => s.Sequence).First().Outcome == GrievanceStepOutcome.AwaitingResponse,
                g.Steps.OrderByDescending(s => s.Sequence).First().ReachedDate,
                g.Steps.OrderByDescending(s => s.Sequence).First().AssignedToId,
                g.Resolution != null && !g.Resolution.IsDeleted ? g.Resolution.Outcome : null,
                g.Resolution != null && !g.Resolution.IsDeleted && g.Resolution.AgreementSignedDate != null))
            .ToListAsync(cancellationToken);

        var total = cases.Count;

        var open = cases.Count(c => c.Status is GrievanceStatus.Filed
                                             or GrievanceStatus.UnderReview
                                             or GrievanceStatus.Escalated);
        var resolved = cases.Count(c => c.Status == GrievanceStatus.Resolved);
        var withdrawn = cases.Count(c => c.Status == GrievanceStatus.Withdrawn);
        var closedUnresolved = cases.Count(c => c.Status == GrievanceStatus.Closed);

        var dto = new EmployeeRelationsAnalyticsDto
        {
            From = from,
            To = to,
            TotalCases = total,
            OpenCases = open,
            ResolvedCases = resolved,
            WithdrawnCases = withdrawn,
            ClosedUnresolvedCases = closedUnresolved,
        };

        // ── Breakdowns. Each carries the denominator it is a share OF, which is not always the
        // page total: where a case currently SITS only means anything for an open one.
        dto.ByCaseType = cases
            .GroupBy(c => c.CaseType)
            .OrderByDescending(g => g.Count())
            .Select(g => Slice(g.Key.ToString(), Humanise(g.Key.ToString()), g.Count(), total))
            .ToList();

        dto.ByStatus = cases
            .GroupBy(c => c.Status)
            .OrderByDescending(g => g.Count())
            .Select(g => Slice(g.Key.ToString(), Humanise(g.Key.ToString()), g.Count(), total))
            .ToList();

        dto.ByCurrentLevel = cases
            .Where(c => c.Status is GrievanceStatus.Filed or GrievanceStatus.UnderReview or GrievanceStatus.Escalated)
            .GroupBy(c => c.CurrentLevel)
            .OrderBy(g => (int)g.Key)
            .Select(g => Slice(g.Key.ToString(), Humanise(g.Key.ToString()), g.Count(), open))
            .ToList();

        dto.ByOutcome = cases
            .Where(c => c.Status == GrievanceStatus.Resolved)
            .GroupBy(c => c.Outcome)
            .OrderByDescending(g => g.Count())
            // A resolved case with no resolution row at all predates slice 2 — it is counted, and
            // labelled for what it is rather than dropped, because dropping it would make the
            // outcome breakdown quietly fail to add up to ResolvedCases.
            .Select(g => Slice(
                g.Key?.ToString() ?? "NoResolutionRecord",
                g.Key == null ? "No resolution record (pre-slice-2)" : Humanise(g.Key.ToString()!),
                g.Count(), resolved))
            .ToList();

        dto.ByOrganizationUnit = cases
            .Where(c => c.OrganizationUnitId != null)
            .GroupBy(c => new { c.OrganizationUnitId, c.OrganizationUnitName })
            .OrderByDescending(g => g.Count())
            .Take(20)
            .Select(g => Slice(
                g.Key.OrganizationUnitId!.Value.ToString(),
                g.Key.OrganizationUnitName ?? "(unnamed unit)",
                g.Count(), total))
            .ToList();

        // ── Rates, each carrying both its numbers ────────────────────────────
        dto.EscalationRate = new ErRateDto
        {
            Label = "Cases escalated at least once",
            // More than one step means it moved up at least a rung. Reading the steps is what makes
            // this true for a case that escalated and was then resolved — the status alone forgets.
            Numerator = cases.Count(c => c.StepCount > 1),
            Denominator = total,
        };

        var settled = cases.Count(c => c.Outcome == GrievanceResolutionOutcome.SettledByAgreement);
        dto.AgreementMissingRate = new ErRateDto
        {
            Label = "Settlements with no signed agreement on file",
            Numerator = cases.Count(c => c.Outcome == GrievanceResolutionOutcome.SettledByAgreement
                                         && !c.HasSignedAgreement),
            Denominator = settled,
        };

        dto.OutcomeNotRecordedRate = new ErRateDto
        {
            Label = "Resolved cases whose outcome was never captured",
            Numerator = cases.Count(c => c.Status == GrievanceStatus.Resolved
                                         && (c.Outcome == null || c.Outcome == GrievanceResolutionOutcome.NotRecorded)),
            Denominator = resolved,
        };

        // ── Time to resolution ───────────────────────────────────────────────
        // ⚠ Resolved only. A withdrawal is not a resolution, and counting one would shorten the
        // average every time somebody gave up — the figure would improve as the process got worse.
        var durations = cases
            .Where(c => c.Status == GrievanceStatus.Resolved && c.ResolvedDate != null)
            .Select(c => (decimal)(c.ResolvedDate!.Value - c.FiledDate).TotalDays)
            .OrderBy(d => d)
            .ToList();

        dto.ResolutionTime = new ErResolutionTimeDto
        {
            ResolvedCount = durations.Count,
            // Null, not zero: "no cases have been resolved" is not "cases resolve instantly".
            AverageDays = durations.Count == 0 ? null : Math.Round(durations.Average(), 1),
            MedianDays = durations.Count == 0 ? null : Math.Round(Median(durations), 1),
            LongestDays = durations.Count == 0 ? null : (int)Math.Round(durations[^1]),
        };

        // ── Where the ladder is stuck ────────────────────────────────────────
        dto.StuckAtRung = cases
            .Where(c => c.CurrentStepUnanswered
                        && c.Status is GrievanceStatus.Filed or GrievanceStatus.UnderReview or GrievanceStatus.Escalated)
            .GroupBy(c => c.CurrentLevel)
            .OrderBy(g => (int)g.Key)
            .Select(g => new ErStuckRungDto
            {
                Level = g.Key,
                Count = g.Count(),
                OldestWaitingDays = (int)Math.Round(g.Max(c => (now - c.CurrentStepReachedDate).TotalDays)),
                // The responder-matrix gap expressed in cases rather than in coverage percentages:
                // these are the ones nobody has been named to answer.
                Unassigned = g.Count(c => c.CurrentStepAssignedToId == null),
            })
            .ToList();

        // ── Anonymous intake. Counts only — see the class remarks. ───────────
        var concerns = _unitOfWork.Repository<EmployeeRelationsConcern>()
            .GetQueryable()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted);

        if (from.HasValue) concerns = concerns.Where(c => c.ReportedAt >= from.Value);
        if (to.HasValue) concerns = concerns.Where(c => c.ReportedAt <= to.Value);

        var concernRows = await concerns
            .Select(c => new { c.Status, c.Category })
            .ToListAsync(cancellationToken);

        dto.ConcernsReported = concernRows.Count;
        dto.ConcernsUntriaged = concernRows.Count(c => c.Status == ConcernStatus.New);
        dto.ConcernsConverted = concernRows.Count(c => c.Status == ConcernStatus.ConvertedToCase);
        dto.ConcernsByCategory = concernRows
            .GroupBy(c => c.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => Slice(g.Key.ToString(), Humanise(g.Key.ToString()), g.Count(), concernRows.Count))
            .ToList();

        return dto;
    }

    private static ErCountSliceDto Slice(string key, string label, int count, int total)
        => new() { Key = key, Label = label, Count = count, Total = total };

    private static decimal Median(IReadOnlyList<decimal> sorted)
        => sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2m;

    /// <summary>"HeadOfDepartment" → "Head Of Department". Enum names are not labels.</summary>
    private static string Humanise(string pascal)
        => string.Concat(pascal.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()));
}

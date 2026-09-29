using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>Everything scoring one criterion needs, resolved once per appraisal rather than per item.</summary>
/// <param name="Share">The criterion's share of the whole form, 0–100 (<see cref="AppraisalScoring.CriterionShare"/>).</param>
/// <param name="MaxScore">The top of the criterion's own scale — its highest grade band, or 100 when it has none.</param>
/// <param name="IsKpi">Measured against a target rather than rated.</param>
public sealed record CriterionScoringInfo(
    decimal Share, decimal MaxScore, bool IsKpi, decimal? KpiTarget, decimal? KpiMin, decimal? KpiMax);

/// <summary>
/// One appraisal's scoring inputs, keyed by template item. Built by
/// <see cref="IAppraisalScoreService.LoadScoringAsync"/>; an item missing from the criterion
/// snapshot is resolved from the live template the first time it is asked for and kept.
/// </summary>
public sealed class AppraisalCriterionScoring
{
    internal AppraisalCriterionScoring(Guid appraisalId, Dictionary<Guid, CriterionScoringInfo> items)
    {
        AppraisalId = appraisalId;
        Items = items;
    }

    public Guid AppraisalId { get; }

    internal Dictionary<Guid, CriterionScoringInfo> Items { get; }
}

/// <summary>What one settle did.</summary>
public sealed record AppraisalSettleResult(
    Guid AppraisalId,
    decimal? ScoreBefore,
    decimal? ScoreAfter,
    Guid? GradeBefore,
    Guid? GradeAfter,
    decimal? ComputedScore,
    bool Published)
{
    public bool Changed => ScoreBefore != ScoreAfter || GradeBefore != GradeAfter;
}

/// <summary>
/// The appraisal's score, in one place (performance closure lane A).
///
/// <para>This service holds the only arithmetic path from raw inputs to a stored number, and
/// <see cref="SettleAsync"/> is the only writer of <see cref="PerformanceAppraisal.OverallScore"/>.
/// Before it there were two copies of the per-criterion arithmetic (the appraisal service and the
/// peer service), an overall that summed stored weighted scores — so no calibration or appeal
/// adjustment to an item ever changed a result — unsubmitted drafts counted as legs, three grade
/// resolvers that disagreed, and five ways to reach Completed of which one computed a score.</para>
/// </summary>
public class AppraisalScoreService : IAppraisalScoreService
{
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<PerformanceAppraisalCriterionConfig> _criterionConfigRepository;
    private readonly IGenericRepository<AppraisalTemplateItem> _templateItemRepository;
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeDefinitionRepository;
    private readonly IGenericRepository<TalentPoolMember> _talentPoolMemberRepository;
    private readonly IPerformanceRatingResolver _ratingResolver;
    private readonly ITalentRatingSyncService _talentRatingSync;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalScoreService> _logger;

    public AppraisalScoreService(
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<PerformanceAppraisalCriterionConfig> criterionConfigRepository,
        IGenericRepository<AppraisalTemplateItem> templateItemRepository,
        IGenericRepository<AppraisalGradeDefinition> gradeDefinitionRepository,
        IGenericRepository<TalentPoolMember> talentPoolMemberRepository,
        IPerformanceRatingResolver ratingResolver,
        ITalentRatingSyncService talentRatingSync,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalScoreService> logger)
    {
        _appraisalRepository = appraisalRepository;
        _criterionConfigRepository = criterionConfigRepository;
        _templateItemRepository = templateItemRepository;
        _gradeDefinitionRepository = gradeDefinitionRepository;
        _talentPoolMemberRepository = talentPoolMemberRepository;
        _ratingResolver = ratingResolver;
        _talentRatingSync = talentRatingSync;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>Every stored score is two decimal places; round explicitly rather than leave it to the driver (A13).</summary>
    private static decimal? Round2(decimal? value)
        => value is decimal v ? Math.Round(v, 2, MidpointRounding.AwayFromZero) : null;

    // ── Scoring inputs ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// ⚠ The snapshot is read with the query filters off, and its own soft-delete applied by hand.
    /// A template item or section removed after generation is soft-deleted, and with the filter
    /// on, its navigation came back null: a KPI item then read as a competency and its section
    /// weight as zero, so deleting a line from a template re-scored every appraisal generated from
    /// it — the very thing the snapshot exists to prevent.
    /// </remarks>
    public async Task<AppraisalCriterionScoring> LoadScoringAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var configs = await _criterionConfigRepository.GetQueryable()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.PerformanceAppraisalId == appraisalId && !c.IsDeleted)
            .Select(c => new
            {
                Config = c,
                SectionWeight = (int?)c.TemplateItem!.Section.Weight,
                TemplateKpiDefinitionId = c.TemplateItem!.KpiDefinitionId,
                TopBand = c.GradeRanges.Where(r => !r.IsDeleted).Max(r => (int?)r.HighScore),
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var items = new Dictionary<Guid, CriterionScoringInfo>();
        foreach (var row in configs)
        {
            var c = row.Config;
            items[c.TemplateKey()] = new CriterionScoringInfo(
                // A0: the section weight frozen at generation; the live section only for rows
                // written before the freeze and not yet backfilled.
                Share: AppraisalScoring.CriterionShare(c.SectionWeightUsed ?? row.SectionWeight ?? 0, c.WeightUsed),
                MaxScore: row.TopBand ?? AppraisalScoring.MaxScore,
                IsKpi: c.ScoringMethod.HasValue
                    ? c.ScoringMethod == CriterionScoringMethod.Measured
                    : row.TemplateKpiDefinitionId.HasValue,
                KpiTarget: c.KpiTargetValue,
                KpiMin: c.KpiMinValue,
                KpiMax: c.KpiMaxValue);
        }

        return new AppraisalCriterionScoring(appraisalId, items);
    }

    /// <summary>
    /// The scoring inputs for one criterion: the snapshot row, or — for an appraisal generated
    /// before the snapshot existed — the live template item, deleted or not (a line removed from
    /// the template keeps scoring what was scored against it, as a snapshot row does). An id that
    /// matches no item at all scores nothing (share 0).
    /// </summary>
    private async Task<CriterionScoringInfo> ResolveAsync(
        AppraisalCriterionScoring scoring, Guid templateItemId, CancellationToken cancellationToken)
    {
        if (scoring.Items.TryGetValue(templateItemId, out var known))
            return known;

        var tenantId = GetTenantId();
        var live = await _templateItemRepository
            .GetQueryableIncludingDeleted(i => i.Id == templateItemId && i.TenantId == tenantId)
            .Select(i => new
            {
                i.Weight,
                SectionWeight = (int?)i.Section.Weight,
                i.KpiDefinitionId,
                i.KpiTargetValue,
                i.KpiMinValue,
                i.KpiMaxValue,
                TopBand = i.GradeRanges.Where(r => !r.IsDeleted).Max(r => (int?)r.HighScore),
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var info = live == null
            ? new CriterionScoringInfo(0m, AppraisalScoring.MaxScore, false, null, null, null)
            : new CriterionScoringInfo(
                AppraisalScoring.CriterionShare(live.SectionWeight ?? 0, live.Weight),
                live.TopBand ?? AppraisalScoring.MaxScore,
                live.KpiDefinitionId.HasValue,
                live.KpiTargetValue,
                live.KpiMinValue,
                live.KpiMaxValue);

        scoring.Items[templateItemId] = info;
        return info;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetScaleTopAsync(
        AppraisalCriterionScoring scoring, Guid templateItemId, CancellationToken cancellationToken = default)
    {
        var info = await ResolveAsync(scoring, templateItemId, cancellationToken);
        // A KPI's NumericScore is an achievement-% override (D-22), always out of 100.
        return info.IsKpi ? AppraisalScoring.MaxScore : info.MaxScore;
    }

    // ── Arithmetic ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A criterion's exact contribution to its evaluator's score, <c>achievement(0–1) × share</c>,
    /// or null when the row carries no value.
    ///
    /// <list type="bullet">
    ///   <item>A rated item: <c>NumericScore ÷ the item's top band</c>.</item>
    ///   <item>A KPI with a <c>NumericScore</c>: an achievement percentage that calibration or an
    ///         appeal restated (D-22), so <c>÷ 100</c> — never the band top, which would have
    ///         read a restated 90 % on an item banded to 80 as 112 %.</item>
    ///   <item>A KPI with only an actual: <see cref="AppraisalScoring.KpiAchievementPercent"/>
    ///         against the snapshot's target, floor and ceiling.</item>
    /// </list>
    ///
    /// Achievement is clamped to 0–1, so one item past the top of its scale cannot make up for
    /// another's shortfall. The inputs are validated to the scale as well (A11); the clamp is for
    /// rows saved before that validation existed.
    ///
    /// ⚠ The evaluator's weight is deliberately NOT applied here — it belongs once, at aggregation
    /// (see the model note on <see cref="AppraisalScoring"/>).
    /// </summary>
    private static decimal? Contribution(CriterionScore score, CriterionScoringInfo info)
    {
        decimal achievement;

        if (score.NumericScore is int numeric)
        {
            var top = info.IsKpi ? AppraisalScoring.MaxScore : info.MaxScore;
            achievement = top > 0 ? numeric / top : 0m;
        }
        else if (score.ActualValue is decimal actual)
        {
            achievement = AppraisalScoring.KpiAchievementPercent(actual, info.KpiTarget, info.KpiMin, info.KpiMax) / 100m;
        }
        else
        {
            return null;
        }

        return Math.Clamp(achievement, 0m, 1m) * info.Share;
    }

    /// <inheritdoc/>
    public async Task ScoreCriterionAsync(
        CriterionScore score, AppraisalCriterionScoring scoring, CancellationToken cancellationToken = default)
    {
        var info = await ResolveAsync(scoring, score.TemplateKey(), cancellationToken);
        score.WeightedScore = Round2(Contribution(score, info)) ?? 0m;
    }

    /// <inheritdoc/>
    public async Task<decimal?> ScoreEvaluatorAsync(
        IEnumerable<CriterionScore> scores, AppraisalCriterionScoring scoring, CancellationToken cancellationToken = default)
    {
        var scored = new List<(decimal, decimal)>();

        foreach (var score in scores)
        {
            var info = await ResolveAsync(scoring, score.TemplateKey(), cancellationToken);
            var contribution = Contribution(score, info);

            // Every row is re-weighted from its raw inputs, so a stored WeightedScore can never be
            // stale — an adjustment that only changed NumericScore used to be invisible here.
            score.WeightedScore = Round2(contribution) ?? 0m;

            if (contribution is decimal c && info.Share > 0)
                scored.Add((c, info.Share));
        }

        return scored.Count == 0 ? null : Round2(AppraisalScoring.EvaluatorScore(scored));
    }

    // ── Settle ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Final means the score has left governance: Completed or Closed, or HR has signed it off —
    /// and the panel has committed it, when the cycle requires calibration — so only the final
    /// conversation or the employee's acknowledgment is outstanding. A remanded appraisal back in
    /// governance still carries the sign-off from before its appeal, and is not final until HR's
    /// post-remand decision.
    ///
    /// <para>⚠ The calibration condition came with B1, which made <c>HRReviewTiming</c> real: with
    /// HR's review before calibration, a sign-off is not the last word, and the sign-off published
    /// the pre-calibration score to the talent pools.</para>
    /// </summary>
    private static bool IsFinal(PerformanceAppraisal appraisal)
        => appraisal.Status is AppraisalStatus.Completed or AppraisalStatus.Closed
           || (appraisal.Status == AppraisalStatus.Governance
               && appraisal.AppealRemandedDate == null
               && appraisal.HRReviews.Any(r => r.ReviewCompletedDate != null && r.IsApproved)
               && (appraisal.IsCalibrated || appraisal.AppraisalCycle?.AppraisalSettings?.RequireCalibration != true));

    private async Task<PerformanceAppraisal> LoadForSettleAsync(Guid appraisalId, bool tracked, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var query = _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.Id == appraisalId)
            .Include(a => a.EvaluatorEvaluations)
                .ThenInclude(e => e.CriterionScores)
            .Include(a => a.HRReviews)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .AsSplitQuery();

        var appraisal = await (tracked ? query : query.AsNoTracking()).FirstOrDefaultAsync(cancellationToken);
        return appraisal ?? throw new ArgumentException($"Performance appraisal with ID '{appraisalId}' not found.");
    }

    /// <summary>
    /// Steps 1–4 of the settle, on whatever is loaded: re-weights every row, recomputes each
    /// submitted evaluator's total, and returns the overall across the submitted legs — null when
    /// no submitted evaluation scored anything.
    /// </summary>
    private async Task<decimal?> ComputeAsync(PerformanceAppraisal appraisal, CancellationToken cancellationToken)
    {
        var scoring = await LoadScoringAsync(appraisal.Id, cancellationToken);
        var legs = new List<(EvaluatorRole Role, decimal Score, decimal Weight)>();

        foreach (var evaluation in appraisal.EvaluatorEvaluations.Where(e => !e.IsDeleted))
        {
            var total = await ScoreEvaluatorAsync(evaluation.CriterionScores.Where(s => !s.IsDeleted), scoring, cancellationToken);

            // A draft's rows are re-weighted for the forms that show them, but a draft is not a
            // leg: a saved-and-never-submitted self or peer evaluation used to move the final score.
            if (evaluation.SubmittedDate == null) continue;

            evaluation.TotalScore = total;
            if (total is decimal t && evaluation.EvaluatorWeight > 0)
                legs.Add((evaluation.EvaluatorRole, t, evaluation.EvaluatorWeight));
        }

        // ⚠ Aggregation is by ROLE, not by evaluator record: peers are averaged into one voice, or
        // their influence would scale with their headcount (see the model note on AppraisalScoring).
        var roles = legs
            .GroupBy(l => l.Role)
            .Select(g => (Score: g.Average(l => l.Score), Weight: g.Max(l => l.Weight)))
            .ToList();

        var contributingWeight = roles.Sum(r => r.Weight);
        if (contributingWeight > 0 && Math.Abs(contributingWeight - 1m) > 0.001m)
        {
            _logger.LogWarning(
                "Appraisal {AppraisalId}: contributing evaluator role weights sum to {Sum} (expected 1.0); normalising the overall score.",
                appraisal.Id, contributingWeight);
        }

        return Round2(AppraisalScoring.OverallScore(roles));
    }

    /// <inheritdoc/>
    public async Task<AppraisalSettleResult> SettleAsync(
        Guid appraisalId, AppraisalScoreChangeSource source, bool publish = true, CancellationToken cancellationToken = default)
    {
        var appraisal = await LoadForSettleAsync(appraisalId, tracked: true, cancellationToken);

        var scoreBefore = appraisal.OverallScore;
        var gradeBefore = appraisal.OverallGradeDefinitionId;

        var computed = await ComputeAsync(appraisal, cancellationToken);

        // A committed calibration restatement stands until a later calibration or an appeal
        // replaces it — HR sign-off used to recompute over it and push the uncalibrated number to
        // the talent pools (P-39).
        var settled = Round2(appraisal.CalibratedOverallScore) ?? computed;

        appraisal.OverallScore = settled;
        appraisal.OverallGradeDefinitionId = await _ratingResolver.ResolveGradeDefinitionIdAsync(settled, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (scoreBefore != settled || gradeBefore != appraisal.OverallGradeDefinitionId)
        {
            _logger.LogInformation(
                "Appraisal {AppraisalId} settled ({Source}): {Before} → {After} (computed {Computed}, calibrated {Calibrated}).",
                appraisalId, source, scoreBefore, settled, computed, appraisal.CalibratedOverallScore);
        }

        var published = false;
        if (publish && IsFinal(appraisal))
            published = await PublishLoadedAsync(appraisal, cancellationToken);

        return new AppraisalSettleResult(
            appraisalId, scoreBefore, settled, gradeBefore, appraisal.OverallGradeDefinitionId, computed, published);
    }

    /// <inheritdoc/>
    public async Task<bool> PublishAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await LoadForSettleAsync(appraisalId, tracked: false, cancellationToken);
        return IsFinal(appraisal) && await PublishLoadedAsync(appraisal, cancellationToken);
    }

    /// <summary>
    /// Pushes the settled rating to the talent pools. Best-effort: the score is already saved, and
    /// a talent-pool failure must not undo or fail the step that settled it.
    /// </summary>
    private async Task<bool> PublishLoadedAsync(PerformanceAppraisal appraisal, CancellationToken cancellationToken)
    {
        try
        {
            return await _talentRatingSync.SyncFromAppraisalAsync(appraisal.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Talent rating sync failed for appraisal {AppraisalId}; its settled score stands.", appraisal.Id);
            return false;
        }
    }

    // ── Validation (A11) ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string?> ValidateItemScoresAsync(
        Guid appraisalId, IEnumerable<EvaluationItemInputDto> items, CancellationToken cancellationToken = default)
    {
        var inputs = items.ToList();
        if (inputs.Count == 0) return null;

        var scoring = await LoadScoringAsync(appraisalId, cancellationToken);

        foreach (var input in inputs)
        {
            if (input.ActualValue is decimal actual && actual < 0)
                return "An actual value cannot be negative.";

            if (input.NumericScore is not int numeric) continue;

            var top = await GetScaleTopAsync(scoring, input.TemplateItemId, cancellationToken);
            if (numeric < AppraisalScoring.MinScore || numeric > top)
                return top == AppraisalScoring.MaxScore
                    ? $"Scores must be between {AppraisalScoring.MinScore:0} and {AppraisalScoring.MaxScore:0}."
                    : $"A score of {numeric} is above the top of this item's scale: its grade bands end at {top:0}.";
        }

        return null;
    }

    // ── Historical scores (A15, D-13) ────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AppraisalSettleDryRunReportDto> DryRunAsync(Guid? cycleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var targets = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId
                     && (a.Status == AppraisalStatus.Completed || a.Status == AppraisalStatus.Closed)
                     && (cycleId == null || a.AppraisalCycleId == cycleId))
            .OrderBy(a => a.AppraisalCycle.CycleName).ThenBy(a => a.AppraisalNumber)
            .Select(a => new
            {
                a.Id,
                a.AppraisalNumber,
                a.EmployeeId,
                EmployeeName = a.Employee.FirstName + " " + a.Employee.LastName,
                a.AppraisalCycle.CycleName,
                a.Status,
            })
            .ToListAsync(cancellationToken);

        var gradeNames = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId)
            .ToDictionaryAsync(g => g.Id, g => g.GradeName, cancellationToken);

        var employeeIds = targets.Select(t => t.EmployeeId).Distinct().ToList();
        var talentNow = (await _talentPoolMemberRepository.GetQueryable()
                .Where(m => m.TenantId == tenantId && employeeIds.Contains(m.EmployeeId))
                .Select(m => new { m.EmployeeId, m.LatestPerformanceRating })
                .ToListAsync(cancellationToken))
            .GroupBy(m => m.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.LatestPerformanceRating).FirstOrDefault(r => r != null));

        var mapRating = await _ratingResolver.GetMapperAsync(cancellationToken);
        var report = new AppraisalSettleDryRunReportDto { GeneratedAt = DateTime.UtcNow, CycleId = cycleId };

        foreach (var t in targets)
        {
            // Untracked: nothing computed here can reach a SaveChanges.
            var appraisal = await LoadForSettleAsync(t.Id, tracked: false, cancellationToken);
            var computed = await ComputeAsync(appraisal, cancellationToken);
            var settled = Round2(appraisal.CalibratedOverallScore) ?? computed;
            var settledGrade = await _ratingResolver.ResolveGradeDefinitionIdAsync(settled, cancellationToken);

            var row = new AppraisalSettleDryRunRowDto
            {
                AppraisalId = t.Id,
                AppraisalNumber = t.AppraisalNumber,
                EmployeeId = t.EmployeeId,
                EmployeeName = t.EmployeeName,
                CycleName = t.CycleName,
                Status = t.Status.ToString(),
                StoredScore = appraisal.OverallScore,
                SettledScore = settled,
                ComputedScore = computed,
                CalibratedOverallScore = appraisal.CalibratedOverallScore,
                StoredGrade = appraisal.OverallGradeDefinitionId is Guid sg && gradeNames.TryGetValue(sg, out var sgn) ? sgn : null,
                SettledGrade = settledGrade is Guid g && gradeNames.TryGetValue(g, out var gn) ? gn : null,
                StoredRating = mapRating(appraisal.OverallScore)?.ToString(),
                SettledRating = mapRating(settled)?.ToString(),
                TalentPoolRatingNow = talentNow.TryGetValue(t.EmployeeId, out var now) ? now?.ToString() : null,
            };
            row.Changed = row.StoredScore != row.SettledScore || row.StoredGrade != row.SettledGrade;

            report.Rows.Add(row);
        }

        report.Examined = report.Rows.Count;
        report.Changed = report.Rows.Count(r => r.Changed);
        return report;
    }
}

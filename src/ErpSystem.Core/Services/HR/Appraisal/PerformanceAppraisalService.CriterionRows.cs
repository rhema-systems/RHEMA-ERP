using System.Globalization;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The rows the appeal reads and the manager's peer review show (performance closure C6, lane D):
/// every criterion an appraisal was scored on — competency, KPI, goal — found by its criterion key and
/// described from the appraisal's own snapshot, and every score read as the one number its row scored
/// as.
/// </summary>
/// <remarks>
/// <para>The five appeal reads each described a row their own way. The appeal page listed
/// competencies only; the status read typed every template item a competency and named a KPI "Item";
/// HR's review never loaded a name and set every weight to 0; the post-remand comparison and the
/// outcome read a KPI's score from <c>NumericScore</c>, which a measured row holds only when calibration
/// or an appeal restated it — so a KPI read "—" everywhere its actual was the score. The manager's peer
/// review listed competencies only, at weight 0 (lane D).</para>
/// </remarks>
public partial class PerformanceAppraisalService
{
    /// <summary>One criterion of an appraisal as the rows' reads describe it.</summary>
    /// <param name="ItemType"><c>Competency</c>, <c>KPI</c>, <c>Goal</c>, or <c>Question</c> for a template item that is neither.</param>
    /// <param name="Weight">The row's weight within its section, frozen on the snapshot (A12).</param>
    /// <param name="ScaleTop">The top of the row's own scale: its highest grade band, or 100 for a measured row (A11).</param>
    private sealed record CriterionRow(
        Guid Key,
        Guid? TemplateItemId,
        Guid? CriterionConfigId,
        string ItemType,
        CriterionScoringMethod ScoringMethod,
        string Name,
        string? Description,
        string? SectionName,
        int? SectionWeight,
        int Weight,
        decimal ScaleTop,
        decimal? TargetValue,
        string? Unit,
        int SectionOrder,
        int RowOrder)
    {
        public bool IsMeasured => ScoringMethod == CriterionScoringMethod.Measured;
    }

    /// <summary>An appraisal's criteria for the appeal reads, and the scoring that reads a score on each.</summary>
    private sealed class CriterionRows
    {
        private readonly Dictionary<Guid, CriterionRow> _byKey;

        public CriterionRows(Dictionary<Guid, CriterionRow> byKey, AppraisalCriterionScoring scoring)
        {
            _byKey = byKey;
            Scoring = scoring;
        }

        public AppraisalCriterionScoring Scoring { get; }

        public CriterionRow? this[Guid key] => _byKey.GetValueOrDefault(key);

        /// <summary>A sort key that puts rows in the order the forms show them: by section, then within it.</summary>
        public (int, int) OrderOf(Guid key)
            => _byKey.TryGetValue(key, out var c) ? (c.SectionOrder, c.RowOrder) : (int.MaxValue, int.MaxValue);

        /// <summary>
        /// The one number a score scored as: a rated row's score on its own scale, a measured row's
        /// achievement percentage — its actual against the snapshot's target, or the percentage
        /// calibration or an appeal restated it to (D-22). What an appeal contests, and what the outcome
        /// says moved.
        /// </summary>
        public decimal? ScoreOf(CriterionScore? score)
        {
            if (score == null || !(score.TemplateItemId.HasValue || score.CriterionConfigId.HasValue)) return null;
            return Scoring.IsMeasured(score.CriterionKey())
                ? Round2(Scoring.AchievementPercent(score))
                : score.NumericScore;
        }

        /// <summary>The same number for a row of the remand snapshot — the scores as the appeal found them.</summary>
        public decimal? ScoreOf(AppraisalCriterionScoreSnapshot? row)
            => row == null
                ? null
                : ScoreOf(new CriterionScore
                {
                    TemplateItemId = row.TemplateItemId,
                    CriterionConfigId = row.CriterionConfigId,
                    NumericScore = row.NumericScore,
                    ActualValue = row.ActualValue,
                });

        /// <summary>A measured row whose achievement calibration or an appeal restated (D-22, A14).</summary>
        public bool Overridden(CriterionScore? score)
            => score?.NumericScore != null
               && (score.TemplateItemId.HasValue || score.CriterionConfigId.HasValue)
               && Scoring.IsMeasured(score.CriterionKey());

        /// <summary>The actual behind a measured row's score; a rated row has none.</summary>
        public decimal? ActualOf(CriterionScore? score)
            => score != null && (score.TemplateItemId.HasValue || score.CriterionConfigId.HasValue)
               && Scoring.IsMeasured(score.CriterionKey())
                ? score.ActualValue
                : null;

        private static decimal? Round2(decimal? value)
            => value is decimal v ? Math.Round(v, 2, MidpointRounding.AwayFromZero) : null;
    }

    /// <summary>Whether a score, appeal item or snapshot row names a criterion at all (every one written since lane L3 does).</summary>
    private static bool HasCriterion(CriterionScore score) => score.TemplateItemId.HasValue || score.CriterionConfigId.HasValue;

    private static bool HasCriterion(AppraisalAppealItem item) => item.TemplateItemId.HasValue || item.CriterionConfigId.HasValue;

    /// <summary>
    /// An appraisal's criteria as the appeal reads describe them: every row of its snapshot, and — for
    /// a key the snapshot does not hold, on an appraisal generated before it existed — the live
    /// template item.
    /// </summary>
    /// <remarks>
    /// ⚠ The snapshot is read with the query filters off and its own soft delete applied by hand, as
    /// the scoring reads it: a template item or section soft-deleted after generation still names the
    /// row it scored.
    /// </remarks>
    private async Task<CriterionRows> LoadCriterionRowsAsync(
        Guid appraisalId, IEnumerable<Guid> keys, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var scoring = await _scores.LoadScoringAsync(appraisalId, cancellationToken);

        var rows = await _criterionConfigRepository.GetQueryable()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.PerformanceAppraisalId == appraisalId && !c.IsDeleted)
            .Select(c => new
            {
                c.Id,
                c.TemplateItemId,
                c.EmployeeGoalId,
                c.WeightUsed,
                c.SectionWeightUsed,
                c.ItemLabel,
                c.Unit,
                c.KpiTargetValue,
                c.DisplayOrder,
                CompetencyName = c.TemplateItem!.Competency!.CriteriaName,
                CompetencyDescription = c.TemplateItem!.Competency!.Description,
                KpiName = c.TemplateItem!.KpiDefinition!.KpiName,
                KpiDescription = c.TemplateItem!.KpiDefinition!.Description,
                KpiUnit = c.TemplateItem!.KpiDefinition!.Unit,
                IsKpiItem = c.TemplateItem!.KpiDefinitionId != null,
                IsCompetencyItem = c.TemplateItem!.CompetencyId != null,
                CustomQuestion = c.TemplateItem!.CustomQuestion,
                ItemOrder = (int?)c.TemplateItem!.DisplayOrder,
                // A goal row names its section itself (lane L); a template row through its item, unless
                // the freeze (A0) put the section on the row.
                SectionName = c.Section!.SectionName ?? c.TemplateItem!.Section.SectionName,
                SectionOrder = (int?)c.Section!.DisplayOrder ?? (int?)c.TemplateItem!.Section.DisplayOrder,
                SectionWeight = (int?)c.Section!.Weight ?? (int?)c.TemplateItem!.Section.Weight,
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var byKey = new Dictionary<Guid, CriterionRow>();
        foreach (var row in rows)
        {
            var key = row.TemplateItemId ?? row.Id;
            if (byKey.ContainsKey(key)) continue;

            var measured = scoring.IsMeasured(key);
            byKey[key] = new CriterionRow(
                key,
                row.TemplateItemId,
                row.Id,
                ItemType: row.EmployeeGoalId.HasValue && !row.TemplateItemId.HasValue ? "Goal"
                    : row.IsKpiItem ? "KPI"
                    : row.IsCompetencyItem ? "Competency"
                    : "Question",
                ScoringMethod: measured ? CriterionScoringMethod.Measured : CriterionScoringMethod.Rated,
                Name: row.CompetencyName ?? row.KpiName ?? row.ItemLabel ?? row.CustomQuestion ?? string.Empty,
                Description: row.CompetencyDescription ?? row.KpiDescription,
                SectionName: row.SectionName,
                SectionWeight: row.SectionWeightUsed ?? row.SectionWeight,
                Weight: row.WeightUsed,
                ScaleTop: await _scores.GetScaleTopAsync(scoring, key, cancellationToken),
                TargetValue: measured ? row.KpiTargetValue : null,
                Unit: measured ? row.Unit ?? row.KpiUnit : null,
                SectionOrder: row.SectionOrder ?? int.MaxValue,
                RowOrder: row.ItemOrder ?? row.DisplayOrder ?? int.MaxValue);
        }

        // An appraisal generated before the snapshot existed is keyed by template item alone; its
        // rows are described from the live template, deleted or not, as its scoring reads them.
        var missing = keys.Distinct().Where(key => !byKey.ContainsKey(key)).ToList();
        if (missing.Count > 0)
        {
            var items = await _templateItemRepository
                .GetQueryableIncludingDeleted(i => i.TenantId == tenantId && missing.Contains(i.Id))
                .Select(i => new
                {
                    i.Id,
                    i.Weight,
                    i.KpiTargetValue,
                    i.DisplayOrder,
                    i.CustomQuestion,
                    CompetencyName = i.Competency!.CriteriaName,
                    CompetencyDescription = i.Competency!.Description,
                    KpiName = i.KpiDefinition!.KpiName,
                    KpiDescription = i.KpiDefinition!.Description,
                    KpiUnit = i.KpiDefinition!.Unit,
                    IsKpiItem = i.KpiDefinitionId != null,
                    IsCompetencyItem = i.CompetencyId != null,
                    SectionName = i.Section.SectionName,
                    SectionOrder = i.Section.DisplayOrder,
                    SectionWeight = i.Section.Weight,
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                // Resolved here first, so the scoring knows the item before a score on it is read.
                var scaleTop = await _scores.GetScaleTopAsync(scoring, item.Id, cancellationToken);
                var measured = scoring.IsMeasured(item.Id);
                byKey[item.Id] = new CriterionRow(
                    item.Id,
                    item.Id,
                    null,
                    ItemType: item.IsKpiItem ? "KPI" : item.IsCompetencyItem ? "Competency" : "Question",
                    ScoringMethod: measured ? CriterionScoringMethod.Measured : CriterionScoringMethod.Rated,
                    Name: item.CompetencyName ?? item.KpiName ?? item.CustomQuestion ?? string.Empty,
                    Description: item.CompetencyDescription ?? item.KpiDescription,
                    SectionName: item.SectionName,
                    SectionWeight: item.SectionWeight,
                    Weight: item.Weight,
                    ScaleTop: scaleTop,
                    TargetValue: measured ? item.KpiTargetValue : null,
                    Unit: measured ? item.KpiUnit : null,
                    SectionOrder: item.SectionOrder,
                    RowOrder: item.DisplayOrder);
            }
        }

        return new CriterionRows(byKey, scoring);
    }

    /// <summary>
    /// The manager's submitted scores with a value in them — what the appraisal's outcome was built
    /// from, and so the criteria an appeal may name (C8) and the list the appeal page offers (C6).
    /// </summary>
    private Task<List<CriterionScore>> SubmittedManagerScoresAsync(Guid appraisalId, CancellationToken cancellationToken)
        => TenantCriterionScoreQuery()
            .Where(cs => cs.EvaluatorEvaluation.AppraisalId == appraisalId
                      && cs.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager
                      && cs.EvaluatorEvaluation.SubmittedDate != null
                      && (cs.NumericScore != null || cs.ActualValue != null)
                      && (cs.TemplateItemId != null || cs.CriterionConfigId != null))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <summary>The manager's scores as they stand, by criterion key — a remand's re-scoring included, so a read that shows them asks the release rule first.</summary>
    private async Task<Dictionary<Guid, CriterionScore>> ManagerScoresByKeyAsync(Guid appraisalId, CancellationToken cancellationToken)
        => (await TenantCriterionScoreQuery()
                .Where(cs => cs.EvaluatorEvaluation.AppraisalId == appraisalId
                          && cs.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager
                          && (cs.TemplateItemId != null || cs.CriterionConfigId != null))
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .GroupBy(cs => cs.CriterionKey())
            .ToDictionary(g => g.Key, g => g.First());

    /// <summary>The remand snapshot's rows by criterion key — the manager's scores as the appeal found them — or null when there was no remand.</summary>
    private async Task<(AppraisalEvaluationSnapshot Snapshot, Dictionary<Guid, AppraisalCriterionScoreSnapshot> ByKey)?> RemandSnapshotAsync(
        Guid appraisalId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var snapshot = await _evaluationSnapshotRepository.GetQueryable()
            .Include(s => s.CriterionScores)
            .Where(s => s.TenantId == tenantId && s.AppraisalId == appraisalId && s.SnapshotReason == AppealRemandSnapshotReason)
            .OrderByDescending(s => s.SnapshotDate)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (snapshot == null) return null;

        var byKey = snapshot.CriterionScores
            .Where(s => !s.IsDeleted && (s.TemplateItemId.HasValue || s.CriterionConfigId.HasValue))
            .GroupBy(s => s.CriterionKey())
            .ToDictionary(g => g.Key, g => g.First());
        return (snapshot, byKey);
    }

    /// <summary>
    /// What an appealed item scored when the appeal was filed (D-38): kept on the item since slice
    /// C-b; for an appeal filed before it, the remand snapshot's (nothing moves the scores between the
    /// filing and a remand), else — while the appeal is open and nothing has moved — the manager's
    /// score now; else not known.
    /// </summary>
    private static decimal? ScoreWhenAppealed(
        AppraisalAppealItem item,
        CriterionRows criteria,
        Dictionary<Guid, AppraisalCriterionScoreSnapshot>? remandSnapshot,
        CriterionScore? managerNow,
        bool openAndUnmoved)
    {
        if (item.OriginalScore is decimal kept) return kept;
        if (remandSnapshot != null && remandSnapshot.TryGetValue(item.CriterionKey(), out var then))
            return criteria.ScoreOf(then);
        return openAndUnmoved ? criteria.ScoreOf(managerNow) : null;
    }

    /// <summary>
    /// The outcome in words (C-b): whether the appeal moved a score. An upheld appeal said "your
    /// appraisal scores were adjusted" whether or not anything had moved — the demo's own appeal, upheld
    /// at 87 → 87 on a profile where HR may not change scores, said so.
    /// </summary>
    private static string AppealOutcomeMessage(
        AppraisalAppealStatus decision, bool overallMoved, bool contestedChanged, decimal? overallBefore, decimal? overallNow)
    {
        static string N(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        if (decision == AppraisalAppealStatus.Rejected)
            return overallNow is decimal kept
                ? $"Your appeal was not upheld. The original scores stand: your overall score is {N(kept)}."
                : "Your appeal was not upheld. The original scores stand.";

        if (overallMoved && overallBefore is decimal from && overallNow is decimal to)
            return $"Your appeal was upheld and your appraisal was re-scored: your overall score moved from {N(from)} to {N(to)}.";

        if (overallMoved || contestedChanged)
            return overallNow is decimal now
                ? $"Your appeal was upheld and the scores you contested were changed; your overall score is {N(now)}."
                : "Your appeal was upheld and the scores you contested were changed.";

        return overallNow is decimal same
            ? $"Your appeal was upheld, but no score was changed: your overall score stays at {N(same)}. HR's notes explain the decision."
            : "Your appeal was upheld, but no score was changed. HR's notes explain the decision.";
    }
}

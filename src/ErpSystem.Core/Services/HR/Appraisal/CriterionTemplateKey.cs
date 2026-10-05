using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// How a criterion is found wherever scores, forms, appeals and calibration meet: by its
/// criterion key (below), and — for the one path still keyed by template item — by
/// <see cref="TemplateKey(CriterionScore)"/>.
/// </summary>
/// <remarks>
/// <para>Migration batch 1 of the performance closure made <c>TemplateItemId</c> nullable on the
/// criterion snapshot and the criterion score, because a goal row (lane L) has no template item.
/// Lane L-b moved the scoring, the forms, the saves, appeals, calibration and the HR review onto
/// the criterion key, and lane C6 the appeal page's list, which offered competencies only. The
/// appeal reads and the manager's peer review describe their rows through <c>PerformanceAppraisalService.LoadCriterionRowsAsync</c>.</para>
///
/// <para>A goal row reaching <see cref="TemplateKey(CriterionScore)"/> is therefore a defect, and
/// it fails there, naming the row, instead of colliding with another row under an empty key.</para>
/// </remarks>
internal static class CriterionTemplateKey
{
    public static Guid TemplateKey(this PerformanceAppraisalCriterionConfig config) =>
        config.TemplateItemId ?? throw NoTemplateItem("criterion snapshot row", config.Id);

    public static Guid TemplateKey(this CriterionScore score) =>
        score.TemplateItemId ?? throw NoTemplateItem("criterion score", score.Id);

    private static InvalidOperationException NoTemplateItem(string row, Guid id) =>
        new($"The {row} {id} has no template item. Goal rows are keyed by criterion config, " +
            "which this path does not read yet (performance closure lane L3).");

    // ── The criterion key (lane L3) ──────────────────────────────────────────────
    //
    // The key a criterion is found by wherever scores, forms, appeals and calibration meet: a
    // template row's template item id — the key it has always had, so every row written before
    // goal-driven scoring needs no migration — and a goal row's own snapshot id. Both are GUIDs of
    // different tables, so the two kinds never collide in one dictionary. A score, an appeal item
    // or a remand snapshot of a goal row carries the row's id as its CriterionConfigId and no
    // template item, and so resolves to the same key.

    public static Guid CriterionKey(this PerformanceAppraisalCriterionConfig config) =>
        config.TemplateItemId ?? config.Id;

    public static Guid CriterionKey(this CriterionScore score) =>
        score.TemplateItemId ?? score.CriterionConfigId ?? throw NoKey("criterion score", score.Id);

    public static Guid CriterionKey(this AppraisalCriterionScoreSnapshot snapshot) =>
        snapshot.TemplateItemId ?? snapshot.CriterionConfigId ?? throw NoKey("remand snapshot row", snapshot.Id);

    public static Guid CriterionKey(this AppraisalAppealItem item) =>
        item.TemplateItemId ?? item.CriterionConfigId ?? throw NoKey("appeal item", item.Id);

    /// <summary>The criterion an adjustment restates, or null for an overall restatement.</summary>
    public static Guid? CriterionKey(this CalibrationRatingAdjustment adjustment) =>
        adjustment.IsOverall ? null : adjustment.TemplateItemId ?? adjustment.CriterionConfigId;

    /// <summary>Whether a snapshot row is one of the employee's goals rather than a template item.</summary>
    public static bool IsGoalRow(this PerformanceAppraisalCriterionConfig config) =>
        config.EmployeeGoalId.HasValue && !config.TemplateItemId.HasValue;

    /// <summary>
    /// Whether a row is measured against a target rather than rated: the snapshot's own flag where
    /// it has one (every goal row), otherwise whether its template item is a KPI. Needs the
    /// template item loaded for a template row.
    /// </summary>
    public static bool IsMeasured(this PerformanceAppraisalCriterionConfig config) =>
        config.ScoringMethod.HasValue
            ? config.ScoringMethod == CriterionScoringMethod.Measured
            : config.TemplateItem?.KpiDefinitionId != null;

    /// <summary>The input a row takes on a form: an actual against a target, or a score on the bands.</summary>
    public static CriterionScoringMethod EffectiveScoringMethod(this PerformanceAppraisalCriterionConfig config) =>
        config.IsMeasured() ? CriterionScoringMethod.Measured : CriterionScoringMethod.Rated;

    /// <summary>
    /// The rows a section shows on a form, in order: a fixed section's template items that the
    /// snapshot holds, or a goals section's goal rows (lane L). A goals section's own template
    /// items, if it has any from before it became one, are not snapshotted and do not show.
    /// </summary>
    public static List<(PerformanceAppraisalCriterionConfig Config, AppraisalTemplateItem? Item)> FormRows(
        this AppraisalTemplateSection section,
        IReadOnlyDictionary<Guid, PerformanceAppraisalCriterionConfig> configsByKey)
        => section.Kind == AppraisalSectionKind.EmployeeGoals
            ? configsByKey.Values
                .Where(c => c.IsGoalRow() && c.AppraisalTemplateSectionId == section.Id)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => (c, (AppraisalTemplateItem?)null))
                .ToList()
            : section.TemplateItems
                .OrderBy(ti => ti.DisplayOrder)
                .Where(ti => configsByKey.ContainsKey(ti.Id))
                .Select(ti => (configsByKey[ti.Id], (AppraisalTemplateItem?)ti))
                .ToList();

    /// <summary>
    /// The name a row shows: its competency or KPI, or — on a goal row — the goal's title as the
    /// snapshot holds it. Needs the template item's competency and KPI loaded for a template row.
    /// </summary>
    public static string CriterionName(this PerformanceAppraisalCriterionConfig config) =>
        config.TemplateItem?.Competency?.CriteriaName
        ?? config.TemplateItem?.KpiDefinition?.KpiName
        ?? config.ItemLabel
        ?? string.Empty;

    /// <summary>
    /// The template item id for a key, when the key is a template row's; null for a goal row's.
    /// What a score, appeal item or snapshot row stores in <c>TemplateItemId</c>.
    /// </summary>
    public static Guid? TemplateItemIdFor(this IReadOnlyDictionary<Guid, PerformanceAppraisalCriterionConfig> configsByKey, Guid key) =>
        configsByKey.TryGetValue(key, out var config) ? config.TemplateItemId : key;

    private static InvalidOperationException NoKey(string row, Guid id) =>
        new($"The {row} {id} names neither a template item nor a criterion config.");

    /// <summary>
    /// The weight a section was scored at: frozen on its snapshot rows when the appraisal was
    /// generated (performance closure A0), and the live section only for rows written before the
    /// freeze. The forms showed the live weight, so a section re-weighted after generation read
    /// one number on screen while the score used another. A goals section has no template items;
    /// its rows name it directly (lane L).
    /// </summary>
    public static int ScoredWeight(
        this AppraisalTemplateSection section,
        IReadOnlyDictionary<Guid, PerformanceAppraisalCriterionConfig> configsByKey)
        => configsByKey.Values
               .Where(config => config.AppraisalTemplateSectionId == section.Id)
               .Select(config => config.SectionWeightUsed)
               .FirstOrDefault(weight => weight.HasValue)
           ?? section.TemplateItems
               .Select(item => configsByKey.TryGetValue(item.Id, out var config) ? config.SectionWeightUsed : null)
               .FirstOrDefault(weight => weight.HasValue)
           ?? section.Weight;
}

using ErpSystem.Core.Entities.HR.Performance;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// The template item a criterion row belongs to, for the code that still keys criteria by
/// template item.
/// </summary>
/// <remarks>
/// <para>Migration batch 1 of the performance closure made <c>TemplateItemId</c> nullable on the
/// criterion snapshot and the criterion score, because a goal row (lane L) has no template item.
/// Until lane L3 moves the scoring, form, appeal and calibration code onto the criterion config
/// id, every row those paths read is a template row.</para>
///
/// <para>A goal row reaching one of them is therefore a defect, and it fails here, naming the row,
/// instead of colliding with another row under an empty key. Every caller is a site lane L3
/// re-keys.</para>
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

    /// <summary>
    /// The weight a section was scored at: frozen on its snapshot rows when the appraisal was
    /// generated (performance closure A0), and the live section only for rows written before the
    /// freeze. The forms showed the live weight, so a section re-weighted after generation read
    /// one number on screen while the score used another.
    /// </summary>
    public static int ScoredWeight(
        this AppraisalTemplateSection section,
        IReadOnlyDictionary<Guid, PerformanceAppraisalCriterionConfig> configsByTemplateItemId)
        => section.TemplateItems
               .Select(item => configsByTemplateItemId.TryGetValue(item.Id, out var config) ? config.SectionWeightUsed : null)
               .FirstOrDefault(weight => weight.HasValue)
           ?? section.Weight;
}

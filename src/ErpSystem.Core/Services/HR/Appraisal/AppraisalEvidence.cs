using ErpSystem.Core.Entities.HR.Performance;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>A criterion of an appraisal whose competency requires evidence, by its criterion key.</summary>
public sealed class RequiredEvidence
{
    public Guid Key { get; init; }
    public string Name { get; init; } = string.Empty;
}

/// <summary>One evaluation entry, as the evidence rule reads it.</summary>
public sealed record EvidenceEntry(bool Scored, string? EvidenceLinks);

/// <summary>
/// <c>AppraisalCompetency.RequireEvidence</c> (performance closure B2): a score on such a criterion
/// needs an evidence link before an evaluation — the employee's, the manager's or a peer's — can be
/// submitted. The flag had no door (no DTO carried it), and no save stored the links the forms sent,
/// so the form's "required" marker could never be true and nothing refused anything.
/// </summary>
public static class AppraisalEvidence
{
    /// <summary>
    /// The appraisal's criteria that require evidence: template rows whose competency asks for it.
    /// A goal row has no competency and never does.
    /// </summary>
    public static IQueryable<RequiredEvidence> Required(IQueryable<PerformanceAppraisalCriterionConfig> configs, Guid appraisalId)
        => configs
            .Where(c => c.PerformanceAppraisalId == appraisalId
                     && c.TemplateItemId != null
                     && c.TemplateItem!.Competency != null
                     && c.TemplateItem.Competency.RequireEvidence)
            .Select(c => new RequiredEvidence
            {
                Key = c.TemplateItemId!.Value,
                Name = c.TemplateItem!.Competency!.CriteriaName,
            });

    /// <summary>
    /// Why the evaluation cannot be submitted, or null: a scored entry on a criterion that requires
    /// evidence carries no link. An entry with no score needs none.
    /// </summary>
    public static string? Missing(IReadOnlyCollection<RequiredEvidence> required, IReadOnlyDictionary<Guid, EvidenceEntry> entries)
    {
        var names = required
            .Where(r => entries.TryGetValue(r.Key, out var entry)
                     && entry.Scored
                     && string.IsNullOrWhiteSpace(entry.EvidenceLinks))
            .Select(r => r.Name)
            .Distinct()
            .ToList();

        return names.Count switch
        {
            0 => null,
            1 => $"\"{names[0]}\" needs an evidence link before this evaluation can be submitted.",
            _ => "These criteria need an evidence link before this evaluation can be submitted: "
                 + string.Join(", ", names.Select(n => $"\"{n}\"")) + ".",
        };
    }
}

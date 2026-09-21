using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// A language a person may speak or write — HR reference data (round 3, lane C1; decision D-16).
///
/// <para>Until now a candidate's languages were free text ("English", "english", "Englsh") and
/// nothing else in HR captured a language at all, while the shortlisting engine scored a
/// <c>Language</c> criterion against exactly that text. The catalogue gives the candidate form a
/// dropdown, the criterion a catalogue id to match on, and a future employee-side record a
/// target. The candidate row keeps its free-text name beside the link, mirrored from this row on
/// save, so old rows stay readable and a one-off language never needs a catalogue entry.</para>
///
/// <para>Retire, don't delete: a row still named by a candidate refuses deletion with a count.
/// Renaming does not rewrite the mirrored text on records already saved.</para>
/// </summary>
public class Language : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>ISO 639-1/-3 where one exists, otherwise a short local code (e.g. <c>AK</c>, <c>DAG</c>). Unique per tenant when set.</summary>
    [MaxLength(10)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

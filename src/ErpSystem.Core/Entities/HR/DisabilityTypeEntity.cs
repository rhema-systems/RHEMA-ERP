using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// A kind of disability — "Visual impairment", "Cerebral palsy" — as a maintained catalogue rather
/// than as free text on every record that asks (round 3, lane P2; register row E-5).
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> The demo asked for the disability to be a SETUP: tick the box, pick
/// from a list, add notes beside it — on the employee and on the dependant alike. Until now both
/// records held only <c>HasDisability</c> and a 500-character free-text description, so no report
/// could count how many staff have a visual impairment, and the same condition was spelt four ways.</para>
///
/// <para><b>⚠ The free text stays, as NOTES.</b> <c>DisabilityDescription</c> is not replaced: it is
/// where the person's own words, the accommodation needed, or a condition the list does not name
/// are written. A record may carry a type, notes, or both. Same shape as the relationship type
/// (round 2, lane D2) and the bank link (lane A).</para>
///
/// <para><b>⚠ Retired, and only deleted while unused.</b> <see cref="IsActive"/> withdraws a value
/// from new records without disturbing the ones already pointing at it; the delete is refused with
/// a count while any employee or dependant still names the row, because these are hard rows behind
/// Restrict foreign keys.</para>
///
/// <para>Seeded from the categories Ghana's census and the Persons with Disability Act (Act 715)
/// practice use — see <c>DisabilityTypeSeeder</c>. A tenant renames, retires and adds its own.</para>
/// </remarks>
public class DisabilityType : TenantEntity
{
    /// <summary>What a user picks — "Visual impairment". Unique per tenant.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code for imports and integrations, e.g. <c>VISUAL</c>. Unique per tenant when set.</summary>
    [MaxLength(50)]
    public string? Code { get; set; }

    /// <summary>The group a dropdown sections on and a headcount report counts by.</summary>
    public DisabilityCategory Category { get; set; } = DisabilityCategory.Other;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Where the value sits in a dropdown. Ties break on <see cref="Name"/>.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

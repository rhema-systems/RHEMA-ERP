using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// How one person is tied to another — "Spouse", "Former manager", "Landlord" — as a maintained
/// catalogue rather than as free text on every record that asks.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> Demo feedback round 2, register rows E-11a and E-11b. Four
/// columns spelt this out in free text and never agreed with each other:
/// <c>EmployeeReferee.Relationship</c>, <c>EmployeeGuarantor.Relationship</c>,
/// <c>EmployeeEmergencyContact.Relationship</c> and <c>JobCandidateReferee.Relationship</c>. One
/// person's next of kin was "wife", another's "Wife", a third's "spouse (married 2004)", and no
/// report could count them.</para>
///
/// <para><b>⚠ Dependants deliberately do NOT use this.</b> <see cref="DependentRelationship"/> is
/// an enum because eligibility logic branches on its members — <c>MaxChildAge</c> applies to Son
/// and Daughter and to nothing else — and <c>ExpatriateFamilyMember</c> reuses the same enum on
/// purpose. Turning that into tenant-maintained data would put a benefit rule at the mercy of a
/// typo in a lookup screen. <see cref="MapsToDependentRelationship"/> is the bridge: a catalogue
/// row can say which enum member it means, so one list can be shown where both live.</para>
///
/// <para><b>⚠ The free text stays.</b> Every consumer keeps its <c>Relationship</c> string, and the
/// service mirrors this row's <see cref="Name"/> into it whenever the id is set. Rows written
/// before the catalogue keep saying what they said, letters and exports that read the string keep
/// working, and a tie nobody has catalogued can still be typed. Same shape as the bank link
/// (lane A) and the certifying body (lane 3b).</para>
///
/// <para><b>⚠ Retired, and only deleted while unused.</b> <see cref="IsActive"/> withdraws a value
/// from new records without disturbing the ones already pointing at it; the delete is refused
/// outright while any of the four consumers still names the row, because these are hard rows
/// behind Restrict foreign keys.</para>
/// </remarks>
public class RelationshipType : TenantEntity
{
    /// <summary>What a user picks — "Spouse", "Former manager". Unique per tenant.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code for imports and integrations, e.g. <c>SPOUSE</c>. Unique per tenant when set.</summary>
    [MaxLength(50)]
    public string? Code { get; set; }

    /// <summary>
    /// Familial, professional, or neither — the axis each screen filters on.
    /// </summary>
    /// <remarks>
    /// This is the whole point of the table, not a tag. The next-of-kin screen offers Familial and
    /// Other; the referee screen offers Professional and Other for a professional or academic
    /// referee, and Familial and Other for a personal one; the guarantor screen offers all three,
    /// because a guarantor may be either. The service enforces those sets — a category the screen
    /// does not accept is refused with a sentence naming both.
    /// </remarks>
    public RelationshipCategory Category { get; set; } = RelationshipCategory.Other;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Which <see cref="DependentRelationship"/> member this row means, where it means one.
    /// </summary>
    /// <remarks>
    /// ⚠ Read-only bridge, never a write target. Nothing derives a dependant's enum from this
    /// column — the dependant screen still writes the enum directly. It exists so a screen that
    /// shows both catalogue rows and dependants can line them up, and so the seeded fourteen
    /// familial values can be recognised as the same fourteen the enum already holds.
    /// </remarks>
    public DependentRelationship? MapsToDependentRelationship { get; set; }

    /// <summary>Where the value sits in a dropdown. Ties break on <see cref="Name"/>.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

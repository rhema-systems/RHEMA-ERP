using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// A family member accompanying an expatriate assignee (Employee Master feedback: "Expatriate: no
/// issue dates, no resident permit, no family members").
/// </summary>
/// <remarks>
/// <para><b>Why a table and not more columns.</b> <c>ExpatriateAssignment.FamilyAccompanying</c> was
/// a bare bool, so the record could assert that a family had come and never say who they were. The
/// consequence is operational rather than cosmetic: each accompanying person needs their own
/// residence permit on their own clock, and a single flag can neither count them nor tell you whose
/// permit lapses next.</para>
///
/// <para><b>Deliberately NOT <c>EmployeeDependent</c>.</b> The two overlap and are not the same:
/// a dependant is a benefits and next-of-kin fact that persists for the whole employment, while
/// this is a fact about one posting — somebody can be a dependant who did not travel, or travel on
/// an assignment without being a benefits dependant. Folding them together would mean an expatriate
/// posting silently editing a person's benefits record.</para>
///
/// <para><b>No permit expiry sweep reads this yet.</b> The dates are stored so the question can be
/// asked; the chase is owed alongside the employee-document expiry engine (finish plan lane 3b/3c),
/// and one engine should serve both rather than two half-engines.</para>
/// </remarks>
public class ExpatriateFamilyMember : TenantEntity
{
    public Guid ExpatriateAssignmentId { get; set; }

    [ForeignKey(nameof(ExpatriateAssignmentId))]
    public virtual ExpatriateAssignment ExpatriateAssignment { get; set; } = null!;

    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// How they are related to the assignee.
    /// </summary>
    /// <remarks>
    /// ⚠ The SAME enum <see cref="EmployeeDependent"/> uses, with the same
    /// description-for-Other companion. The first draft made this free text and that was wrong on
    /// two counts: it would have been a second vocabulary for one idea, and the question this table
    /// exists to answer — "how many residence permits does this posting owe, and for whom" — needs
    /// a value you can group by, not prose.
    /// <para>The enum is NAMED for dependants and is a general kinship vocabulary. Minting an
    /// identical <c>FamilyRelationship</c> beside it would be worse than the slightly-off name.</para>
    /// </remarks>
    public DependentRelationship Relationship { get; set; }

    /// <summary>Used when <see cref="Relationship"/> is Other.</summary>
    [MaxLength(100)]
    public string? RelationshipDescription { get; set; }

    public Gender? Gender { get; set; }

    /// <summary>How they describe their gender, where Gender is Other.</summary>
    [MaxLength(100)]
    public string? GenderDescription { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(100)]
    public string? PassportNumber { get; set; }

    public DateOnly? PassportExpiryDate { get; set; }

    /// <summary>Their own residence permit — separate from the assignee's.</summary>
    [MaxLength(100)]
    public string? ResidentPermitNumber { get; set; }

    public DateOnly? ResidentPermitIssueDate { get; set; }

    public DateOnly? ResidentPermitExpiryDate { get; set; }

    /// <summary>When they arrived, where that differs from the assignment start.</summary>
    public DateOnly? ArrivalDate { get; set; }

    /// <summary>Set when they leave ahead of the assignee, so the row stays true without deletion.</summary>
    public DateOnly? DepartureDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// A row of the relationship catalogue, with the reach a retire or delete decision needs.
/// </summary>
public class RelationshipTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public RelationshipCategory Category { get; set; }
    public string? Description { get; set; }
    public DependentRelationship? MapsToDependentRelationship { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    /// <summary>
    /// How many records name this row, across all four consumers — referees, guarantors, next of
    /// kin and candidate referees.
    /// </summary>
    /// <remarks>
    /// ⚠ Not decoration. A row nothing names can be deleted; one that carries records can only be
    /// retired, and the screen has to be able to say which before the user clicks. The delete
    /// endpoint refuses on the same number.
    /// </remarks>
    public int UsageCount { get; set; }
}

public class CreateRelationshipTypeDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    public RelationshipCategory Category { get; set; } = RelationshipCategory.Other;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Which <c>DependentRelationship</c> member this row means, where it means one.
    /// </summary>
    /// <remarks>
    /// ⚠ Only meaningful on a FAMILIAL row — the dependant enum has no professional members — and
    /// the service refuses the pairing anywhere else rather than storing a mapping nothing can use.
    /// </remarks>
    public DependentRelationship? MapsToDependentRelationship { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateRelationshipTypeDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    /// <summary>
    /// The category.
    /// </summary>
    /// <remarks>
    /// ⚠ Changing this on a row records already point at is allowed but consequential: the screens
    /// filter on it, so a next of kin whose relationship is moved to Professional keeps the words
    /// it already stored while the dropdown stops offering the value. That is the correct outcome —
    /// rewriting history to tidy a category would be worse — but it is why the screen warns.
    /// </remarks>
    public RelationshipCategory Category { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DependentRelationship? MapsToDependentRelationship { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }
}

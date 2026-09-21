using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

#region Union DTOs

public class UnionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
    public int AgreementCount { get; set; }

    /// <summary>
    /// How many of this union's agreements are actually in force today.
    /// </summary>
    /// <remarks>
    /// Added in areas 19-23 slice 6, and distinct from <see cref="AgreementCount"/> on purpose:
    /// a register that reports "3 agreements" against a union whose last one lapsed in 2021 is
    /// reporting filing-cabinet depth, not industrial relations. Derived, never stored.
    /// </remarks>
    public int InForceAgreementCount { get; set; }

    public List<CollectiveBargainingAgreementDto> Agreements { get; set; } = new();

    // Round 3, lane U. The contact trio above is a MIRROR of the primary contact once contacts
    // exist; the rows themselves are here. The logo is a gated image (GET {id}/logo), never a URL.
    public List<UnionContactDto> Contacts { get; set; } = new();
    public UnionContactDto? PrimaryContact { get; set; }
    public List<UnionDocumentDto> Documents { get; set; } = new();
    public bool HasLogo { get; set; }
    public string? LogoFileName { get; set; }
}

/// <summary>A person the company deals with at the union (round 3, lane U; D-8): one of our employees or an external person.</summary>
public class UnionContactDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid UnionId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? ExternalName { get; set; }
    /// <summary>The employee's name, or the external name.</summary>
    public string DisplayName { get; set; } = string.Empty;
    public bool IsInternal => EmployeeId.HasValue;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
}

/// <summary>An employee (by id) OR an external person (by name); a role; whether they are the primary contact.</summary>
public class CreateUnionContactDto
{
    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? ExternalName { get; set; }

    [MaxLength(150)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [Required]
    [MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateUnionContactDto : CreateUnionContactDto
{
    [Required]
    public Guid Id { get; set; }
}

/// <summary>A file on the union's record (round 3, lane U). Download by id through the gated route; there is no path.</summary>
public class UnionDocumentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid UnionId { get; set; }
    public Guid? AgreementId { get; set; }
    public string? AgreementTitle { get; set; }
    public UnionDocumentKind Kind { get; set; }
    public string KindName => Kind.ToString();
    public string FileName { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class CreateUnionDto : CreateDtoBase
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(150)]
    public string? ContactPerson { get; set; }

    [MaxLength(150)]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    public string? ContactPhone { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateUnionDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(150)]
    public string? ContactPerson { get; set; }

    [MaxLength(150)]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    public string? ContactPhone { get; set; }

    public bool IsActive { get; set; }
}

#endregion

#region Collective Bargaining Agreement DTOs

public class CollectiveBargainingAgreementDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid UnionId { get; set; }
    public string? UnionName { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Summary { get; set; }
    public string? DocumentReference { get; set; }
    public bool IsActive { get; set; }

    /// <summary>
    /// Where this agreement stands today: <c>Inactive</c>, <c>Pending</c>, <c>Active</c> or <c>Expired</c>.
    /// </summary>
    /// <remarks>
    /// <para>Added in areas 19-23 slice 6. <c>IsActive</c> is a flag somebody sets and nothing ever
    /// clears, so it says whether the record was switched on — not whether the agreement is in force.
    /// Slice 6's probe measured the gap directly: an agreement running 2019-2021 came back
    /// <c>isActive: true</c> five years after it lapsed, and with nothing else on the payload every
    /// screen would have had to re-derive "in force" from two dates, each in its own way.</para>
    ///
    /// <para>Derived, never stored, from <c>CollectiveBargainingAgreementStatuses.Classify</c> — the
    /// one definition <see cref="IsInForce"/> and the union's in-force count also read.</para>
    /// </remarks>
    public string Status { get; set; } = string.Empty;

    /// <summary>Switched on, already started, and not yet expired.</summary>
    public bool IsInForce { get; set; }

    /// <summary>Files attached to this agreement — the signed copy among them (round 3, lane U).</summary>
    public int DocumentCount { get; set; }
}

public class CreateCollectiveBargainingAgreementDto : CreateDtoBase
{
    [Required]
    public Guid UnionId { get; set; }

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? DocumentReference { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateCollectiveBargainingAgreementDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? DocumentReference { get; set; }

    public bool IsActive { get; set; }
}

#endregion

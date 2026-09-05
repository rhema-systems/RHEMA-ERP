using System.ComponentModel.DataAnnotations;

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

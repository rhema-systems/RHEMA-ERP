using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// A trade union / labour organization that may represent a bargaining unit
/// (e.g. ICU, GTUC affiliate). Master data referenced by job descriptions and positions.
/// </summary>
public class Union : TenantEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Union name is required")]
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

    public virtual ICollection<CollectiveBargainingAgreement> Agreements { get; set; } = new List<CollectiveBargainingAgreement>();
}

/// <summary>
/// A collective bargaining agreement (CBA) negotiated with a union.
/// </summary>
public class CollectiveBargainingAgreement : TenantEntity
{
    public Guid UnionId { get; set; }

    [ForeignKey(nameof(UnionId))]
    public virtual Union Union { get; set; } = null!;

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    [Required(ErrorMessage = "Agreement title is required")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? Summary { get; set; }

    /// <summary>Optional reference to a stored document (path/URL/id).</summary>
    [MaxLength(500)]
    public string? DocumentReference { get; set; }

    public bool IsActive { get; set; } = true;
}

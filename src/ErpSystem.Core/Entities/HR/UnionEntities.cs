using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

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

    // ── Legacy contact trio (round 3, lane U; decision D-8) ───────────────────
    // Kept as a MIRROR of the primary UnionContact row: the service rewrites all three on every
    // contact save, so a job description or an offer letter that still reads them keeps working.
    // While a union has no contact rows they remain the typed values.

    [MaxLength(150)]
    public string? ContactPerson { get; set; }

    [MaxLength(150)]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    public string? ContactPhone { get; set; }

    public bool IsActive { get; set; } = true;

    // ── Logo (round 3, lane U; register row U-2) ──────────────────────────────
    // A gated image, never a public URL: served by GET api/hr/unions/{id}/logo through the same
    // download helper as an employee's photograph.

    public Guid? LogoFileUploadRecordId { get; set; }
    public Guid? LogoDocumentRecordId { get; set; }
    public Guid? LogoDocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? LogoFileName { get; set; }

    [MaxLength(100)]
    public string? LogoMimeType { get; set; }

    public long? LogoFileSizeBytes { get; set; }

    public virtual ICollection<CollectiveBargainingAgreement> Agreements { get; set; } = new List<CollectiveBargainingAgreement>();
    public virtual ICollection<UnionContact> Contacts { get; set; } = new List<UnionContact>();
    public virtual ICollection<UnionDocument> Documents { get; set; } = new List<UnionDocument>();
}

/// <summary>
/// A person the company deals with at a union (round 3, lane U; register row U-1; decision D-8):
/// an employee of ours who holds a union office (a shop steward, a branch chair), or an external
/// person (the general secretary at head office). One of them is the primary contact, and the
/// union's legacy contact trio mirrors that row.
/// </summary>
public class UnionContact : TenantEntity
{
    public Guid UnionId { get; set; }

    [ForeignKey(nameof(UnionId))]
    public virtual Union Union { get; set; } = null!;

    /// <summary>Set when the contact is one of our employees; the name is theirs.</summary>
    public Guid? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    /// <summary>The person's name when they are not an employee. Ignored when <see cref="EmployeeId"/> is set.</summary>
    [MaxLength(200)]
    public string? ExternalName { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    /// <summary>Their role at the union — Shop Steward, Branch Chair, General Secretary.</summary>
    [MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// A file on a union's record (round 3, lane U; register row U-2): the signed collective agreement
/// (<see cref="AgreementId"/> set, kind <see cref="UnionDocumentKind.CollectiveAgreement"/>), the
/// constitution, correspondence. Stored through the controlled-upload gate and served only by the
/// gated download — the same shape as a staff requisition's attachments.
/// </summary>
public class UnionDocument : TenantEntity
{
    public Guid UnionId { get; set; }

    [ForeignKey(nameof(UnionId))]
    public virtual Union Union { get; set; } = null!;

    /// <summary>The agreement this file is the copy of, when it is one.</summary>
    public Guid? AgreementId { get; set; }

    [ForeignKey(nameof(AgreementId))]
    public virtual CollectiveBargainingAgreement? Agreement { get; set; }

    public UnionDocumentKind Kind { get; set; } = UnionDocumentKind.Other;

    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    public long? FileSize { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; }

    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;

    /// <summary>Scanned controlled upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }
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

    /// <summary>
    /// Where the signed copy is filed, in words. The FILE itself is a <see cref="UnionDocument"/> of
    /// kind CollectiveAgreement naming this row (round 3, lane U) — nothing uploads through here.
    /// </summary>
    [MaxLength(500)]
    public string? DocumentReference { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<UnionDocument> Documents { get; set; } = new List<UnionDocument>();
}

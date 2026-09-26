using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

// =============================================================================
// DEMO FEEDBACK ROUND 2, LANE C3 — NAMED SETS (plan § 1.5, § 6.4)
//
// The PDF asks for the same thing three times: a named bundle of benefits, of
// skills, of certifications, attachable to a post in one move instead of row
// by row, "and it shouldn't be added as individual [row] again". So it is
// built once as one shape, three times over:
//
//   BenefitGroup     ──< BenefitGroupMember      >── BenefitPolicy
//   SkillSet         ──< SkillSetMember          >── Skill
//   CertificationSet ──< CertificationSetMember  >── Certification
//
//   EmployeePosition ──< EmployeePositionBenefitGroup >── BenefitGroup
//                    ──< PositionSkillSet             >── SkillSet
//                    ──< PositionCertificationSet     >── CertificationSet
//
// ⚠ THREE PARALLEL CLASSES, NOT A BASE CLASS. They are deliberately copied
// rather than inherited: a shared base would be a TPH hierarchy in EF, giving
// the three masters one table and one discriminator, and the member rows point
// at three unrelated catalogues with three different attribute sets. The shape
// is the pattern; the tables are separate.
//
// ⚠ A MEMBER CARRIES WHAT THE INDIVIDUAL ROW WOULD, so attaching a set is
// exactly equivalent to attaching its rows one at a time — that equivalence is
// what makes the duplicate rule (§ 6.4.2) statable at all. Benefit members are
// the exception and carry NO amount and NO expiry: a post needing a different
// amount for one benefit does not take that benefit from the set (Q-5).
// =============================================================================

// ─── Benefits ────────────────────────────────────────────────────────────────

/// <summary>A named bundle of benefit policies, attachable to a position (register row P-3).</summary>
public class BenefitGroup : TenantEntity
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code, unique per tenant where given. What the effective read cites as the source.</summary>
    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// A retired group stops being offered on new positions; posts already holding it keep it, and
    /// its members stay in their effective benefits. Retire rather than delete — see the delete guard.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public virtual ICollection<BenefitGroupMember> Members { get; set; } = new List<BenefitGroupMember>();
    public virtual ICollection<EmployeePositionBenefitGroup> Positions { get; set; } = new List<EmployeePositionBenefitGroup>();
}

/// <summary>
/// One benefit policy inside a group. ⚠ Deliberately carries neither amount nor expiry, unlike the
/// individual <c>EmployeePositionBenefit</c> row: a position that needs its own figure for a
/// benefit takes that benefit individually and not from a set (Q-5, default kept).
/// </summary>
public class BenefitGroupMember : TenantEntity
{
    [Required]
    public Guid BenefitGroupId { get; set; }

    [ForeignKey(nameof(BenefitGroupId))]
    public virtual BenefitGroup BenefitGroup { get; set; } = null!;

    [Required]
    public Guid PolicyId { get; set; }

    [ForeignKey(nameof(PolicyId))]
    public virtual BenefitPolicy Policy { get; set; } = null!;
}

// ─── Skills ──────────────────────────────────────────────────────────────────

/// <summary>A named bundle of skills, attachable to a position (register row S-3).</summary>
public class SkillSet : TenantEntity
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SkillSetMember> Members { get; set; } = new List<SkillSetMember>();
    public virtual ICollection<PositionSkillSet> Positions { get; set; } = new List<PositionSkillSet>();
}

/// <summary>
/// One skill inside a set, carrying the same three attributes the individual
/// <c>PositionSkillRequirement</c> row carries, so the set is equivalent to its rows.
/// </summary>
public class SkillSetMember : TenantEntity
{
    [Required]
    public Guid SkillSetId { get; set; }

    [ForeignKey(nameof(SkillSetId))]
    public virtual SkillSet SkillSet { get; set; } = null!;

    [Required]
    public Guid SkillId { get; set; }

    [ForeignKey(nameof(SkillId))]
    public virtual Skill Skill { get; set; } = null!;

    public SkillLevel RequiredLevel { get; set; } = SkillLevel.Beginner;

    /// <summary>True = required, false = preferred. Mirrors <c>PositionSkillRequirement.IsRequired</c>.</summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>Higher number = higher priority. Mirrors <c>PositionSkillRequirement.Priority</c>.</summary>
    public int Priority { get; set; } = 1;
}

// ─── Certifications ──────────────────────────────────────────────────────────

/// <summary>
/// A named bundle of credentials, attachable to a position (§ 1.5 — advised and accepted, so the
/// regulatory bundle a post needs is maintained in one place when the regulator changes it).
/// </summary>
public class CertificationSet : TenantEntity
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<CertificationSetMember> Members { get; set; } = new List<CertificationSetMember>();
    public virtual ICollection<PositionCertificationSet> Positions { get; set; } = new List<PositionCertificationSet>();
}

/// <summary>
/// One credential inside a set, carrying the <c>IsMandatory</c> flag the individual
/// <c>PositionCertificationRequirement</c> row carries.
/// </summary>
public class CertificationSetMember : TenantEntity
{
    [Required]
    public Guid CertificationSetId { get; set; }

    [ForeignKey(nameof(CertificationSetId))]
    public virtual CertificationSet CertificationSet { get; set; } = null!;

    [Required]
    public Guid CertificationId { get; set; }

    [ForeignKey(nameof(CertificationId))]
    public virtual Certification Certification { get; set; } = null!;

    /// <summary>False = any one accepted credential will do; true = this one is always needed.</summary>
    public bool IsMandatory { get; set; } = true;
}

// ─── Position links ──────────────────────────────────────────────────────────
//
// Attachment rows, nothing more: the attributes live on the set's members. Each
// is unique per (tenant, position, set) with the IsDeleted filter C2 established,
// so a soft-deleted attachment does not block re-attaching the same set.

/// <summary>A benefit group attached to a position.</summary>
public class EmployeePositionBenefitGroup : TenantEntity
{
    [Required]
    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    [Required]
    public Guid BenefitGroupId { get; set; }

    [ForeignKey(nameof(BenefitGroupId))]
    public virtual BenefitGroup BenefitGroup { get; set; } = null!;
}

/// <summary>A skill set attached to a position.</summary>
public class PositionSkillSet : TenantEntity
{
    [Required]
    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    [Required]
    public Guid SkillSetId { get; set; }

    [ForeignKey(nameof(SkillSetId))]
    public virtual SkillSet SkillSet { get; set; } = null!;
}

/// <summary>A certification set attached to a position.</summary>
public class PositionCertificationSet : TenantEntity
{
    [Required]
    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    [Required]
    public Guid CertificationSetId { get; set; }

    [ForeignKey(nameof(CertificationSetId))]
    public virtual CertificationSet CertificationSet { get; set; } = null!;
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

// =============================================================================
// DEMO FEEDBACK ROUND 2, LANE C2 — THE CERTIFICATION MODEL (plan § 1.3, § 6.3)
//
// A credential is a compliance object; a qualification is an education object.
// A licence expires, is renewed, is revoked, and its lapse is a regulatory or
// safety event. Modelling it as a Qualification row with Type = Certification
// left the expiry, the renewal, the evidence and the issuer relationship with
// nowhere to live except free text. This file gives them a home:
//
//   CertifyingBody (exists)
//    └── Certification                 the catalogue row
//          ├── SkillCertification       which credential(s) evidence a skill
//          ├── PositionCertificationRequirement   what a post must hold
//          └── EmployeeCertification    what a person holds, with evidence
//
// Qualification is untouched. Its Certification / License types stay valid for
// the rows that hold them; the data pass that may migrate them is Q-6.
// =============================================================================

/// <summary>A credential the catalogue knows, issued by one certifying body.</summary>
public class Certification : TenantEntity
{
    [Required]
    public Guid CertifyingBodyId { get; set; }

    [ForeignKey(nameof(CertifyingBodyId))]
    public virtual CertifyingBody CertifyingBody { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The body's own short code for it, where one exists. Unique per body.</summary>
    [MaxLength(50)]
    public string? Code { get; set; }

    public CertificationKind Kind { get; set; } = CertificationKind.Certification;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>How long a credential stays valid from issue. Null = does not expire.</summary>
    public int? ValidityMonths { get; set; }

    public bool RenewalRequired { get; set; }

    /// <summary>
    /// How far ahead of expiry the sweep warns. Null = the tenant's policy default
    /// (<c>CompanyHrPolicySettings.CertificationExpiryLeadDays</c>).
    /// </summary>
    public int? ExpiryNotificationLeadDays { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SkillCertification> SkillLinks { get; set; } = new List<SkillCertification>();
    public virtual ICollection<PositionCertificationRequirement> PositionRequirements { get; set; } = new List<PositionCertificationRequirement>();
    public virtual ICollection<EmployeeCertification> EmployeeCertifications { get; set; } = new List<EmployeeCertification>();

    /// <summary>The certification sets this credential belongs to (round 2, lane C3 — plan § 1.5).</summary>
    public virtual ICollection<CertificationSetMember> SetMemberships { get; set; } = new List<CertificationSetMember>();
}

/// <summary>
/// A credential that evidences a skill. A skill flagged <c>RequiresCertification</c> carries at
/// least one of these — the flag used to be a bare bool nothing consumed (register row S-1).
/// </summary>
public class SkillCertification : TenantEntity
{
    [Required]
    public Guid SkillId { get; set; }

    [ForeignKey(nameof(SkillId))]
    public virtual Skill Skill { get; set; } = null!;

    [Required]
    public Guid CertificationId { get; set; }

    [ForeignKey(nameof(CertificationId))]
    public virtual Certification Certification { get; set; } = null!;

    /// <summary>False = any one accepted credential will do; true = this one is always needed.</summary>
    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// A credential a post must hold. The position's <c>RequiresCertification</c> and
/// <c>RequiresLicense</c> switches stay; these rows are what they now mean (register row P-2).
/// </summary>
public class PositionCertificationRequirement : TenantEntity
{
    [Required]
    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    [Required]
    public Guid CertificationId { get; set; }

    [ForeignKey(nameof(CertificationId))]
    public virtual Certification Certification { get; set; } = null!;

    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>What a person holds: one credential, its dates, its evidence, its verification.</summary>
/// <remarks>
/// <para>Status is computed, never stored, except for revocation: Valid / ExpiringSoon / Expired
/// fall out of <see cref="ExpiresOn"/> and the lead days; <see cref="IsRevoked"/> is the one thing
/// only a person can say.</para>
/// <para><b>Three ids, never a path</b> for the evidence — the controlled upload gate, as on every
/// other HR attachment since lane 3a.</para>
/// <para><see cref="VerifiedById"/> is stamped from the token by the controller, never read off
/// a DTO — the actor lesson.</para>
/// </remarks>
public class EmployeeCertification : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required]
    public Guid CertificationId { get; set; }

    [ForeignKey(nameof(CertificationId))]
    public virtual Certification Certification { get; set; } = null!;

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    public DateOnly? IssuedOn { get; set; }

    /// <summary>Defaults to IssuedOn + the catalogue's ValidityMonths when omitted and both exist.</summary>
    public DateOnly? ExpiresOn { get; set; }

    public bool IsRevoked { get; set; }

    public DateOnly? RevokedOn { get; set; }

    [MaxLength(500)]
    public string? RevocationReason { get; set; }

    // ── The evidence, through the gate ──────────────────────────────────────

    public Guid? EvidenceFileUploadRecordId { get; set; }

    public Guid? EvidenceDocumentRecordId { get; set; }

    public Guid? EvidenceDocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? EvidenceFileName { get; set; }

    [MaxLength(150)]
    public string? EvidenceMimeType { get; set; }

    public long? EvidenceFileSizeBytes { get; set; }

    // ── Verification ─────────────────────────────────────────────────────────

    public bool IsVerified { get; set; }

    public Guid? VerifiedById { get; set; }

    [ForeignKey(nameof(VerifiedById))]
    public virtual Employee? VerifiedBy { get; set; }

    public DateTime? VerifiedOn { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>Employee skills this credential evidences.</summary>
    public virtual ICollection<EmployeeSkill> EvidencedSkills { get; set; } = new List<EmployeeSkill>();
}

// =============================================================================
// THE CERTIFICATION EXPIRY SWEEP — the identification-expiry pair, one family over
// (IdentificationExpiryReminderEntities.cs). A run header plus one dispatch row
// per reminder, keyed so a second sweep on the same day raises nothing twice.
// =============================================================================

/// <summary>One pass of the certification-expiry sweep.</summary>
public class CertificationExpiryReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" for the daily pass, "Manual" when somebody ran it.</summary>
    [Required]
    [MaxLength(30)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<CertificationExpiryDispatchLog> DispatchLogs { get; set; }
        = new List<CertificationExpiryDispatchLog>();
}

/// <summary>
/// One reminder the sweep raised — whose credential, which certification, when it expires, and
/// the key that stops it being raised again tomorrow.
/// </summary>
/// <remarks>
/// Tier 1 (inside the lead window) routes to the holder; tier 2 (expired) routes to HR, because
/// it has stopped being personal admin and become a compliance gap. HR sees every row either way.
/// </remarks>
public class CertificationExpiryDispatchLog : TenantEntity
{
    [Required]
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual CertificationExpiryReminderRun Run { get; set; } = null!;

    /// <summary><c>CertificationExpiring</c> or <c>CertificationExpired</c>.</summary>
    [Required]
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>The credential. No FK — the row survives the credential being corrected or replaced.</summary>
    public Guid EmployeeCertificationId { get; set; }

    public Guid CertificationId { get; set; }

    /// <summary>Certification name and number, so the reader can find it without opening it.</summary>
    [MaxLength(300)]
    public string? Reference { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>Negative once the date has passed.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>1 inside the lead window; 2 once expired.</summary>
    public int EscalationTier { get; set; }

    public Guid? RoutedToEmployeeId { get; set; }

    /// <summary>Kind + credential + due date + tier. A renewal changes the date and re-arms the ladder.</summary>
    [Required]
    [MaxLength(200)]
    public string DedupeKey { get; set; } = string.Empty;
}

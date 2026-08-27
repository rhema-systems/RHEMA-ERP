using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Policies;

/// <summary>
/// A company policy in the staff-facing library, and the acknowledgement it may require
/// (area 25 slice 12d, decision D7).
/// </summary>
/// <remarks>
/// <para><b>The file lives in the central DMS, not here.</b> Only the register row and a binding
/// to <c>CentralDocumentRecord</c> are stored, which is the rule <c>SheControlledDocument</c>
/// states for its own controlled documents: "SHE deliberately mints no parallel version store."
/// A policy PDF and a SHE procedure are the same kind of object and should not be versioned two
/// different ways.</para>
///
/// <para><b>A new version is a new row, not an edit.</b> Superseding a policy archives the old
/// one and publishes a replacement, because every acknowledgement points at the version it was
/// given for: silently editing the document under a signature would turn "I agree to this" into
/// "I agree to whatever this becomes", which is exactly what an acknowledgement exists to
/// prevent. <see cref="SupersedesPolicyId"/> keeps the chain readable.</para>
///
/// <para><b>Acknowledgement is optional per policy.</b> A staff handbook is published to be
/// read; a code of conduct is published to be signed. <see cref="RequiresAcknowledgement"/>
/// separates the two, and <see cref="AcknowledgementText"/> is the declaration the employee is
/// agreeing to — frozen onto each signature so that changing the wording later cannot rewrite
/// what somebody already agreed.</para>
/// </remarks>
public class HrPolicyDocument : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string PolicyNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Summary { get; set; }

    public HrPolicyCategory Category { get; set; } = HrPolicyCategory.General;

    /// <summary>The version as people cite it — "v2.1", "2026 revision".</summary>
    [MaxLength(30)]
    public string? VersionLabel { get; set; }

    public HrPolicyStatus Status { get; set; } = HrPolicyStatus.Draft;

    public DateTime? EffectiveFrom { get; set; }

    /// <summary>When somebody should look at it again. Advisory; nothing enforces it yet.</summary>
    public DateTime? ReviewOn { get; set; }

    /// <summary>The version this one replaces, when it replaces one.</summary>
    public Guid? SupersedesPolicyId { get; set; }

    [ForeignKey(nameof(SupersedesPolicyId))]
    public virtual HrPolicyDocument? Supersedes { get; set; }

    // ── Acknowledgement ───────────────────────────────────────────────────────
    public bool RequiresAcknowledgement { get; set; }

    /// <summary>
    /// The declaration the employee agrees to. Copied onto every signature, so later edits
    /// cannot rewrite what somebody has already agreed to.
    /// </summary>
    [MaxLength(4000)]
    public string? AcknowledgementText { get; set; }

    /// <summary>How long people have from publication. Null means no deadline is stated.</summary>
    public int? AcknowledgementDueDays { get; set; }

    // ── Publication ───────────────────────────────────────────────────────────
    public DateTime? PublishedAt { get; set; }

    public Guid? PublishedById { get; set; }

    [ForeignKey(nameof(PublishedById))]
    public virtual Employee? PublishedBy { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public Guid? ArchivedById { get; set; }

    [ForeignKey(nameof(ArchivedById))]
    public virtual Employee? ArchivedBy { get; set; }

    // ── The document itself, through the shared controlled-upload gate ───────
    public Guid? FileUploadRecordId { get; set; }
    public Guid? DocumentRecordId { get; set; }
    public Guid? DocumentVersionId { get; set; }

    /// <summary>Storage path — NOT a URL, and never rendered as one.</summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(100)]
    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    public virtual ICollection<HrPolicyAudience> Audiences { get; set; }
        = new List<HrPolicyAudience>();

    public virtual ICollection<HrPolicyAcknowledgement> Acknowledgements { get; set; }
        = new List<HrPolicyAcknowledgement>();

    [NotMapped]
    public bool IsLive =>
        Status == HrPolicyStatus.Published
        && (EffectiveFrom is null || EffectiveFrom <= DateTime.UtcNow);

    /// <summary>A policy with no document attached cannot honestly be asked to be read.</summary>
    [NotMapped]
    public bool HasDocument => FileUploadRecordId.HasValue;
}

/// <summary>
/// Who a policy applies to. Same include-then-exclude shape as an announcement's audience, and
/// resolved by the same <c>IHrAudienceResolver</c>.
/// </summary>
public class HrPolicyAudience : TenantEntity
{
    [Required]
    public Guid PolicyId { get; set; }

    [ForeignKey(nameof(PolicyId))]
    public virtual HrPolicyDocument Policy { get; set; } = null!;

    public HrAudienceTargetType TargetType { get; set; }

    /// <summary>Null — and only null — for <c>AllEmployees</c>.</summary>
    public Guid? TargetId { get; set; }

    public bool IsExclusion { get; set; }
}

/// <summary>
/// One employee's answer to one policy: that they read it and agreed, or read it and refused.
/// </summary>
/// <remarks>
/// <para><b>Rows exist only where somebody ACTED.</b> "Outstanding" is the absence of a row, not
/// a Pending row — which is why there is no <c>Pending</c> outcome. Pre-seeding a row per
/// targeted employee at publication would mean 7,900 rows for a tenant-wide policy, would
/// freeze the audience at publish (contradicting the read-time membership the resolver is built
/// on), and would silently miss anybody who joins afterwards. The outstanding roster is instead
/// COMPUTED: resolve the audience now, subtract those who have signed.</para>
///
/// <para><b>The text is frozen onto the row.</b> <see cref="AcknowledgementText"/> is copied
/// from the policy at the moment of signing, so amending the declaration later cannot rewrite
/// what somebody already agreed to. Same reasoning as the letter frozen at issue in slice 12b,
/// and as <c>OrientationAcknowledgement</c>, whose shape this follows.</para>
///
/// <para><b>A decline is kept even after a later signature.</b> <see cref="DeclinedAt"/> and
/// <see cref="DeclineReason"/> survive somebody changing their mind, because "refused, then
/// signed after a conversation" is a materially different fact from "signed", and the second
/// one is the one an employer would want to remember.</para>
///
/// <para>There is deliberately no "presented at" column. Rows are created when somebody acts,
/// so a presented timestamp would either require writing to the database on a read, or be
/// invented at signing time — and an invented timestamp on an evidentiary record is worse than
/// no timestamp at all.</para>
/// </remarks>
public class HrPolicyAcknowledgement : TenantEntity
{
    [Required]
    public Guid PolicyId { get; set; }

    [ForeignKey(nameof(PolicyId))]
    public virtual HrPolicyDocument Policy { get; set; } = null!;

    /// <summary>Always the token's employee — never supplied in a payload.</summary>
    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public HrPolicyAcknowledgementOutcome Outcome { get; set; }

    /// <summary>The declaration as it read when they agreed to it.</summary>
    [MaxLength(4000)]
    public string? AcknowledgementText { get; set; }

    public DateTime? SignedAt { get; set; }

    public DateTime? DeclinedAt { get; set; }

    [MaxLength(1000)]
    public string? DeclineReason { get; set; }

    /// <summary>Where the signature came from. Server-recorded, never sent by the client.</summary>
    [MaxLength(50)]
    public string? SignatureIpAddress { get; set; }

    /// <summary>
    /// A hash over the policy id, employee id, frozen text and timestamp — so a later change to
    /// any of them is detectable rather than merely unlikely.
    /// </summary>
    [MaxLength(128)]
    public string? SignatureHash { get; set; }
}

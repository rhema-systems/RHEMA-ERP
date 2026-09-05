using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Announcements;

/// <summary>
/// Something the organisation is telling its staff (area 25 slice 12c, decision D7).
/// </summary>
/// <remarks>
/// <para><b><see cref="Body"/> is PLAIN TEXT, deliberately.</b> An announcement is broadcast to
/// everybody, so storing HR-authored HTML and rendering it would be a stored-XSS vector aimed at
/// the entire staff — the portal renders this as text with line breaks preserved, never as
/// markup. (Contrast the HR letters of slice 12b, which ARE HTML: those are composed by the
/// server from a template, and no user-supplied string reaches them as markup.) Rich text can be
/// added later, but it needs a sanitiser and that is its own decision, not a field type.</para>
///
/// <para><b>No per-employee read rows.</b> A "seen" row per employee per announcement is a large
/// table that answers a question nobody asked: an announcement is a notice board, not an
/// obligation. When the organisation genuinely needs to prove somebody was told, that is a
/// policy acknowledgement (slice 12d) — a different thing, with a signature, a frozen text and a
/// declinable outcome.</para>
///
/// <para><b>Archived, never deleted.</b> What was announced, to whom, and when is a record: an
/// announcement withdrawn after it caused a decision still needs to be findable.</para>
/// </remarks>
public class HrAnnouncement : TenantEntity
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>The one line that appears on the portal dashboard. Falls back to the body's
    /// opening when unset — a dashboard row with no summary is not a useful dashboard row.</summary>
    [MaxLength(500)]
    public string? Summary { get; set; }

    /// <summary>Plain text. See the remarks: this is never rendered as markup.</summary>
    [Required]
    public string Body { get; set; } = string.Empty;

    public HrAnnouncementCategory Category { get; set; } = HrAnnouncementCategory.General;

    public HrAnnouncementStatus Status { get; set; } = HrAnnouncementStatus.Draft;

    /// <summary>Holds it at the top of the list, above the newest.</summary>
    public bool IsPinned { get; set; }

    // ── Publication ───────────────────────────────────────────────────────────
    public DateTime? PublishedAt { get; set; }

    public Guid? PublishedById { get; set; }

    [ForeignKey(nameof(PublishedById))]
    public virtual Employee? PublishedBy { get; set; }

    /// <summary>
    /// When it starts showing. Null means the moment it is published — the common case, and the
    /// reason this is nullable rather than defaulted at publish time.
    /// </summary>
    public DateTime? EffectiveFrom { get; set; }

    /// <summary>
    /// When it stops showing. Null means it stays until archived. A staff notice about a car
    /// park closure next Tuesday should not still be on the dashboard in March.
    /// </summary>
    public DateTime? ExpiresOn { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public Guid? ArchivedById { get; set; }

    [ForeignKey(nameof(ArchivedById))]
    public virtual Employee? ArchivedBy { get; set; }

    // ── An optional attachment, through the shared controlled-upload gate ─────
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

    public virtual ICollection<HrAnnouncementAudience> Audiences { get; set; }
        = new List<HrAnnouncementAudience>();

    /// <summary>True when it should be visible to its audience right now.</summary>
    [NotMapped]
    public bool IsLive =>
        Status == HrAnnouncementStatus.Published
        && (EffectiveFrom is null || EffectiveFrom <= DateTime.UtcNow)
        && (ExpiresOn is null || ExpiresOn >= DateTime.UtcNow);
}

/// <summary>
/// One rule about who an announcement is for. Rules are additive, except
/// <see cref="IsExclusion"/> ones, which are subtracted after everything else has been added.
/// </summary>
/// <remarks>
/// The include-then-exclude shape is what makes "everyone except the depot" expressible without
/// listing every unit that is not the depot — the same reason <c>AwardTarget</c> carries its own
/// exclusion flag.
/// </remarks>
public class HrAnnouncementAudience : TenantEntity
{
    [Required]
    public Guid AnnouncementId { get; set; }

    [ForeignKey(nameof(AnnouncementId))]
    public virtual HrAnnouncement Announcement { get; set; } = null!;

    public HrAudienceTargetType TargetType { get; set; }

    /// <summary>
    /// The unit, department, position, level, location or employee. Null — and only null — for
    /// <see cref="HrAudienceTargetType.AllEmployees"/>.
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>Subtracted from the audience rather than added to it.</summary>
    public bool IsExclusion { get; set; }
}

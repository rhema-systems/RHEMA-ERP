using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// ANNOUNCEMENTS — area 25 slice 12c (decision D7)
// ============================================================================

/// <summary>One audience rule, as the desk form sends it.</summary>
public sealed class HrAnnouncementAudienceDto
{
    public Guid? Id { get; set; }

    [Required]
    public HrAudienceTargetType TargetType { get; set; }

    /// <summary>Required for every target type except <c>AllEmployees</c>.</summary>
    public Guid? TargetId { get; set; }

    /// <summary>Subtracted from the audience rather than added to it.</summary>
    public bool IsExclusion { get; set; }

    /// <summary>Resolved name for display — the screen never has to look it up itself.</summary>
    public string? TargetName { get; set; }
}

/// <summary>
/// Not sealed: <see cref="UpdateHrAnnouncementDto"/> derives from it, because editing an
/// announcement means restating exactly the same payload.
/// </summary>
public class CreateHrAnnouncementDto
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Summary { get; set; }

    /// <summary>Plain text. It is never rendered as markup — see the entity's remarks.</summary>
    [Required]
    public string Body { get; set; } = string.Empty;

    public HrAnnouncementCategory Category { get; set; } = HrAnnouncementCategory.General;

    public bool IsPinned { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? ExpiresOn { get; set; }

    /// <summary>
    /// Who it is for. An empty list reaches nobody, deliberately — publish refuses it rather
    /// than quietly broadcasting to everyone.
    /// </summary>
    public List<HrAnnouncementAudienceDto> Audiences { get; set; } = [];
}

/// <summary>
/// Identical to the create payload — an announcement is edited by restating it whole, and the
/// audience list is a replace-set (omitting a rule removes it).
/// </summary>
public sealed class UpdateHrAnnouncementDto : CreateHrAnnouncementDto
{
}

public sealed class HrAnnouncementDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Body { get; set; } = string.Empty;

    public HrAnnouncementCategory Category { get; set; }
    public string CategoryName => Category.ToString();

    public HrAnnouncementStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public bool IsPinned { get; set; }
    public bool IsLive { get; set; }

    public DateTime? PublishedAt { get; set; }
    public string? PublishedByName { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? ArchivedByName { get; set; }

    public bool HasAttachment { get; set; }
    public string? FileName { get; set; }

    public IEnumerable<HrAnnouncementAudienceDto> Audiences { get; set; } = [];

    /// <summary>
    /// How many people this actually reaches, resolved from the rules. Desk-side only: the
    /// sender should see it BEFORE publishing, not discover it afterwards.
    /// </summary>
    public int? AudienceCount { get; set; }
}

/// <summary>
/// The employee's view. Deliberately smaller than the desk DTO: an employee reading a staff
/// notice has no business knowing the targeting rules that selected them, or how many other
/// people got it.
/// </summary>
public sealed class MyAnnouncementDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Body { get; set; } = string.Empty;

    public HrAnnouncementCategory Category { get; set; }
    public string CategoryName => Category.ToString();

    public bool IsPinned { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? PublishedByName { get; set; }
    public DateTime? ExpiresOn { get; set; }

    public bool HasAttachment { get; set; }
    public string? FileName { get; set; }
}

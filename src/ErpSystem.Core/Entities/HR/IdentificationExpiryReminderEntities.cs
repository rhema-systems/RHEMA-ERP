using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR;

// =============================================================================
// LANE 3b — THE IDENTIFICATION EXPIRY SWEEP
//
// EmployeeIdentificationCard has carried an ExpiryDate since it shipped and
// NOTHING read it: no sweep, no reminder, no report. IdentificationType now
// carries ExpiryNotificationLeadDays, and this is what acts on it — the two were
// built together, because a lead time nothing reads is a setting that only looks
// like a feature.
//
// The shape follows SeparationReminderRun / ProbationReminderRun deliberately: a
// run header plus one dispatch row per reminder, keyed so a second sweep on the
// same day does not raise the same thing twice. This is the sixth area to carry
// its own pair; a shared reminder store would be better, but retrofitting five
// working sweeps is a different job from adding a sixth correctly.
// =============================================================================

/// <summary>One pass of the identification-expiry sweep.</summary>
public class IdentificationExpiryReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" for the daily pass, "Manual" when somebody ran it.</summary>
    [Required]
    [MaxLength(30)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<IdentificationExpiryDispatchLog> DispatchLogs { get; set; }
        = new List<IdentificationExpiryDispatchLog>();
}

/// <summary>
/// One reminder the sweep raised — whose card, which type, when it expires, and the key that stops
/// it being raised again tomorrow.
/// </summary>
/// <remarks>
/// <para><b>Who sees it.</b> HR sees every row: the read is gated on a policy, not filtered by
/// recipient, exactly as the separation and probation logs are. So this table IS HR's view of what
/// is expiring across the workforce.</para>
///
/// <para><b>Who owns it.</b> <see cref="RoutedToEmployeeId"/> is an ownership stamp, not a
/// visibility switch. Tier 1 routes to the card HOLDER — their document, their renewal. Tier 2,
/// once the date has passed, routes to HR, because it has stopped being personal admin and become a
/// compliance gap.</para>
///
/// <para>⚠ <b>One card, one row per tier.</b> Raising a second row to HR at tier 1 would double the
/// log and make "how many cards are expiring" ambiguous — every card counted twice. HR already sees
/// tier 1; it does not need a copy addressed to it.</para>
/// </remarks>
public class IdentificationExpiryDispatchLog : TenantEntity
{
    [Required]
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual IdentificationExpiryReminderRun Run { get; set; } = null!;

    /// <summary><c>IdentificationExpiring</c> or <c>IdentificationExpired</c>.</summary>
    [Required]
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Whose card it is.</summary>
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>The card itself. No FK — the row survives the card being corrected or replaced.</summary>
    public Guid EmployeeIdentificationCardId { get; set; }

    /// <summary>The type, so a reader can see whether it is a passport or a works pass.</summary>
    public Guid IdentificationTypeId { get; set; }

    /// <summary>Type name and document number, so the reader can find the card without opening it.</summary>
    [MaxLength(200)]
    public string? Reference { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>Negative once the date has passed, which is exactly when someone should look.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>
    /// 1 while the card is still valid and inside its type's lead days; 2 once it has expired.
    /// </summary>
    /// <remarks>
    /// The tier is what moves ownership. It is deliberately not a count of how many times the
    /// reminder has fired: an expired card is tier 2 whether it expired yesterday or last year, and
    /// a ladder that climbed for ever would say less each rung, not more.
    /// </remarks>
    public int EscalationTier { get; set; }

    /// <summary>
    /// Whose responsibility this is now. Null when it cannot be resolved.
    /// </summary>
    /// <remarks>
    /// ⚠ A reminder with no owner still fires, and says so. A card belonging to somebody whose
    /// employee record is inactive is the case HR most needs surfaced, not the one to suppress —
    /// the same call the probation sweep made when no confirming authority resolved.
    /// </remarks>
    public Guid? RoutedToEmployeeId { get; set; }

    /// <summary>
    /// What makes a reminder the same reminder: kind + card + due date + tier.
    /// </summary>
    /// <remarks>
    /// A daily sweep re-raises nothing already sent at the same tier, so running it twice in one
    /// morning is harmless and running it every morning does not become noise. ⚠ Changing the card's
    /// expiry date changes the key, which re-arms the ladder — deliberately, because a renewed
    /// document is a new deadline.
    /// </remarks>
    [Required]
    [MaxLength(200)]
    public string DedupeKey { get; set; } = string.Empty;
}

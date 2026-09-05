using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR;

// =============================================================================
// AREA 9b — THE REMINDER SWEEP (slice 10)
//
// FR-HR-111: "raise alerts 30 days ahead of a due event (retirement, contract
// expiry, probation, inspection…)". Retirement and contract expiry are this
// area's two; the rest belong to their own areas and already have engines.
//
// The shape follows ProbationReminderRun / DisciplineReminderRun deliberately —
// a run header plus one dispatch row per reminder, keyed so a second sweep on
// the same day does not send the same thing twice.
// =============================================================================

/// <summary>One pass of the separation reminder sweep.</summary>
public class SeparationReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" for the daily pass, "Manual" when somebody ran it.</summary>
    [Required]
    [MaxLength(30)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<SeparationReminderDispatchLog> DispatchLogs { get; set; }
        = new List<SeparationReminderDispatchLog>();
}

/// <summary>
/// One reminder the sweep raised — what it was about, who it went to, and the key that stops it
/// being raised again tomorrow.
/// </summary>
public class SeparationReminderDispatchLog : TenantEntity
{
    [Required]
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual SeparationReminderRun Run { get; set; } = null!;

    /// <summary>
    /// What kind of reminder — <c>RetirementApproaching</c>, <c>ContractExpiring</c>,
    /// <c>ClearanceOutstanding</c>, <c>SettlementAwaitingReview</c>,
    /// <c>SettlementApprovedNotCompleted</c>.
    /// </summary>
    [Required]
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>The employee the reminder is about. Every kind here is about somebody leaving.</summary>
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// The separation, where one exists. Null for the two forward-looking kinds — a retirement or a
    /// contract running out is a reminder that no separation has been raised yet.
    /// </summary>
    public Guid? SeparationId { get; set; }

    /// <summary>A separation number, a contract number — whatever the reader needs to find it.</summary>
    [MaxLength(60)]
    public string? Reference { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>Negative once the date has passed, which is exactly when someone should look.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>
    /// How many times this has now been raised without anybody acting. Rises with the age of the
    /// item, so a reminder that nobody has answered stops looking like a fresh one.
    /// </summary>
    public int EscalationTier { get; set; }

    public Guid? RoutedToEmployeeId { get; set; }

    /// <summary>
    /// What makes a reminder the same reminder: kind + subject + due date + tier. A daily sweep
    /// re-raises nothing already sent at the same tier, so running it twice on one morning is
    /// harmless — and running it every morning does not become noise.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string DedupeKey { get; set; } = string.Empty;
}

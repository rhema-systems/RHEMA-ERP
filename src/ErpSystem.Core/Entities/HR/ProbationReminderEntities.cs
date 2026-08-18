using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR.Recruitment;

/// <summary>
/// One execution of the probation reminder sweep — the daily background pass or a manual run.
/// </summary>
/// <remarks>
/// The run row exists so "the sweep ran and found nothing" is distinguishable from "the sweep never
/// ran", which is the difference between a quiet system and a broken one.
/// </remarks>
public class ProbationReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" (background service) or "Manual" (run-now endpoint).</summary>
    [MaxLength(20)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<ProbationReminderDispatchLog> DispatchLogs { get; set; }
        = new List<ProbationReminderDispatchLog>();
}

/// <summary>
/// One reminder actually dispatched by a sweep.
/// </summary>
/// <remarks>
/// <para>The unique (TenantId, DedupeKey) index is the send-once guarantee, not an optimisation: a
/// sweep claims its keys in the same <c>SaveChanges</c> that records the run, so the daily host and
/// the run-now button cannot double-send even if they overlap. A key encodes the item, the kind,
/// the DUE date and the escalation tier — so moving a probation's end date re-arms the ladder,
/// which is the behaviour we want, and previewing a future date claims nothing, which is what makes
/// the preview endpoint safe.</para>
///
/// <para>⚠ Nothing here carries a rating, a recommendation or a reviewer's comment. A reminder
/// travels further than the record it is about — into notification lists and, one day, email — so
/// it names the employee and a date and makes the reader open the record to learn anything else.
/// The same reasoning governs the discipline engine's dispatch log.</para>
/// </remarks>
public class ProbationReminderDispatchLog : TenantEntity
{
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual ProbationReminderRun Run { get; set; } = null!;

    /// <summary>
    /// Machine kind: "ConfirmationFormDue", "ProbationEndingSoon", "ProbationOverdue",
    /// "ReviewOverdue", "ReviewUnacknowledged".
    /// </summary>
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Human label for the swept item, e.g. "Probation period", "Probation review".</summary>
    [MaxLength(100)]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Id of the swept record. No FK — the target table varies by kind.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The probation the reminder belongs to, so a screen can group by case.</summary>
    public Guid ProbationPeriodId { get; set; }

    /// <summary>What the notification shows: the employee and what is due, and nothing more.</summary>
    [MaxLength(250)]
    public string Reference { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    /// <summary>Days remaining at dispatch time; negative when overdue.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>0 for a due-soon rung; 1, 2 or 3 for an overdue escalation tier.</summary>
    public int EscalationTier { get; set; }

    [Required]
    [MaxLength(300)]
    public string DedupeKey { get; set; } = string.Empty;
}

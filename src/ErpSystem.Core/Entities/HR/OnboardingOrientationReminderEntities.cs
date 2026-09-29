using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR.Orientation;

/// <summary>
/// One execution of the orientation & onboarding reminder sweep (round 4, lane K) — the daily
/// background pass or a manual run.
/// </summary>
/// <remarks>
/// <para>The run row exists so "the sweep ran and found nothing" is distinguishable from "the sweep
/// never ran" — the difference between a quiet system and a broken one.</para>
///
/// <para><b>Unlike every HR sweep before it, this one delivers</b>, so the run also says what reached
/// people: how many notifications were written, how many emails went and how many did not, how many
/// items had nobody to go to, and whether a mail server was configured at all.</para>
/// </remarks>
public class OnboardingOrientationReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" (background service) or "Manual" (run-now endpoint).</summary>
    [MaxLength(20)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    /// <summary>Items claimed by this run — each reminded once, ever, per due date and tier.</summary>
    public int RemindersQueued { get; set; }

    /// <summary>In-app notifications written — one per person, carrying all of their items.</summary>
    public int NotificationsDelivered { get; set; }

    public int EmailsSent { get; set; }

    /// <summary>Emails attempted and not delivered, or never attempted for want of an address or a server.</summary>
    public int EmailsNotSent { get; set; }

    /// <summary>Items with nobody to route to — logged, delivered to no one.</summary>
    public int Unrouted { get; set; }

    /// <summary>Whether a mail server was configured when the run delivered.</summary>
    public bool MailServerConfigured { get; set; }

    public virtual ICollection<OnboardingOrientationReminderDispatchLog> DispatchLogs { get; set; }
        = new List<OnboardingOrientationReminderDispatchLog>();
}

/// <summary>
/// One reminder claimed by a sweep, and what became of it.
/// </summary>
/// <remarks>
/// <para><b>Send-once.</b> The unique (TenantId, DedupeKey) index is the guarantee: a sweep claims its
/// keys in the same <c>SaveChanges</c> that records the run and writes the notifications. A key
/// encodes the kind, the item, the DUE date and the escalation tier — so moving a due date re-arms
/// the reminder, each overdue rung fires once, and a preview claims nothing.</para>
///
/// <para><b>What happened to it</b> is recorded rather than assumed: the notification it went out in
/// (null when nobody could be routed to), and the email outcome — <c>Sent</c>, <c>Failed</c>,
/// <c>TimedOut</c>, <c>NoAddress</c>, <c>NoMailServer</c> or <c>NotRouted</c>.</para>
/// </remarks>
public class OnboardingOrientationReminderDispatchLog : TenantEntity
{
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual OnboardingOrientationReminderRun Run { get; set; } = null!;

    /// <summary>
    /// Machine kind: "OnboardingTaskDueSoon", "OnboardingTaskOverdue", "OnboardingTaskAwaitingSignOff",
    /// "OrientationDueSoon", "OrientationOverdue", "OrientationAssessmentNotAttempted",
    /// "OrientationAcknowledgementOutstanding", "OrientationCertificateExpiring".
    /// </summary>
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Human label for the swept item: "Onboarding task", "Orientation", "Certificate".</summary>
    [MaxLength(100)]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Id of the swept record. No FK — the target table varies by kind.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The onboarding plan, for a task reminder.</summary>
    public Guid? OnboardingPlanId { get; set; }

    /// <summary>The enrolment, for an orientation or certificate reminder.</summary>
    public Guid? EmployeeOrientationId { get; set; }

    /// <summary>What the reminder says: the item, whose it is, and when — nothing more.</summary>
    [MaxLength(300)]
    public string Reference { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    /// <summary>Days remaining at dispatch; negative when overdue.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>0 for a due-soon or waiting reminder; 1, 2 or 3 for an overdue escalation tier.</summary>
    public int EscalationTier { get; set; }

    /// <summary>
    /// Who it went to. ⚠ Null is meaningful: the sweep found work and had nobody to route it to — an
    /// onboarding plan with no coordinator, say. It is logged so HR can see it, and delivered to no one.
    /// </summary>
    public Guid? RoutedToEmployeeId { get; set; }

    /// <summary>The in-app notification (<c>OrientationNotification</c>) that carried it.</summary>
    public Guid? NotificationId { get; set; }

    /// <summary>Sent, Failed, TimedOut, NoAddress, NoMailServer or NotRouted.</summary>
    [MaxLength(20)]
    public string EmailOutcome { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string DedupeKey { get; set; } = string.Empty;
}

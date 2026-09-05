using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR.Assets;

/// <summary>
/// One execution of the asset reminder sweep — the daily background pass or a manual run.
/// Area 16, slice 9.
/// </summary>
/// <remarks>
/// The run row exists so "the sweep ran and found nothing" is distinguishable from "the sweep never
/// ran". For a register of physical property that difference matters more than it does elsewhere:
/// a quiet maintenance list is either an estate in good order or an engine that stopped, and
/// without a run history nobody can tell which.
/// </remarks>
public class AssetReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" (background service) or "Manual" (run-now endpoint).</summary>
    [MaxLength(20)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<AssetReminderDispatchLog> DispatchLogs { get; set; }
        = new List<AssetReminderDispatchLog>();
}

/// <summary>
/// One reminder actually dispatched by a sweep.
/// </summary>
/// <remarks>
/// <para><b>Send-once.</b> The unique <c>(TenantId, DedupeKey)</c> index is the guarantee, not an
/// optimisation: a sweep claims its keys in the same <c>SaveChanges</c> that records the run, then
/// publishes only after that commits. The daily host and the run-now button therefore cannot
/// double-send even if they overlap. A key encodes the item, the kind, the <b>due date</b> and the
/// escalation tier, so re-scheduling an asset's maintenance re-arms the ladder — which is what we
/// want, because a date that has been corrected is genuinely a new thing to chase.</para>
///
/// <para>⚠ Nothing here carries a serial number, a location, a value or a holder's name. A reminder
/// travels further than the record it is about — into notification lists and, one day, email — so
/// it names the asset and a date and makes the reader open the register to learn anything else.
/// The same reasoning governs the discipline, probation and travel dispatch logs.</para>
/// </remarks>
public class AssetReminderDispatchLog : TenantEntity
{
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual AssetReminderRun Run { get; set; } = null!;

    /// <summary>
    /// Machine kind. Slice 9 fires "MaintenanceDueSoon", "MaintenanceOverdue" and
    /// "MaintenanceUnscheduled"; slice 11 adds insurance expiry and overdue returns to the same
    /// engine.
    /// </summary>
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Human label for the swept item, e.g. "Company asset".</summary>
    [MaxLength(100)]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Id of the swept record. No FK — the target table varies by kind.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The asset the reminder is about, so a screen can group by asset across kinds.</summary>
    public Guid AssetId { get; set; }

    /// <summary>What the notification shows: the asset number and name, and nothing more.</summary>
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

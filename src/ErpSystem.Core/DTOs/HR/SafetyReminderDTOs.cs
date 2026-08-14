namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE — REMINDER ENGINE (slice 13; FRD §17)
// Read models for the sweep runs and the per-item dispatch log, plus the
// run-now result the admin screen shows immediately after a manual sweep.
// ============================================================================

public class SheReminderRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int RemindersQueued { get; set; }
    public int PermitsExpired { get; set; }
    public int RiskAssessmentsExpired { get; set; }
}

public class SheReminderRunResultDto : SheReminderRunDto
{
    /// <summary>Reminders queued this run, broken down by kind (e.g. "PermitExpiringSoon" → 2).</summary>
    public Dictionary<string, int> QueuedByKind { get; set; } = new();
}

public class SheReminderLogEntryDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public DateTime DispatchedAt { get; set; }
}

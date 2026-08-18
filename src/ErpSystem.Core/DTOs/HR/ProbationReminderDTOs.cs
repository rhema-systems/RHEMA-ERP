namespace ErpSystem.Core.DTOs.HR;

#region Probation Reminders

public class ProbationReminderRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int RemindersQueued { get; set; }
}

public class ProbationReminderRunResultDto
{
    public Guid RunId { get; set; }
    public int RemindersQueued { get; set; }

    /// <summary>Counts per reminder kind, so a run-now shows what it actually found.</summary>
    public Dictionary<string, int> ByKind { get; set; } = new();
}

/// <summary>
/// One reminder a sweep WOULD fire. Carries the same fields the log records, plus the dedupe key,
/// so a preview can be checked against what the run afterwards actually claimed.
/// </summary>
public class ProbationReminderPreviewItemDto
{
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid ProbationPeriodId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public string DedupeKey { get; set; } = string.Empty;
}

public class ProbationReminderLogEntryDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid ProbationPeriodId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public DateTime DispatchedAt { get; set; }
}

#endregion

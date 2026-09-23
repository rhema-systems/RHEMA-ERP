namespace ErpSystem.Core.DTOs.HR;

// Round 4, lane K — the orientation & onboarding reminder sweep, the first HR sweep that delivers.

/// <summary>What one sweep did: what it claimed, and what reached people.</summary>
public class OnboardingOrientationReminderRunResultDto
{
    public Guid RunId { get; set; }
    public int RemindersQueued { get; set; }
    public int NotificationsDelivered { get; set; }
    public int EmailsSent { get; set; }
    public int EmailsNotSent { get; set; }
    public int Unrouted { get; set; }
    public bool MailServerConfigured { get; set; }
    public Dictionary<string, int> ByKind { get; set; } = new();
}

/// <summary>A reminder the sweep would send — the preview, and each item a run claims.</summary>
public class OnboardingOrientationReminderPreviewItemDto
{
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid? OnboardingPlanId { get; set; }
    public Guid? EmployeeOrientationId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public Guid? RoutedToEmployeeId { get; set; }
    public string? RoutedToName { get; set; }
    public string DedupeKey { get; set; } = string.Empty;

    /// <summary>Where the reminder points its reader — the enrolment, or the onboarding queues.</summary>
    public string? NavigationUrl { get; set; }
}

public class OnboardingOrientationReminderRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int RemindersQueued { get; set; }
    public int NotificationsDelivered { get; set; }
    public int EmailsSent { get; set; }
    public int EmailsNotSent { get; set; }
    public int Unrouted { get; set; }
    public bool MailServerConfigured { get; set; }
}

public class OnboardingOrientationReminderLogEntryDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid? OnboardingPlanId { get; set; }
    public Guid? EmployeeOrientationId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public Guid? RoutedToEmployeeId { get; set; }
    public string? RoutedToName { get; set; }
    public Guid? NotificationId { get; set; }
    public string EmailOutcome { get; set; } = string.Empty;
    public DateTime DispatchedAt { get; set; }
}

// ── Lifecycle notices (round 4, lane K-b) ────────────────────────────────────

/// <summary>What one pass of the notice email dispatcher did.</summary>
public class OrientationNoticeDispatchResultDto
{
    /// <summary>Another pass held the dispatch lock; nothing was sent by this one.</summary>
    public bool Busy { get; set; }
    public bool MailServerConfigured { get; set; }
    public int Picked { get; set; }
    public int Sent { get; set; }

    /// <summary>Failed or timed out on their last allowed attempt.</summary>
    public int Failed { get; set; }

    /// <summary>Failed or timed out with attempts left; still queued.</summary>
    public int Retrying { get; set; }
    public int NoAddress { get; set; }
    public int NoMailServer { get; set; }
    public int Stale { get; set; }

    /// <summary>Queued for the tenant after this pass — retries, and anything past the batch.</summary>
    public int StillQueued { get; set; }
}

/// <summary>One notice as HR sees it: who was told what, and what its email did.</summary>
public class OrientationNoticeLogEntryDto
{
    public Guid Id { get; set; }
    public DateTime SentAt { get; set; }
    public string Type { get; set; } = string.Empty;

    /// <summary>The catalogue event — which lifecycle event, or the reminder digest. Null for a manual notice.</summary>
    public string? Kind { get; set; }
    public Guid RecipientEmployeeId { get; set; }
    public string? RecipientName { get; set; }
    public Guid? ProgramId { get; set; }
    public string? ProgramTitle { get; set; }
    public Guid? EmployeeOrientationId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Message { get; set; }
    public bool IsRead { get; set; }
    public string? EmailStatus { get; set; }
    public int EmailAttempts { get; set; }
    public DateTime? EmailLastAttemptAt { get; set; }
}

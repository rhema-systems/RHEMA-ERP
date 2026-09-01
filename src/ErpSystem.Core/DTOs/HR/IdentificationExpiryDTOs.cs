using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

// ═════════════════════════════════════════════════════════════════════════════
//  Lane 3b — the identification-expiry sweep.
//
//  Kept apart from ReferenceDimensionDTOs for the same reason the endpoints have
//  their own controller: that file is reference-data CRUD, this is an engine.
//  Sharing a file coupled them for no reason the design called for.
// ═════════════════════════════════════════════════════════════════════════════


/// <summary>One card the sweep would remind about, or has.</summary>
public class IdentificationExpiryReminderItemDto
{
    /// <summary><c>IdentificationExpiring</c> or <c>IdentificationExpired</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }

    public Guid EmployeeIdentificationCardId { get; set; }
    public Guid IdentificationTypeId { get; set; }
    public string IdentificationTypeName { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }

    /// <summary>Type name and document number, so the reader can find the card without opening it.</summary>
    public string? Reference { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>Negative once the date has passed, which is exactly when someone should look.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>The type's own lead time, so a reader can see why this surfaced when it did.</summary>
    public int LeadDays { get; set; }

    /// <summary>1 while still valid and inside the window; 2 once expired.</summary>
    public int EscalationTier { get; set; }

    /// <summary>Whose responsibility it is now — the holder at tier 1, HR at tier 2.</summary>
    public Guid? RoutedToEmployeeId { get; set; }

    /// <summary>
    /// Whether a sweep has already raised this exact reminder.
    /// </summary>
    /// <remarks>
    /// Only meaningful on a PREVIEW: it lets a screen show what is due without implying the sweep
    /// would send it all again. Running twice in a morning raises nothing new.
    /// </remarks>
    public bool AlreadyRaised { get; set; }
}

/// <summary>What one pass of the sweep did.</summary>
public class IdentificationExpiryRunResultDto
{
    public Guid RunId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;

    /// <summary>Cards inside their type's lead window, or past expiry.</summary>
    public int CardsConsidered { get; set; }

    public int RemindersQueued { get; set; }

    /// <summary>Considered but already raised at this tier — the dedupe working, not a failure.</summary>
    public int AlreadyRaised { get; set; }
}


/// <summary>One pass of the sweep, for the history list.</summary>
/// <remarks>
/// The run header answers "did it run last night, and did it find anything" — the question a
/// reminder engine gets asked when someone did not receive a reminder they expected. Without it the
/// only evidence a sweep happened is the absence of new dispatch rows, which is indistinguishable
/// from the sweep never running at all.
/// </remarks>
public class IdentificationExpiryRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" for the nightly host, "Manual" for a run from the screen.</summary>
    public string Trigger { get; set; } = string.Empty;

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }
}

/// <summary>A reminder that was actually raised — the dispatch log, read back.</summary>
/// <remarks>
/// ⚠ This is the RAISED record, not a live view of the card. <c>DueDate</c> and
/// <c>DaysRemaining</c> are as they stood when the reminder went out; renewing the document does
/// not rewrite history here, it raises a fresh reminder against the new deadline.
/// </remarks>
public class IdentificationExpiryLogEntryDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }

    public Guid EmployeeIdentificationCardId { get; set; }
    public Guid IdentificationTypeId { get; set; }
    public string? IdentificationTypeName { get; set; }

    public string? Reference { get; set; }
    public DateOnly? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }

    /// <summary>The holder at tier 1, null (HR) at tier 2 — whose job it was, not who could see it.</summary>
    public Guid? RoutedToEmployeeId { get; set; }

    /// <summary>When the reminder was raised.</summary>
    public DateTime RaisedAt { get; set; }
}

using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class RecurringJournalTemplateLineInputDto
{
    public Guid AccountId { get; set; }
    public bool IsDebit { get; set; }
    public decimal FixedAmount { get; set; }
    public string? Description { get; set; }
    public string DimensionValuesJson { get; set; } = "{}";
}

// Kept extensible so the update contract can reuse the complete accounting
// definition while adding only its optimistic-concurrency token.
public class CreateRecurringJournalTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JournalType { get; set; } = "Recurring";
    public string BookClassification { get; set; } = "IFRS";
    public string CurrencyCode { get; set; } = "GHS";
    public string? ReferencePattern { get; set; }
    public string? Notes { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? MaximumOccurrences { get; set; }
    public string TimeZoneId { get; set; } = "Africa/Accra";
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;
    public int Interval { get; set; } = 1;
    public string RecurrenceRuleJson { get; set; } = "{}";
    public BusinessDayConvention BusinessDayConvention { get; set; } = BusinessDayConvention.NextBusinessDay;
    public bool AutoReverse { get; set; }
    public RecurringJournalReversalRule ReversalRule { get; set; }
    public int? ReversalDayOffset { get; set; }
    public IReadOnlyList<RecurringJournalTemplateLineInputDto> Lines { get; set; } = [];
}

public sealed class UpdateRecurringJournalTemplateDto : CreateRecurringJournalTemplateDto
{
    /// <summary>Base64 SQL row-version used to reject stale browser edits.</summary>
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class RecurringJournalDecisionDto
{
    public string Comment { get; set; } = string.Empty;
}

public sealed class RecurringJournalTemplateLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public bool IsDebit { get; set; }
    public decimal FixedAmount { get; set; }
    public string? Description { get; set; }
    public string DimensionValuesJson { get; set; } = "{}";
}

public sealed class RecurringJournalOccurrenceDto
{
    public Guid Id { get; set; }
    public int TemplateVersion { get; set; }
    public int SequenceNumber { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public RecurringJournalOccurrenceStatus Status { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public DateOnly? ReversalDueDate { get; set; }
    public RecurringJournalReversalStatus ReversalStatus { get; set; }
    public DateTime? ReversalAuthorizedAt { get; set; }
    public Guid? ReversalAuthorizedByUserId { get; set; }
    public int ReversalAttemptCount { get; set; }
    public DateTime? ReversalLastAttemptAt { get; set; }
    public string? ReversalError { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public string? ReversalProcessedBy { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTime? ReversedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AdjustmentExplanation { get; set; }
}

public sealed class RecurringJournalTemplateDto
{
    public Guid Id { get; set; }
    public string TemplateNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JournalType { get; set; } = string.Empty;
    public string BookClassification { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string? ReferencePattern { get; set; }
    public string? Notes { get; set; }
    public RecurringJournalStatus Status { get; set; }
    public int Version { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? MaximumOccurrences { get; set; }
    public int GeneratedOccurrenceCount { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public RecurrenceFrequency Frequency { get; set; }
    public int Interval { get; set; }
    public string RecurrenceRuleJson { get; set; } = "{}";
    public BusinessDayConvention BusinessDayConvention { get; set; }
    public DateOnly? NextDueDate { get; set; }
    public DateOnly? LastGeneratedDueDate { get; set; }
    public bool AutoReverse { get; set; }
    public RecurringJournalReversalRule ReversalRule { get; set; }
    public int? ReversalDayOffset { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public Guid? ActivatedByUserId { get; set; }
    public int ConsecutiveFailureCount { get; set; }
    public string? LastFailure { get; set; }
    /// <summary>
    /// Server-calculated exception count keeps the landing-page control metric
    /// accurate without returning every occurrence for every template.
    /// </summary>
    public int ExceptionCount { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyList<RecurringJournalTemplateLineDto> Lines { get; set; } = [];
    public IReadOnlyList<RecurringJournalOccurrenceDto> Occurrences { get; set; } = [];
}

public sealed class RecurringJournalGenerationResultDto
{
    public int TemplateCount { get; set; }
    public int GeneratedCount { get; set; }
    public int ExistingCount { get; set; }
    public int FailedCount { get; set; }
}

public sealed class RecurringJournalReversalProcessingResultDto
{
    public int CandidateCount { get; set; }
    public int PostedCount { get; set; }
    public int ExistingCount { get; set; }
    public int FailedCount { get; set; }
}

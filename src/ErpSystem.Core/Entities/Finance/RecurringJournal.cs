using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Finance;

public enum RecurringJournalStatus { Draft, PendingApproval, Active, Rejected, Paused, Completed, Cancelled }
public enum RecurrenceFrequency { Daily, Weekly, SemiMonthly, Monthly, Quarterly, Annually, Custom }
public enum BusinessDayConvention { NoAdjustment, NextBusinessDay, PreviousBusinessDay }
public enum RecurringJournalOccurrenceStatus { Due, Generating, SubmissionFailed, PendingApproval, Approved, Posted, Failed, WaiverPending, Waived, Superseded }
public enum RecurringJournalReversalRule { None, NextCalendarDay, FirstDayOfNextFiscalPeriod, DayOffset }
public enum RecurringJournalReversalStatus { NotApplicable, PendingAuthorization, Scheduled, Processing, Failed, Posted }

/// <summary>Versioned standing instruction. It never posts directly to the ledger.</summary>
public sealed class RecurringJournalTemplate : TenantEntity
{
    [Required, MaxLength(50)] public string TemplateNumber { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    [Required, MaxLength(50)] public string JournalType { get; set; } = "General";
    [Required, MaxLength(20)] public string BookClassification { get; set; } = "IFRS";
    [Required, MaxLength(3)] public string CurrencyCode { get; set; } = "GHS";
    [MaxLength(200)] public string? ReferencePattern { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    public RecurringJournalStatus Status { get; set; } = RecurringJournalStatus.Draft;
    public int Version { get; set; } = 1;
    public Guid DefinitionKey { get; set; } = Guid.NewGuid();
    public Guid? SupersedesTemplateId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? MaximumOccurrences { get; set; }
    public int GeneratedOccurrenceCount { get; set; }
    [Required, MaxLength(100)] public string TimeZoneId { get; set; } = "Africa/Accra";
    public RecurrenceFrequency Frequency { get; set; }
    public int Interval { get; set; } = 1;
    /// <summary>Versioned JSON rule produced by the accounting-friendly rule builder.</summary>
    [Required, Column(TypeName = "nvarchar(max)")] public string RecurrenceRuleJson { get; set; } = "{}";
    public BusinessDayConvention BusinessDayConvention { get; set; } = BusinessDayConvention.NextBusinessDay;
    public DateOnly? NextDueDate { get; set; }
    public DateOnly? LastGeneratedDueDate { get; set; }
    public bool AutoReverse { get; set; }
    public RecurringJournalReversalRule ReversalRule { get; set; }
    public int? ReversalDayOffset { get; set; }
    public Guid? OwnerUserId { get; set; }
    /// <summary>
    /// Submission and review evidence belongs to the versioned template so TDC can
    /// prove who proposed and independently activated the standing instruction.
    /// These fields must not be inferred from mutable workflow history.
    /// </summary>
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    [MaxLength(1000)] public string? ReviewComment { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public Guid? ActivatedByUserId { get; set; }
    public int ConsecutiveFailureCount { get; set; }
    [MaxLength(2000)] public string? LastFailure { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ICollection<RecurringJournalTemplateLine> Lines { get; set; } = [];
    public ICollection<RecurringJournalOccurrence> Occurrences { get; set; } = [];
}

public sealed class RecurringJournalTemplateLine : TenantEntity
{
    public Guid TemplateId { get; set; }
    public int LineNumber { get; set; }
    public Guid AccountId { get; set; }
    public bool IsDebit { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal FixedAmount { get; set; }
    [Column(TypeName = "decimal(9,6)")] public decimal? AllocationPercentage { get; set; }
    public bool IsRoundingResidualLine { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    /// <summary>Metadata-driven accounting dimension key/value assignments.</summary>
    [Column(TypeName = "nvarchar(max)")] public string DimensionValuesJson { get; set; } = "{}";
    public RecurringJournalTemplate Template { get; set; } = null!;
}

public sealed class RecurringJournalOccurrence : TenantEntity
{
    public Guid TemplateId { get; set; }
    public int TemplateVersion { get; set; }
    public int SequenceNumber { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public RecurringJournalOccurrenceStatus Status { get; set; } = RecurringJournalOccurrenceStatus.Due;
    public Guid? JournalEntryId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? GeneratedAt { get; set; }
    /// <summary>
    /// Occurrence review/posting evidence is stored on the immutable generated
    /// occurrence. Template approval authorises the standing instruction; it does
    /// not remove the independent review required for each accounting event.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    [MaxLength(1000)] public string? ReviewComment { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateOnly? ReversalDueDate { get; set; }
    public RecurringJournalReversalStatus ReversalStatus { get; set; } = RecurringJournalReversalStatus.NotApplicable;
    public DateTime? ReversalAuthorizedAt { get; set; }
    public Guid? ReversalAuthorizedByUserId { get; set; }
    public int ReversalAttemptCount { get; set; }
    public DateTime? ReversalLastAttemptAt { get; set; }
    [MaxLength(2000)] public string? ReversalError { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    [MaxLength(100)] public string? ReversalProcessedBy { get; set; }
    public DateTime? ReversedAt { get; set; }
    [MaxLength(2000)] public string? ErrorMessage { get; set; }
    [MaxLength(1000)] public string? AdjustmentExplanation { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string TemplateSnapshotJson { get; set; } = "{}";
    public DateTime? WaivedAt { get; set; }
    public Guid? WaivedByUserId { get; set; }
    [MaxLength(1000)] public string? WaiverReason { get; set; }
    public RecurringJournalTemplate Template { get; set; } = null!;
    public JournalEntry? JournalEntry { get; set; }
}

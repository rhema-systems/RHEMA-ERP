using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

public sealed class ProcurementCalendarProfile : TenantEntity
{
    [Required] public Guid ProfileKey { get; set; }
    [Required, StringLength(50)] public string ProfileCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Range(1, int.MaxValue)] public int Version { get; set; } = 1;
    public ProcurementCalendarProfileStatus Status { get; set; } = ProcurementCalendarProfileStatus.Draft;
    [Required, StringLength(100)] public string TimeZoneId { get; set; } = "UTC";
    [Range(1, 730)] public int GenerationHorizonDays { get; set; } = 365;
    [Range(0, 365)] public int CatchUpDays { get; set; } = 30;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    [StringLength(300)] public string? ApprovalReference { get; set; }
    public Guid? SupersedesProfileId { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }
    [StringLength(300)] public string? PublishedByName { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public Guid? RetiredById { get; set; }
    [StringLength(300)] public string? RetiredByName { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementCalendarProfile? SupersedesProfile { get; set; }
    public ICollection<ProcurementCalendarRule> Rules { get; set; } = new List<ProcurementCalendarRule>();
}

public sealed class ProcurementCalendarRule : TenantEntity
{
    [Required] public Guid ProfileId { get; set; }
    [Required] public Guid RuleKey { get; set; }
    [Required, StringLength(50)] public string RuleCode { get; set; } = string.Empty;
    public ProcurementCalendarEventType EventType { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Range(1, 12)] public int DueMonth { get; set; }
    [Range(1, 31)] public int DueDay { get; set; }
    public TimeSpan DueLocalTime { get; set; }
    [Range(0, 365)] public int ReminderLeadDays { get; set; } = 14;
    [Range(0, 365)] public int EscalationAfterDays { get; set; } = 1;
    public Guid? OwnerUserId { get; set; }
    [StringLength(100)] public string? OwnerRoleName { get; set; }
    public Guid? EscalationUserId { get; set; }
    [StringLength(100)] public string? EscalationRoleName { get; set; }
    [Required, StringLength(500)] public string StatutoryReference { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    public ProcurementCalendarProfile Profile { get; set; } = null!;
}

public sealed class ProcurementCalendarOccurrence : TenantEntity
{
    [Required, StringLength(160)] public string OccurrenceKey { get; set; } = string.Empty;
    [Required] public Guid ProfileId { get; set; }
    [Required] public Guid ProfileKey { get; set; }
    [Required] public Guid RuleId { get; set; }
    [Required] public Guid RuleKey { get; set; }
    [Range(1, int.MaxValue)] public int ProfileVersion { get; set; }
    public ProcurementCalendarEventType EventType { get; set; }
    [Range(2000, 9999)] public int CalendarYear { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime DueLocal { get; set; }
    [Required, StringLength(100)] public string TimeZoneId { get; set; } = "UTC";
    [Range(0, 365)] public int ReminderLeadDays { get; set; }
    [Range(0, 365)] public int EscalationAfterDays { get; set; }
    public Guid OwnerUserId { get; set; }
    [Required, StringLength(300)] public string OwnerName { get; set; } = string.Empty;
    [StringLength(100)] public string? OwnerRoleName { get; set; }
    public Guid EscalationUserId { get; set; }
    [Required, StringLength(300)] public string EscalationOwnerName { get; set; } = string.Empty;
    [StringLength(100)] public string? EscalationRoleName { get; set; }
    [Required, StringLength(500)] public string StatutoryReference { get; set; } = string.Empty;
    public ProcurementCalendarOccurrenceStatus Status { get; set; } = ProcurementCalendarOccurrenceStatus.Upcoming;
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime? LastEvaluatedAtUtc { get; set; }
    public DateTime? RescheduledAtUtc { get; set; }
    [StringLength(1000)] public string? RescheduleReason { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public Guid? AcknowledgedById { get; set; }
    [StringLength(300)] public string? AcknowledgedByName { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedById { get; set; }
    [StringLength(300)] public string? CompletedByName { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public Guid? CancelledById { get; set; }
    [StringLength(300)] public string? CancelledByName { get; set; }
    [StringLength(1000)] public string? ActionReason { get; set; }
    public Guid? ReminderNotificationId { get; set; }
    public DateTime? ReminderSentAtUtc { get; set; }
    public Guid? DueNotificationId { get; set; }
    public DateTime? DueNotificationSentAtUtc { get; set; }
    public Guid? EscalationNotificationId { get; set; }
    public DateTime? EscalatedAtUtc { get; set; }
    public int ProcessingAttemptCount { get; set; }
    [StringLength(2000)] public string? LastProcessingError { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementCalendarProfile Profile { get; set; } = null!;
    public ProcurementCalendarRule Rule { get; set; } = null!;
}

public sealed class ProcurementCalendarRun : TenantEntity
{
    [Required, StringLength(160)] public string RunKey { get; set; } = string.Empty;
    public ProcurementCalendarRunTrigger Trigger { get; set; }
    public ProcurementCalendarRunStatus Status { get; set; } = ProcurementCalendarRunStatus.Running;
    public int AttemptCount { get; set; } = 1;
    public DateTime EvaluationAtUtc { get; set; }
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? RequestedById { get; set; }
    [Required, StringLength(300)] public string RequestedByName { get; set; } = string.Empty;
    public int ProfilesEvaluated { get; set; }
    public int RulesEvaluated { get; set; }
    public int CreatedCount { get; set; }
    public int RescheduledCount { get; set; }
    public int ReminderCount { get; set; }
    public int DueCount { get; set; }
    public int EscalationCount { get; set; }
    public int FailedCount { get; set; }
    [StringLength(4000)] public string? ErrorSummary { get; set; }
}

using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementCalendarRuleRequest
{
    public Guid? RuleKey { get; set; }
    public ProcurementCalendarEventType EventType { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    [Range(1, 12)] public int DueMonth { get; set; }
    [Range(1, 31)] public int DueDay { get; set; }
    public TimeSpan DueLocalTime { get; set; } = TimeSpan.FromHours(9);
    [Range(0, 365)] public int ReminderLeadDays { get; set; } = 14;
    [Range(0, 365)] public int EscalationAfterDays { get; set; } = 1;
    public Guid? OwnerUserId { get; set; }
    [MaxLength(100)] public string? OwnerRoleName { get; set; }
    public Guid? EscalationUserId { get; set; }
    [MaxLength(100)] public string? EscalationRoleName { get; set; }
    [Required, MaxLength(500)] public string StatutoryReference { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public sealed class SaveProcurementCalendarProfileRequest
{
    [Required, MaxLength(50)] public string ProfileCode { get; set; } = "TDC-ANNUAL-CALENDAR";
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    [Required, MaxLength(100)] public string TimeZoneId { get; set; } = "UTC";
    [Range(1, 730)] public int GenerationHorizonDays { get; set; } = 365;
    [Range(0, 365)] public int CatchUpDays { get; set; } = 30;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    [Required, MaxLength(1000)] public string ChangeSummary { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
    public List<ProcurementCalendarRuleRequest> Rules { get; set; } = new();
}

public sealed class ProcurementCalendarLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [MaxLength(300)] public string? ApprovalReference { get; set; }
}

public sealed class CloneProcurementCalendarProfileRequest
{
    [Required, MaxLength(1000)] public string ChangeSummary { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
}

public sealed class ProcurementCalendarRuleDto
{
    public Guid Id { get; set; }
    public Guid RuleKey { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public ProcurementCalendarEventType EventType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DueMonth { get; set; }
    public int DueDay { get; set; }
    public TimeSpan DueLocalTime { get; set; }
    public int ReminderLeadDays { get; set; }
    public int EscalationAfterDays { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? OwnerRoleName { get; set; }
    public Guid? EscalationUserId { get; set; }
    public string? EscalationRoleName { get; set; }
    public string StatutoryReference { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public sealed class ProcurementCalendarProfileDto
{
    public Guid Id { get; set; }
    public Guid ProfileKey { get; set; }
    public string ProfileCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public ProcurementCalendarProfileStatus Status { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public int GenerationHorizonDays { get; set; }
    public int CatchUpDays { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsEffective { get; set; }
    public string? ChangeSummary { get; set; }
    public string? ApprovalReference { get; set; }
    public Guid? SupersedesProfileId { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string? PublishedByName { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public string? RetiredByName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementCalendarRuleDto> Rules { get; set; } = new();
    public List<ProcurementCalendarValidationIssueDto> ValidationIssues { get; set; } = new();
}

public sealed class ProcurementCalendarValidationIssueDto
{
    public string Code { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ProcurementCalendarSummaryDto
{
    public int ProfileFamilies { get; set; }
    public int DraftProfiles { get; set; }
    public int PublishedProfiles { get; set; }
    public int OpenTasks { get; set; }
    public int DueTasks { get; set; }
    public int EscalatedTasks { get; set; }
    public int CompletedTasks { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
}

public sealed class ProcurementCalendarOccurrenceSearchRequest
{
    public string? Search { get; set; }
    public ProcurementCalendarEventType? EventType { get; set; }
    public ProcurementCalendarOccurrenceStatus? Status { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class ProcurementCalendarOccurrenceDto
{
    public Guid Id { get; set; }
    public string OccurrenceKey { get; set; } = string.Empty;
    public Guid ProfileId { get; set; }
    public int ProfileVersion { get; set; }
    public string ProfileCode { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public ProcurementCalendarEventType EventType { get; set; }
    public int CalendarYear { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime DueLocal { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public Guid OwnerUserId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerRoleName { get; set; }
    public Guid EscalationUserId { get; set; }
    public string EscalationOwnerName { get; set; } = string.Empty;
    public string? EscalationRoleName { get; set; }
    public string StatutoryReference { get; set; } = string.Empty;
    public ProcurementCalendarOccurrenceStatus Status { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime? ReminderSentAtUtc { get; set; }
    public DateTime? DueNotificationSentAtUtc { get; set; }
    public DateTime? EscalatedAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? ActionReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementCalendarOccurrencePageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementCalendarOccurrenceDto> Items { get; set; } = new();
}

public sealed class ProcurementCalendarOccurrenceActionRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class ProcurementCalendarRunRequest
{
    public DateTime? EvaluationAtUtc { get; set; }
    [Range(1, 730)] public int? HorizonDays { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class ProcurementCalendarRunDto
{
    public Guid Id { get; set; }
    public string RunKey { get; set; } = string.Empty;
    public ProcurementCalendarRunTrigger Trigger { get; set; }
    public ProcurementCalendarRunStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime EvaluationAtUtc { get; set; }
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public int ProfilesEvaluated { get; set; }
    public int RulesEvaluated { get; set; }
    public int CreatedCount { get; set; }
    public int RescheduledCount { get; set; }
    public int ReminderCount { get; set; }
    public int DueCount { get; set; }
    public int EscalationCount { get; set; }
    public int FailedCount { get; set; }
    public string? ErrorSummary { get; set; }
}

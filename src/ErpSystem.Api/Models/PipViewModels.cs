using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Api.Models;

// ── Shared helpers ──────────────────────────────────────────────────────────

public record EmployeeSearchResult(
    Guid EmployeeId,
    string FullName,
    string Position,
    string Department,
    string? PhotoUrl,
    Guid? ManagerId,
    string? ManagerName);

// ── PIP create/update request (mirrors PipFormModel from Blazor frontend) ───

public class PipCreateRequest
{
    public Guid EmployeeId { get; set; }
    public Guid? AppraisalId { get; set; }
    public Guid SupervisorId { get; set; }
    public Guid? HROwnerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string PerformanceIssues { get; set; } = string.Empty;
    public string ExpectedStandards { get; set; } = string.Empty;
    public string ImprovementActions { get; set; } = string.Empty;
    public string? SupportProvided { get; set; }
    public string? MeasurementCriteria { get; set; }
    public string? ReviewSchedule { get; set; }
}

public class PipUpdateRequest : PipCreateRequest
{
    public PipStatus Status { get; set; } = PipStatus.Active;
}

// ── PIP goal request (mirrors PipGoalFormItem from Blazor frontend) ─────────

public class PipGoalRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }
    public DateOnly DueDate { get; set; }
    public GoalProgressStatus Status { get; set; } = GoalProgressStatus.NotStarted;
    public decimal? ProgressPercent { get; set; }
    public string? ProgressNotes { get; set; }
}

// ── PIP prepare response ─────────────────────────────────────────────────────

public class PipPrepareResponse
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeePosition { get; set; } = string.Empty;
    public string EmployeeDepartment { get; set; } = string.Empty;
    public string? EmployeePhotoUrl { get; set; }
    public Guid? AppraisalId { get; set; }
    public string? AppraisalCycleName { get; set; }
    public decimal? AppraisalScore { get; set; }
    public string? AppraisalGradeLabel { get; set; }
    public Guid SupervisorId { get; set; }
    public string SupervisorName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(90);
}

// ── PIP detail (full shape for PipDetailPage) ───────────────────────────────

public class PipDetailResponse
{
    public Guid PipId { get; set; }
    public string PipNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeePosition { get; set; } = string.Empty;
    public string EmployeeDepartment { get; set; } = string.Empty;
    public string? EmployeePhotoUrl { get; set; }

    public Guid? AppraisalId { get; set; }
    public string? AppraisalCycleName { get; set; }
    public decimal? AppraisalScore { get; set; }
    public string? AppraisalGradeLabel { get; set; }

    public Guid SupervisorId { get; set; }
    public string SupervisorName { get; set; } = string.Empty;
    public Guid? HROwnerId { get; set; }
    public string? HROwnerName { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public PipStatus Status { get; set; }

    public string PerformanceIssues { get; set; } = string.Empty;
    public string ExpectedStandards { get; set; } = string.Empty;
    public string ImprovementActions { get; set; } = string.Empty;
    public string SupportProvided { get; set; } = string.Empty;
    public string MeasurementCriteria { get; set; } = string.Empty;
    public string? ReviewSchedule { get; set; }

    public List<PipGoalResponse> Goals { get; set; } = new();
    public List<PipAttachmentResponse> Attachments { get; set; } = new();
    public List<PipMeetingSummaryResponse> ReviewMeetings { get; set; } = new();
    public PipMeetingSummaryResponse? NextScheduledMeeting { get; set; }

    public PipOutcome? Outcome { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? OutcomeNotes { get; set; }
}

public class PipGoalResponse
{
    public Guid GoalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }
    public DateOnly DueDate { get; set; }
    public GoalProgressStatus Status { get; set; }
    public decimal? ProgressPercent { get; set; }
    public string? ProgressNotes { get; set; }
}

public class PipAttachmentResponse
{
    public Guid AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long? FileSizeBytes { get; set; }
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
}

public class PipMeetingSummaryResponse
{
    public Guid MeetingId { get; set; }
    public DateTime MeetingDate { get; set; }
    public bool EmployeeAttended { get; set; }
    public string ConductedByName { get; set; } = string.Empty;
    public string ProgressNotesPreview { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

// ── PIP Meeting form (full shape for PipReviewMeetingPage) ──────────────────

public class PipMeetingFormResponse
{
    public Guid? MeetingId { get; set; }
    public Guid PipId { get; set; }
    public string PipNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeePosition { get; set; } = string.Empty;
    public string? EmployeePhotoUrl { get; set; }

    public DateTime PipStartDate { get; set; }
    public DateTime PipEndDate { get; set; }
    public int MeetingNumber { get; set; }
    public int TotalScheduledMeetings { get; set; }

    public DateTime MeetingDate { get; set; }
    public Guid ConductedById { get; set; }
    public string ConductedByName { get; set; } = string.Empty;
    public bool EmployeeAttended { get; set; } = true;
    public string? AbsenceReason { get; set; }

    public string ProgressNotes { get; set; } = string.Empty;
    public string? IssuesDiscussed { get; set; }
    public string? ActionsAgreed { get; set; }
    public string? EmployeeComments { get; set; }
    public DateTime? EmployeeCommentsLastUpdated { get; set; }

    public List<PipGoalMeetingUpdateResponse> GoalUpdates { get; set; } = new();

    public int Status { get; set; } // 1=Scheduled, 2=Completed
    public DateTime? CompletedOn { get; set; }
}

public class PipGoalMeetingUpdateResponse
{
    public Guid GoalId { get; set; }
    public string GoalTitle { get; set; } = string.Empty;
    public string? SuccessCriteria { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal? CurrentProgressPercent { get; set; }
    public GoalProgressStatus CurrentStatus { get; set; }
}

// ── Schedule list ────────────────────────────────────────────────────────────

public class PipMeetingScheduleResponse
{
    public Guid PipId { get; set; }
    public List<PipMeetingListItemResponse> Meetings { get; set; } = new();
    public bool CanScheduleMore { get; set; }
}

public class PipMeetingListItemResponse
{
    public Guid MeetingId { get; set; }
    public int MeetingNumber { get; set; }
    public DateTime MeetingDate { get; set; }
    public int Status { get; set; }
    public bool EmployeeAttended { get; set; }
    public string ConductedByName { get; set; } = string.Empty;
    public string ProgressNotesPreview { get; set; } = string.Empty;
    public int GoalsUpdatedCount { get; set; }
}

// ── Request bodies ───────────────────────────────────────────────────────────

public class ScheduleMeetingRequest
{
    public Guid PipId { get; set; }
    public DateTime MeetingDate { get; set; }
    public Guid ConductedById { get; set; }
}

public class PipOutcomeRequest
{
    public int Outcome { get; set; }
    public string? Notes { get; set; }
    public DateTime CompletionDate { get; set; }
    public DateTime? NewEndDate { get; set; }
}

public class ManualAdvanceRequest
{
    /// <summary>
    /// The specific sub-step to advance past. When null, the service advances past the current
    /// stuck step automatically (identical to omitting the body field).
    /// </summary>
    public AppraisalSubStatus? TargetSubStatus { get; set; }

    /// <summary>HR-provided justification, stored verbatim in the audit log.</summary>
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Employee ID of the HR officer performing the advance.</summary>
    [Required]
    public Guid AdvancedByEmployeeId { get; set; }
}



public class EmployeeCommentRequest
{
    public string Comment { get; set; } = string.Empty;
}

// ── Dashboard ────────────────────────────────────────────────────────────────

public class PipDashboardResponse
{
    public List<PipListItemResponse> PIPs { get; set; } = new();
    public int ActiveCount { get; set; }
    public int DraftCount { get; set; }
    public int OverdueCount { get; set; }
    public int CompletedThisYearCount { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int ExtendedCount { get; set; }
    public int TerminatedCount { get; set; }
}

public class PipListItemResponse
{
    public Guid PipId { get; set; }
    public string PipNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string SupervisorName { get; set; } = string.Empty;
    public string? HROwnerName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public int DaysRemaining { get; set; }
    public decimal PeriodProgressPercent { get; set; }
    public PipStatus Status { get; set; }
    public PipOutcome? Outcome { get; set; }
    public DateTime? CompletionDate { get; set; }
    public bool IsOverdue { get; set; }
    public int TotalGoals { get; set; }
    public int CompletedGoals { get; set; }
    public decimal GoalProgressPercent { get; set; }
    public int TotalMeetings { get; set; }
    public int CompletedMeetings { get; set; }
    public DateTime? NextMeetingDate { get; set; }
    public Guid? AppraisalId { get; set; }
    public string? AppraisalCycleName { get; set; }
    public decimal? AppraisalScore { get; set; }
    public string? AppraisalGradeLabel { get; set; }
}

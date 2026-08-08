using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// Complete aggregated dashboard response for an HR appraisal cycle.
/// Serialised by the API and deserialised by the Blazor client into the matching
/// <c>HRCycleDashboardModel</c> — property names must match exactly.
/// </summary>
public class HRCycleDashboardDto
{
    // ── Cycle header ───────────────────────────────────────────────────────────
    public Guid                 CycleId     { get; set; }
    public string               CycleName   { get; set; } = string.Empty;
    public int                  Year        { get; set; }
    public AppraisalCycleStatus CycleStatus { get; set; }
    public DateOnly             StartDate   { get; set; }
    public DateOnly             EndDate     { get; set; }

    // ── Settings summary ───────────────────────────────────────────────────────
    public bool    HasPeerReviews   { get; set; }
    public bool    HasCalibration   { get; set; }
    public bool    HasHRReview      { get; set; }
    public bool    HasAppeals       { get; set; }
    public bool    AutoLockEnabled  { get; set; }
    public int     MinPeers         { get; set; } = 2;
    public int     MaxPeers         { get; set; } = 5;
    public int     AppealWindowDays { get; set; }

    // ── Weight distribution ────────────────────────────────────────────────────
    public decimal SelfWeight    { get; set; }
    public decimal PeerWeight    { get; set; }
    public decimal ManagerWeight { get; set; }

    // ── Aggregate pipeline progress ────────────────────────────────────────────
    public HRCyclePipelineProgressDto PipelineProgress { get; set; } = new();

    // ── Deadlines ──────────────────────────────────────────────────────────────
    public List<HRCycleDeadlineDto> Deadlines { get; set; } = new();

    // ── Grade distribution ─────────────────────────────────────────────────────
    public List<HRCycleGradeDistributionItemDto> GradeDistribution  { get; set; } = new();
    public int      ScoredAppraisalCount { get; set; }
    public decimal? AverageScore         { get; set; }

    // ── Recommendation summary ─────────────────────────────────────────────────
    public HRCycleRecommendationSummaryDto Recommendations { get; set; } = new();

    // ── What the recommendations actually became ───────────────────────────────
    public HRCycleOutcomePipelineDto OutcomePipeline { get; set; } = new();

    // ── Department breakdown ───────────────────────────────────────────────────
    public List<HRCycleDepartmentProgressDto> DepartmentBreakdown { get; set; } = new();

    // ── Attention items ────────────────────────────────────────────────────────
    public List<HRCycleAttentionItemDto> AttentionItems { get; set; } = new();

    // ── Recent activity feed ───────────────────────────────────────────────────
    public List<HRCycleActivityItemDto> RecentActivity { get; set; } = new();
}

/// <summary>Matches the shape of <c>PipelineProgressModel</c> in the Blazor client.</summary>
public class HRCyclePipelineProgressDto
{
    public int TotalAppraisals              { get; set; }
    public int GoalSettingCount             { get; set; }
    public int PeerNominationCount          { get; set; }
    public int SelfEvalPendingCount         { get; set; }
    public int SelfEvalCompletedCount       { get; set; }
    public int PeerEvalPendingCount         { get; set; }
    public int PeerEvalCompletedCount       { get; set; }
    public int ManagerEvalPendingCount      { get; set; }
    public int ManagerEvalCompletedCount    { get; set; }
    public int CalibrationPendingCount      { get; set; }
    public int CalibrationCompletedCount    { get; set; }
    public int HRReviewPendingCount         { get; set; }
    public int HRReviewCompletedCount       { get; set; }
    public int AcknowledgmentPendingCount   { get; set; }
    public int AcknowledgmentCompletedCount { get; set; }
    public int AppealCount                  { get; set; }
    public int CompletedCount               { get; set; }
    public int ClosedCount                  { get; set; }
}

/// <summary>Matches the shape of <c>CycleDeadlineItem</c> in the Blazor client.</summary>
public class HRCycleDeadlineDto
{
    public string    StepName            { get; set; } = string.Empty;
    public DateOnly? Deadline            { get; set; }
    public DeadlineState State           { get; set; }
    public int?      DaysRemaining       { get; set; }
    public int       AppraisalsAffected  { get; set; }
    public int       AppraisalsCompleted { get; set; }
    public bool      IsActive            { get; set; }
}

/// <summary>Matches the shape of <c>GradeDistributionItem</c> in the Blazor client.</summary>
public class HRCycleGradeDistributionItemDto
{
    public Guid    GradeDefinitionId { get; set; }
    public string  GradeName         { get; set; } = string.Empty;
    public string  Color             { get; set; } = "#6B7280";
    public int     Count             { get; set; }
    public decimal Percentage        { get; set; }
}

/// <summary>Matches the shape of <c>RecommendationSummary</c> in the Blazor client.</summary>
public class HRCycleRecommendationSummaryDto
{
    public int AwardCount       { get; set; }
    public int PromotionCount   { get; set; }
    public int IncrementCount   { get; set; }
    public int TrainingCount    { get; set; }
    public int PIPCount         { get; set; }
    public int TerminationCount { get; set; }
}

/// <summary>
/// One bucket in an outcome stream — a status, or a recommendation type, with how many rows
/// of this cycle's appraisals sit in it.
/// </summary>
public class HRCycleOutcomeCountDto
{
    /// <summary>The enum member name, so the client can link straight to a filtered list.</summary>
    public string Key   { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int    Count { get; set; }

    /// <summary>
    /// True when the rows behind this count are out for approval on the workflow engine and
    /// nothing further happens until an approver acts.
    /// </summary>
    public bool AwaitingApproval { get; set; }
}

/// <summary>
/// What the cycle's recommendations actually became.
///
/// <para><see cref="HRCycleRecommendationSummaryDto"/> counts the boxes managers ticked on the
/// appraisal form — intent. This counts the records those ticks produced and where each one has
/// got to: the recommendation itself, then the salary-review proposal, employment-action
/// proposal or improvement plan raised when HR approved it. The three downstream streams run on
/// the workflow engine, so a cycle can look finished while its outcomes are still queued behind
/// an approver who has not acted.</para>
/// </summary>
public class HRCycleOutcomePipelineDto
{
    public List<HRCycleOutcomeCountDto> Recommendations           { get; set; } = new();
    public List<HRCycleOutcomeCountDto> RecommendationTypes       { get; set; } = new();
    public List<HRCycleOutcomeCountDto> SalaryProposals           { get; set; } = new();
    public List<HRCycleOutcomeCountDto> EmploymentActionProposals { get; set; } = new();
    public List<HRCycleOutcomeCountDto> ImprovementPlans          { get; set; } = new();

    /// <summary>Every record raised from this cycle's appraisals, across all four streams.</summary>
    public int TotalRaised { get; set; }

    /// <summary>How many of those are sitting on the workflow engine waiting for a decision.</summary>
    public int AwaitingApproval { get; set; }
}

/// <summary>Matches the shape of <c>DepartmentProgressItem</c> in the Blazor client.</summary>
public class HRCycleDepartmentProgressDto
{
    public Guid              OrganizationUnitId       { get; set; }
    public string            DepartmentName           { get; set; } = string.Empty;
    public string?           ManagerName              { get; set; }
    public int               TotalAppraisals          { get; set; }
    public int               CompletedSteps           { get; set; }
    public decimal           CompletionPercent        { get; set; }
    public int               OverdueCount             { get; set; }
    public decimal?          AverageScore             { get; set; }
    public bool              HasCalibrationSession    { get; set; }
    public CalibrationStatus? CalibrationSessionStatus { get; set; }
}

/// <summary>Matches the shape of <c>AttentionItem</c> in the Blazor client.</summary>
public class HRCycleAttentionItemDto
{
    public Guid               AppraisalId      { get; set; }
    public string             EmployeeName     { get; set; } = string.Empty;
    public string             Position         { get; set; } = string.Empty;
    public string             Department       { get; set; } = string.Empty;
    public string?            PhotoUrl         { get; set; }
    public string             ManagerName      { get; set; } = string.Empty;
    public AttentionReason    Reason           { get; set; }
    public string             ReasonLabel      { get; set; } = string.Empty;
    public string?            Detail           { get; set; }
    public AttentionSeverity  Severity         { get; set; }
    public decimal?           OverallScore     { get; set; }
    public string?            GradeLabel       { get; set; }
    public int                DaysOverdue      { get; set; }
    public AppraisalSubStatus CurrentSubStatus { get; set; }
}

/// <summary>
/// The result of nudging one stalled appraisal from the dashboard — who was told, and about what.
/// </summary>
public class HRCycleNudgeResultDto
{
    public Guid   AppraisalId  { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>The step the appraisal is stuck on, as shown on the dashboard.</summary>
    public string StepName     { get; set; } = string.Empty;

    /// <summary>Names of the people notified, so the sender can see who was actually reached.</summary>
    public List<string> Recipients { get; set; } = new();

    /// <summary>
    /// How many notifications were written. Lower than <c>Recipients.Count</c> when someone
    /// already has an identical unread nudge — those are skipped rather than piled up.
    /// </summary>
    public int NotificationsRaised { get; set; }
}

/// <summary>Matches the shape of <c>CycleActivityItem</c> in the Blazor client.</summary>
public class HRCycleActivityItemDto
{
    public DateTime                  Timestamp           { get; set; }
    public string                    Description         { get; set; } = string.Empty;
    public string?                   SubjectEmployeeName { get; set; }
    public string?                   ActorName           { get; set; }
    public AppraisalNotificationType EventType           { get; set; }
    public Guid?                     AppraisalId         { get; set; }
    public string                    TimeAgo             { get; set; } = string.Empty;
}

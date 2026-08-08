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

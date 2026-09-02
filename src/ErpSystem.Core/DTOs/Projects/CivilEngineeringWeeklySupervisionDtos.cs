using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringWeeklySupervisionLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringWeeklySupervisionDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringWeeklySupervisionLookupsDto
{
    public IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto> ActivityCategories { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto> Contractors { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto> Milestones { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto> Risks { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto> Issues { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto> Dependencies { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWeeklySupervisionLookupDto> RecoveryOwners { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWeeklySupervisionDocumentLookupDto> Documents { get; init; } = [];
    public IReadOnlyList<string> ActorTypes { get; init; } = [];
    public IReadOnlyList<string> EvidenceRoles { get; init; } = [];
    public IReadOnlyList<string> SiteStatuses { get; init; } = [];
    public int MinimumPhotoCount { get; init; }
    public string DueDay { get; init; } = string.Empty;
}

public sealed class CivilEngineeringWeeklySupervisionActivityRequest
{
    public Guid ActivityCategoryId { get; set; }
    [Required, StringLength(30)] public string ActorType { get; set; } = string.Empty;
    public Guid? ContractorBusinessPartnerId { get; set; }
    [Required, StringLength(4000, MinimumLength = 3)] public string Description { get; set; } = string.Empty;
    [Range(0, 100)] public decimal? ProgressPercent { get; set; }
}

public sealed class CivilEngineeringWeeklySupervisionEvidenceRequest
{
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(30)] public string EvidenceRole { get; set; } = string.Empty;
}

public sealed class CreateCivilEngineeringWeeklySupervisionReportRequest
{
    public Guid ClientRequestId { get; set; }
    public DateTime WeekStart { get; set; }
    public Guid ProjectMilestoneId { get; set; }
    public Guid? ProjectRiskId { get; set; }
    public Guid? ProjectIssueId { get; set; }
    public Guid? ProjectTaskDependencyId { get; set; }
    public decimal? OverallProgressPercent { get; set; }
    [Required, StringLength(30)] public string SiteStatus { get; set; } = CivilEngineeringWeeklySupervisionSiteStatuses.OnTrack;
    [StringLength(2000)] public string? DelayReason { get; set; }
    [StringLength(2000)] public string? RecoveryAction { get; set; }
    public Guid? RecoveryOwnerUserId { get; set; }
    public DateTime? RecoveryDueDate { get; set; }
    [StringLength(4000)] public string? MaterialUsageSummary { get; set; }
    [StringLength(4000)] public string? SafetyNotes { get; set; }
    [StringLength(4000)] public string? TestSummary { get; set; }
    [MinLength(1)] public List<CivilEngineeringWeeklySupervisionActivityRequest> Activities { get; set; } = [];
    [MinLength(1)] public List<CivilEngineeringWeeklySupervisionEvidenceRequest> Evidence { get; set; } = [];
}

public sealed class ProcessCivilEngineeringWeeklySupervisionReportRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public bool Approve { get; set; }
    [Required, StringLength(2000, MinimumLength = 3)] public string Comment { get; set; } = string.Empty;
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
}

public sealed class CivilEngineeringWeeklySupervisionActivityDto
{
    public int Sequence { get; init; }
    public Guid ActivityCategoryId { get; init; }
    public string ActivityCategoryLabel { get; init; } = string.Empty;
    public string ActorType { get; init; } = string.Empty;
    public Guid? ContractorBusinessPartnerId { get; init; }
    public string? ContractorName { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal? ProgressPercent { get; init; }
}

public sealed class CivilEngineeringWeeklySupervisionEvidenceDto
{
    public string EvidenceRole { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringWeeklySupervisionReviewDto
{
    public int Sequence { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public Guid? CentralDocumentRecordId { get; init; }
    public Guid? CentralDocumentVersionId { get; init; }
    public string? DocumentReference { get; init; }
    public string? DocumentTitle { get; init; }
    public string? VersionNumber { get; init; }
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class CivilEngineeringWeeklySupervisionReportDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectEngineerAssignmentId { get; init; }
    public string ProjectEngineerName { get; init; } = string.Empty;
    public Guid? ProjectMilestoneId { get; init; }
    public string ProjectMilestoneTitle { get; init; } = string.Empty;
    public DateTime? MilestoneTargetDateSnapshot { get; init; }
    public DateTime? MilestoneActualDateSnapshot { get; init; }
    public Guid? ProjectRiskId { get; init; }
    public string? ProjectRiskTitle { get; init; }
    public Guid? ProjectIssueId { get; init; }
    public string? ProjectIssueTitle { get; init; }
    public Guid? ProjectTaskDependencyId { get; init; }
    public string? ProjectTaskDependencyLabel { get; init; }
    public DateTime WeekStart { get; init; }
    public DateTime WeekEnd { get; init; }
    public decimal? OverallProgressPercent { get; init; }
    public string SiteStatus { get; init; } = string.Empty;
    public string? DelayReason { get; init; }
    public Guid? RecoveryActionItemId { get; init; }
    public string? RecoveryActionTitle { get; init; }
    public bool IsProgressCorrection { get; init; }
    public Guid? ProgressCorrectionDecisionId { get; init; }
    public string? ProgressCorrectionDecisionTitle { get; init; }
    public string? MaterialUsageSummary { get; init; }
    public string? SafetyNotes { get; init; }
    public string? TestSummary { get; init; }
    public DateTime DueAt { get; init; }
    public bool IsOverdue { get; init; }
    public DateTime? EscalatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string? RejectionReason { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<CivilEngineeringWeeklySupervisionActivityDto> Activities { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWeeklySupervisionEvidenceDto> Evidence { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWeeklySupervisionReviewDto> Reviews { get; init; } = [];
}

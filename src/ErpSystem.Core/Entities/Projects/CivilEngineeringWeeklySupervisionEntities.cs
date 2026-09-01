using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Services.Projects;

namespace ErpSystem.Core.Entities.Projects;

/// <summary>
/// Projects-owned weekly Civil supervision register. Workflow, DMS files, notification delivery,
/// project membership and audit remain owned by their established shared services.
/// </summary>
[Table("ProjectCivilWeeklySupervisionReports")]
public sealed class ProjectCivilWeeklySupervisionReport : TenantEntity, ICivilEngineeringWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid ProjectEngineerAssignmentId { get; set; }
    public Guid? ProjectMilestoneId { get; set; }
    public DateTime? MilestoneTargetDateSnapshot { get; set; }
    public DateTime? MilestoneActualDateSnapshot { get; set; }
    public Guid? ProjectRiskId { get; set; }
    public Guid? ProjectIssueId { get; set; }
    public Guid? ProjectTaskDependencyId { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    [Range(0, 100)] public decimal? OverallProgressPercent { get; set; }
    [Required, StringLength(30)] public string SiteStatus { get; set; } = CivilEngineeringWeeklySupervisionSiteStatuses.OnTrack;
    [StringLength(2000)] public string? DelayReason { get; set; }
    public Guid? RecoveryActionItemId { get; set; }
    public bool IsProgressCorrection { get; set; }
    public Guid? ProgressCorrectionDecisionId { get; set; }
    public bool HasGovernedProgressControl { get; set; }
    [StringLength(4000)] public string? MaterialUsageSummary { get; set; }
    [StringLength(4000)] public string? SafetyNotes { get; set; }
    [StringLength(4000)] public string? TestSummary { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(80)] public string EvidenceMetadataTemplateCodeSnapshot { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = "PendingApproval";
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Pending";
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectCivilProjectEngineerAssignment ProjectEngineerAssignment { get; set; } = null!;
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public ICollection<ProjectCivilWeeklySupervisionActivity> Activities { get; set; } = [];
    public ICollection<ProjectCivilWeeklySupervisionEvidence> Evidence { get; set; } = [];
    public ICollection<ProjectCivilWeeklySupervisionReview> Reviews { get; set; } = [];
    public ICollection<ProjectCivilWeeklySupervisionRevision> Revisions { get; set; } = [];
}

/// <summary>
/// Controlled site condition values for a weekly report. They are deliberately closed values,
/// rather than a free-text status, so delayed work can require a responsible recovery action.
/// </summary>
public static class CivilEngineeringWeeklySupervisionSiteStatuses
{
    public const string OnTrack = "OnTrack";
    public const string AtRisk = "AtRisk";
    public const string Delayed = "Delayed";
    public const string Stopped = "Stopped";

    public static IReadOnlyList<string> All { get; } = [OnTrack, AtRisk, Delayed, Stopped];
    public static bool RequiresRecoveryAction(string? value) =>
        string.Equals(value, AtRisk, StringComparison.Ordinal)
        || string.Equals(value, Delayed, StringComparison.Ordinal)
        || string.Equals(value, Stopped, StringComparison.Ordinal);
}

public static class CivilEngineeringWeeklySupervisionActorTypes
{
    public const string Contractor = "Contractor";
    public const string LabourGang = "LabourGang";
    public static IReadOnlyList<string> All { get; } = [Contractor, LabourGang];
}

public static class CivilEngineeringWeeklySupervisionEvidenceRoles
{
    public const string Report = "Report";
    public const string Photo = "Photo";
    public const string Test = "Test";
    public const string Review = "Review";
    public static IReadOnlyList<string> All { get; } = [Report, Photo, Test, Review];
}

[Table("ProjectCivilWeeklySupervisionActivities")]
public sealed class ProjectCivilWeeklySupervisionActivity : TenantEntity
{
    public Guid ReportId { get; set; }
    public int Sequence { get; set; }
    public Guid ActivityCategoryId { get; set; }
    [Required, StringLength(30)] public string ActorType { get; set; } = CivilEngineeringWeeklySupervisionActorTypes.Contractor;
    public Guid? ContractorBusinessPartnerId { get; set; }
    [Required, StringLength(4000)] public string Description { get; set; } = string.Empty;
    [Range(0, 100)] public decimal? ProgressPercent { get; set; }

    public ProjectCivilWeeklySupervisionReport Report { get; set; } = null!;
    public ProjectCatalogEntry ActivityCategory { get; set; } = null!;
}

[Table("ProjectCivilWeeklySupervisionEvidence")]
public sealed class ProjectCivilWeeklySupervisionEvidence : TenantEntity
{
    public Guid ReportId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(30)] public string EvidenceRole { get; set; } = CivilEngineeringWeeklySupervisionEvidenceRoles.Report;
    public Guid LinkedByUserId { get; set; }
    public DateTime LinkedAt { get; set; }

    public ProjectCivilWeeklySupervisionReport Report { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("ProjectCivilWeeklySupervisionReviews")]
public sealed class ProjectCivilWeeklySupervisionReview : TenantEntity
{
    public Guid ReportId { get; set; }
    public Guid ClientRequestId { get; set; }
    public int Sequence { get; set; }
    [Required, StringLength(30)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Outcome { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Comment { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;

    public ProjectCivilWeeklySupervisionReport Report { get; set; } = null!;
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
}

[Table("ProjectCivilWeeklySupervisionRevisions")]
public sealed class ProjectCivilWeeklySupervisionRevision : TenantEntity
{
    public Guid ReportId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilWeeklySupervisionReport Report { get; set; } = null!;
}

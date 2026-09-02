using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;

namespace ErpSystem.Core.Entities.Projects;

/// <summary>
/// Projects-owned Civil quality/laboratory test register. File bytes stay in central DMS;
/// Finance/QS remain authoritative for the optional payment-certificate link.
/// </summary>
[Table("ProjectCivilQualityTestReports")]
public sealed class ProjectCivilQualityTestReport : TenantEntity, ICivilEngineeringWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ProjectPaymentCertificateId { get; set; }
    public Guid? SourceBusinessPartnerId { get; set; }
    public CivilEngineeringQualityTestCategory TestCategory { get; set; }
    [Required, StringLength(30)] public string SourceType { get; set; } = CivilEngineeringQualityTestSourceTypes.Laboratory;
    [Required, StringLength(100)] public string ReportReference { get; set; } = string.Empty;
    public DateTime TestedAt { get; set; }
    [Required, StringLength(30)] public string ResultStatus { get; set; } = CivilEngineeringQualityTestResultStatuses.Inconclusive;
    [Required, StringLength(2000)] public string ResultSummary { get; set; } = string.Empty;
    public Guid ReviewerUserId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid? EndorsementDocumentRecordId { get; set; }
    public Guid? EndorsementDocumentVersionId { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(80)] public string EvidenceMetadataTemplateCodeSnapshot { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = "PendingApproval";
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Pending";
    public bool AcceptanceBlocked { get; set; }
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
    public ProjectPhase? ProjectPhase { get; set; }
    public ProjectPackage? ProjectPackage { get; set; }
    public ProjectPaymentCertificate? ProjectPaymentCertificate { get; set; }
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public CentralDocumentRecord? EndorsementDocumentRecord { get; set; }
    public CentralDocumentVersion? EndorsementDocumentVersion { get; set; }
    public ICollection<ProjectCivilQualityTestRevision> Revisions { get; set; } = [];
}

public static class CivilEngineeringQualityTestSourceTypes
{
    public const string Laboratory = "Laboratory";
    public const string Contractor = "Contractor";
    public const string Consultant = "Consultant";
    public const string Internal = "Internal";

    public static IReadOnlyList<string> All { get; } = [Laboratory, Contractor, Consultant, Internal];
}

public static class CivilEngineeringQualityTestResultStatuses
{
    public const string Pass = "Pass";
    public const string Fail = "Fail";
    public const string Inconclusive = "Inconclusive";

    public static IReadOnlyList<string> All { get; } = [Pass, Fail, Inconclusive];
}

[Table("ProjectCivilQualityTestRevisions")]
public sealed class ProjectCivilQualityTestRevision : TenantEntity
{
    public Guid TestReportId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilQualityTestReport TestReport { get; set; } = null!;
}

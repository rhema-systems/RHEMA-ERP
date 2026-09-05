using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;

namespace ErpSystem.Core.Entities.Projects;

public enum CivilEngineeringDevelopmentApprovalFileStatus { Registered = 0, SiteInspectionScheduled = 1, SiteInspectionCompleted = 2 }
public enum CivilEngineeringDevelopmentApprovalEvidenceKind { ApplicationPackage = 0, SiteInspection = 1 }
public enum CivilEngineeringPermittingSection { BuildingInspectorate = 0, Architecture = 1, CivilEngineering = 2, HeadOfDepartment = 3, GeodeticEngineering = 4, TownPlanning = 5, OtherTechnicalSection = 6 }
public enum CivilEngineeringPermittingEngineeringReviewStage { CorrectionRequested = 0, PendingHodDecision = 1, HodApproved = 2, HodRejected = 3, HodReturned = 4 }
public enum CivilEngineeringPermittingHodDecisionOutcome { Approve = 0, Reject = 1, ReturnForCorrection = 2 }

/// <summary>
/// Civil permitting register only.  File handoffs, engineering review and HOD decisions are added by CIV-0402 through CIV-0404.
/// Physical evidence remains owned by central DMS.
/// </summary>
[Table("ProjectCivilDevelopmentApprovalFiles")]
public sealed class ProjectCivilDevelopmentApprovalFile : TenantEntity
{
    [Required, StringLength(40)] public string FileNumber { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? ApplicantBusinessPartnerId { get; set; }
    [Required, StringLength(250)] public string ApplicantName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid EstateManagedAssetId { get; set; }
    [Required, StringLength(120)] public string ApplicationReference { get; set; } = string.Empty;
    [Required, StringLength(80)] public string CurrentSection { get; set; } = "BuildingInspectorate";
    public DateTime DueDate { get; set; }
    public CivilEngineeringDevelopmentApprovalFileStatus Status { get; set; } = CivilEngineeringDevelopmentApprovalFileStatus.Registered;
    public DateTime? SiteInspectionDueDate { get; set; }
    public DateTime? SiteInspectedAt { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid PermittingConfigurationDecisionId { get; set; }
    public Guid DocumentConfigurationDecisionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid MetadataTemplateId { get; set; }
    [Required, StringLength(80)] public string MetadataTemplateCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public BusinessPartner? ApplicantBusinessPartner { get; set; }
    public Project Project { get; set; } = null!;
    public EstateManagedAsset EstateManagedAsset { get; set; } = null!;
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision PermittingConfigurationDecision { get; set; } = null!;
    public CivilEngineeringConfigurationDecision DocumentConfigurationDecision { get; set; } = null!;
    public CentralDocumentMetadataTemplate MetadataTemplate { get; set; } = null!;
    public ICollection<ProjectCivilDevelopmentApprovalEvidence> Evidence { get; set; } = [];
    public ICollection<ProjectCivilDevelopmentApprovalFileHandoff> Handoffs { get; set; } = [];
    public ICollection<ProjectCivilDevelopmentApprovalEngineeringReview> EngineeringReviews { get; set; } = [];
    public ICollection<ProjectCivilDevelopmentApprovalFileRevision> Revisions { get; set; } = [];
}

/// <summary>Immutable controlled section-routing event.  It does not decide the permit or mutate the opened file.</summary>
[Table("ProjectCivilDevelopmentApprovalFileHandoffs")]
public sealed class ProjectCivilDevelopmentApprovalFileHandoff : TenantEntity
{
    public Guid DevelopmentApprovalFileId { get; set; }
    public int SequenceNumber { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public CivilEngineeringPermittingSection FromSection { get; set; }
    public CivilEngineeringPermittingSection ToSection { get; set; }
    public Guid FromUserId { get; set; }
    public Guid RecipientRoleId { get; set; }
    public Guid RecipientUserId { get; set; }
    [StringLength(2000)] public string? CoverNote { get; set; }
    public DateTime DueDate { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProjectCivilDevelopmentApprovalFile DevelopmentApprovalFile { get; set; } = null!;
    public ApplicationUser FromUser { get; set; } = null!;
    public ApplicationRole RecipientRole { get; set; } = null!;
    public ApplicationUser RecipientUser { get; set; } = null!;
    public ICollection<ProjectCivilDevelopmentApprovalHandoffEvidence> Evidence { get; set; } = [];
}

[Table("ProjectCivilDevelopmentApprovalHandoffEvidence")]
public sealed class ProjectCivilDevelopmentApprovalHandoffEvidence : TenantEntity
{
    public Guid HandoffId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;

    public ProjectCivilDevelopmentApprovalFileHandoff Handoff { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

/// <summary>SCE technical review attached to one Architecture-to-Civil (or rework) handoff. HOD action remains CIV-0404.</summary>
[Table("ProjectCivilDevelopmentApprovalEngineeringReviews")]
public sealed class ProjectCivilDevelopmentApprovalEngineeringReview : TenantEntity, ICivilEngineeringWorkflowRecord
{
    public Guid DevelopmentApprovalFileId { get; set; }
    public Guid SourceHandoffId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid ReviewerUserId { get; set; }
    public Guid CommentCategoryId { get; set; }
    [Required, StringLength(4000)] public string ReviewComment { get; set; } = string.Empty;
    public CivilEngineeringPermittingOutcome RecommendedOutcome { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public CivilEngineeringPermittingEngineeringReviewStage Stage { get; set; }
    [Required, StringLength(40)] public string Status { get; set; } = "PendingApproval";
    [Required, StringLength(40)] public string ApprovalStatus { get; set; } = "Pending";
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public DateTime ReviewedAt { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProjectCivilDevelopmentApprovalFile DevelopmentApprovalFile { get; set; } = null!;
    public ProjectCivilDevelopmentApprovalFileHandoff SourceHandoff { get; set; } = null!;
    public ApplicationUser ReviewerUser { get; set; } = null!;
    public ProjectCatalogEntry CommentCategory { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public ICollection<ProjectCivilDevelopmentApprovalEngineeringReviewDecision> HodDecisions { get; set; } = [];
    public ICollection<ProjectCivilDevelopmentApprovalEngineeringReviewRevision> Revisions { get; set; } = [];
}

/// <summary>Immutable HOD decision on one pending SCE recommendation; the shared workflow instance remains authoritative for approval progression.</summary>
[Table("ProjectCivilDevelopmentApprovalEngineeringReviewDecisions")]
public sealed class ProjectCivilDevelopmentApprovalEngineeringReviewDecision : TenantEntity
{
    public Guid EngineeringReviewId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public CivilEngineeringPermittingHodDecisionOutcome Outcome { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
    public Guid DecidedById { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    [Required, StringLength(30)] public string WorkflowAction { get; set; } = string.Empty;
    [Required, StringLength(30)] public string WorkflowOutcome { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProjectCivilDevelopmentApprovalEngineeringReview EngineeringReview { get; set; } = null!;
    public ApplicationUser DecidedBy { get; set; } = null!;
}

[Table("ProjectCivilDevelopmentApprovalEngineeringReviewRevisions")]
public sealed class ProjectCivilDevelopmentApprovalEngineeringReviewRevision : TenantEntity
{
    public Guid EngineeringReviewId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilDevelopmentApprovalEngineeringReview EngineeringReview { get; set; } = null!;
}

[Table("ProjectCivilDevelopmentApprovalEvidence")]
public sealed class ProjectCivilDevelopmentApprovalEvidence : TenantEntity
{
    public Guid DevelopmentApprovalFileId { get; set; }
    public CivilEngineeringDevelopmentApprovalEvidenceKind Kind { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;

    public ProjectCivilDevelopmentApprovalFile DevelopmentApprovalFile { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("ProjectCivilDevelopmentApprovalFileRevisions")]
public sealed class ProjectCivilDevelopmentApprovalFileRevision : TenantEntity
{
    public Guid DevelopmentApprovalFileId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilDevelopmentApprovalFile DevelopmentApprovalFile { get; set; } = null!;
}

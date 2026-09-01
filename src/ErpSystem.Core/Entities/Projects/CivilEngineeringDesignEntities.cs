using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;

namespace ErpSystem.Core.Entities.Projects;

[Table("ProjectCivilDesignCases")]
public sealed class ProjectCivilDesignCase : TenantEntity, ICivilEngineeringWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public CivilEngineeringWorksInitiationSource? InitiationSource { get; set; }
    public Guid? InitiationSourceId { get; set; }
    public Guid? InitiationSourceDocumentVersionId { get; set; }
    [StringLength(240)] public string? InitiationSourceReference { get; set; }
    public Guid? EstateManagedAssetId { get; set; }
    public Guid? EngineeringCategoryId { get; set; }
    public CivilEngineeringWorkClassification? WorkClassification { get; set; }
    [StringLength(4000)] public string? ScopeSummary { get; set; }
    [StringLength(4000)] public string? ConstraintSummary { get; set; }
    [StringLength(4000)] public string? RiskSummary { get; set; }
    [StringLength(4000)] public string? Recommendation { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(50)] public string ReferenceNumber { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Directive { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Stage { get; set; } = CivilEngineeringDesignStages.DraftDirective;
    [Required, StringLength(30)] public string Status { get; set; } = "Draft";
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public bool RequireSiteReconnaissance { get; set; } = true;
    public bool RequireVersionedReview { get; set; } = true;
    public bool RequireHodApproval { get; set; } = true;
    public bool RequirePlanningGisValidation { get; set; } = true;
    public Guid HodUserId { get; set; }
    public Guid SupervisingCivilEngineerUserId { get; set; }
    public Guid? CivilEngineerUserId { get; set; }
    public Guid? DraftsmanUserId { get; set; }
    public Guid? CurrentAssigneeUserId { get; set; }
    public DateTime? CurrentDueAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public EstateManagedAsset? EstateManagedAsset { get; set; }
    public ProjectCatalogEntry? EngineeringCategory { get; set; }
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public ICollection<ProjectCivilDesignEvidence> Evidence { get; set; } = [];
    public ICollection<ProjectCivilDesignRevision> Revisions { get; set; } = [];
    public ICollection<ProjectCivilReconnaissanceReport> ReconnaissanceReports { get; set; } = [];
    public ICollection<ProjectRfi> InformationRequests { get; set; } = [];
    public ICollection<ProjectCivilEngineeringDocument> EngineeringDocuments { get; set; } = [];
    public ICollection<ProjectCivilPlanningGisValidation> PlanningGisValidations { get; set; } = [];
}

/// <summary>
/// A tenant-scoped Planning/GIS review gate for an existing Civil design case.
/// Estate remains authoritative for the live parcel/GIS facts; this record only
/// snapshots the governed references that were reviewed.
/// </summary>
[Table("ProjectCivilPlanningGisValidations")]
public sealed class ProjectCivilPlanningGisValidation : TenantEntity
{
    public Guid DesignCaseId { get; set; }
    public Guid EstateManagedAssetId { get; set; }
    public Guid DevelopmentApprovalFileId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    public Guid PlanningConditionId { get; set; }
    public Guid DevelopmentConstraintId { get; set; }
    public Guid LandUseImpactId { get; set; }
    public CivilEngineeringLayoutConformity LayoutConformity { get; set; }
    [Required, StringLength(512)] public string SpatialReferenceSnapshot { get; set; } = string.Empty;
    [StringLength(200)] public string? GisProviderSnapshot { get; set; }
    [StringLength(200)] public string? GisFeatureIdSnapshot { get; set; }
    [StringLength(120)] public string? GisSourceCrsSnapshot { get; set; }
    [StringLength(4000)] public string? BoundaryCoordinatesSnapshot { get; set; }
    [StringLength(120)] public string? ZoningClassificationSnapshot { get; set; }
    [StringLength(120)] public string? PlanningComplianceSnapshot { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public CivilEngineeringPlanningGisValidationStatus Status { get; set; } = CivilEngineeringPlanningGisValidationStatus.Draft;
    public Guid PreparedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProjectCivilDesignCase DesignCase { get; set; } = null!;
    public EstateManagedAsset EstateManagedAsset { get; set; } = null!;
    public ProjectCivilDevelopmentApprovalFile DevelopmentApprovalFile { get; set; } = null!;
    public ProjectCatalogEntry PlanningCondition { get; set; } = null!;
    public ProjectCatalogEntry DevelopmentConstraint { get; set; } = null!;
    public ProjectCatalogEntry LandUseImpact { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public ICollection<ProjectCivilPlanningGisValidationRevision> Revisions { get; set; } = [];
}

[Table("ProjectCivilPlanningGisValidationRevisions")]
public sealed class ProjectCivilPlanningGisValidationRevision : TenantEntity
{
    public Guid PlanningGisValidationId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilPlanningGisValidation PlanningGisValidation { get; set; } = null!;
}

[Table("ProjectCivilDesignEvidence")]
public sealed class ProjectCivilDesignEvidence : TenantEntity
{
    public Guid DesignCaseId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(40)] public string EvidenceType { get; set; } = string.Empty;
    [Required, StringLength(40)] public string StageSnapshot { get; set; } = string.Empty;
    public Guid LinkedById { get; set; }
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    public ProjectCivilDesignCase DesignCase { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("ProjectCivilDesignRevisions")]
public sealed class ProjectCivilDesignRevision : TenantEntity
{
    public Guid DesignCaseId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(40)] public string FromStage { get; set; } = string.Empty;
    [Required, StringLength(40)] public string ToStage { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilDesignCase DesignCase { get; set; } = null!;
}

[Table("ProjectCivilReconnaissanceReports")]
public sealed class ProjectCivilReconnaissanceReport : TenantEntity
{
    public Guid DesignCaseId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(50)] public string ReportNumber { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    [Required, StringLength(500)] public string SiteLocation { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Summary { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string SiteConditions { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Status { get; set; } = CivilEngineeringReconnaissanceStatuses.Draft;
    public Guid SiteReconnaissanceTemplateId { get; set; }
    public Guid CrossSectionTemplateId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid PreparedByUserId { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProjectCivilDesignCase DesignCase { get; set; } = null!;
    public CentralDocumentMetadataTemplate SiteReconnaissanceTemplate { get; set; } = null!;
    public CentralDocumentMetadataTemplate CrossSectionTemplate { get; set; } = null!;
    public ICollection<ProjectCivilReconnaissanceItem> Items { get; set; } = [];
    public ICollection<ProjectCivilReconnaissanceRevision> Revisions { get; set; } = [];
}

[Table("ProjectCivilReconnaissanceItems")]
public sealed class ProjectCivilReconnaissanceItem : TenantEntity
{
    public Guid ReportId { get; set; }
    public CivilEngineeringReconnaissanceItemKind Kind { get; set; }
    [Required, StringLength(2000)] public string Description { get; set; } = string.Empty;
    public Guid? InformationSourceSectionId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    public CivilEngineeringConstraintCategory? ConstraintCategory { get; set; }
    public CivilEngineeringConstraintSeverity? Severity { get; set; }
    public CivilEngineeringConstraintResolutionStatus? ResolutionStatus { get; set; }
    public bool BlocksDesign { get; set; }
    public int DisplayOrder { get; set; }

    public ProjectCivilReconnaissanceReport Report { get; set; } = null!;
    public Section? InformationSourceSection { get; set; }
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
}

[Table("ProjectCivilReconnaissanceRevisions")]
public sealed class ProjectCivilReconnaissanceRevision : TenantEntity
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

    public ProjectCivilReconnaissanceReport Report { get; set; } = null!;
}

[Table("ProjectCivilDesignInputResponses")]
public sealed class ProjectCivilDesignInputResponse : TenantEntity
{
    public Guid ProjectRfiId { get; set; }
    public int ResponseSequence { get; set; }
    [Required, StringLength(4000)] public string ResponseText { get; set; } = string.Empty;
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid RespondedByUserId { get; set; }
    public DateTime RespondedAt { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;

    public ProjectRfi ProjectRfi { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("ProjectCivilEngineeringDocuments")]
public sealed class ProjectCivilEngineeringDocument : TenantEntity
{
    public Guid DesignCaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid DocumentKey { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    public Guid? SupersedesDocumentId { get; set; }
    public CivilEngineeringDesignDiscipline Discipline { get; set; }
    public CivilEngineeringFileCategory FileCategory { get; set; }
    [Required, StringLength(20)] public string FileExtension { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public int RevisionNumber { get; set; }
    [Required, StringLength(120)] public string ExpectedDocumentReference { get; set; } = string.Empty;
    [Required, StringLength(120)] public string DocumentReferenceSnapshot { get; set; } = string.Empty;
    [Required, StringLength(250)] public string DocumentTitleSnapshot { get; set; } = string.Empty;
    [Required, StringLength(80)] public string DmsVersionSnapshot { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public CivilEngineeringDocumentStatus Status { get; set; } = CivilEngineeringDocumentStatus.Draft;
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid MetadataTemplateId { get; set; }
    [Required, StringLength(80)] public string MetadataTemplateCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    [StringLength(2000)] public string? ReviewReason { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProjectCivilDesignCase DesignCase { get; set; } = null!;
    public ProjectPackage? ProjectPackage { get; set; }
    public ProjectCivilEngineeringDocument? SupersedesDocument { get; set; }
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public CentralDocumentMetadataTemplate MetadataTemplate { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public ICollection<ProjectCivilEngineeringDocumentRevision> Revisions { get; set; } = [];
}

[Table("ProjectCivilEngineeringDocumentRevisions")]
public sealed class ProjectCivilEngineeringDocumentRevision : TenantEntity
{
    public Guid EngineeringDocumentId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilEngineeringDocument EngineeringDocument { get; set; } = null!;
}

[Table("ProjectCivilProjectEngineerAssignments")]
public sealed class ProjectCivilProjectEngineerAssignment : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid ProjectMemberId { get; set; }
    public Guid AssignedUserId { get; set; }
    [Required, StringLength(100)] public string SourceCivilRole { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ProjectRole { get; set; } = CivilEngineeringAccessControlRegistry.ProjectEngineerRole;
    public CivilEngineeringProjectEngineerAuthority Authority { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectMember ProjectMember { get; set; } = null!;
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public ICollection<ProjectCivilProjectEngineerAssignmentRevision> Revisions { get; set; } = [];
}

[Table("ProjectCivilProjectEngineerAssignmentRevisions")]
public sealed class ProjectCivilProjectEngineerAssignmentRevision : TenantEntity
{
    public Guid AssignmentId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilProjectEngineerAssignment Assignment { get; set; } = null!;
}

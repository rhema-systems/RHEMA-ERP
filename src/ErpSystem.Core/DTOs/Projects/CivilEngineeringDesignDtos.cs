using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringDesignCaseDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public CivilEngineeringWorksInitiationSource? InitiationSource { get; init; }
    public Guid? InitiationSourceId { get; init; }
    public Guid? InitiationSourceDocumentVersionId { get; init; }
    public string? InitiationSourceReference { get; init; }
    public Guid? EstateManagedAssetId { get; init; }
    public string? EstateManagedAssetLabel { get; init; }
    public Guid? EngineeringCategoryId { get; init; }
    public string? EngineeringCategoryLabel { get; init; }
    public CivilEngineeringWorkClassification? WorkClassification { get; init; }
    public string? ScopeSummary { get; init; }
    public string? ConstraintSummary { get; init; }
    public string? RiskSummary { get; init; }
    public string? Recommendation { get; init; }
    public Guid ProjectMemberId { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Directive { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public bool RequirePlanningGisValidation { get; init; }
    public Guid HodUserId { get; init; }
    public Guid SupervisingCivilEngineerUserId { get; init; }
    public Guid? CivilEngineerUserId { get; init; }
    public Guid? DraftsmanUserId { get; init; }
    public Guid? CurrentAssigneeUserId { get; init; }
    public DateTime? CurrentDueAt { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<CivilEngineeringDesignEvidenceDto> Evidence { get; init; } = [];
}

public sealed class CivilEngineeringDesignEvidenceDto
{
    public Guid Id { get; init; }
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
    public string EvidenceType { get; init; } = string.Empty;
    public string StageSnapshot { get; init; } = string.Empty;
    public DateTime LinkedAt { get; init; }
}

public sealed class CivilEngineeringDesignRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string FromStage { get; init; } = string.Empty;
    public string ToStage { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// A live, read-only view over the authoritative QS, Project Budget, Procurement,
/// Contract, Business Partner and DMS records for a Civil Engineering case.  It
/// deliberately contains no financial, commercial, or delivery ledger state.
/// </summary>
public sealed class CivilEngineeringCommercialReadinessDto
{
    public Guid DesignCaseId { get; init; }
    public Guid ProjectId { get; init; }
    public DateTime RevalidatedAt { get; init; }
    public bool ReadyForExecution { get; init; }
    public IReadOnlyList<CivilEngineeringCommercialReadinessGateDto> Gates { get; init; } = [];
    public IReadOnlyList<CivilEngineeringCommercialReadinessLinkDto> Links { get; init; } = [];
}

public sealed class CivilEngineeringCommercialReadinessGateDto
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public bool IsSatisfied { get; init; }
    public string Detail { get; init; } = string.Empty;
}

public sealed class CivilEngineeringCommercialReadinessLinkDto
{
    public string Owner { get; init; } = string.Empty;
    public string RecordType { get; init; } = string.Empty;
    public Guid? RecordId { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDesignMemberLookupDto
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDesignSourceLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public Guid? ProjectId { get; init; }
}

public sealed class CivilEngineeringDesignSourceDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDesignLookupsDto
{
    public IReadOnlyList<CivilEngineeringDesignMemberLookupDto> SupervisingCivilEngineers { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignMemberLookupDto> CivilEngineers { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignMemberLookupDto> Draftsmen { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignSectionLookupDto> InformationSourceSections { get; init; } = [];
    public IReadOnlyList<CivilEngineeringWorkClassification> WorkClassifications { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignSourceLookupDto> EngineeringCategories { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignSourceLookupDto> EstateManagedAssets { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignSourceLookupDto> ApprovedCapitalProjects { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignSourceLookupDto> MaintenanceEscalations { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignSourceLookupDto> PropertyDevelopmentNeeds { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignSourceLookupDto> PlanningConditions { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignSourceDocumentLookupDto> ManagementDirectives { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignSourceLookupDto> DefectMonitoringCases { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDesignSourceLookupDto> InfrastructureImprovementRequests { get; init; } = [];
}

public sealed class CivilEngineeringDesignSectionLookupDto
{
    public Guid SectionId { get; init; }
    public Guid DepartmentId { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CreateCivilEngineeringDesignCaseRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid ProjectId { get; set; }
    public CivilEngineeringWorksInitiationSource? InitiationSource { get; set; }
    public Guid? InitiationSourceId { get; set; }
    public Guid? InitiationSourceDocumentVersionId { get; set; }
    public Guid? EstateManagedAssetId { get; set; }
    public Guid EngineeringCategoryId { get; set; }
    public CivilEngineeringWorkClassification? WorkClassification { get; set; }
    [Required, StringLength(4000, MinimumLength = 3)] public string ScopeSummary { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 3)] public string ConstraintSummary { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 3)] public string RiskSummary { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 3)] public string Recommendation { get; set; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Directive { get; set; } = string.Empty;
    public Guid SupervisingCivilEngineerUserId { get; set; }
    public DateTime? DueAt { get; set; }
}

public sealed class CivilEngineeringDesignEvidenceRequest
{
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(40)] public string EvidenceType { get; set; } = string.Empty;
}

public sealed class CivilEngineeringDesignTransitionRequest
{
    public Guid ClientRequestId { get; set; }
    public CivilEngineeringDesignAction Action { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public DateTime? DueAt { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public List<CivilEngineeringDesignEvidenceRequest> Evidence { get; set; } = [];
}

public sealed class CivilEngineeringReconnaissanceItemDto
{
    public Guid Id { get; init; }
    public CivilEngineeringReconnaissanceItemKind Kind { get; init; }
    public string Description { get; init; } = string.Empty;
    public Guid? InformationSourceSectionId { get; init; }
    public string? InformationSourceSectionLabel { get; init; }
    public Guid? CentralDocumentRecordId { get; init; }
    public Guid? CentralDocumentVersionId { get; init; }
    public string? DocumentReference { get; init; }
    public string? DocumentTitle { get; init; }
    public string? VersionNumber { get; init; }
    public CivilEngineeringConstraintCategory? ConstraintCategory { get; init; }
    public CivilEngineeringConstraintSeverity? Severity { get; init; }
    public CivilEngineeringConstraintResolutionStatus? ResolutionStatus { get; init; }
    public bool BlocksDesign { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed class CivilEngineeringReconnaissanceReportDto
{
    public Guid Id { get; init; }
    public Guid DesignCaseId { get; init; }
    public string ReportNumber { get; init; } = string.Empty;
    public DateTime VisitDate { get; init; }
    public string SiteLocation { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string SiteConditions { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid SiteReconnaissanceTemplateId { get; init; }
    public Guid CrossSectionTemplateId { get; init; }
    public Guid PreparedByUserId { get; init; }
    public Guid? CompletedByUserId { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<CivilEngineeringReconnaissanceItemDto> Items { get; init; } = [];
}

public sealed class CivilEngineeringReconnaissanceItemRequest
{
    public CivilEngineeringReconnaissanceItemKind Kind { get; set; }
    [Required, StringLength(2000, MinimumLength = 3)] public string Description { get; set; } = string.Empty;
    public Guid? InformationSourceSectionId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    public CivilEngineeringConstraintCategory? ConstraintCategory { get; set; }
    public CivilEngineeringConstraintSeverity? Severity { get; set; }
    public CivilEngineeringConstraintResolutionStatus? ResolutionStatus { get; set; }
    public bool BlocksDesign { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateCivilEngineeringReconnaissanceRequest
{
    public Guid ClientRequestId { get; set; }
    public DateTime VisitDate { get; set; }
    [Required, StringLength(500, MinimumLength = 3)] public string SiteLocation { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Summary { get; set; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string SiteConditions { get; set; } = string.Empty;
    public List<CivilEngineeringReconnaissanceItemRequest> Items { get; set; } = [];
}

public sealed class UpdateCivilEngineeringReconnaissanceRequest : CreateCivilEngineeringReconnaissanceRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; set; } = string.Empty;
}

public sealed class CompleteCivilEngineeringReconnaissanceRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; set; } = string.Empty;
}

public sealed class CivilEngineeringDesignInputResponseDto
{
    public Guid Id { get; init; }
    public int ResponseSequence { get; init; }
    public string ResponseText { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
    public Guid RespondedByUserId { get; init; }
    public string RespondedByName { get; init; } = string.Empty;
    public DateTime RespondedAt { get; init; }
}

public sealed class CivilEngineeringDesignInputRequestDto
{
    public Guid Id { get; init; }
    public Guid DesignCaseId { get; init; }
    public Guid ProjectId { get; init; }
    public string? ReferenceNumber { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime RaisedDate { get; init; }
    public DateTime? ResponseDueDate { get; init; }
    public Guid RequestedSectionId { get; init; }
    public string RequestedSectionLabel { get; init; } = string.Empty;
    public Guid RequestedByUserId { get; init; }
    public bool BlocksDesignReadiness { get; init; }
    public bool IsDesignReady { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<CivilEngineeringDesignInputResponseDto> Responses { get; init; } = [];
}

public sealed class CreateCivilEngineeringDesignInputRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid RequestedSectionId { get; set; }
    [Required, StringLength(200, MinimumLength = 3)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Question { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Priority { get; set; } = ProjectRfiPriorities.Medium;
    public DateTime ResponseDueDate { get; set; }
    public bool BlocksDesignReadiness { get; set; } = true;
}

public sealed class SubmitCivilEngineeringDesignInputResponseRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string ResponseText { get; set; } = string.Empty;
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

public sealed class ReviewCivilEngineeringDesignInputRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public CivilEngineeringDesignInputReviewAction Action { get; set; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; set; } = string.Empty;
}

public sealed class CivilEngineeringDocumentMemberLookupDto
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDocumentVersionLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string FileExtension { get; init; } = string.Empty;
    public CivilEngineeringFileCategory FileCategory { get; init; }
    public long FileSize { get; init; }
}

public sealed class CivilEngineeringDocumentPackageLookupDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDocumentLookupsDto
{
    public CivilEngineeringDocumentNamingPolicy NamingPolicy { get; init; }
    public IReadOnlyList<string> AllowedFileExtensions { get; init; } = [];
    public int MaximumFileSizeMb { get; init; }
    public string MetadataTemplateCode { get; init; } = string.Empty;
    public bool AllowAuthorizedPreview { get; init; }
    public bool AllowAuthorizedDownload { get; init; }
    public IReadOnlyList<CivilEngineeringDocumentMemberLookupDto> Owners { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDocumentMemberLookupDto> Reviewers { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDocumentPackageLookupDto> WorkPackages { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDocumentVersionLookupDto> Documents { get; init; } = [];
}

public sealed class CivilEngineeringDocumentDto
{
    public Guid Id { get; init; }
    public Guid DesignCaseId { get; init; }
    public Guid? ProjectPackageId { get; init; }
    public string? ProjectPackageLabel { get; init; }
    public Guid DocumentKey { get; init; }
    public Guid? SupersedesDocumentId { get; init; }
    public CivilEngineeringDesignDiscipline Discipline { get; init; }
    public CivilEngineeringFileCategory FileCategory { get; init; }
    public string FileExtension { get; init; } = string.Empty;
    public int SequenceNumber { get; init; }
    public int RevisionNumber { get; init; }
    public string ExpectedDocumentReference { get; init; } = string.Empty;
    public string DocumentReference { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public string DmsVersion { get; init; } = string.Empty;
    public Guid OwnerUserId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public Guid ReviewerUserId { get; init; }
    public string ReviewerName { get; init; } = string.Empty;
    public CivilEngineeringDocumentStatus Status { get; init; }
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CreateCivilEngineeringDocumentRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? SupersedesDocumentId { get; set; }
    public CivilEngineeringDesignDiscipline Discipline { get; set; }
    [Range(1, 999999)] public int SequenceNumber { get; set; }
    [Range(0, 9999)] public int RevisionNumber { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid ReviewerUserId { get; set; }
}

public class SubmitCivilEngineeringDocumentRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReviewCivilEngineeringDocumentRequest : SubmitCivilEngineeringDocumentRequest
{
    public bool Approve { get; set; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; set; } = string.Empty;
}

public sealed class CivilEngineeringProjectEngineerCandidateDto
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string SourceCivilRole { get; init; } = string.Empty;
}

public sealed class CivilEngineeringProjectEngineerAssignmentLookupsDto
{
    public IReadOnlyList<CivilEngineeringProjectEngineerCandidateDto> Candidates { get; init; } = [];
    public IReadOnlyList<CivilEngineeringProjectEngineerAuthority> Authorities { get; init; } = [];
    public CivilEngineeringProjectEngineerAuthority DefaultAuthority { get; init; }
}

public sealed class CivilEngineeringProjectEngineerAssignmentDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectMemberId { get; init; }
    public Guid AssignedUserId { get; init; }
    public string AssignedUserName { get; init; } = string.Empty;
    public string SourceCivilRole { get; init; } = string.Empty;
    public string ProjectRole { get; init; } = string.Empty;
    public CivilEngineeringProjectEngineerAuthority Authority { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
    public string? Reason { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class AssignCivilEngineeringProjectEngineerRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid AssignedUserId { get; set; }
    public CivilEngineeringProjectEngineerAuthority Authority { get; set; }
    public DateTime EffectiveFrom { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
}

public sealed class EndCivilEngineeringProjectEngineerAssignmentRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public DateTime EffectiveTo { get; set; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; set; } = string.Empty;
}

public sealed class CivilEngineeringProjectEngineerAssignmentRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public object? Before { get; init; }
    public object? After { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class CivilEngineeringSiteInstructionDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringSiteInstructionLookupsDto
{
    public Guid ProjectEngineerAssignmentId { get; init; }
    public string ProjectEngineerName { get; init; } = string.Empty;
    public Guid ProjectManagerUserId { get; init; }
    public string ProjectManagerName { get; init; } = string.Empty;
    public Guid ContractorBusinessPartnerId { get; init; }
    public string ContractorName { get; init; } = string.Empty;
    public bool RequiresDmsEvidence { get; init; }
    public IReadOnlyList<CivilEngineeringSiteInstructionDocumentLookupDto> Documents { get; init; } = [];
}

public sealed class CivilEngineeringSiteInstructionEvidenceDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string EvidenceRole { get; init; } = string.Empty;
    public string DocumentReference { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringSiteInstructionResponseDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public Guid? CentralDocumentRecordId { get; init; }
    public Guid? CentralDocumentVersionId { get; init; }
}

public sealed class CivilEngineeringSiteInstructionRoutingDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectSiteInstructionId { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid ProjectEngineerAssignmentId { get; init; }
    public string ProjectEngineerName { get; init; } = string.Empty;
    public Guid ProjectManagerUserId { get; init; }
    public string ProjectManagerName { get; init; } = string.Empty;
    public Guid ContractorBusinessPartnerId { get; init; }
    public string ContractorName { get; init; } = string.Empty;
    public Guid? ContractId { get; init; }
    public int InstructionVersion { get; init; }
    public Guid? SupersedesRoutingId { get; init; }
    public Guid? ContractorResponseReviewedById { get; init; }
    public DateTime? ContractorResponseReviewedAt { get; init; }
    public string? ContractorResponseReviewReason { get; init; }
    public Guid? ClosedById { get; init; }
    public DateTime? ClosedAt { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public DateTime CreatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<CivilEngineeringSiteInstructionEvidenceDto> Evidence { get; init; } = [];
    public IReadOnlyList<CivilEngineeringSiteInstructionResponseDto> Responses { get; init; } = [];
}

public sealed class CreateCivilEngineeringSiteInstructionRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid ProjectEngineerAssignmentId { get; set; }
    [Required, StringLength(100)] public string ReferenceNumber { get; set; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Description { get; set; } = string.Empty;
    public Guid? ProjectPhaseId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public decimal? EstimatedCostImpact { get; set; }
    public int? ScheduleImpactDays { get; set; }
    public Guid? SupersedesRoutingId { get; set; }
    [MinLength(1)] public List<CivilEngineeringSiteInstructionEvidenceRequest> Evidence { get; set; } = [];
}

public sealed class CivilEngineeringSiteInstructionEvidenceRequest
{
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

public sealed class ProcessCivilEngineeringSiteInstructionRoutingRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public bool Approve { get; set; }
    [Required, StringLength(2000, MinimumLength = 3)] public string Reason { get; set; } = string.Empty;
}

public sealed class RespondToCivilEngineeringSiteInstructionRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 3)] public string Message { get; set; } = string.Empty;
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
}

public sealed class ReviewCivilEngineeringSiteInstructionResponseRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public bool Approve { get; set; }
    [Required, StringLength(2000, MinimumLength = 3)] public string Reason { get; set; } = string.Empty;
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

public sealed class FollowUpCivilEngineeringSiteInstructionRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 3)] public string Reason { get; set; } = string.Empty;
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

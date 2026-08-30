using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Services.Projects;

namespace ErpSystem.Core.Entities.Projects;

/// <summary>
/// Governance envelope for an existing Projects RFI. The RFI question/header remains
/// authoritative in <see cref="ProjectRfi"/>; this record owns only Civil routing,
/// frozen policy lineage and the immutable response history.
/// </summary>
[Table("ProjectCivilRfiRoutings")]
public sealed class ProjectCivilRfiRouting : TenantEntity, ICivilEngineeringWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid ProjectRfiId { get; set; }
    public Guid ProjectEngineerAssignmentId { get; set; }
    public Guid ProjectManagerUserId { get; set; }
    public Guid ExternalBusinessPartnerId { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Status { get; set; } = CivilEngineeringRfiRoutingStatuses.AwaitingProjectEngineerResponse;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
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
    public ProjectRfi ProjectRfi { get; set; } = null!;
    public ProjectCivilProjectEngineerAssignment ProjectEngineerAssignment { get; set; } = null!;
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public ICollection<ProjectCivilRfiEvidence> Evidence { get; set; } = [];
    public ICollection<ProjectCivilRfiResponse> Responses { get; set; } = [];
    public ICollection<ProjectCivilRfiRevision> Revisions { get; set; } = [];
}

public static class CivilEngineeringRfiRoutingStatuses
{
    public const string AwaitingProjectEngineerResponse = "AwaitingProjectEngineerResponse";
    public const string AwaitingProjectManagerApproval = "AwaitingProjectManagerApproval";
    public const string ReturnedToProjectEngineer = "ReturnedToProjectEngineer";
    public const string Answered = "Answered";
    public const string Closed = "Closed";
}

[Table("ProjectCivilRfiEvidence")]
public sealed class ProjectCivilRfiEvidence : TenantEntity
{
    public Guid RoutingId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(40)] public string EvidenceRole { get; set; } = "Question";
    public Guid LinkedByUserId { get; set; }
    public DateTime LinkedAt { get; set; }

    public ProjectCivilRfiRouting Routing { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("ProjectCivilRfiResponses")]
public sealed class ProjectCivilRfiResponse : TenantEntity
{
    public Guid RoutingId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public int Sequence { get; set; }
    [Required, StringLength(4000)] public string ResponseText { get; set; } = string.Empty;
    public Guid RespondedByUserId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;

    public ProjectCivilRfiRouting Routing { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("ProjectCivilRfiRevisions")]
public sealed class ProjectCivilRfiRevision : TenantEntity
{
    public Guid RoutingId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilRfiRouting Routing { get; set; } = null!;
}

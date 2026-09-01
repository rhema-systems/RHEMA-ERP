using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Services.Projects;

namespace ErpSystem.Core.Entities.Projects;

/// <summary>
/// Governance envelope for the authoritative Projects site-instruction record.
/// It deliberately stores routing/control lineage only; title, scope and commercial
/// impacts continue to be owned by <see cref="ProjectSiteInstruction"/>.
/// </summary>
[Table("ProjectCivilSiteInstructionRoutings")]
public sealed class ProjectCivilSiteInstructionRouting : TenantEntity, ICivilEngineeringWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid ProjectSiteInstructionId { get; set; }
    public Guid ProjectEngineerAssignmentId { get; set; }
    public Guid ProjectManagerUserId { get; set; }
    public Guid ContractorBusinessPartnerId { get; set; }
    /// <summary>Snapshot of the authoritative Procurement contract where the project has one.</summary>
    public Guid? ContractId { get; set; }
    /// <summary>Monotonic version within one governed instruction lineage.</summary>
    public int InstructionVersion { get; set; } = 1;
    public Guid? SupersedesRoutingId { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Status { get; set; } = CivilEngineeringSiteInstructionRoutingStatuses.PendingApproval;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Pending";
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public Guid? ContractorResponseReviewedById { get; set; }
    public DateTime? ContractorResponseReviewedAt { get; set; }
    [StringLength(2000)] public string? ContractorResponseReviewReason { get; set; }
    public Guid? ClosedById { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectSiteInstruction ProjectSiteInstruction { get; set; } = null!;
    public ProjectCivilProjectEngineerAssignment ProjectEngineerAssignment { get; set; } = null!;
    public ProjectCivilSiteInstructionRouting? SupersedesRouting { get; set; }
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public ICollection<ProjectCivilSiteInstructionEvidence> Evidence { get; set; } = [];
    public ICollection<ProjectCivilSiteInstructionResponse> Responses { get; set; } = [];
    public ICollection<ProjectCivilSiteInstructionRevision> Revisions { get; set; } = [];
}

public static class CivilEngineeringSiteInstructionRoutingStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string AwaitingContractorAcknowledgement = "AwaitingContractorAcknowledgement";
    public const string ContractorResponded = "ContractorResponded";
    public const string AwaitingEngineeringReview = "AwaitingEngineeringReview";
    public const string AwaitingEngineeringFollowUp = "AwaitingEngineeringFollowUp";
    public const string Closed = "Closed";
    public const string Rejected = "Rejected";
    public const string Superseded = "Superseded";
}

[Table("ProjectCivilSiteInstructionEvidence")]
public sealed class ProjectCivilSiteInstructionEvidence : TenantEntity
{
    public Guid RoutingId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(40)] public string EvidenceRole { get; set; } = "Instruction";
    public Guid LinkedByUserId { get; set; }
    public DateTime LinkedAt { get; set; }

    public ProjectCivilSiteInstructionRouting Routing { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("ProjectCivilSiteInstructionResponses")]
public sealed class ProjectCivilSiteInstructionResponse : TenantEntity
{
    public Guid RoutingId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public int Sequence { get; set; }
    [Required, StringLength(40)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Message { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;

    public ProjectCivilSiteInstructionRouting Routing { get; set; } = null!;
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
}

[Table("ProjectCivilSiteInstructionRevisions")]
public sealed class ProjectCivilSiteInstructionRevision : TenantEntity
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

    public ProjectCivilSiteInstructionRouting Routing { get; set; } = null!;
}

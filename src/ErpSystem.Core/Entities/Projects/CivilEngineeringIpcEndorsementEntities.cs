using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;

namespace ErpSystem.Core.Entities.Projects;

/// <summary>
/// Civil Engineering's governed review of a QS-owned interim payment certificate (IPC).
/// The record contains only the review control and immutable evidence lineage; QS remains
/// the owner of the certificate, its workflow, AP handoff and all Finance postings.
/// </summary>
[Table("ProjectCivilIpcEndorsements")]
public sealed class ProjectCivilIpcEndorsement : TenantEntity
{
    public Guid ProjectPaymentCertificateId { get; set; }
    public Guid ProjectId { get; set; }
    public int Sequence { get; set; }
    public Guid ProjectEngineerAssignmentId { get; set; }
    public Guid SubmittedById { get; set; }
    public DateTime SubmittedAt { get; set; }
    [Required, StringLength(2000)] public string SubmissionNotes { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Status { get; set; } = CivilEngineeringIpcEndorsementStatuses.AwaitingProjectEngineerReview;
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [StringLength(2000)] public string? ReviewNotes { get; set; }
    public Guid? EvidenceDocumentRecordId { get; set; }
    public Guid? EvidenceDocumentVersionId { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(80)] public string EvidenceMetadataTemplateCodeSnapshot { get; set; } = string.Empty;
    public bool RequiresDmsEvidence { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProjectPaymentCertificate ProjectPaymentCertificate { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public ProjectCivilProjectEngineerAssignment ProjectEngineerAssignment { get; set; } = null!;
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public CentralDocumentMetadataTemplate EvidenceMetadataTemplate { get; set; } = null!;
    public CentralDocumentRecord? EvidenceDocumentRecord { get; set; }
    public CentralDocumentVersion? EvidenceDocumentVersion { get; set; }
    public ICollection<ProjectCivilIpcEndorsementRevision> Revisions { get; set; } = [];
}

public static class CivilEngineeringIpcEndorsementStatuses
{
    public const string AwaitingProjectEngineerReview = "AwaitingProjectEngineerReview";
    public const string Endorsed = "Endorsed";
    public const string ReturnedToProjectsCoordinator = "ReturnedToProjectsCoordinator";

    public static IReadOnlyList<string> All { get; } =
    [
        AwaitingProjectEngineerReview,
        Endorsed,
        ReturnedToProjectsCoordinator
    ];
}

[Table("ProjectCivilIpcEndorsementRevisions")]
public sealed class ProjectCivilIpcEndorsementRevision : TenantEntity
{
    public Guid IpcEndorsementId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilIpcEndorsement IpcEndorsement { get; set; } = null!;
}

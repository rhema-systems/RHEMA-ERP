using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;

namespace ErpSystem.Core.Entities.Projects;

public enum CivilEngineeringDirectTaskFeedbackAction
{
    Acknowledge = 0,
    UpdateProgress = 1,
    Complete = 2,
    Accept = 3,
    Return = 4
}

/// <summary>
/// Governed overlay for a Project work item assigned by the Civil Engineering section.
/// ProjectWorkItem remains the authoritative Projects task record; this record supplies
/// the Civil policy snapshot, controlled assignee and immutable assignment history.
/// </summary>
[Table("ProjectCivilDirectTaskControls")]
public sealed class ProjectCivilDirectTaskControl : TenantEntity, ICivilEngineeringWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid AssignedToUserId { get; set; }
    public Guid AssignedRoleId { get; set; }
    [Required, StringLength(256)] public string AssignedRoleName { get; set; } = string.Empty;
    public CivilEngineeringUrgency Urgency { get; set; }
    public bool IsUrgentPath { get; set; }
    [StringLength(1000)] public string? UrgencyReason { get; set; }
    public DateTime? UrgentResponseDueAt { get; set; }
    public DateTime? UrgentEscalatedAt { get; set; }
    public Guid? UrgentEscalationClientRequestId { get; set; }
    [StringLength(64)] public string? UrgentEscalationRequestHash { get; set; }
    public DateTime DueDate { get; set; }
    [Required, StringLength(4000)] public string Instructions { get; set; } = string.Empty;
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid FeedbackMetadataTemplateId { get; set; }
    [Required, StringLength(120)] public string FeedbackMetadataTemplateCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    [Required, StringLength(40)] public string Status { get; set; } = "Assigned";
    [Required, StringLength(40)] public string ApprovalStatus { get; set; } = "Draft";
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal ProgressPercent { get; set; }
    public Guid? AcknowledgedById { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? AcceptedById { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid? LastFeedbackClientRequestId { get; set; }
    [StringLength(64)] public string? LastFeedbackRequestHash { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectWorkItem WorkItem { get; set; } = null!;
    public ApplicationUser AssignedToUser { get; set; } = null!;
    public ApplicationRole AssignedRole { get; set; } = null!;
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public CentralDocumentMetadataTemplate FeedbackMetadataTemplate { get; set; } = null!;
    public ICollection<ProjectCivilDirectTaskRevision> Revisions { get; set; } = [];
    public ICollection<ProjectCivilDirectTaskFeedbackEntry> FeedbackEntries { get; set; } = [];
}

/// <summary>Append-only assignment audit owned by the Civil control overlay.</summary>
[Table("ProjectCivilDirectTaskRevisions")]
public sealed class ProjectCivilDirectTaskRevision : TenantEntity
{
    public Guid DirectTaskControlId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;

    public ProjectCivilDirectTaskControl DirectTaskControl { get; set; } = null!;
}

/// <summary>Append-only assignee and reviewer feedback for a governed Civil task.</summary>
[Table("ProjectCivilDirectTaskFeedbackEntries")]
public sealed class ProjectCivilDirectTaskFeedbackEntry : TenantEntity
{
    public Guid DirectTaskControlId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public CivilEngineeringDirectTaskFeedbackAction Action { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? ProgressPercent { get; set; }
    [StringLength(2000)] public string? Message { get; set; }
    public Guid ActorUserId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? MeasurementValue { get; set; }
    public Guid? MeasurementUnitId { get; set; }
    public DateTime? CapturedOfflineAtUtc { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [StringLength(40)] public string? WorkflowOutcome { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;

    public ProjectCivilDirectTaskControl DirectTaskControl { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
    public UnitOfMeasure? MeasurementUnit { get; set; }
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
}

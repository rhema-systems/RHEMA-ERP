using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.QuantitySurvey;

[Table("QuantitySurveyConfigurationProfiles")]
public sealed class QuantitySurveyConfigurationProfile : TenantEntity
{
    public Guid ProfileKey { get; set; } = Guid.NewGuid();
    [Required, StringLength(50)] public string ProfileCode { get; set; } = "TDC-QUANTITY-SURVEY";
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public QuantitySurveyConfigurationProfileStatus LifecycleStatus { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    public bool IsDefault { get; set; }
    public Guid? SupersedesProfileId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? RetiredAt { get; set; }
    public Guid? RetiredById { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<QuantitySurveyConfigurationDecision> Decisions { get; set; } = new List<QuantitySurveyConfigurationDecision>();
    public ICollection<QuantitySurveyConfigurationEvidenceLink> EvidenceLinks { get; set; } = new List<QuantitySurveyConfigurationEvidenceLink>();
}

[Table("QuantitySurveyConfigurationDecisions")]
public sealed class QuantitySurveyConfigurationDecision : TenantEntity
{
    public Guid ProfileId { get; set; }
    [Required, StringLength(10)] public string DecisionKey { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    [Required, StringLength(200)] public string OwnerGroup { get; set; } = string.Empty;
    public QuantitySurveyConfigurationDecisionStatus Status { get; set; }
    public QuantitySurveyConfigurationApprovalStatus ApprovalStatus { get; set; }
    public QuantitySurveyConfigurationEvidenceStatus EvidenceStatus { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string ValueJson { get; set; } = "{}";
    public DateTime? DecisionDate { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovalWorkflowInstanceId { get; set; }
    [StringLength(500)] public string? ApprovalReference { get; set; }
    [StringLength(1000)] public string? SourceLineage { get; set; }
    public Guid? SourceDecisionId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public QuantitySurveyConfigurationProfile Profile { get; set; } = null!;
    public ICollection<QuantitySurveyConfigurationEvidenceLink> EvidenceLinks { get; set; } = new List<QuantitySurveyConfigurationEvidenceLink>();
}

[Table("QuantitySurveyConfigurationEvidenceLinks")]
public sealed class QuantitySurveyConfigurationEvidenceLink : TenantEntity
{
    public Guid ProfileId { get; set; }
    public Guid DecisionId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [Required, StringLength(100)] public string EvidenceType { get; set; } = string.Empty;
    [StringLength(1000)] public string? ExternalReference { get; set; }
    [StringLength(128)] public string? Checksum { get; set; }
    public Guid LinkedById { get; set; }
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    public QuantitySurveyConfigurationProfile Profile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision Decision { get; set; } = null!;
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
}

[Table("QuantitySurveyConfigurationRevisions")]
public sealed class QuantitySurveyConfigurationRevision : TenantEntity
{
    public Guid ProfileId { get; set; }
    public Guid? DecisionId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Result { get; set; } = "Succeeded";
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? AfterJson { get; set; }
}

[Table("QuantitySurveyBoqImportSessions")]
public sealed class QuantitySurveyBoqImportSession : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    public Guid? FileUploadRecordId { get; set; }

    [Required, StringLength(128)] public string PreviewTokenHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string FileHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string NormalizedPayloadHash { get; set; } = string.Empty;
    [Required, StringLength(20)] public string TemplateVersion { get; set; } = string.Empty;
    [Required, StringLength(255)] public string OriginalFileName { get; set; } = string.Empty;
    [StringLength(100)] public string? IdempotencyKey { get; set; }
    public QuantitySurveyBoqImportStatus Status { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int LineCount { get; set; }
    public int ErrorCount { get; set; }
    public int CommittedLineCount { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string NormalizedPayloadJson { get; set; } = "[]";
    [Required, Column(TypeName = "nvarchar(max)")] public string IssuesJson { get; set; } = "[]";
    public Guid? ReconciledByUserId { get; set; }
    public DateTime? ReconciledAt { get; set; }
    [StringLength(500)] public string? ReconciliationDeclaration { get; set; }
    public DateTime? CommittedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
}

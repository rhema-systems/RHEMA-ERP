using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Projects;

[Table("CivilEngineeringConfigurationProfiles")]
public sealed class CivilEngineeringConfigurationProfile : TenantEntity
{
    public Guid ProfileKey { get; set; } = Guid.NewGuid();
    [Required, StringLength(50)] public string ProfileCode { get; set; } = "TDC-CIVIL-ENGINEERING";
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public CivilEngineeringConfigurationProfileStatus LifecycleStatus { get; set; }
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
    public ICollection<CivilEngineeringConfigurationDecision> Decisions { get; set; } = new List<CivilEngineeringConfigurationDecision>();
    public ICollection<CivilEngineeringConfigurationEvidenceLink> EvidenceLinks { get; set; } = new List<CivilEngineeringConfigurationEvidenceLink>();
}

[Table("CivilEngineeringConfigurationDecisions")]
public sealed class CivilEngineeringConfigurationDecision : TenantEntity
{
    public Guid ProfileId { get; set; }
    [Required, StringLength(11)] public string ConfigurationKey { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    [Required, StringLength(200)] public string OwnerGroup { get; set; } = string.Empty;
    public CivilEngineeringConfigurationDecisionStatus Status { get; set; }
    public CivilEngineeringConfigurationApprovalStatus ApprovalStatus { get; set; }
    public CivilEngineeringConfigurationEvidenceStatus EvidenceStatus { get; set; }
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
    public CivilEngineeringConfigurationProfile Profile { get; set; } = null!;
    public ICollection<CivilEngineeringConfigurationEvidenceLink> EvidenceLinks { get; set; } = new List<CivilEngineeringConfigurationEvidenceLink>();
}

[Table("CivilEngineeringConfigurationEvidenceLinks")]
public sealed class CivilEngineeringConfigurationEvidenceLink : TenantEntity
{
    public Guid ProfileId { get; set; }
    public Guid DecisionId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(100)] public string EvidenceType { get; set; } = string.Empty;
    [StringLength(128)] public string? Checksum { get; set; }
    public Guid LinkedById { get; set; }
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    public CivilEngineeringConfigurationProfile Profile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision Decision { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("CivilEngineeringConfigurationRevisions")]
public sealed class CivilEngineeringConfigurationRevision : TenantEntity
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


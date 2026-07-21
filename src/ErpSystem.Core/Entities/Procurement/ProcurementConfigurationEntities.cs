using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementConfigurationProfiles")]
public class ProcurementConfigurationProfile : TenantEntity
{
    public Guid ProfileKey { get; set; } = Guid.NewGuid();

    [Required, StringLength(50)]
    public string ProfileCode { get; set; } = "TDC-PROCUREMENT";

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public int Version { get; set; } = 1;
    public ProcurementConfigurationProfileStatus LifecycleStatus { get; set; } = ProcurementConfigurationProfileStatus.Draft;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }

    [StringLength(1000)]
    public string? ChangeSummary { get; set; }

    public bool IsDefault { get; set; }
    public Guid? SupersedesProfileId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? RetiredAt { get; set; }
    public Guid? RetiredById { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<ProcurementConfigurationDecision> Decisions { get; set; } = new List<ProcurementConfigurationDecision>();
    public virtual ICollection<ProcurementConfigurationEvidenceLink> EvidenceLinks { get; set; } = new List<ProcurementConfigurationEvidenceLink>();
}

[Table("ProcurementConfigurationDecisions")]
public class ProcurementConfigurationDecision : TenantEntity
{
    public Guid ProfileId { get; set; }

    [Required, StringLength(7)]
    public string DecisionKey { get; set; } = string.Empty;

    public int SchemaVersion { get; set; } = 1;

    [Required, StringLength(200)]
    public string OwnerGroup { get; set; } = string.Empty;

    public ProcurementConfigurationDecisionStatus Status { get; set; } = ProcurementConfigurationDecisionStatus.Draft;
    public ProcurementConfigurationApprovalStatus ApprovalStatus { get; set; } = ProcurementConfigurationApprovalStatus.Pending;
    public ProcurementConfigurationEvidenceStatus EvidenceStatus { get; set; } = ProcurementConfigurationEvidenceStatus.Missing;

    [Required, Column(TypeName = "nvarchar(max)")]
    public string ValueJson { get; set; } = "{}";

    public DateTime? DecisionDate { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovalWorkflowInstanceId { get; set; }

    [StringLength(500)]
    public string? ApprovalReference { get; set; }

    [StringLength(1000)]
    public string? SourceLineage { get; set; }

    public Guid? SourceDecisionId { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ProcurementConfigurationProfile Profile { get; set; } = null!;
    public virtual ICollection<ProcurementConfigurationEvidenceLink> EvidenceLinks { get; set; } = new List<ProcurementConfigurationEvidenceLink>();
}

[Table("ProcurementConfigurationEvidenceLinks")]
public class ProcurementConfigurationEvidenceLink : TenantEntity
{
    public Guid ProfileId { get; set; }
    public Guid DecisionId { get; set; }
    public Guid? FileUploadRecordId { get; set; }

    [Required, StringLength(100)]
    public string EvidenceType { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ExternalReference { get; set; }

    [StringLength(128)]
    public string? Checksum { get; set; }

    [StringLength(2000)]
    public string? ReferenceMetadataJson { get; set; }

    public Guid UploadedById { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public virtual ProcurementConfigurationProfile Profile { get; set; } = null!;
    public virtual ProcurementConfigurationDecision Decision { get; set; } = null!;
    public virtual FileUploadRecord? FileUploadRecord { get; set; }
}

[Table("ProcurementConfigurationRevisions")]
public class ProcurementConfigurationRevision : TenantEntity
{
    public Guid ProfileId { get; set; }
    public Guid? DecisionId { get; set; }

    [Required, StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Result { get; set; } = "Succeeded";

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    public Guid ActorUserId { get; set; }

    [Required, StringLength(300)]
    public string ActorName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ActorRoles { get; set; }

    [StringLength(1000)]
    public string? Reason { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? BeforeJson { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? AfterJson { get; set; }
}

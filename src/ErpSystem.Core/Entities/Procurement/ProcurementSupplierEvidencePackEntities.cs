using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementSupplierEvidencePackVersions")]
public sealed class ProcurementSupplierEvidencePackVersion : TenantEntity
{
    public Guid PackKey { get; set; } = Guid.NewGuid();

    [Required, StringLength(50)]
    public string PackCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public ProcurementSupplierRegistrationCategory Category { get; set; }
    public int Version { get; set; } = 1;
    public ProcurementSupplierEvidencePackStatus Status { get; set; } =
        ProcurementSupplierEvidencePackStatus.Draft;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }

    public Guid SourceConfigurationProfileId { get; set; }

    [Required, StringLength(50)]
    public string SourceConfigurationProfileCode { get; set; } = string.Empty;

    public int SourceConfigurationProfileVersion { get; set; }
    [Required, StringLength(100)]
    public string CreationCorrelationId { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastOperation { get; set; } = "Created";

    [Required, StringLength(100)]
    public string LastOperationCorrelationId { get; set; } = string.Empty;

    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SupersedesVersionId { get; set; }

    [StringLength(1000)]
    public string? ChangeSummary { get; set; }

    [StringLength(1000)]
    public string? ReviewComment { get; set; }

    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? RetiredById { get; set; }
    public DateTime? RetiredAtUtc { get; set; }

    [Required, Column(TypeName = "nvarchar(max)")]
    public string LifecycleSnapshotJson { get; set; } = "{}";

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementConfigurationProfile SourceConfigurationProfile { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ProcurementSupplierEvidencePackVersion? SupersedesVersion { get; set; }
    public ICollection<ProcurementSupplierEvidenceRequirement> Requirements { get; set; } =
        new List<ProcurementSupplierEvidenceRequirement>();
}

[Table("ProcurementSupplierEvidenceRequirements")]
public sealed class ProcurementSupplierEvidenceRequirement : TenantEntity
{
    public Guid PackVersionId { get; set; }

    [Required, StringLength(50)]
    public string RequirementCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public ProcurementSupplierEvidenceRequirementKind Kind { get; set; }

    [StringLength(100)]
    public string? DocumentType { get; set; }

    public bool IsMandatory { get; set; } = true;

    [StringLength(100)]
    public string? ClassificationScheme { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? AllowedClassificationsJson { get; set; }

    public ProcurementSupplierEvidenceValidityMode ValidityMode { get; set; }
    public int? MinimumRemainingDays { get; set; }
    public int ApprovalStepOrder { get; set; }

    [Required, StringLength(100)]
    public string ApprovalStepName { get; set; } = string.Empty;

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    [Required, Column(TypeName = "nvarchar(max)")]
    public string AllowedMimeTypesJson { get; set; } = "[]";

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementSupplierEvidencePackVersion PackVersion { get; set; } = null!;
}

[Table("ProcurementSupplierRegistrationEvidencePackBindings")]
public sealed class ProcurementSupplierRegistrationEvidencePackBinding : TenantEntity
{
    public Guid RegistrationId { get; set; }
    public Guid PackVersionId { get; set; }
    public ProcurementSupplierRegistrationCategory RegistrationCategory { get; set; }

    [Required, StringLength(50)]
    public string PackCode { get; set; } = string.Empty;

    public int PackVersion { get; set; }
    public DateTime BoundAtUtc { get; set; }
    public Guid BoundById { get; set; }

    [Required, Column(TypeName = "nvarchar(max)")]
    public string PackSnapshotJson { get; set; } = "{}";

    [Required, StringLength(64)]
    public string PackSnapshotHash { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public BusinessPartnerRegistration Registration { get; set; } = null!;
    public ProcurementSupplierEvidencePackVersion PackVersionRecord { get; set; } = null!;
}

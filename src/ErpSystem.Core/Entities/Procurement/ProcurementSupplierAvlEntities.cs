using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementSupplierAvlRegisters")]
public sealed class ProcurementSupplierAvlRegister : TenantEntity
{
    [Required, StringLength(50)] public string RegisterCode { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public int ReviewYear { get; set; }
    public ProcurementSupplierAvlRegisterStatus Status { get; set; } =
        ProcurementSupplierAvlRegisterStatus.Draft;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ScheduledRetirementAtUtc { get; set; }
    public Guid? SupersededByRegisterId { get; set; }

    public Guid PolicyDecisionId { get; set; }
    public Guid PolicyProfileId { get; set; }
    [Required, StringLength(50)] public string PolicyProfileCode { get; set; } = string.Empty;
    public int PolicyProfileVersion { get; set; }
    public int ReviewFrequencyMonths { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string PolicySnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string PolicyValueHash { get; set; } = string.Empty;

    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    [StringLength(1000)] public string? ReviewComment { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? RetiredById { get; set; }
    public DateTime? RetiredAtUtc { get; set; }

    [Required, StringLength(100)] public string CreationCorrelationId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastOperationCorrelationId { get; set; } = string.Empty;
    [Required, StringLength(50)] public string LastOperation { get; set; } = "Created";
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementConfigurationDecision PolicyDecision { get; set; } = null!;
    public ProcurementConfigurationProfile PolicyProfile { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ProcurementSupplierAvlRegister? SupersededByRegister { get; set; }
    public ICollection<ProcurementSupplierAvlEntry> Entries { get; set; } =
        new List<ProcurementSupplierAvlEntry>();
    public ICollection<ProcurementSupplierAvlPublicationSnapshot> PublicationSnapshots { get; set; } =
        new List<ProcurementSupplierAvlPublicationSnapshot>();
}

[Table("ProcurementSupplierAvlEntries")]
public sealed class ProcurementSupplierAvlEntry : TenantEntity
{
    public Guid RegisterId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public ProcurementSupplierAvlEntryStatus Status { get; set; } =
        ProcurementSupplierAvlEntryStatus.Active;
    public Guid DueDiligenceReviewId { get; set; }
    public Guid? RegistrationId { get; set; }
    public Guid? EvidencePackVersionId { get; set; }
    public Guid? QualifiedListEntryId { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public Guid AddedById { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
    public Guid? SuspendedById { get; set; }
    [StringLength(1000)] public string? SuspensionReason { get; set; }
    public DateTime? ReinstatedAtUtc { get; set; }
    public Guid? ReinstatedById { get; set; }
    public DateTime? ExpiredAtUtc { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string EligibilitySnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string EligibilityDecisionHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementSupplierAvlRegister Register { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
    public ProcurementSupplierDueDiligenceReview DueDiligenceReview { get; set; } = null!;
    public BusinessPartnerRegistration? Registration { get; set; }
    public ProcurementSupplierEvidencePackVersion? EvidencePackVersion { get; set; }
    public ProcurementQualifiedListEntry? QualifiedListEntry { get; set; }
    public ICollection<ProcurementSupplierAvlEntryStatusHistory> StatusHistory { get; set; } =
        new List<ProcurementSupplierAvlEntryStatusHistory>();
}

[Table("ProcurementSupplierAvlEntryStatusHistories")]
public sealed class ProcurementSupplierAvlEntryStatusHistory : TenantEntity
{
    public Guid RegisterId { get; set; }
    public Guid EntryId { get; set; }
    public ProcurementSupplierAvlEntryAction Action { get; set; }
    public ProcurementSupplierAvlEntryStatus BeforeStatus { get; set; }
    public ProcurementSupplierAvlEntryStatus AfterStatus { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string EvidenceJson { get; set; } = "[]";
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementSupplierAvlRegister Register { get; set; } = null!;
    public ProcurementSupplierAvlEntry Entry { get; set; } = null!;
}

[Table("ProcurementSupplierAvlPublicationSnapshots")]
public sealed class ProcurementSupplierAvlPublicationSnapshot : TenantEntity
{
    public Guid RegisterId { get; set; }
    public int Sequence { get; set; } = 1;
    public DateTime PublishedAtUtc { get; set; }
    public Guid PublishedById { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementSupplierAvlRegister Register { get; set; } = null!;
}

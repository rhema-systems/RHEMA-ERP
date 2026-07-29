using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementFrameworkAgreements")]
public sealed class ProcurementFrameworkAgreement : TenantEntity
{
    public Guid AgreementKey { get; set; } = Guid.NewGuid();
    [Required, StringLength(50)] public string AgreementNumber { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int Version { get; set; } = 1;
    public ProcurementFrameworkAgreementStatus Status { get; set; } =
        ProcurementFrameworkAgreementStatus.Draft;

    public Guid BusinessPartnerId { get; set; }
    public ProcurementAwardReadinessSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    [Required, StringLength(100)] public string SourceReference { get; set; } = string.Empty;
    public Guid AwardReadinessDecisionId { get; set; }
    [Required, StringLength(64)] public string SourceIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string SupplierEligibilityDecisionHash { get; set; } = string.Empty;

    [Required, StringLength(50)] public string PriceListReference { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int PriceListVersion { get; set; } = 1;
    [Column(TypeName = "decimal(18,2)")] public decimal CeilingAmount { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveToUtc { get; set; }

    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SupersedesAgreementId { get; set; }
    public Guid? SupersededByAgreementId { get; set; }

    [StringLength(1000)] public string? Description { get; set; }
    [StringLength(1000)] public string? TermsSummary { get; set; }
    [StringLength(1000)] public string? ReviewComment { get; set; }
    public Guid? SubmittedById { get; set; }
    [StringLength(300)] public string? SubmittedByName { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }
    [StringLength(300)] public string? PublishedByName { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }
    [StringLength(300)] public string? RejectedByName { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public Guid? TerminatedById { get; set; }
    public DateTime? TerminatedAtUtc { get; set; }
    [StringLength(1000)] public string? TerminationReason { get; set; }

    [Required, StringLength(100)] public string CreationCorrelationId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastOperationCorrelationId { get; set; } = string.Empty;
    [Required, StringLength(50)] public string LastOperation { get; set; } = "Created";
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public BusinessPartner BusinessPartner { get; set; } = null!;
    public ProcurementAwardReadinessDecision AwardReadinessDecision { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ProcurementFrameworkAgreement? SupersedesAgreement { get; set; }
    public ICollection<ProcurementFrameworkAgreementCategory> Categories { get; set; } =
        new List<ProcurementFrameworkAgreementCategory>();
    public ICollection<ProcurementFrameworkPriceListLine> PriceLines { get; set; } =
        new List<ProcurementFrameworkPriceListLine>();
    public ICollection<ProcurementFrameworkCallOffAuthority> CallOffAuthorities { get; set; } =
        new List<ProcurementFrameworkCallOffAuthority>();
    public ICollection<ProcurementFrameworkAgreementDocument> Documents { get; set; } =
        new List<ProcurementFrameworkAgreementDocument>();
    public ICollection<ProcurementFrameworkAgreementExtension> Extensions { get; set; } =
        new List<ProcurementFrameworkAgreementExtension>();
    public ProcurementFrameworkAgreementBalance? Balance { get; set; }
    public ICollection<ProcurementFrameworkCallOff> CallOffs { get; set; } =
        new List<ProcurementFrameworkCallOff>();
}

[Table("ProcurementFrameworkAgreementCategories")]
public sealed class ProcurementFrameworkAgreementCategory : TenantEntity
{
    public Guid AgreementId { get; set; }
    public Guid PartnerCategoryId { get; set; }
    [Required, StringLength(50)] public string CategoryCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string CategoryName { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementFrameworkAgreement Agreement { get; set; } = null!;
    public PartnerCategory PartnerCategory { get; set; } = null!;
}

[Table("ProcurementFrameworkPriceListLines")]
public sealed class ProcurementFrameworkPriceListLine : TenantEntity
{
    public Guid AgreementId { get; set; }
    public Guid InventoryItemId { get; set; }
    [Required, StringLength(100)] public string ItemCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string ItemName { get; set; } = string.Empty;
    [Required, StringLength(20)] public string UnitOfMeasure { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal UnitPrice { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal MinimumQuantity { get; set; } = 1m;
    [Column(TypeName = "decimal(18,4)")] public decimal? MaximumQuantity { get; set; }
    public int LeadTimeDays { get; set; }
    [StringLength(500)] public string? Specifications { get; set; }
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementFrameworkAgreement Agreement { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
}

[Table("ProcurementFrameworkCallOffAuthorities")]
public sealed class ProcurementFrameworkCallOffAuthority : TenantEntity
{
    public Guid AgreementId { get; set; }
    public ProcurementFrameworkAuthorityKind AuthorityKind { get; set; }
    public Guid? AuthorityUserId { get; set; }
    [Required, StringLength(200)] public string AuthorityValue { get; set; } = string.Empty;
    [Required, StringLength(300)] public string DisplayName { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal? MaximumCallOffAmount { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; } = true;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementFrameworkAgreement Agreement { get; set; } = null!;
}

[Table("ProcurementFrameworkAgreementDocuments")]
public sealed class ProcurementFrameworkAgreementDocument : TenantEntity
{
    public Guid AgreementId { get; set; }
    [Required, StringLength(100)] public string DocumentType { get; set; } = string.Empty;
    [Required, StringLength(250)] public string Title { get; set; } = string.Empty;
    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(100)] public string DmsReference { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsCurrent { get; set; } = true;
    public DateTime? RetiredAtUtc { get; set; }
    public Guid? RetiredById { get; set; }
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementFrameworkAgreement Agreement { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("ProcurementFrameworkAgreementExtensions")]
public sealed class ProcurementFrameworkAgreementExtension : TenantEntity
{
    public Guid AgreementId { get; set; }
    [Range(1, int.MaxValue)] public int SequenceNumber { get; set; }
    public ProcurementFrameworkExtensionStatus Status { get; set; } =
        ProcurementFrameworkExtensionStatus.PendingApproval;
    public DateTime PreviousEndUtc { get; set; }
    public DateTime ProposedEndUtc { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid SubmittedById { get; set; }
    [Required, StringLength(300)] public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public Guid? DecidedById { get; set; }
    [StringLength(300)] public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [StringLength(1000)] public string? DecisionComment { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementFrameworkAgreement Agreement { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
}

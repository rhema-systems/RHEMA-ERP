using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementTenderDocumentTemplateVersions")]
public sealed class ProcurementTenderDocumentTemplateVersion : TenantEntity
{
    public Guid TemplateKey { get; set; } = Guid.NewGuid();

    [Required, StringLength(50)]
    public string TemplateCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required, StringLength(100)]
    public string DocumentTypeCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Version { get; set; } = 1;

    public ProcurementTenderDocumentTemplateStatus Status { get; set; } =
        ProcurementTenderDocumentTemplateStatus.Draft;

    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }

    public Guid PolicySetId { get; set; }

    [Required, StringLength(50)]
    public string PolicySetCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PolicySetVersion { get; set; }

    public Guid SourceConfigurationProfileId { get; set; }

    [Required, StringLength(2000)]
    public string ContentReference { get; set; } = string.Empty;

    public Guid? ContentWorkflowEvidenceDocumentId { get; set; }
    public Guid? ContentFileUploadRecordId { get; set; }

    [Required, StringLength(64)]
    public string ContentChecksumSha256 { get; set; } = string.Empty;

    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SupersedesVersionId { get; set; }

    [StringLength(1000)]
    public string? ChangeSummary { get; set; }

    [StringLength(500)]
    public string? ApprovalEvidenceReference { get; set; }

    [StringLength(1000)]
    public string? ReviewComment { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? SubmittedById { get; set; }

    [StringLength(300)]
    public string? SubmittedByName { get; set; }

    public DateTime? PublishedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }

    [StringLength(300)]
    public string? PublishedByName { get; set; }

    public DateTime? RetiredAtUtc { get; set; }
    public Guid? RetiredById { get; set; }

    [StringLength(300)]
    public string? RetiredByName { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string LifecycleSnapshotJson { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementPolicySet PolicySet { get; set; } = null!;
    public ProcurementConfigurationProfile SourceConfigurationProfile { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public WorkflowEvidenceDocument? ContentWorkflowEvidenceDocument { get; set; }
    public FileUploadRecord? ContentFileUploadRecord { get; set; }
    public ProcurementTenderDocumentTemplateVersion? SupersedesVersion { get; set; }
    public ICollection<ProcurementTenderDocumentTemplateMethod> ApplicableMethods { get; set; } =
        new List<ProcurementTenderDocumentTemplateMethod>();
    public ICollection<ProcurementTenderDocumentRegister> Registers { get; set; } =
        new List<ProcurementTenderDocumentRegister>();
}

[Table("ProcurementTenderDocumentTemplateMethods")]
public sealed class ProcurementTenderDocumentTemplateMethod : TenantEntity
{
    public Guid TemplateVersionId { get; set; }
    public ProcurementMethodType Method { get; set; }

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementTenderDocumentTemplateVersion TemplateVersion { get; set; } = null!;
}

[Table("ProcurementTenderDocumentRegisters")]
public sealed class ProcurementTenderDocumentRegister : TenantEntity
{
    public ProcurementTenderDocumentSourceType SourceType { get; set; }
    public Guid? TenderId { get; set; }
    public Guid? RequestForQuotationId { get; set; }
    public Guid SourcingCaseId { get; set; }
    public Guid MethodRuleId { get; set; }
    public ProcurementMethodType Method { get; set; }

    [Required, StringLength(100)]
    public string MethodRuleCode { get; set; } = string.Empty;

    public Guid PolicySetId { get; set; }

    [Required, StringLength(50)]
    public string PolicySetCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PolicySetVersion { get; set; }

    public Guid SourceConfigurationProfileId { get; set; }
    public Guid InitialTemplateVersionId { get; set; }

    public DateTime OriginalSubmissionDeadlineUtc { get; set; }
    public DateTime? OpeningScheduledAtUtc { get; set; }
    public DateTime OriginalBidValidityUntilUtc { get; set; }

    public ProcurementTenderDocumentFeeMode FeeMode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FeeAmount { get; set; }

    [Required, StringLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    public DateTime BoundAtUtc { get; set; }
    public Guid BoundByUserId { get; set; }

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string LifecycleSnapshotJson { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Tender? Tender { get; set; }
    public RequestForQuotation? RequestForQuotation { get; set; }
    public ProcurementSourcingCase SourcingCase { get; set; } = null!;
    public ProcurementPolicyMethodRule MethodRule { get; set; } = null!;
    public ProcurementPolicySet PolicySet { get; set; } = null!;
    public ProcurementConfigurationProfile SourceConfigurationProfile { get; set; } = null!;
    public ProcurementTenderDocumentTemplateVersion InitialTemplateVersion { get; set; } = null!;
    public ICollection<ProcurementTenderDocumentIssuance> Issuances { get; set; } =
        new List<ProcurementTenderDocumentIssuance>();
    public ICollection<ProcurementTenderDocumentChange> Changes { get; set; } =
        new List<ProcurementTenderDocumentChange>();
}

[Table("ProcurementTenderDocumentIssuances")]
public sealed class ProcurementTenderDocumentIssuance : TenantEntity
{
    public Guid RegisterId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public Guid? BusinessPartnerId { get; set; }

    [Required, StringLength(64)]
    public string RecipientKey { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string RecipientName { get; set; } = string.Empty;

    [StringLength(320)]
    public string? RecipientEmail { get; set; }

    [StringLength(30)]
    public string? RecipientPhone { get; set; }

    public ProcurementTenderDocumentFeeMode FeeMode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FeeAmount { get; set; }

    [Required, StringLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    [StringLength(200)]
    public string? PaymentReference { get; set; }

    [Required, StringLength(200)]
    public string ReceiptNumber { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string IssueChannel { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }
    public Guid IssuedByUserId { get; set; }

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string EvidenceReference { get; set; } = string.Empty;

    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string IssuanceSnapshotJson { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementTenderDocumentRegister Register { get; set; } = null!;
    public ProcurementTenderDocumentTemplateVersion TemplateVersion { get; set; } = null!;
    public BusinessPartner? BusinessPartner { get; set; }
    public WorkflowEvidenceDocument? EvidenceWorkflowDocument { get; set; }
    public FileUploadRecord? EvidenceFileUploadRecord { get; set; }
    public ICollection<ProcurementTenderDocumentAcknowledgement> Acknowledgements { get; set; } =
        new List<ProcurementTenderDocumentAcknowledgement>();
}

[Table("ProcurementTenderDocumentChanges")]
public sealed class ProcurementTenderDocumentChange : TenantEntity
{
    public Guid RegisterId { get; set; }

    [Range(1, int.MaxValue)]
    public int Sequence { get; set; }

    public ProcurementTenderDocumentChangeType ChangeType { get; set; }
    public ProcurementTenderDocumentChangeStatus Status { get; set; } =
        ProcurementTenderDocumentChangeStatus.PendingApproval;

    public Guid? PreviousTemplateVersionId { get; set; }
    public Guid? NewTemplateVersionId { get; set; }
    public DateTime? PreviousValueUtc { get; set; }
    public DateTime? NewValueUtc { get; set; }
    public DateTime? PreviousOpeningScheduledAtUtc { get; set; }
    public DateTime? NewOpeningScheduledAtUtc { get; set; }
    public bool RequiresAcknowledgement { get; set; } = true;

    [Required, StringLength(2000)]
    public string Reason { get; set; } = string.Empty;

    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }

    [StringLength(100)]
    public string? WorkflowOutcome { get; set; }

    [StringLength(300)]
    public string? ApprovalReference { get; set; }

    [Required, StringLength(500)]
    public string EvidenceReference { get; set; } = string.Empty;

    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }

    public DateTime RequestedAtUtc { get; set; }
    public Guid RequestedByUserId { get; set; }

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    public DateTime? DecidedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }
    public Guid? DispatchedByUserId { get; set; }

    [StringLength(500)]
    public string? DispatchEvidenceReference { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string LifecycleSnapshotJson { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementTenderDocumentRegister Register { get; set; } = null!;
    public ProcurementTenderDocumentTemplateVersion? PreviousTemplateVersion { get; set; }
    public ProcurementTenderDocumentTemplateVersion? NewTemplateVersion { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public WorkflowEvidenceDocument? EvidenceWorkflowDocument { get; set; }
    public FileUploadRecord? EvidenceFileUploadRecord { get; set; }
    public ICollection<ProcurementTenderDocumentChangeRecipient> Recipients { get; set; } =
        new List<ProcurementTenderDocumentChangeRecipient>();
}

[Table("ProcurementTenderDocumentChangeRecipients")]
public sealed class ProcurementTenderDocumentChangeRecipient : TenantEntity
{
    public Guid ChangeId { get; set; }
    public ProcurementTenderDocumentRecipientSourceType SourceType { get; set; }
    public Guid? IssuanceId { get; set; }
    public Guid? TenderBidId { get; set; }
    public Guid? RequestForQuotationQuoteId { get; set; }
    public Guid? BusinessPartnerId { get; set; }

    [Required, StringLength(64)]
    public string RecipientKey { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string RecipientName { get; set; } = string.Empty;

    [StringLength(320)]
    public string? RecipientEmail { get; set; }

    [StringLength(30)]
    public string? RecipientPhone { get; set; }

    [Required, StringLength(100)]
    public string DispatchChannel { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string DispatchReference { get; set; } = string.Empty;

    public DateTime DispatchedAtUtc { get; set; }
    public Guid DispatchedByUserId { get; set; }

    [Required, StringLength(500)]
    public string DispatchEvidenceReference { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string DispatchSnapshotJson { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementTenderDocumentChange Change { get; set; } = null!;
    public ProcurementTenderDocumentIssuance? Issuance { get; set; }
    public TenderBid? TenderBid { get; set; }
    public RequestForQuotationQuote? RequestForQuotationQuote { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }
    public ICollection<ProcurementTenderDocumentAcknowledgement> Acknowledgements { get; set; } =
        new List<ProcurementTenderDocumentAcknowledgement>();
}

[Table("ProcurementTenderDocumentAcknowledgements")]
public sealed class ProcurementTenderDocumentAcknowledgement : TenantEntity
{
    public Guid? IssuanceId { get; set; }
    public Guid? ChangeRecipientId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public ProcurementTenderDocumentAcknowledgementOutcome Outcome { get; set; }

    public DateTime AcknowledgedAtUtc { get; set; }
    public Guid AcknowledgedByUserId { get; set; }

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string AcknowledgementChannel { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string AcknowledgementReference { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string EvidenceReference { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string AcknowledgementSnapshotJson { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementTenderDocumentIssuance? Issuance { get; set; }
    public ProcurementTenderDocumentChangeRecipient? ChangeRecipient { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }
}

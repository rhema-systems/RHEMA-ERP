using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementBidderCommunicationRegisters")]
public sealed class ProcurementBidderCommunicationRegister : TenantEntity
{
    public ProcurementAwardReadinessSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    [Required, StringLength(100)] public string SourceReference { get; set; } = string.Empty;
    public ProcurementBidderCommunicationAwardFamily AwardFamily { get; set; }
    public Guid AwardId { get; set; }
    [Required, StringLength(200)] public string AwardReference { get; set; } = string.Empty;
    public DateTime AwardedAtUtc { get; set; }
    public Guid AwardReadinessDecisionId { get; set; }
    public int AwardReadinessDecisionSequence { get; set; }
    [Required, StringLength(64)] public string AwardReadinessIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string AwardReadinessSourceIntegrityHash { get; set; } = string.Empty;
    public DateTime StandstillStartsAtUtc { get; set; }
    public DateTime StandstillEndsAtUtc { get; set; }
    public DateTime AppealWindowEndsAtUtc { get; set; }
    [Required, StringLength(100)] public string StandstillAuthorityReference { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string AwardSnapshotJson { get; set; } = "{}";
    [Column(TypeName = "nvarchar(max)")] public string RecipientSnapshotJson { get; set; } = "[]";
    [Required, StringLength(64)] public string RecipientSnapshotHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public DateTime InitializedAtUtc { get; set; }
    public Guid InitializedByUserId { get; set; }
    [Required, StringLength(300)] public string InitializedByName { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ProcurementBidderCommunicationRecipient> Recipients { get; set; } =
        new List<ProcurementBidderCommunicationRecipient>();
}

[Table("ProcurementBidderCommunicationRecipients")]
public sealed class ProcurementBidderCommunicationRecipient : TenantEntity
{
    public Guid RegisterId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public ProcurementBidderCommunicationRecipientOutcome Outcome { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string BidOrQuoteIdsJson { get; set; } = "[]";
    [Required, StringLength(50)] public string PartnerCode { get; set; } = string.Empty;
    [Required, StringLength(300)] public string PartnerName { get; set; } = string.Empty;
    [StringLength(320)] public string? RecipientEmail { get; set; }
    [StringLength(30)] public string? RecipientPhone { get; set; }
    [Required, StringLength(64)] public string LineageHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementBidderCommunicationRegister Register { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
    public ICollection<ProcurementBidderCommunicationLetterVersion> LetterVersions { get; set; } =
        new List<ProcurementBidderCommunicationLetterVersion>();
    public ICollection<ProcurementBidderAppeal> Appeals { get; set; } =
        new List<ProcurementBidderAppeal>();
    public ICollection<ProcurementTenderSecurityInstrument> SecurityInstruments { get; set; } =
        new List<ProcurementTenderSecurityInstrument>();
}

[Table("ProcurementBidderCommunicationLetterVersions")]
public sealed class ProcurementBidderCommunicationLetterVersion : TenantEntity
{
    public Guid RecipientId { get; set; }
    [Range(1, int.MaxValue)] public int Version { get; set; }
    public Guid TemplateVersionId { get; set; }
    [Required, StringLength(2000)] public string TemplateReference { get; set; } = string.Empty;
    [Required, StringLength(64)] public string TemplateChecksumSha256 { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string ContentReference { get; set; } = string.Empty;
    [Required, StringLength(64)] public string ContentChecksumSha256 { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    [Required, StringLength(300)] public string ApprovalReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string ApprovalEvidenceReference { get; set; } = string.Empty;
    public Guid? ApprovalWorkflowEvidenceDocumentId { get; set; }
    public Guid? ApprovalFileUploadRecordId { get; set; }
    public DateTime ApprovedAtUtc { get; set; }
    public Guid ApprovedByUserId { get; set; }
    [Required, StringLength(300)] public string ApprovedByName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementBidderCommunicationRecipient Recipient { get; set; } = null!;
    public ProcurementTenderDocumentTemplateVersion TemplateVersion { get; set; } = null!;
    public ICollection<ProcurementBidderCommunicationDispatch> Dispatches { get; set; } =
        new List<ProcurementBidderCommunicationDispatch>();
}

[Table("ProcurementBidderCommunicationDispatches")]
public sealed class ProcurementBidderCommunicationDispatch : TenantEntity
{
    public Guid LetterVersionId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public ProcurementBidderCommunicationDispatchChannel Channel { get; set; }
    [Required, StringLength(320)] public string Destination { get; set; } = string.Empty;
    [Required, StringLength(300)] public string DispatchReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string DispatchEvidenceReference { get; set; } = string.Empty;
    public Guid? DispatchWorkflowEvidenceDocumentId { get; set; }
    public Guid? DispatchFileUploadRecordId { get; set; }
    public DateTime DispatchedAtUtc { get; set; }
    public Guid DispatchedByUserId { get; set; }
    [Required, StringLength(300)] public string DispatchedByName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementBidderCommunicationLetterVersion LetterVersion { get; set; } = null!;
    public ICollection<ProcurementBidderCommunicationDelivery> Deliveries { get; set; } =
        new List<ProcurementBidderCommunicationDelivery>();
    public ICollection<ProcurementBidderCommunicationAcknowledgement> Acknowledgements { get; set; } =
        new List<ProcurementBidderCommunicationAcknowledgement>();
}

[Table("ProcurementBidderCommunicationDeliveries")]
public sealed class ProcurementBidderCommunicationDelivery : TenantEntity
{
    public Guid DispatchId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public ProcurementBidderCommunicationDeliveryOutcome Outcome { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, StringLength(300)] public string ProviderReference { get; set; } = string.Empty;
    [StringLength(1000)] public string? Detail { get; set; }
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid RecordedByUserId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementBidderCommunicationDispatch Dispatch { get; set; } = null!;
}

[Table("ProcurementBidderCommunicationAcknowledgements")]
public sealed class ProcurementBidderCommunicationAcknowledgement : TenantEntity
{
    public Guid DispatchId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public ProcurementBidderCommunicationAcknowledgementOutcome Outcome { get; set; }
    public DateTime AcknowledgedAtUtc { get; set; }
    public Guid AcknowledgedByUserId { get; set; }
    public Guid? AcknowledgedByBusinessPartnerId { get; set; }
    [Required, StringLength(100)] public string AcknowledgementChannel { get; set; } = string.Empty;
    [Required, StringLength(300)] public string AcknowledgementReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementBidderCommunicationDispatch Dispatch { get; set; } = null!;
}

[Table("ProcurementBidderAppeals")]
public sealed class ProcurementBidderAppeal : TenantEntity
{
    public Guid RecipientId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    [Required, StringLength(2000)] public string Grounds { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    public DateTime FiledAtUtc { get; set; }
    public Guid FiledByUserId { get; set; }
    public Guid? FiledByBusinessPartnerId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementBidderCommunicationRecipient Recipient { get; set; } = null!;
    public ICollection<ProcurementBidderAppealDecision> Decisions { get; set; } =
        new List<ProcurementBidderAppealDecision>();
}

[Table("ProcurementBidderAppealDecisions")]
public sealed class ProcurementBidderAppealDecision : TenantEntity
{
    public Guid AppealId { get; set; }
    public ProcurementBidderAppealOutcome Outcome { get; set; }
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    [Required, StringLength(300)] public string DecisionReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    public DateTime DecidedAtUtc { get; set; }
    public Guid DecidedByUserId { get; set; }
    [Required, StringLength(300)] public string DecidedByName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementBidderAppeal Appeal { get; set; } = null!;
}

[Table("ProcurementTenderSecurityInstruments")]
public sealed class ProcurementTenderSecurityInstrument : TenantEntity
{
    public Guid RecipientId { get; set; }
    public Guid? TenderBidId { get; set; }
    public Guid? RequestForQuotationQuoteId { get; set; }
    public ProcurementTenderSecurityInstrumentType InstrumentType { get; set; }
    [Required, StringLength(200)] public string InstrumentReference { get; set; } = string.Empty;
    [Required, StringLength(300)] public string IssuerName { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
    public Guid RegisteredByUserId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementBidderCommunicationRecipient Recipient { get; set; } = null!;
    public ICollection<ProcurementTenderSecurityAction> Actions { get; set; } =
        new List<ProcurementTenderSecurityAction>();
}

[Table("ProcurementTenderSecurityActions")]
public sealed class ProcurementTenderSecurityAction : TenantEntity
{
    public Guid SecurityInstrumentId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public ProcurementTenderSecurityActionType ActionType { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    [Required, StringLength(300)] public string ActionReference { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    public DateTime ActionedAtUtc { get; set; }
    public Guid ActionedByUserId { get; set; }
    [Required, StringLength(300)] public string ActionedByName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementTenderSecurityInstrument SecurityInstrument { get; set; } = null!;
}

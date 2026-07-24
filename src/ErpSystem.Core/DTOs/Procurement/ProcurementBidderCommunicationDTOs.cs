using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class InitializeProcurementBidderCommunicationRegisterRequest
{
    public ProcurementAwardReadinessSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public DateTime StandstillEndsAtUtc { get; set; }
    public DateTime AppealWindowEndsAtUtc { get; set; }
    [Required, StringLength(100)] public string StandstillAuthorityReference { get; set; } = string.Empty;
    public Guid ExpectedAwardReadinessDecisionId { get; set; }
    [Required, RegularExpression("^[A-Fa-f0-9]{64}$")]
    public string ExpectedAwardReadinessIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class ApproveProcurementBidderCommunicationLetterRequest
{
    public Guid TemplateVersionId { get; set; }
    [Required, StringLength(2000)] public string ContentReference { get; set; } = string.Empty;
    [Required, RegularExpression("^[A-Fa-f0-9]{64}$")]
    public string ContentChecksumSha256 { get; set; } = string.Empty;
    public Guid WorkflowInstanceId { get; set; }
    [Required, StringLength(300)] public string ApprovalReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string ApprovalEvidenceReference { get; set; } = string.Empty;
    public Guid? ApprovalWorkflowEvidenceDocumentId { get; set; }
    public Guid? ApprovalFileUploadRecordId { get; set; }
    [Required, RegularExpression("^[A-Fa-f0-9]{64}$")]
    public string ExpectedRegisterIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class DispatchProcurementBidderCommunicationLetterRequest
{
    public ProcurementBidderCommunicationDispatchChannel Channel { get; set; }
    [Required, StringLength(320)] public string Destination { get; set; } = string.Empty;
    [Required, StringLength(300)] public string DispatchReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string DispatchEvidenceReference { get; set; } = string.Empty;
    public Guid? DispatchWorkflowEvidenceDocumentId { get; set; }
    public Guid? DispatchFileUploadRecordId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class RecordProcurementBidderCommunicationDeliveryRequest
{
    public ProcurementBidderCommunicationDeliveryOutcome Outcome { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, StringLength(300)] public string ProviderReference { get; set; } = string.Empty;
    [StringLength(1000)] public string? Detail { get; set; }
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class RecordProcurementBidderCommunicationAcknowledgementRequest
{
    public ProcurementBidderCommunicationAcknowledgementOutcome Outcome { get; set; }
    [Required, StringLength(100)] public string AcknowledgementChannel { get; set; } = string.Empty;
    [Required, StringLength(300)] public string AcknowledgementReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class FileProcurementBidderAppealRequest
{
    [Required, StringLength(2000)] public string Grounds { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class ResolveProcurementBidderAppealRequest
{
    public ProcurementBidderAppealOutcome Outcome { get; set; }
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    public Guid WorkflowInstanceId { get; set; }
    [Required, StringLength(300)] public string DecisionReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class RegisterProcurementTenderSecurityRequest
{
    public Guid? TenderBidId { get; set; }
    public Guid? RequestForQuotationQuoteId { get; set; }
    public ProcurementTenderSecurityInstrumentType InstrumentType { get; set; }
    [Required, StringLength(200)] public string InstrumentReference { get; set; } = string.Empty;
    [Required, StringLength(300)] public string IssuerName { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Amount { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class RecordProcurementTenderSecurityActionRequest
{
    public ProcurementTenderSecurityActionType ActionType { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    [Required, StringLength(300)] public string ActionReference { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    [StringLength(64)] public string? ExpectedLatestActionIntegrityHash { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class ProcurementBidderCommunicationOverviewDto
{
    public Guid Id { get; set; }
    public ProcurementAwardReadinessSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public ProcurementBidderCommunicationAwardFamily AwardFamily { get; set; }
    public Guid AwardId { get; set; }
    public string AwardReference { get; set; } = string.Empty;
    public DateTime AwardedAtUtc { get; set; }
    public Guid AwardReadinessDecisionId { get; set; }
    public int AwardReadinessDecisionSequence { get; set; }
    public string AwardReadinessIntegrityHash { get; set; } = string.Empty;
    public string AwardReadinessSourceIntegrityHash { get; set; } = string.Empty;
    public bool AwardReadinessIsCurrent { get; set; }
    public DateTime StandstillStartsAtUtc { get; set; }
    public DateTime StandstillEndsAtUtc { get; set; }
    public DateTime AppealWindowEndsAtUtc { get; set; }
    public string StandstillAuthorityReference { get; set; } = string.Empty;
    public bool StandstillElapsed { get; set; }
    public bool AppealWindowOpen { get; set; }
    public bool HasOpenAppeals { get; set; }
    public DateTime InitializedAtUtc { get; set; }
    public Guid InitializedByUserId { get; set; }
    public string InitializedByName { get; set; } = string.Empty;
    public string RecipientSnapshotHash { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementBidderCommunicationRecipientDto> Recipients { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
    public List<string> BlockedReasons { get; set; } = new();
}

public sealed class ProcurementBidderCommunicationRecipientDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public ProcurementBidderCommunicationRecipientOutcome Outcome { get; set; }
    public List<Guid> BidOrQuoteIds { get; set; } = new();
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }
    public string? RecipientPhone { get; set; }
    public string LineageHash { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementBidderCommunicationLetterVersionDto> LetterVersions { get; set; } = new();
    public List<ProcurementBidderAppealDto> Appeals { get; set; } = new();
    public List<ProcurementTenderSecurityInstrumentDto> SecurityInstruments { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
}

public sealed class ProcurementBidderCommunicationLetterVersionDto
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public Guid TemplateVersionId { get; set; }
    public string TemplateReference { get; set; } = string.Empty;
    public string TemplateChecksumSha256 { get; set; } = string.Empty;
    public string ContentReference { get; set; } = string.Empty;
    public string ContentChecksumSha256 { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public string ApprovalReference { get; set; } = string.Empty;
    public string ApprovalEvidenceReference { get; set; } = string.Empty;
    public DateTime ApprovedAtUtc { get; set; }
    public Guid ApprovedByUserId { get; set; }
    public string ApprovedByName { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementBidderCommunicationDispatchDto> Dispatches { get; set; } = new();
}

public sealed class ProcurementBidderCommunicationDispatchDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public ProcurementBidderCommunicationDispatchChannel Channel { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string DispatchReference { get; set; } = string.Empty;
    public string DispatchEvidenceReference { get; set; } = string.Empty;
    public DateTime DispatchedAtUtc { get; set; }
    public Guid DispatchedByUserId { get; set; }
    public string DispatchedByName { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementBidderCommunicationDeliveryDto> Deliveries { get; set; } = new();
    public List<ProcurementBidderCommunicationAcknowledgementDto> Acknowledgements { get; set; } = new();
}

public sealed class ProcurementBidderCommunicationDeliveryDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public ProcurementBidderCommunicationDeliveryOutcome Outcome { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string ProviderReference { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementBidderCommunicationAcknowledgementDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public ProcurementBidderCommunicationAcknowledgementOutcome Outcome { get; set; }
    public DateTime AcknowledgedAtUtc { get; set; }
    public Guid AcknowledgedByUserId { get; set; }
    public Guid? AcknowledgedByBusinessPartnerId { get; set; }
    public string AcknowledgementChannel { get; set; } = string.Empty;
    public string AcknowledgementReference { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementBidderAppealDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public string Grounds { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public DateTime FiledAtUtc { get; set; }
    public Guid FiledByUserId { get; set; }
    public Guid? FiledByBusinessPartnerId { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public ProcurementBidderAppealDecisionDto? Decision { get; set; }
}

public sealed class ProcurementBidderAppealDecisionDto
{
    public Guid Id { get; set; }
    public ProcurementBidderAppealOutcome Outcome { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public string DecisionReference { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public DateTime DecidedAtUtc { get; set; }
    public Guid DecidedByUserId { get; set; }
    public string DecidedByName { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementTenderSecurityInstrumentDto
{
    public Guid Id { get; set; }
    public Guid? TenderBidId { get; set; }
    public Guid? RequestForQuotationQuoteId { get; set; }
    public ProcurementTenderSecurityInstrumentType InstrumentType { get; set; }
    public string InstrumentReference { get; set; } = string.Empty;
    public string IssuerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public DateTime RegisteredAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public ProcurementTenderSecurityActionType? CurrentAction { get; set; }
    public List<ProcurementTenderSecurityActionDto> Actions { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
    public List<string> BlockedReasons { get; set; } = new();
}

public sealed class ProcurementTenderSecurityActionDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public ProcurementTenderSecurityActionType ActionType { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public string ActionReference { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public DateTime ActionedAtUtc { get; set; }
    public Guid ActionedByUserId { get; set; }
    public string ActionedByName { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

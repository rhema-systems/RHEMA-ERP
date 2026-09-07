using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementTenderDocumentTemplateSummaryDto
{
    public int TemplateFamilyCount { get; set; }
    public int DraftCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int PublishedCount { get; set; }
    public int EffectiveCount { get; set; }
    public int RetiredCount { get; set; }
}

public sealed class ProcurementTenderDocumentTemplateSearchRequest
{
    [StringLength(200)] public string? Search { get; set; }
    public ProcurementTenderDocumentTemplateStatus? Status { get; set; }
    public ProcurementMethodType? Method { get; set; }
    public DateTime? EffectiveAtUtc { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 200)] public int PageSize { get; set; } = 25;
}

public sealed class ProcurementTenderDocumentTemplatePageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementTenderDocumentTemplateListItemDto> Items { get; set; } = new();
}

public sealed class ProcurementTenderDocumentTemplateListItemDto
{
    public Guid Id { get; set; }
    public Guid TemplateKey { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DocumentTypeCode { get; set; } = string.Empty;
    public int Version { get; set; }
    public ProcurementTenderDocumentTemplateStatus Status { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsEffective { get; set; }
    public string PolicySetCode { get; set; } = string.Empty;
    public int PolicySetVersion { get; set; }
    public List<ProcurementMethodType> ApplicableMethods { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementTenderDocumentTemplateDto
{
    public Guid Id { get; set; }
    public Guid TemplateKey { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DocumentTypeCode { get; set; } = string.Empty;
    public int Version { get; set; }
    public ProcurementTenderDocumentTemplateStatus Status { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsEffective { get; set; }
    public Guid PolicySetId { get; set; }
    public string PolicySetCode { get; set; } = string.Empty;
    public int PolicySetVersion { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
    public string ContentReference { get; set; } = string.Empty;
    public Guid? ContentWorkflowEvidenceDocumentId { get; set; }
    public Guid? ContentFileUploadRecordId { get; set; }
    public string ContentChecksumSha256 { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    /// <summary>Server-validated content choices, populated by the template detail endpoint.</summary>
    public List<Guid> EligibleContentEvidenceDocumentIds { get; set; } = new();
    public Guid? SupersedesVersionId { get; set; }
    public string? ChangeSummary { get; set; }
    public string? ApprovalEvidenceReference { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }
    public string? PublishedByName { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public Guid? RetiredById { get; set; }
    public string? RetiredByName { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementMethodType> ApplicableMethods { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
    public List<string> BlockedReasons { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementTenderDocumentWorkflowOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string LifecycleStatus { get; set; } = string.Empty;
}

public sealed class ProcurementTenderDocumentPolicyOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
    public string SourceConfigurationProfileCode { get; set; } = string.Empty;
}

public sealed class SaveProcurementTenderDocumentTemplateRequest
{
    [Required, StringLength(50)] public string TemplateCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Required, StringLength(100)] public string DocumentTypeCode { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string PolicySetCode { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int PolicySetVersion { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
    [StringLength(2000)] public string ContentReference { get; set; } = string.Empty;
    public Guid? ContentWorkflowEvidenceDocumentId { get; set; }
    public Guid? ContentFileUploadRecordId { get; set; }
    [RegularExpression("^[A-Fa-f0-9]{64}$")] public string ContentChecksumSha256 { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    [MinLength(1)] public List<ProcurementMethodType> ApplicableMethods { get; set; } = new();
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class AttachProcurementTenderDocumentTemplateContentRequest
{
    public Guid ContentWorkflowEvidenceDocumentId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementTenderDocumentTemplateLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Comment { get; set; }
    [MinLength(1)] public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class CloneProcurementTenderDocumentTemplateRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    [Required, StringLength(1000)] public string ChangeSummary { get; set; } = string.Empty;
}

public sealed class ProcurementTenderDocumentRegisterReadinessDto
{
    public string SourceStatus { get; set; } = string.Empty;
    public bool IsSourcePublished { get; set; }
    public ProcurementTenderDocumentSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public Guid? SourcingCaseId { get; set; }
    public ProcurementMethodType? Method { get; set; }
    public Guid? MethodRuleId { get; set; }
    public string? MethodRuleCode { get; set; }
    public bool HasRegister { get; set; }
    public bool AllowsNewRecipient { get; set; }
    public List<string> AllowedExternalRecipientEmails { get; set; } = new();
    public Guid? RegisterId { get; set; }
    public Guid? EffectiveTemplateVersionId { get; set; }
    public string? EffectiveTemplateReference { get; set; }
    public DateTime? EffectiveSubmissionDeadlineUtc { get; set; }
    public DateTime? OpeningScheduledAtUtc { get; set; }
    public DateTime? EffectiveBidValidityUntilUtc { get; set; }
    public ProcurementTenderDocumentFeeMode? FeeMode { get; set; }
    public decimal? FeeAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public int IssuanceCount { get; set; }
    public int PendingAcknowledgementCount { get; set; }
    public int PendingChangeCount { get; set; }
    public bool Ready { get; set; }
    public List<string> BlockedReasons { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
    public string? RowVersion { get; set; }
}

public sealed class ProcurementTenderDocumentRegisterDto
{
    public string SourceStatus { get; set; } = string.Empty;
    public bool IsSourcePublished { get; set; }
    public Guid Id { get; set; }
    public bool AllowsNewRecipient { get; set; }
    public List<string> AllowedExternalRecipientEmails { get; set; } = new();
    public ProcurementTenderDocumentSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public Guid? TenderId { get; set; }
    public Guid? RequestForQuotationId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public Guid SourcingCaseId { get; set; }
    public Guid MethodRuleId { get; set; }
    public ProcurementMethodType Method { get; set; }
    public string MethodRuleCode { get; set; } = string.Empty;
    public Guid PolicySetId { get; set; }
    public string PolicySetCode { get; set; } = string.Empty;
    public int PolicySetVersion { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
    public Guid InitialTemplateVersionId { get; set; }
    public Guid EffectiveTemplateVersionId { get; set; }
    public string EffectiveTemplateReference { get; set; } = string.Empty;
    public DateTime OriginalSubmissionDeadlineUtc { get; set; }
    public DateTime? OriginalOpeningScheduledAtUtc { get; set; }
    public DateTime EffectiveSubmissionDeadlineUtc { get; set; }
    public DateTime? OpeningScheduledAtUtc { get; set; }
    public DateTime OriginalBidValidityUntilUtc { get; set; }
    public DateTime EffectiveBidValidityUntilUtc { get; set; }
    public ProcurementTenderDocumentFeeMode FeeMode { get; set; }
    public decimal FeeAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime BoundAtUtc { get; set; }
    public Guid BoundByUserId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementTenderDocumentIssuanceDto> Issuances { get; set; } = new();
    public List<ProcurementTenderDocumentChangeDto> Changes { get; set; } = new();
    public List<string> BlockedReasons { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementTenderDocumentIssuanceDto
{
    public Guid Id { get; set; }
    public Guid RegisterId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public string TemplateReference { get; set; } = string.Empty;
    public Guid? BusinessPartnerId { get; set; }
    public string RecipientKey { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }
    public string? RecipientPhone { get; set; }
    public ProcurementTenderDocumentFeeMode FeeMode { get; set; }
    public decimal FeeAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public string? PaymentReference { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string IssueChannel { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public Guid IssuedByUserId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public ProcurementTenderDocumentAcknowledgementDto? Acknowledgement { get; set; }
}

public sealed class ProcurementTenderDocumentChangeDto
{
    public Guid Id { get; set; }
    public Guid RegisterId { get; set; }
    public int Sequence { get; set; }
    public ProcurementTenderDocumentChangeType ChangeType { get; set; }
    public ProcurementTenderDocumentChangeStatus Status { get; set; }
    public Guid? PreviousTemplateVersionId { get; set; }
    public Guid? NewTemplateVersionId { get; set; }
    public DateTime? PreviousValueUtc { get; set; }
    public DateTime? NewValueUtc { get; set; }
    public DateTime? PreviousOpeningScheduledAtUtc { get; set; }
    public DateTime? NewOpeningScheduledAtUtc { get; set; }
    public bool RequiresAcknowledgement { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? WorkflowOutcome { get; set; }
    public string? ApprovalReference { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime? DecidedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }
    public Guid? DispatchedByUserId { get; set; }
    public string? DispatchEvidenceReference { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementTenderDocumentChangeRecipientDto> Recipients { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
    public List<string> BlockedReasons { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementTenderDocumentChangeRecipientDto
{
    public Guid Id { get; set; }
    public ProcurementTenderDocumentRecipientSourceType SourceType { get; set; }
    public Guid? IssuanceId { get; set; }
    public Guid? TenderBidId { get; set; }
    public Guid? RequestForQuotationQuoteId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string RecipientKey { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }
    public string? RecipientPhone { get; set; }
    public string DispatchChannel { get; set; } = string.Empty;
    public string DispatchReference { get; set; } = string.Empty;
    public DateTime DispatchedAtUtc { get; set; }
    public string DispatchEvidenceReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public ProcurementTenderDocumentAcknowledgementDto? Acknowledgement { get; set; }
}

public sealed class ProcurementTenderDocumentAcknowledgementDto
{
    public Guid Id { get; set; }
    public Guid? IssuanceId { get; set; }
    public Guid? ChangeRecipientId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public ProcurementTenderDocumentAcknowledgementOutcome Outcome { get; set; }
    public DateTime AcknowledgedAtUtc { get; set; }
    public Guid AcknowledgedByUserId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string AcknowledgementChannel { get; set; } = string.Empty;
    public string AcknowledgementReference { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class BindProcurementTenderDocumentRegisterRequest
{
    public ProcurementTenderDocumentSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public DateTime SubmissionDeadlineUtc { get; set; }
    public DateTime? OpeningScheduledAtUtc { get; set; }
    public DateTime BidValidityUntilUtc { get; set; }
    public ProcurementTenderDocumentFeeMode FeeMode { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal FeeAmount { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    public BindProcurementTenderDocumentScheduleChangeRequest? ScheduleChange { get; set; }
}

public sealed class BindProcurementTenderDocumentScheduleChangeRequest
{
    public DateTime SubmissionDeadlineUtc { get; set; }
    public DateTime OpeningScheduledAtUtc { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
}

public sealed class IssueProcurementTenderDocumentControlRequest
{
    public ProcurementTenderDocumentSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    [Required, StringLength(300)] public string RecipientName { get; set; } = string.Empty;
    [EmailAddress, StringLength(320)] public string? RecipientEmail { get; set; }
    [StringLength(30)] public string? RecipientPhone { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal AmountPaid { get; set; }
    [StringLength(200)] public string? PaymentReference { get; set; }
    [Required, StringLength(200)] public string ReceiptNumber { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IssueChannel { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    [Required] public string RegisterRowVersion { get; set; } = string.Empty;
}

public sealed class CreateProcurementTenderDocumentChangeRequest
{
    public ProcurementTenderDocumentSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public ProcurementTenderDocumentChangeType ChangeType { get; set; }
    public Guid? NewTemplateVersionId { get; set; }
    public DateTime? NewValueUtc { get; set; }
    public DateTime? NewOpeningScheduledAtUtc { get; set; }
    public bool RequiresAcknowledgement { get; set; } = true;
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    [Required] public string RegisterRowVersion { get; set; } = string.Empty;
}

public sealed class DecideProcurementTenderDocumentChangeRequest
{
    [Required] public string Action { get; set; } = string.Empty;
    [Required, StringLength(300)] public string ApprovalReference { get; set; } = string.Empty;
    [StringLength(1000)] public string? Comments { get; set; }
    [StringLength(100)] public string? DispatchChannel { get; set; }
    [StringLength(300)] public string? DispatchReference { get; set; }
    [StringLength(500)] public string? DispatchEvidenceReference { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class AcknowledgeProcurementTenderDocumentRequest
{
    public Guid? IssuanceId { get; set; }
    public Guid? ChangeRecipientId { get; set; }
    public ProcurementTenderDocumentAcknowledgementOutcome Outcome { get; set; }
    [Required, StringLength(100)] public string AcknowledgementChannel { get; set; } = string.Empty;
    [Required, StringLength(300)] public string AcknowledgementReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
}

public sealed class ProcurementTenderDocumentEffectiveStateDto
{
    public Guid RegisterId { get; set; }
    public ProcurementTenderDocumentSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public Guid EffectiveTemplateVersionId { get; set; }
    public string EffectiveTemplateReference { get; set; } = string.Empty;
    public DateTime EffectiveSubmissionDeadlineUtc { get; set; }
    public DateTime EffectiveBidValidityUntilUtc { get; set; }
    public bool Ready { get; set; }
    public List<string> BlockedReasons { get; set; } = new();
}

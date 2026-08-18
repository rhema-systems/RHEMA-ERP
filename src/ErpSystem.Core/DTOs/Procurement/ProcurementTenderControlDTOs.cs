using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementTenderControlDto
{
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public ProcurementMethodType Method { get; set; }
    public string MethodRuleCode { get; set; } = string.Empty;
    public string AuthorityRouteReference { get; set; } = string.Empty;
    public bool PpaApprovalRequired { get; set; }
    public ProcurementTenderControlStatus Status { get; set; }
    public string AdvertisementReference { get; set; } = string.Empty;
    public string PublicationChannel { get; set; } = string.Empty;
    public string TenderDocumentReference { get; set; } = string.Empty;
    public string TenderDocumentVersion { get; set; } = string.Empty;
    public decimal DocumentFee { get; set; }
    public string AdvertisementEvidenceReference { get; set; } = string.Empty;
    public DateTime AdvertisedAtUtc { get; set; }
    public DateTime SubmissionDeadlineUtc { get; set; }
    public DateTime OpeningScheduledAtUtc { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime? TechnicalEvaluatedAtUtc { get; set; }
    public DateTime? FinancialEvaluatedAtUtc { get; set; }
    public decimal MinimumTechnicalScore { get; set; }
    public decimal TechnicalWeight { get; set; }
    public decimal FinancialWeight { get; set; }
    public Guid? RecommendedBidId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? AuthorityApprovalReference { get; set; }
    public string? PpaApprovalReference { get; set; }
    public Guid? AwardBidId { get; set; }
    public string? AwardReference { get; set; }
    public string? ContractReference { get; set; }
    public string? BidderAcceptanceReference { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementTenderDocumentIssueDto> DocumentIssues { get; set; } = new();
    public List<ProcurementTenderSubmissionReceiptDto> SubmissionReceipts { get; set; } = new();
    public List<ProcurementTenderTechnicalResultDto> TechnicalResults { get; set; } = new();
    public List<ProcurementTenderMilestoneDto> Milestones { get; set; } = new();
}

public sealed class ProcurementTenderDocumentIssueDto
{
    public Guid Id { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public string? PaymentReference { get; set; }
    public string IssueReceiptNumber { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementTenderSubmissionReceiptDto
{
    public Guid Id { get; set; }
    public Guid TenderBidId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public ProcurementTenderSubmissionDisposition Disposition { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public decimal BidAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementTenderTechnicalResultDto
{
    public Guid BidId { get; set; }
    public decimal Score { get; set; }
    public bool Qualified { get; set; }
}

public sealed class ProcurementTenderMilestoneDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime? CompletedAtUtc { get; set; }
    public string? Reference { get; set; }
}

public sealed class PublishProcurementTenderRequest
{
    [Required, StringLength(200)] public string AdvertisementReference { get; set; } = string.Empty;
    [Required, StringLength(200)] public string PublicationChannel { get; set; } = string.Empty;
    [Required, StringLength(200)] public string TenderDocumentReference { get; set; } = string.Empty;
    [Required, StringLength(100)] public string TenderDocumentVersion { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal DocumentFee { get; set; }
    [Required, StringLength(500)] public string AdvertisementEvidenceReference { get; set; } = string.Empty;
    public DateTime SubmissionDeadlineUtc { get; set; }
    public DateTime OpeningScheduledAtUtc { get; set; }
}

public sealed class IssueProcurementTenderDocumentRequest
{
    public Guid? BusinessPartnerId { get; set; }
    [Required, StringLength(300)] public string RecipientName { get; set; } = string.Empty;
    [EmailAddress, StringLength(320)] public string? RecipientEmail { get; set; }
    [StringLength(30)] public string? RecipientPhone { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal AmountPaid { get; set; }
    [StringLength(200)] public string? PaymentReference { get; set; }
    [Required, StringLength(200)] public string IssueReceiptNumber { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
}

public sealed class CompleteProcurementTenderOpeningRequest
{
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [MinLength(2)] public List<ProcurementTenderOpeningParticipantRequest> Participants { get; set; } = new();
}

public sealed class ProcurementTenderOpeningParticipantRequest
{
    public Guid? UserId { get; set; }
    [Required, StringLength(300)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Role { get; set; } = string.Empty;
    public bool IsObserver { get; set; }
    [Required, StringLength(500)] public string SignatureReference { get; set; } = string.Empty;
}

public sealed class SaveProcurementTenderTechnicalEvaluationRequest
{
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public List<ProcurementTenderTechnicalScoreRequest> Scores { get; set; } = new();
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementTenderTechnicalScoreRequest
{
    public Guid BidId { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal Score { get; set; }
    public bool Qualified { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class SaveProcurementTenderFinancialEvaluationRequest
{
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public List<ProcurementTenderFinancialScoreRequest> Scores { get; set; } = new();
    public Guid RecommendedBidId { get; set; }
    [Required, StringLength(2000)] public string RecommendationReason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementTenderFinancialScoreRequest
{
    public Guid BidId { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal Score { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal EvaluatedAmount { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class SubmitProcurementTenderApprovalRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class DecideProcurementTenderApprovalRequest
{
    [Required] public string Action { get; set; } = string.Empty;
    [Required, StringLength(300)] public string AuthorityApprovalReference { get; set; } = string.Empty;
    [StringLength(300)] public string? PpaApprovalReference { get; set; }
    [StringLength(1000)] public string? Comments { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class RecordProcurementTenderAwardRequest
{
    public Guid BidId { get; set; }
    [Required, StringLength(200)] public string AwardReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class RecordProcurementTenderContractRequest
{
    [Required, StringLength(200)] public string ContractReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class RecordProcurementTenderAcceptanceRequest
{
    [Required, StringLength(300)] public string AcceptanceReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

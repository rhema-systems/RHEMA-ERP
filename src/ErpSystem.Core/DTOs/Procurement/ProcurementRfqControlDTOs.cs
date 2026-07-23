using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementRfqControlDto
{
    public Guid RfqId { get; set; }
    public string RfqNumber { get; set; } = string.Empty;
    public string RfqStatus { get; set; } = string.Empty;
    public Guid MethodRuleId { get; set; }
    public string MethodRuleCode { get; set; } = string.Empty;
    public int MinimumQuotationCount { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public int QualifiedInvitationCount { get; set; }
    public int OnTimeReceiptCount { get; set; }
    public int LateReceiptCount { get; set; }
    public bool SubmissionDeadlinePassed { get; set; }
    public bool QuotesRemainSealed { get; set; }
    public bool MinimumCompetitionMet { get; set; }
    public List<ProcurementRfqReceiptDto> Receipts { get; set; } = new();
    public List<ProcurementRfqEvaluationOptionDto> EvaluationOptions { get; set; } = new();
    public ProcurementRfqOpeningRegisterDto? OpeningRegister { get; set; }
    public ProcurementRfqEvaluationDto? Evaluation { get; set; }
}

public sealed class ProcurementRfqReceiptDto
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime SubmissionDeadlineUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public ProcurementRfqReceiptDisposition Disposition { get; set; }
    public DateTime SealedAtUtc { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class CompleteProcurementRfqOpeningRequest
{
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [MinLength(2)] public List<ProcurementRfqOpeningParticipantRequest> Participants { get; set; } = new();
    public List<ProcurementRfqOpeningSecurityRequest> Securities { get; set; } = new();
}

public sealed class ProcurementRfqOpeningSecurityRequest
{
    public Guid ReceiptId { get; set; }
    [Required, StringLength(300)] public string SecurityReference { get; set; } = string.Empty;
}

public sealed class ProcurementRfqOpeningParticipantRequest
{
    public Guid? ParticipantUserId { get; set; }
    [Required, StringLength(300)] public string ParticipantName { get; set; } = string.Empty;
    [Required, StringLength(150)] public string RoleName { get; set; } = string.Empty;
    public bool IsObserver { get; set; }
    [Required, StringLength(500)] public string SignatureReference { get; set; } = string.Empty;
}

public sealed class ProcurementRfqOpeningRegisterDto
{
    public Guid Id { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public Guid OpenedByUserId { get; set; }
    public string OpenedByName { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementRfqOpeningParticipantDto> Participants { get; set; } = new();
    public List<ProcurementRfqOpeningEntryDto> Entries { get; set; } = new();
}

public sealed class ProcurementRfqOpeningParticipantDto
{
    public Guid Id { get; set; }
    public Guid? ParticipantUserId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsObserver { get; set; }
    public DateTime SignedAtUtc { get; set; }
    public string SignatureReference { get; set; } = string.Empty;
}

public sealed class ProcurementRfqOpeningEntryDto
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public ProcurementRfqReceiptDisposition Disposition { get; set; }
    public decimal DeclaredAmount { get; set; }
    public string? SecurityReference { get; set; }
    public string? RejectionReason { get; set; }
    public string QuoteIntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementRfqEvaluationOptionDto
{
    public Guid RfqItemId { get; set; }
    public int RfqLineNumber { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public Guid QuoteId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class SaveProcurementRfqEvaluationRequest
{
    [Required, StringLength(30)] public string AwardMode { get; set; } = "WinnerTakesAll";
    [Required, StringLength(2000)] public string RecommendationReason { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public List<SaveProcurementRfqEvaluationLineRequest> Lines { get; set; } = new();
    public string? RowVersion { get; set; }
}

public sealed class SaveProcurementRfqEvaluationLineRequest
{
    public Guid RfqItemId { get; set; }
    public Guid QuoteId { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal TechnicalScore { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal CommercialScore { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal TotalScore { get; set; }
    [StringLength(1000)] public string? RecommendationReason { get; set; }
}

public sealed class SubmitProcurementRfqEvaluationRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class DecideProcurementRfqEvaluationRequest
{
    [Required] public string Action { get; set; } = string.Empty;
    [StringLength(1000)] public string? Comments { get; set; }
    [Required, StringLength(200)] public string ApprovalReference { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementRfqEvaluationDto
{
    public Guid Id { get; set; }
    public ProcurementRfqEvaluationStatus Status { get; set; }
    public string AwardMode { get; set; } = string.Empty;
    public string RecommendationReason { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public Guid MethodRuleId { get; set; }
    public string MethodRuleCode { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? ApprovalReference { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedByName { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementRfqEvaluationLineDto> Lines { get; set; } = new();
}

public sealed class ProcurementRfqEvaluationLineDto
{
    public Guid Id { get; set; }
    public Guid RfqItemId { get; set; }
    public int RfqLineNumber { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public Guid QuoteId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TechnicalScore { get; set; }
    public decimal CommercialScore { get; set; }
    public decimal TotalScore { get; set; }
    public string? RecommendationReason { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class FinanceExchangeRateOverrideCommandDto
{
    [Required, MaxLength(100)] public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    [Required, StringLength(3, MinimumLength = 3)] public string TransactionCurrencyCode { get; set; } = string.Empty;
    public Guid GovernedExchangeRateId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")]
    public decimal RequestedRate { get; set; }
    [Required, StringLength(1000, MinimumLength = 10)] public string Reason { get; set; } = string.Empty;
}

public sealed class FinanceExchangeRateOverrideRequestDto
{
    public Guid Id { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string? SourceDocumentReference { get; set; }
    public string TransactionCurrencyCode { get; set; } = string.Empty;
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public Guid GovernedExchangeRateId { get; set; }
    public decimal GovernedRate { get; set; }
    public string GovernedRateSource { get; set; } = string.Empty;
    public DateTime GovernedRateEffectiveDate { get; set; }
    public string GovernedRateType { get; set; } = string.Empty;
    public string GovernedQuoteSide { get; set; } = string.Empty;
    public decimal RequestedRate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ConsumedByPostingEventId { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
}

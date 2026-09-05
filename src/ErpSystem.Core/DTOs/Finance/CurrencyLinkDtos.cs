using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// DTO for adding a currency to an account
    /// </summary>
    public class AddCurrencyLinkDto
    {
        [Required]
        public Guid AccountId { get; set; }

        [MaxLength(3)]
        public string CurrencyCode { get; set; } = string.Empty;

        [MaxLength(3)]
        public string? LinkedCurrencyCode { get; set; }

        [MaxLength(50)]
        public string? CurrencyName { get; set; }

        public decimal? OpeningBalance { get; set; }
        public decimal? OpeningBalanceBaseCurrency { get; set; }
        public DateTime? OpeningBalanceDate { get; set; }
        public string? RevaluationFrequency { get; set; } = "Monthly";
        public string? TransactionRateType { get; set; } = "Daily";
        public string? TransactionQuoteSide { get; set; } = "Mid";
        public string? RevaluationRateType { get; set; } = "Month-End";
        public string? RevaluationQuoteSide { get; set; } = "Mid";
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// DTO for currency link response
    /// </summary>
    public class CurrencyLinkDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public string LinkedCurrencyCode { get; set; } = string.Empty;
        public string? CurrencyName { get; set; }
        [Obsolete("AccountCurrencyLink is configuration. Use the exact-book balance/exposure inquiry.")]
        public decimal? CurrentBalance { get; set; }
        [Obsolete("AccountCurrencyLink is configuration. Use the exact-book balance/exposure inquiry.")]
        public decimal? CurrentBalanceBaseCurrency { get; set; }
        [Obsolete("AccountCurrencyLink is configuration. Use the exact-book balance/exposure inquiry.")]
        public decimal? ForeignCurrencyBalance { get; set; }
        [Obsolete("AccountCurrencyLink is configuration. Use the exact-book balance/exposure inquiry.")]
        public decimal? BaseCurrencyBalance { get; set; }
        public bool HasAuthoritativeCurrentBalance { get; set; }
        public string CurrentBalanceAuthority { get; set; } = "ExactBookExposureRequired";
        public decimal? CurrentExchangeRate { get; set; }
        public decimal? OpeningBalance { get; set; }
        public decimal? OpeningBalanceBaseCurrency { get; set; }
        public DateTime? OpeningBalanceDate { get; set; }
        public string RevaluationFrequency { get; set; } = string.Empty;
        public string TransactionRateType { get; set; } = string.Empty;
        public string TransactionQuoteSide { get; set; } = "Mid";
        public string RevaluationRateType { get; set; } = string.Empty;
        public string RevaluationQuoteSide { get; set; } = "Mid";
        public DateTime EffectiveDate { get; set; }
        public DateTime? EffectiveEndDate { get; set; }
        public DateTime? LastRevaluationDate { get; set; }
        public decimal? LastRevaluationRate { get; set; }
        public decimal? LastRevaluationAdjustment { get; set; }
        public decimal CumulativeRevaluationAdjustment { get; set; }
        public decimal? UnrealizedGainLoss { get; set; }
        public bool IsActive { get; set; }
        public bool HasTransactions { get; set; }
        public int TransactionCount { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<AccountBookCurrencyPolicyDto> BookPolicies { get; set; } = new();
    }

    /// <summary>
    /// Updates rate-selection defaults without replacing or deleting the link.
    /// </summary>
    public class UpdateCurrencyLinkRatePolicyDto
    {
        public string? RevaluationFrequency { get; set; } = "Monthly";
        public string? TransactionRateType { get; set; } = "Daily";
        public string? TransactionQuoteSide { get; set; } = "Mid";
        public string? RevaluationRateType { get; set; } = "Month-End";
        public string? RevaluationQuoteSide { get; set; } = "Mid";

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }

    public sealed class AccountBookCurrencyPolicyDto
    {
        public Guid? Id { get; set; }
        public Guid AccountId { get; set; }
        public Guid AccountAccountingBookId { get; set; }
        public Guid AccountCurrencyLinkId { get; set; }
        public Guid AccountingBookId { get; set; }
        public string AccountingBookCode { get; set; } = string.Empty;
        public string AccountingBookName { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public Guid AccountClassificationId { get; set; }
        public string AccountClassificationCode { get; set; } = string.Empty;
        public string AccountClassificationName { get; set; } = string.Empty;
        public string CoreAccountType { get; set; } = string.Empty;
        public string ClassificationDefault { get; set; } = string.Empty;
        public bool? RevaluationOverride { get; set; }
        public bool EffectiveRevaluationRequired { get; set; }
        public string EffectiveSource { get; set; } = "Classification";
        public bool IsNonstandardInclusion { get; set; }
        public string? Warning { get; set; }
        public string LifecycleStatus { get; set; } = "Active";
        public bool? PendingRevaluationOverride { get; set; }
        public string? PendingReason { get; set; }
        public Guid? WorkflowInstanceId { get; set; }
        public Guid? RequestedByUserId { get; set; }
        public DateTime? RequestedAtUtc { get; set; }
        public Guid? DecidedByUserId { get; set; }
        public DateTime? DecidedAtUtc { get; set; }
        public string? DecisionReason { get; set; }
        public string? RowVersion { get; set; }
    }

    public sealed class SaveAccountBookCurrencyPolicyDto
    {
        public bool? RevaluationOverride { get; set; }

        [Required, MinLength(5), MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public bool ConfirmNonstandardInclusion { get; set; }
        public string? RowVersion { get; set; }
    }

    public sealed class DecideAccountBookCurrencyPolicyDto
    {
        [Required, MinLength(5), MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public string RowVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for removing/inactivating a currency link
    /// </summary>
    public class RemoveCurrencyLinkDto
    {
        [Required]
        public Guid AccountId { get; set; }

        [Required]
        [MaxLength(3)]
        public string CurrencyCode { get; set; } = string.Empty;

        public bool ForceRemove { get; set; } = false;

        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// DTO for currency link removal result
    /// </summary>
    public class CurrencyLinkRemovalResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool WasInactivated { get; set; }
        public bool WasDeleted { get; set; }
        public int TransactionCount { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}

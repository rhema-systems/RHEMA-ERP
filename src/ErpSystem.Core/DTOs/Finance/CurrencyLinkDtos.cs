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

        [Required]
        [MaxLength(3)]
        public string CurrencyCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? CurrencyName { get; set; }

        public decimal? OpeningBalance { get; set; }
        public decimal? OpeningBalanceBaseCurrency { get; set; }
        public DateTime? OpeningBalanceDate { get; set; }
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
        public string? CurrencyName { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal CurrentBalanceBaseCurrency { get; set; }
        public decimal? OpeningBalance { get; set; }
        public decimal? OpeningBalanceBaseCurrency { get; set; }
        public DateTime? OpeningBalanceDate { get; set; }
        public DateTime? LastRevaluationDate { get; set; }
        public decimal? LastRevaluationRate { get; set; }
        public decimal? UnrealizedGainLoss { get; set; }
        public bool IsActive { get; set; }
        public bool HasTransactions { get; set; }
        public int TransactionCount { get; set; }
        public DateTime CreatedDate { get; set; }
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

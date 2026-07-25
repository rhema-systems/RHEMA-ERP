using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// READ DTO: Represents an Exchange Rate as returned by the Finance API.
    /// Used for currency conversion in multi-currency transactions.
    /// </summary>
    public class ExchangeRateDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        
        /// <summary>Base currency code (converting FROM)</summary>
        public string BaseCurrencyCode { get; set; } = "GHS";
        
        /// <summary>Target currency code (converting TO)</summary>
        public string TargetCurrencyCode { get; set; } = string.Empty;
        
        /// <summary>Exchange rate value (1 base = X target)</summary>
        public decimal Rate { get; set; }
        
        /// <summary>Inverse rate (1 target = X base)</summary>
        public decimal InverseRate { get; set; }
        
        /// <summary>Date this rate is effective from</summary>
        public DateTime EffectiveDate { get; set; }
        
        /// <summary>Date this rate expires (if applicable)</summary>
        public DateTime? ExpiryDate { get; set; }
        
        /// <summary>Rate type: "Daily", "Average", "MonthEnd", "YearEnd", "Budget", "Fixed"</summary>
        public string RateType { get; set; } = "Daily";
        
        /// <summary>Rate source: "Manual", "API", "CentralBank", "Market"</summary>
        public string RateSource { get; set; } = "Manual";
        
        /// <summary>Name of external API or source</summary>
        public string? SourceName { get; set; }
        
        /// <summary>Reference number from source</summary>
        public string? SourceReference { get; set; }
        
        /// <summary>TRUE if rate is currently active</summary>
        public bool IsActive { get; set; }
        
        /// <summary>TRUE if rate has been used in transactions</summary>
        public bool HasBeenUsed { get; set; }
        public bool UsageLocked { get; set; }
        public string ApprovalStatus { get; set; } = "Approved";
        
        /// <summary>Count of transactions using this rate</summary>
        public int UsageCount { get; set; }
        
        /// <summary>Date of first transaction using this rate</summary>
        public DateTime? FirstUsedDate { get; set; }
        
        /// <summary>Date of last transaction using this rate</summary>
        public DateTime? LastUsedDate { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// CREATE DTO: Payload for creating a new exchange rate.
    /// </summary>
    public class CreateExchangeRateDto
    {
        [Required]
        [MaxLength(3)]
        public string BaseCurrencyCode { get; set; } = "GHS";
        
        [Required]
        [MaxLength(3)]
        public string TargetCurrencyCode { get; set; } = string.Empty;
        
        [Required]
        [Range(0.000001, 1000000)]
        public decimal Rate { get; set; }
        
        [Required]
        public DateTime EffectiveDate { get; set; }
        
        public DateTime? ExpiryDate { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string RateType { get; set; } = "Daily";
        
        [Required]
        [MaxLength(20)]
        public string RateSource { get; set; } = "Manual";
        
        [MaxLength(100)]
        public string? SourceName { get; set; }
        
        [MaxLength(100)]
        public string? SourceReference { get; set; }
        
        public bool IsActive { get; set; } = true;
        public string ApprovalStatus { get; set; } = "Approved";
    }

    /// <summary>
    /// UPDATE DTO: Payload for updating an existing exchange rate.
    /// </summary>
    public class UpdateExchangeRateDto
    {
        [Required]
        [Range(0.000001, 1000000)]
        public decimal Rate { get; set; }
        
        public DateTime? ExpiryDate { get; set; }
        
        [MaxLength(20)]
        public string RateType { get; set; } = "Daily";
        
        [MaxLength(20)]
        public string RateSource { get; set; } = "Manual";
        
        [MaxLength(100)]
        public string? SourceName { get; set; }
        
        [MaxLength(100)]
        public string? SourceReference { get; set; }
        
        public bool IsActive { get; set; } = true;
        public string ApprovalStatus { get; set; } = "Approved";
    }

    /// <summary>
    /// READ DTO: Represents exchange rate trend analysis data over a specific period.
    /// </summary>
    public class TrendAnalysisDto
    {
        public DateTime Date { get; set; }
        public string SourceCurrency { get; set; } = string.Empty;
        public string TargetCurrency { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public decimal? PreviousRate { get; set; }
        public decimal? ChangeAmount { get; set; }
        public decimal? ChangePercentage { get; set; }
        public decimal? MovingAverage { get; set; }
        public decimal? Volatility { get; set; }
        public decimal? MinRate { get; set; }
        public decimal? MaxRate { get; set; }
    }
}

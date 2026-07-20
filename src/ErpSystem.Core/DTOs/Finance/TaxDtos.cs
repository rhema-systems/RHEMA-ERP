using System;
using System.Collections.Generic;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance
{
    #region Tax DTOs

    /// <summary>
    /// Tax read DTO
    /// </summary>
    public class TaxDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Rate { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public TaxApplicability Applicability { get; set; }
        public TaxCategory Category { get; set; }
        public bool IsActive { get; set; }
        public bool IsInputTaxDeductible { get; set; }
        public decimal? ThresholdAmount { get; set; }
        public Guid? TaxPayableAccountId { get; set; }
        public Guid? TaxReceivableAccountId { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Tax create DTO
    /// </summary>
    public class CreateTaxDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Rate { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public TaxApplicability Applicability { get; set; } = TaxApplicability.Both;
        public TaxCategory Category { get; set; } = TaxCategory.Standard;
        public bool IsActive { get; set; } = true;
        public bool IsInputTaxDeductible { get; set; } = true;
        public decimal? ThresholdAmount { get; set; }
        public Guid? TaxPayableAccountId { get; set; }
        public Guid? TaxReceivableAccountId { get; set; }
    }

    /// <summary>
    /// Tax update DTO
    /// </summary>
    public class UpdateTaxDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Rate { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public TaxApplicability? Applicability { get; set; }
        public TaxCategory? Category { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsInputTaxDeductible { get; set; }
        public decimal? ThresholdAmount { get; set; }
        public Guid? TaxPayableAccountId { get; set; }
        public Guid? TaxReceivableAccountId { get; set; }
        public bool ClearTaxPayableAccount { get; set; }
        public bool ClearTaxReceivableAccount { get; set; }
    }

    #endregion

    #region Tax Group DTOs

    /// <summary>
    /// Tax group read DTO
    /// </summary>
    public class TaxGroupDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TaxApplicability Applicability { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public List<TaxGroupComponentDto> Components { get; set; } = new();
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Tax group component DTO
    /// </summary>
    public class TaxGroupComponentDto
    {
        public Guid Id { get; set; }
        public Guid TaxId { get; set; }
        public string TaxCode { get; set; } = string.Empty;
        public string TaxName { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public TaxCategory TaxCategory { get; set; }
        public int CalculationOrder { get; set; }
        public CompoundBasis CompoundBasis { get; set; }
        public List<string>? AppliesOnTaxCodes { get; set; }
    }

    /// <summary>
    /// Tax group create DTO
    /// </summary>
    public class CreateTaxGroupDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TaxApplicability Applicability { get; set; } = TaxApplicability.Both;
        public bool IsDefault { get; set; }
        public List<CreateTaxGroupComponentDto>? Components { get; set; }
    }

    /// <summary>
    /// Tax group component create DTO
    /// </summary>
    public class CreateTaxGroupComponentDto
    {
        public Guid TaxId { get; set; }
        public int CalculationOrder { get; set; }
        public CompoundBasis CompoundBasis { get; set; } = CompoundBasis.BaseOnly;
        public List<string>? AppliesOnTaxCodes { get; set; }
    }

    /// <summary>
    /// Tax group update DTO
    /// </summary>
    public class UpdateTaxGroupDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public TaxApplicability? Applicability { get; set; }
        public bool? IsDefault { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Add component to group DTO
    /// </summary>
    public class AddTaxGroupComponentDto
    {
        public Guid TaxId { get; set; }
        public int? CalculationOrder { get; set; }
        public CompoundBasis CompoundBasis { get; set; } = CompoundBasis.BaseOnly;
        public List<string>? AppliesOnTaxCodes { get; set; }
    }

    /// <summary>
    /// Update component DTO
    /// </summary>
    public class UpdateTaxGroupComponentDto
    {
        public int? CalculationOrder { get; set; }
        public CompoundBasis? CompoundBasis { get; set; }
        public List<string>? AppliesOnTaxCodes { get; set; }
    }

    #endregion

    #region Tax Calculation DTOs

    /// <summary>
    /// Tax calculation request DTO
    /// </summary>
    public class TaxCalculationRequestDto
    {
        /// <summary>
        /// Base amount to calculate taxes on
        /// </summary>
        public decimal BaseAmount { get; set; }

        /// <summary>
        /// Tax group to use for calculation (optional - uses default if not specified)
        /// </summary>
        public Guid? TaxGroupId { get; set; }

        /// <summary>
        /// Manual selection of individual taxes (overrides TaxGroupId)
        /// </summary>
        public List<Guid>? ManualTaxIds { get; set; }

        /// <summary>
        /// Transaction date for rate lookup
        /// </summary>
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Transaction type for applicability
        /// </summary>
        public TaxTransactionType TransactionType { get; set; }

        /// <summary>
        /// Customer ID (for threshold checks)
        /// </summary>
        public Guid? CustomerId { get; set; }

        /// <summary>
        /// Supplier ID (for WHT threshold checks)
        /// </summary>
        public Guid? SupplierId { get; set; }
    }

    /// <summary>
    /// Tax calculation result DTO
    /// </summary>
    public class TaxCalculationResultDto
    {
        /// <summary>
        /// Original base amount
        /// </summary>
        public decimal BaseAmount { get; set; }

        /// <summary>
        /// Total tax amount
        /// </summary>
        public decimal TotalTaxAmount { get; set; }

        /// <summary>
        /// Grand total (base + tax)
        /// </summary>
        public decimal GrandTotal { get; set; }

        /// <summary>
        /// Effective tax rate (total tax / base * 100)
        /// </summary>
        public decimal EffectiveTaxRate { get; set; }

        /// <summary>
        /// Tax group used (if any)
        /// </summary>
        public Guid? TaxGroupId { get; set; }

        /// <summary>
        /// Tax group name
        /// </summary>
        public string? TaxGroupName { get; set; }

        /// <summary>
        /// Detailed breakdown by tax
        /// </summary>
        public List<TaxBreakdownDto> TaxBreakdowns { get; set; } = new();

        /// <summary>
        /// Whether manual overrides were applied
        /// </summary>
        public bool HasManualOverrides { get; set; }
    }

    /// <summary>
    /// Individual tax breakdown
    /// </summary>
    public class TaxBreakdownDto
    {
        public Guid TaxId { get; set; }
        public string TaxCode { get; set; } = string.Empty;
        public string TaxName { get; set; } = string.Empty;
        public TaxCategory TaxCategory { get; set; }
        public Guid? TaxGroupComponentId { get; set; }
        public Guid? TaxPayableAccountId { get; set; }
        public Guid? TaxReceivableAccountId { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public CompoundBasis CompoundBasis { get; set; }
        public int CalculationOrder { get; set; }
        public List<string>? AppliedOnTaxCodes { get; set; }
        public bool IsInputTaxDeductible { get; set; }
        public bool IsManualOverride { get; set; }
    }

    #endregion

    #region Tax Threshold DTOs

    /// <summary>
    /// Tax threshold status DTO
    /// </summary>
    public class TaxThresholdStatusDto
    {
        public Guid TaxId { get; set; }
        public string TaxName { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int FiscalYear { get; set; }
        public decimal CumulativeAmount { get; set; }
        public decimal ThresholdAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public bool IsThresholdExceeded { get; set; }
        public DateTime? ThresholdExceededDate { get; set; }
        public decimal PercentageUsed { get; set; }
    }

    #endregion

    #region Tax Rate History DTOs

    /// <summary>
    /// Tax rate history DTO
    /// </summary>
    public class TaxRateHistoryDto
    {
        public Guid Id { get; set; }
        public Guid TaxId { get; set; }
        public decimal Rate { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    #endregion
}

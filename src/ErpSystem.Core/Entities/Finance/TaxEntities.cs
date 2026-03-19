using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance
{
    #region Core Tax Entities

    /// <summary>
    /// Tax master - defines individual taxes (VAT, NHIL, GETFL, WHT, etc.)
    /// Simplified model that combines the old TaxType and current rate
    /// </summary>
    [Table("Taxes")]
    public class Tax : TenantEntity
    {
        /// <summary>
        /// Tax code (e.g., "VAT", "NHIL", "WHT-SVC")
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Tax name (e.g., "Value Added Tax", "National Health Insurance Levy")
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Current tax rate percentage (e.g., 15.00 for 15%)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Rate { get; set; }

        /// <summary>
        /// Date this rate became effective
        /// </summary>
        [Required]
        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Applicability - Sales, Purchases, or Both
        /// </summary>
        [Required]
        public TaxApplicability Applicability { get; set; } = TaxApplicability.Both;

        /// <summary>
        /// Category - Standard (VAT), Levy (NHIL), Withholding (WHT)
        /// </summary>
        [Required]
        public TaxCategory Category { get; set; } = TaxCategory.Standard;

        /// <summary>
        /// Whether this tax is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Whether this tax is deductible as input tax (for VAT recovery)
        /// </summary>
        public bool IsInputTaxDeductible { get; set; } = true;

        /// <summary>
        /// Threshold amount for withholding taxes (null = no threshold)
        /// e.g., GHS 2,000 for Ghana WHT
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ThresholdAmount { get; set; }

        /// <summary>
        /// GL account for tax payable (output tax)
        /// </summary>
        public Guid? TaxPayableAccountId { get; set; }

        /// <summary>
        /// GL account for tax receivable (input tax)
        /// </summary>
        public Guid? TaxReceivableAccountId { get; set; }

        /// <summary>
        /// Navigation property for rate history
        /// </summary>
        public virtual ICollection<TaxRateHistory> RateHistory { get; set; } = new List<TaxRateHistory>();

        /// <summary>
        /// Navigation property for group components this tax belongs to
        /// </summary>
        public virtual ICollection<TaxGroupComponent> GroupComponents { get; set; } = new List<TaxGroupComponent>();

        /// <summary>
        /// Navigation property for thresholds
        /// </summary>
        public virtual ICollection<TaxThreshold> Thresholds { get; set; } = new List<TaxThreshold>();

        /// <summary>
        /// Navigation property for calculations
        /// </summary>
        public virtual ICollection<TaxCalculation> TaxCalculations { get; set; } = new List<TaxCalculation>();
    }

    /// <summary>
    /// Tax rate history - tracks historical rate changes for audit purposes
    /// </summary>
    [Table("TaxRateHistory")]
    public class TaxRateHistory : TenantEntity
    {
        /// <summary>
        /// Tax ID
        /// </summary>
        [Required]
        public Guid TaxId { get; set; }

        /// <summary>
        /// Rate percentage at this point in time
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Rate { get; set; }

        /// <summary>
        /// Effective from date
        /// </summary>
        [Required]
        public DateTime EffectiveFrom { get; set; }

        /// <summary>
        /// Effective to date (null = current)
        /// </summary>
        public DateTime? EffectiveTo { get; set; }

        /// <summary>
        /// Notes about this rate change
        /// </summary>
        [StringLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// Navigation property for tax
        /// </summary>
        [ForeignKey(nameof(TaxId))]
        public virtual Tax Tax { get; set; } = null!;
    }

    #endregion

    #region Tax Groups

    /// <summary>
    /// Tax group - bundles multiple taxes for common scenarios
    /// e.g., "Ghana Standard Sales Tax" = NHIL + GETFL + COVID + VAT
    /// </summary>
    [Table("TaxGroups")]
    public class TaxGroup : TenantEntity
    {
        /// <summary>
        /// Group code (e.g., "GH-SALES-STD")
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Group name (e.g., "Ghana Standard Sales Tax")
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Applicability - Sales, Purchases, or Both
        /// </summary>
        [Required]
        public TaxApplicability Applicability { get; set; } = TaxApplicability.Both;

        /// <summary>
        /// Whether this group should be auto-applied as default
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Whether this group is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Navigation property for components
        /// </summary>
        public virtual ICollection<TaxGroupComponent> Components { get; set; } = new List<TaxGroupComponent>();
    }

    /// <summary>
    /// Tax group component - defines a tax within a group with compound behavior
    /// </summary>
    [Table("TaxGroupComponents")]
    public class TaxGroupComponent : TenantEntity
    {
        /// <summary>
        /// Tax group ID
        /// </summary>
        [Required]
        public Guid TaxGroupId { get; set; }

        /// <summary>
        /// Tax ID
        /// </summary>
        [Required]
        public Guid TaxId { get; set; }

        /// <summary>
        /// Order of calculation (1, 2, 3...)
        /// Lower numbers calculated first
        /// </summary>
        public int CalculationOrder { get; set; } = 1;

        /// <summary>
        /// Compound basis - how this tax is calculated
        /// BaseOnly: Tax on base amount only
        /// Cumulative: Tax on base + all previous taxes
        /// Specific: Tax on base + specific taxes (defined in AppliesOnTaxCodes)
        /// </summary>
        [Required]
        public CompoundBasis CompoundBasis { get; set; } = CompoundBasis.BaseOnly;

        /// <summary>
        /// Comma-separated list of tax codes this tax applies on (for Specific compound basis)
        /// e.g., "NHIL,GETFL,COVID" means VAT is calculated on Base + NHIL + GETFL + COVID
        /// Only used when CompoundBasis = Specific
        /// </summary>
        [StringLength(500)]
        public string? AppliesOnTaxCodes { get; set; }

        /// <summary>
        /// Navigation property for tax group
        /// </summary>
        [ForeignKey(nameof(TaxGroupId))]
        public virtual TaxGroup TaxGroup { get; set; } = null!;

        /// <summary>
        /// Navigation property for tax
        /// </summary>
        [ForeignKey(nameof(TaxId))]
        public virtual Tax Tax { get; set; } = null!;
    }

    #endregion

    #region Threshold Tracking

    /// <summary>
    /// Tax threshold tracking (e.g., WHT GHS 2,000 threshold per entity per year)
    /// </summary>
    [Table("TaxThresholds")]
    public class TaxThreshold : TenantEntity
    {
        /// <summary>
        /// Tax ID
        /// </summary>
        [Required]
        public Guid TaxId { get; set; }

        /// <summary>
        /// Entity type (Supplier, Customer, etc.)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// Entity ID (Supplier ID, Customer ID, etc.)
        /// </summary>
        [Required]
        public Guid EntityId { get; set; }

        /// <summary>
        /// Fiscal year
        /// </summary>
        [Required]
        public int FiscalYear { get; set; }

        /// <summary>
        /// Cumulative amount for the year
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CumulativeAmount { get; set; }

        /// <summary>
        /// Threshold amount
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ThresholdAmount { get; set; }

        /// <summary>
        /// Whether threshold has been exceeded
        /// </summary>
        public bool IsThresholdExceeded { get; set; }

        /// <summary>
        /// Date threshold was first exceeded
        /// </summary>
        public DateTime? ThresholdExceededDate { get; set; }

        /// <summary>
        /// Last updated date
        /// </summary>
        public DateTime LastUpdatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property for tax
        /// </summary>
        [ForeignKey(nameof(TaxId))]
        public virtual Tax Tax { get; set; } = null!;
    }

    #endregion

    #region Tax Calculation Audit

    /// <summary>
    /// Tax calculation audit trail
    /// </summary>
    [Table("TaxCalculations")]
    public class TaxCalculation : TenantEntity
    {
        /// <summary>
        /// Source document type (Invoice, Payment, etc.)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string DocumentType { get; set; } = string.Empty;

        /// <summary>
        /// Source document ID
        /// </summary>
        [Required]
        public Guid DocumentId { get; set; }

        /// <summary>
        /// Tax ID
        /// </summary>
        [Required]
        public Guid TaxId { get; set; }

        /// <summary>
        /// Tax group ID (if calculated via group)
        /// </summary>
        public Guid? TaxGroupId { get; set; }

        /// <summary>
        /// Base amount
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseAmount { get; set; }

        /// <summary>
        /// Taxable amount (may differ from base for compound taxes)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxableAmount { get; set; }

        /// <summary>
        /// Tax rate applied
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal TaxRate { get; set; }

        /// <summary>
        /// Tax amount calculated
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        /// <summary>
        /// Compound basis used
        /// </summary>
        [Required]
        public CompoundBasis CompoundBasis { get; set; }

        /// <summary>
        /// Calculation order within the group
        /// </summary>
        public int CalculationOrder { get; set; }

        /// <summary>
        /// Calculation date
        /// </summary>
        [Required]
        public DateTime CalculationDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this was manually overridden
        /// </summary>
        public bool IsManualOverride { get; set; }

        /// <summary>
        /// Override reason
        /// </summary>
        [StringLength(500)]
        public string? OverrideReason { get; set; }

        /// <summary>
        /// Navigation property for tax
        /// </summary>
        [ForeignKey(nameof(TaxId))]
        public virtual Tax Tax { get; set; } = null!;

        /// <summary>
        /// Navigation property for tax group
        /// </summary>
        [ForeignKey(nameof(TaxGroupId))]
        public virtual TaxGroup? TaxGroup { get; set; }
    }

    #endregion

    #region Legacy Tax Entities (Backward Compatibility)

    /// <summary>
    /// Legacy TaxType entity - maintained for backward compatibility with existing DbSet.
    /// New implementations should use the Tax entity instead.
    /// </summary>
    [Table("TaxTypes")]
    public class TaxType : TenantEntity
    {
        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public TaxApplicability Applicability { get; set; } = TaxApplicability.Both;

        public virtual ICollection<TaxRate> Rates { get; set; } = new List<TaxRate>();
    }

    /// <summary>
    /// Legacy TaxRate entity - maintained for backward compatibility with existing DbSet.
    /// New implementations should use the Tax entity instead.
    /// </summary>
    [Table("TaxRates")]
    public class TaxRate : TenantEntity
    {
        [Required]
        public Guid TaxTypeId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Rate { get; set; }

        [Required]
        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

        public DateTime? EffectiveTo { get; set; }

        public bool IsActive { get; set; } = true;

        [ForeignKey(nameof(TaxTypeId))]
        public virtual TaxType TaxType { get; set; } = null!;
    }



    #endregion
}

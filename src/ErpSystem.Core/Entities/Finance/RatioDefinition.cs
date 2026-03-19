using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Type of value used in ratio numerator or denominator.
/// </summary>
public enum RatioComponentType
{
    /// <summary>Financial account balance from GL.</summary>
    FinancialAccount = 0,
    
    /// <summary>Unit account balance.</summary>
    UnitAccount = 1,
    
    /// <summary>Fixed constant value.</summary>
    Constant = 2
}

/// <summary>
/// Format for displaying ratio calculation results.
/// </summary>
public enum RatioResultFormat
{
    /// <summary>Display as decimal number (e.g., 3.45).</summary>
    Decimal = 0,
    
    /// <summary>Display as percentage (e.g., 45.5%).</summary>
    Percentage = 1,
    
    /// <summary>Display as currency (e.g., GHS 1,234.56).</summary>
    Currency = 2
}

/// <summary>
/// Defines a custom KPI ratio that combines financial and/or unit account data.
/// Enables calculation of metrics like Revenue Per Employee, Cost Per Sq Ft, etc.
/// Formula: Numerator / Denominator = Result
/// </summary>
public class RatioDefinition : TenantEntity
{
    // ========================================================================
    // RATIO IDENTIFICATION
    // ========================================================================

    /// <summary>
    /// Unique code for the ratio (e.g., "REV-EMP", "COST-SQFT").
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the ratio.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this ratio measures.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    // ========================================================================
    // NUMERATOR CONFIGURATION
    // ========================================================================

    /// <summary>
    /// Type of the numerator value.
    /// </summary>
    [Required]
    public RatioComponentType NumeratorType { get; set; }

    /// <summary>
    /// Account ID for numerator (GL Account or Unit Account).
    /// Required if NumeratorType is FinancialAccount or UnitAccount.
    /// </summary>
    public Guid? NumeratorAccountId { get; set; }

    /// <summary>
    /// Constant value for numerator.
    /// Required if NumeratorType is Constant.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? NumeratorConstant { get; set; }

    // ========================================================================
    // DENOMINATOR CONFIGURATION
    // ========================================================================

    /// <summary>
    /// Type of the denominator value.
    /// </summary>
    [Required]
    public RatioComponentType DenominatorType { get; set; }

    /// <summary>
    /// Account ID for denominator (GL Account or Unit Account).
    /// Required if DenominatorType is FinancialAccount or UnitAccount.
    /// </summary>
    public Guid? DenominatorAccountId { get; set; }

    /// <summary>
    /// Constant value for denominator.
    /// Required if DenominatorType is Constant.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? DenominatorConstant { get; set; }

    // ========================================================================
    // RESULT FORMATTING
    // ========================================================================

    /// <summary>
    /// How to format the calculated result.
    /// </summary>
    [Required]
    public RatioResultFormat ResultFormat { get; set; } = RatioResultFormat.Decimal;

    /// <summary>
    /// Number of decimal places for the result.
    /// </summary>
    [Required]
    [Range(0, 6)]
    public int DecimalPlaces { get; set; } = 2;

    // ========================================================================
    // STATUS
    // ========================================================================

    /// <summary>
    /// Whether this ratio is active and available for calculation.
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;
}

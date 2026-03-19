using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a payment term configuration (e.g., Net 30, Net 60, COD)
/// </summary>
public class PaymentTerm : TenantEntity
{
    /// <summary>
    /// Unique code for the payment term (e.g., "NET30", "COD", "NET60")
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the payment term (e.g., "Net 30 Days", "Cash on Delivery")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the payment term
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Number of days until payment is due (0 for immediate payment)
    /// </summary>
    public int DueDays { get; set; }

    /// <summary>
    /// Discount percentage if paid within the discount period
    /// </summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>
    /// Number of days within which the discount applies
    /// </summary>
    public int DiscountDays { get; set; }

    /// <summary>
    /// Whether this payment term is active and can be selected
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this is the default payment term for new business partners
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Display order for sorting in dropdowns
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Applicable to: All, Supplier, Customer, Contractor
    /// </summary>
    [MaxLength(20)]
    public string ApplicableTo { get; set; } = "All";
}

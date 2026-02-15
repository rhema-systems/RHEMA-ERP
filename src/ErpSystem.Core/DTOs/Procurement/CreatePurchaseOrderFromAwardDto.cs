using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// DTO for creating a purchase order from a tender award
/// </summary>
public class CreatePurchaseOrderFromAwardDto
{
    /// <summary>
    /// Tender award ID to convert to PO
    /// </summary>
    [Required]
    public Guid TenderAwardId { get; set; }

    /// <summary>
    /// Optional contract ID if contract already exists
    /// </summary>
    public Guid? ContractId { get; set; }

    /// <summary>
    /// Optional contract number if contract already exists
    /// </summary>
    [MaxLength(50)]
    public string? ContractNumber { get; set; }

    /// <summary>
    /// Required delivery date for the PO
    /// </summary>
    public DateTime? RequiredDate { get; set; }

    /// <summary>
    /// Delivery warehouse ID
    /// </summary>
    public Guid? DeliveryWarehouseId { get; set; }

    /// <summary>
    /// Delivery address
    /// </summary>
    [MaxLength(500)]
    public string? DeliveryAddress { get; set; }

    /// <summary>
    /// Delivery instructions
    /// </summary>
    [MaxLength(2000)]
    public string? DeliveryInstructions { get; set; }

    /// <summary>
    /// Payment terms (if different from award)
    /// </summary>
    [MaxLength(100)]
    public string? PaymentTerms { get; set; }

    /// <summary>
    /// Shipping terms
    /// </summary>
    [MaxLength(100)]
    public string? ShippingTerms { get; set; }

    /// <summary>
    /// Additional notes for the PO
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Whether to auto-approve the PO
    /// </summary>
    public bool AutoApprove { get; set; } = false;
}

/// <summary>
/// Response DTO for PO creation from award
/// </summary>
public class PurchaseOrderFromAwardResponseDto
{
    public Guid PurchaseOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid TenderAwardId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public int ItemCount { get; set; }
    public string? ContractNumber { get; set; }
}

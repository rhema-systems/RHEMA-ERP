using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

// ============================================================================
// PAYMENT TERM DTOs
// ============================================================================

/// <summary>
/// DTO for Payment Term list view
/// </summary>
public class PaymentTermDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DueDays { get; set; }
    public decimal DiscountPercent { get; set; }
    public int DiscountDays { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public int DisplayOrder { get; set; }
    public string ApplicableTo { get; set; } = "All";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for creating a new Payment Term
/// </summary>
public class CreatePaymentTermDto
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0, 365)]
    public int DueDays { get; set; }

    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }

    [Range(0, 365)]
    public int DiscountDays { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }

    [MaxLength(20)]
    public string ApplicableTo { get; set; } = "All";
}

/// <summary>
/// DTO for updating a Payment Term
/// </summary>
public class UpdatePaymentTermDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0, 365)]
    public int DueDays { get; set; }

    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }

    [Range(0, 365)]
    public int DiscountDays { get; set; }

    public bool IsActive { get; set; }

    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }

    [MaxLength(20)]
    public string ApplicableTo { get; set; } = "All";
}


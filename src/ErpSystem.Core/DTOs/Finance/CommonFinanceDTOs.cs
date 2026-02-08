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

// ============================================================================
// CURRENCY DTOs
// ============================================================================

/// <summary>
/// DTO for Currency list view
/// </summary>
public class CurrencyDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
    public decimal ExchangeRate { get; set; }
    public DateTime? ExchangeRateDate { get; set; }
    public bool IsBaseCurrency { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public string? FormatString { get; set; }
    public string? Country { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for creating a new Currency
/// </summary>
public class CreateCurrencyDto
{
    [Required]
    [MaxLength(3)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Symbol { get; set; } = string.Empty;

    [Range(0, 6)]
    public int DecimalPlaces { get; set; } = 2;

    [Range(0.000001, 1000000)]
    public decimal ExchangeRate { get; set; } = 1;

    public bool IsBaseCurrency { get; set; }

    public int DisplayOrder { get; set; }

    [MaxLength(50)]
    public string? FormatString { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }
}

/// <summary>
/// DTO for updating a Currency
/// </summary>
public class UpdateCurrencyDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Symbol { get; set; } = string.Empty;

    [Range(0, 6)]
    public int DecimalPlaces { get; set; } = 2;

    [Range(0.000001, 1000000)]
    public decimal ExchangeRate { get; set; } = 1;

    public bool IsBaseCurrency { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    [MaxLength(50)]
    public string? FormatString { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }
}

/// <summary>
/// DTO for updating exchange rate only
/// </summary>
public class UpdateExchangeRateDto
{
    [Required]
    [Range(0.000001, 1000000)]
    public decimal ExchangeRate { get; set; }
}

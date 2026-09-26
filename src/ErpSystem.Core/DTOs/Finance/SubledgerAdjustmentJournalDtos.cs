using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public class SubledgerAdjustmentJournalDto
{
    public Guid Id { get; set; }
    public string Module { get; set; } = string.Empty;
    public string AdjustmentNumber { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime AdjustmentDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal SignedSubledgerAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal BaseCurrencyAmount { get; set; }
    public Guid ContraAccountId { get; set; }
    public string? ContraAccountNumber { get; set; }
    public string? ContraAccountName { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? JournalNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid? OriginalAdjustmentId { get; set; }
    public Guid? ReversalAdjustmentId { get; set; }
    public string? ReversalReason { get; set; }
    public DateTime? ReversedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateSubledgerAdjustmentJournalDto
{
    public Guid? RequestId { get; set; }
    [Required]
    [MaxLength(2)]
    public string Module { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Purpose { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? SupplierId { get; set; }

    [Required]
    public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow;

    public DateTime? DueDate { get; set; }

    [Required]
    [MaxLength(10)]
    public string AdjustmentType { get; set; } = string.Empty;

    [Range(0.01, 999999999.99)]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }

    [Range(0.000001, 999999.999999)]
    public decimal ExchangeRate { get; set; } = 1m;

    [Required]
    public Guid ContraAccountId { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class ReverseSubledgerAdjustmentJournalDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateTime? ReversalDate { get; set; }
}

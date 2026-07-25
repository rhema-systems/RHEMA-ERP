using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents the allocation of a customer payment to a specific invoice
/// Supports partial and multi-invoice payment allocation
/// </summary>
public class PaymentAllocation : BaseEntity
{
    [Required]
    public Guid CustomerPaymentId { get; set; }
    public virtual CustomerPayment CustomerPayment { get; set; } = null!;

    [Required]
    public Guid InvoiceId { get; set; }
    public virtual Invoice Invoice { get; set; } = null!;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    public DateTime AllocationDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Track if this is a reversal/adjustment
    public bool IsReversal { get; set; } = false;
    public Guid? OriginalAllocationId { get; set; }

    /// <summary>
    /// Present only when a previously posted customer advance is applied. This records the
    /// engine-created advance-to-AR-control reclassification without altering the cash receipt.
    /// </summary>
    public Guid? ApplicationJournalEntryId { get; set; }
    public Guid? ApplicationPostingEventId { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

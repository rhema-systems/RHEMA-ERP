using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Sales;

/// <summary>
/// Generic reservation/allocation ledger for any saleable source connected to Sales.
/// </summary>
public class SalesAllocation : TenantEntity
{
    [Required]
    public Guid SaleableSourceId { get; set; }
    public virtual SalesSaleableSource SaleableSource { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string SourceCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string SourceType { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string AdapterKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string SourceItemId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SourceItemCode { get; set; }

    [Required]
    [MaxLength(250)]
    public string SourceItemName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? SourceItemType { get; set; }

    public Guid? BusinessPartnerId { get; set; }
    public virtual BusinessPartner? BusinessPartner { get; set; }

    [MaxLength(200)]
    public string? CustomerName { get; set; }

    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }

    public Guid? SalesOrderId { get; set; }
    public virtual SalesOrder? SalesOrder { get; set; }

    public Guid? SalesAgreementId { get; set; }
    public virtual SalesAgreement? SalesAgreement { get; set; }

    [Required]
    [MaxLength(50)]
    public string AllocationType { get; set; } = "Reservation";

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Reserved";

    public DateTime? ReservedUntil { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ReleasedDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AgreedValue { get; set; }

    [MaxLength(10)]
    public string? Currency { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? ReleaseReason { get; set; }

    public virtual ICollection<SalesAllocationHistory> History { get; set; } = new List<SalesAllocationHistory>();
}

public class SalesAllocationHistory : TenantEntity
{
    [Required]
    public Guid SalesAllocationId { get; set; }
    public virtual SalesAllocation SalesAllocation { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? FromStatus { get; set; }

    [Required]
    [MaxLength(50)]
    public string ToStatus { get; set; } = string.Empty;

    public Guid? PerformedById { get; set; }

    [MaxLength(200)]
    public string? PerformedByName { get; set; }

    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

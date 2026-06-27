using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Sales;

public class SalesAllocationDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SaleableSourceId { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string AdapterKey { get; set; } = string.Empty;
    public string SourceItemId { get; set; } = string.Empty;
    public string? SourceItemCode { get; set; }
    public string SourceItemName { get; set; } = string.Empty;
    public string? SourceItemType { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public string? SalesOrderNumber { get; set; }
    public Guid? SalesAgreementId { get; set; }
    public string? SalesAgreementTitle { get; set; }
    public string AllocationType { get; set; } = "Reservation";
    public string Status { get; set; } = "Reserved";
    public DateTime? ReservedUntil { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public decimal? EstimatedValue { get; set; }
    public decimal? AgreedValue { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
    public string? ReleaseReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<SalesAllocationHistoryDto> History { get; set; } = new();
}

public class SalesAllocationHistoryDto
{
    public Guid Id { get; set; }
    public Guid SalesAllocationId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public Guid? PerformedById { get; set; }
    public string? PerformedByName { get; set; }
    public DateTime PerformedAt { get; set; }
    public string? Notes { get; set; }
}

public class CreateSalesAllocationDto
{
    [Required]
    public Guid SaleableSourceId { get; set; }

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

    [MaxLength(200)]
    public string? CustomerName { get; set; }

    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? SalesAgreementId { get; set; }

    [MaxLength(50)]
    public string AllocationType { get; set; } = "Reservation";

    [MaxLength(50)]
    public string Status { get; set; } = "Reserved";

    public DateTime? ReservedUntil { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public decimal? EstimatedValue { get; set; }
    public decimal? AgreedValue { get; set; }

    [MaxLength(10)]
    public string? Currency { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateSalesAllocationStatusDto
{
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    public Guid? SalesOrderId { get; set; }
    public Guid? SalesAgreementId { get; set; }
    public decimal? AgreedValue { get; set; }
    public DateTime? ReservedUntil { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? ReleaseReason { get; set; }
}

public class SalesAllocationApprovalDto
{
    public bool IsApproved { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
}

public class TransferSalesAllocationDto
{
    public Guid? BusinessPartnerId { get; set; }

    [MaxLength(200)]
    public string? CustomerName { get; set; }

    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }

    public bool ClearLinkedDocuments { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

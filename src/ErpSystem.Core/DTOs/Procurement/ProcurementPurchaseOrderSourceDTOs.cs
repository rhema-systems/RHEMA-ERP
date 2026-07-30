using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementPurchaseOrderSourceOptionDto
{
    public ProcurementPurchaseOrderSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public Guid PurchaseRequisitionId { get; set; }
    public string PurchaseRequisitionNumber { get; set; } = string.Empty;
    public Guid SourcingCaseId { get; set; }
    public Guid SourcingReleaseId { get; set; }
    public Guid AwardReadinessDecisionId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal? ApprovedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public IReadOnlyList<ProcurementPurchaseOrderSourceLineDto> ApprovedLines { get; set; } = [];
}

public sealed class ProcurementPurchaseOrderSourceLineDto
{
    public Guid SourceLineId { get; init; }
    public Guid? InventoryItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string UnitOfMeasure { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed class ProcurementPurchaseOrderSourceOrderLine
{
    public Guid? InventoryItemId { get; init; }
    public string? ItemDescription { get; init; }
    public decimal OrderedQuantity { get; init; }
    public string UnitOfMeasure { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
}

public sealed class ProcurementPurchaseOrderSourceResolution
{
    public ProcurementPurchaseOrderSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public Guid PurchaseRequisitionId { get; init; }
    public string PurchaseRequisitionNumber { get; init; } = string.Empty;
    public Guid PurchaseRequisitionRequestedById { get; init; }
    public Guid SourcingCaseId { get; init; }
    public Guid SourcingReleaseId { get; init; }
    public Guid AwardReadinessDecisionId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal? ApprovedAmount { get; init; }
    public IReadOnlyList<ProcurementPurchaseOrderSourceLineDto> ApprovedLines { get; init; } = [];
    public string SourceSnapshotJson { get; init; } = "{}";
    public string SourceIntegrityHash { get; init; } = string.Empty;
    public DateTime ValidatedAtUtc { get; init; }
}

public sealed class ProcurementPurchaseOrderSourceStatusDto
{
    public bool Ready { get; set; }
    public string Permission { get; set; } = "procurement.purchase-order.create";
    public int CandidateCount { get; set; }
    public IReadOnlyList<string> BlockedReasons { get; set; } = [];
    public IReadOnlyList<ProcurementPurchaseOrderSourceOptionDto> Sources { get; set; } = [];
    public string FrameworkCallOffRoute { get; set; } = "/procurement/framework-call-offs";
}

public sealed class ProcurementPurchaseOrderSourceSelection
{
    [Required]
    public ProcurementPurchaseOrderSourceType? SourceType { get; set; }

    [Required]
    public Guid? SourceId { get; set; }
}

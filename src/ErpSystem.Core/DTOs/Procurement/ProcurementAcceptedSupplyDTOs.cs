using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementAcceptedSupplyOptionDto
{
    public ProcurementAcceptedSupplyKind Kind { get; init; }
    public Guid SourceId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public ProcurementCategoryClass Category { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal? AcceptedAmount { get; init; }
    public DateTime AcceptedAtUtc { get; init; }
    public string SourceIntegrityHash { get; init; } = string.Empty;
}

public sealed class ProcurementAcceptedSupplyOptionsDto
{
    public Guid PurchaseOrderId { get; init; }
    public ProcurementCategoryClass Category { get; init; }
    public IReadOnlyList<ProcurementAcceptedSupplyOptionDto> Options { get; init; } = [];
    public IReadOnlyList<string> BlockedReasons { get; init; } = [];
    public bool Ready => Options.Count != 0;
    public string WorksHandoffRoute { get; init; } = "/quantity-survey/payment-certificates";
}

public sealed class ProcurementAcceptedSupplyLineDto
{
    public Guid PurchaseOrderItemId { get; init; }
    public decimal AcceptedQuantity { get; init; }
    public decimal UnitPrice { get; init; }
}

public sealed class ProcurementAcceptedSupplyResolutionDto
{
    public ProcurementAcceptedSupplyKind Kind { get; init; }
    public Guid SourceId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public ProcurementCategoryClass Category { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public Guid BusinessPartnerId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal? AcceptedAmount { get; init; }
    public DateTime AcceptedAtUtc { get; init; }
    public IReadOnlyList<ProcurementAcceptedSupplyLineDto> Lines { get; init; } = [];
    public string SnapshotJson { get; init; } = "{}";
    public string SourceIntegrityHash { get; init; } = string.Empty;
}

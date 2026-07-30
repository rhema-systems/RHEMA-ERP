namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementPurchaseOrderComplianceDto
{
    public Guid PurchaseOrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public bool IsCompliant { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; init; }
    public IReadOnlyList<string> DecisionKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ProcurementPurchaseOrderComplianceCheckDto> Checks { get; init; } =
        Array.Empty<ProcurementPurchaseOrderComplianceCheckDto>();
    public IReadOnlyList<string> BlockedReasons { get; init; } = Array.Empty<string>();
}

public sealed class ProcurementPurchaseOrderComplianceCheckDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool Required { get; init; } = true;
    public bool Passed { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Guid? ReferenceId { get; init; }
    public string? Reference { get; init; }
    public string? IntegrityHash { get; init; }
    public IReadOnlyList<string> Details { get; init; } = Array.Empty<string>();
}

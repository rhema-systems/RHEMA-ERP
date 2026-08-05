namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementPurchaseOrderSodReadinessDto
{
    public Guid PurchaseOrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid CurrentActorUserId { get; init; }
    public bool CanApprove { get; init; }
    public bool CanReceive { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; init; }
    public IReadOnlyList<string> DecisionKeys { get; init; } =
        Array.Empty<string>();
    public IReadOnlyList<string> ReceiptActionCoverage { get; init; } =
        Array.Empty<string>();
    public IReadOnlyList<ProcurementPurchaseOrderSodCheckDto> Checks { get; init; } =
        Array.Empty<ProcurementPurchaseOrderSodCheckDto>();
}

public sealed class ProcurementPurchaseOrderSodCheckDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string ControlCode { get; init; } = string.Empty;
    public bool Allowed { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> ParticipantRoles { get; init; } =
        Array.Empty<string>();
    public IReadOnlyList<Guid> ProhibitedActorUserIds { get; init; } =
        Array.Empty<Guid>();
    public Guid? PolicySetId { get; init; }
    public string? PolicyCode { get; init; }
    public int? PolicyVersion { get; init; }
    public Guid? RuleId { get; init; }
    public string? RuleCode { get; init; }
}

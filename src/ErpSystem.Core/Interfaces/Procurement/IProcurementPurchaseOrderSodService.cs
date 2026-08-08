using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementPurchaseOrderSodService
{
    Task<ProcurementPurchaseOrderSodReadinessDto> GetReadinessAsync(
        Guid purchaseOrderId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderSodReadinessDto> EnforceApprovalAsync(
        PurchaseOrder purchaseOrder,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderSodReadinessDto> EnforceReceiptAsync(
        PurchaseOrder purchaseOrder,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderSodReadinessDto> EnforceReceiptActionAsync(
        PurchaseOrder purchaseOrder,
        string receiptAction,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task RejectApprovalBypassAsync(
        PurchaseOrder purchaseOrder,
        string attempt,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementPurchaseOrderSodNotFoundException(
    string code,
    string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementPurchaseOrderSodBlockedException(
    string code,
    string message,
    ProcurementPurchaseOrderSodReadinessDto readiness)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public ProcurementPurchaseOrderSodReadinessDto Readiness { get; } = readiness;
}

public sealed class ProcurementPurchaseOrderSodAuthorizationException(
    string message) : UnauthorizedAccessException(message);

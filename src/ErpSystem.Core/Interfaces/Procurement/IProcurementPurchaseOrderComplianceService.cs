using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementPurchaseOrderComplianceService
{
    Task<ProcurementPurchaseOrderComplianceDto> GetReadinessAsync(
        Guid purchaseOrderId,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderComplianceDto> EnforceAsync(
        PurchaseOrder purchaseOrder,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementPurchaseOrderComplianceNotFoundException(
    string code,
    string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementPurchaseOrderComplianceBlockedException(
    string code,
    string message,
    ProcurementPurchaseOrderComplianceDto readiness) : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public ProcurementPurchaseOrderComplianceDto Readiness { get; } = readiness;
}

public sealed class ProcurementPurchaseOrderComplianceAuthorizationException(
    string message) : UnauthorizedAccessException(message);

using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementPurchaseOrderSourceService
{
    Task<ProcurementPurchaseOrderSourceStatusDto> GetOptionsAsync(
        Guid? purchaseRequisitionId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderSourceResolution> ResolveAsync(
        ProcurementPurchaseOrderSourceType sourceType,
        Guid sourceId,
        Guid businessPartnerId,
        string correlationId,
        CancellationToken cancellationToken = default);

    void Apply(PurchaseOrder purchaseOrder, ProcurementPurchaseOrderSourceResolution source);

    Task<ProcurementPurchaseOrderSourceResolution> ResolveFrameworkCallOffAsync(
        ProcurementFrameworkCallOff callOff,
        ProcurementFrameworkAgreement agreement,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderSourceResolution> RevalidateAsync(
        PurchaseOrder purchaseOrder,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task AuthorizeDraftCancellationAsync(
        PurchaseOrder purchaseOrder,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderSourceResolution> EvaluateCurrentAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken = default);

    Task ValidateOrderAsync(
        ProcurementPurchaseOrderSourceResolution source,
        IReadOnlyCollection<ProcurementPurchaseOrderSourceOrderLine> lines,
        decimal totalAmount,
        string? currencyCode,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task ReserveAsync(
        ProcurementPurchaseOrderSourceResolution source,
        IReadOnlyCollection<ProcurementPurchaseOrderSourceOrderLine> lines,
        decimal totalAmount,
        string? currencyCode,
        Guid purchaseOrderId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task EnsureBudgetAvailabilityForSubmissionAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken = default);

    Task EnsureBudgetCommitmentForIssueAsync(
        PurchaseOrder purchaseOrder,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task ClaimTenderAwardAsync(
        Guid tenderAwardId,
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken = default);

    Task RecordBoundAsync(
        PurchaseOrder purchaseOrder,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementPurchaseOrderSourceValidationException(
    string code,
    string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementPurchaseOrderSourceAuthorizationException(string message)
    : UnauthorizedAccessException(message);

using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementReceiptSourceControlService
{
    Task<ProcurementReceiptSourceReadinessDto> GetReadinessAsync(
        Guid purchaseOrderId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementReceiptSourceSnapshot> EnforceCreateAsync(
        PurchaseOrder purchaseOrder,
        IReadOnlyCollection<ProcurementReceiptSourceLineRequest> lines,
        string receiptKind,
        Guid receiptId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementReceiptSourceSnapshot> RevalidatePurchaseOrderReceiptAsync(
        Guid receiptId,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementReceiptSourceSnapshot> RevalidateGoodsReceiptNoteAsync(
        Guid goodsReceiptNoteId,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task EnforceInventoryPostingAsync(
        ReferenceType referenceType,
        Guid? referenceId,
        Guid inventoryItemId,
        decimal quantity,
        string correlationId,
        CancellationToken cancellationToken = default);

    void ResetInventoryPostingAttempt();

    Task RecordDeniedAsync(
        Guid? purchaseOrderId,
        string sourceReference,
        string action,
        string code,
        string message,
        string correlationId,
        object? inputValues = null,
        CancellationToken cancellationToken = default);
}

public class ProcurementReceiptSourceException(
    string code,
    string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementReceiptSourceNotFoundException(
    string code,
    string message) : ProcurementReceiptSourceException(code, message);

public sealed class ProcurementReceiptSourceValidationException(
    string code,
    string message,
    ProcurementReceiptSourceReadinessDto? readiness = null)
    : ProcurementReceiptSourceException(code, message)
{
    public ProcurementReceiptSourceReadinessDto? Readiness { get; } = readiness;
}

public sealed class ProcurementReceiptSourceAuthorizationException(
    string message) : UnauthorizedAccessException(message);

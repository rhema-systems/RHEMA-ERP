using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementInvoicePaymentSodService
{
    Task<ProcurementInvoicePaymentSodReadinessDto> GetPaymentReadinessAsync(
        Guid paymentId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementInvoicePaymentSodReadinessDto> GetBatchReadinessAsync(
        Guid batchId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementInvoicePaymentSodQueueReadinessDto> GetQueueReadinessAsync(
        IReadOnlyCollection<Guid> paymentIds,
        IReadOnlyCollection<Guid> batchIds,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementInvoicePaymentSodReadinessDto> EnforcePaymentApprovalAsync(
        Guid paymentId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementInvoicePaymentSodReadinessDto> EnforceBatchApprovalAsync(
        Guid batchId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task RevalidatePaymentAuthorizationAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task RevalidateBatchAuthorizationAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementInvoicePaymentSodNotFoundException(string message) : Exception(message);

public sealed class ProcurementInvoicePaymentSodBlockedException(
    string code,
    string message,
    ProcurementInvoicePaymentSodReadinessDto readiness) : Exception(message)
{
    public string Code { get; } = code;
    public ProcurementInvoicePaymentSodReadinessDto Readiness { get; } = readiness;
}

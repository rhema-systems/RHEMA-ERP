using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementPurchaseOrderAmendmentService
{
    Task<ProcurementPurchaseOrderAmendmentOverviewDto> GetOverviewAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcurementPurchaseOrderAmendmentDto>> GetExternalOverviewAsync(
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderAmendmentDto> CreateAsync(
        Guid purchaseOrderId,
        CreateProcurementPurchaseOrderAmendmentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderAmendmentDto> SubmitAsync(
        Guid amendmentId,
        ProcurementPurchaseOrderAmendmentLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderAmendmentDto> DecideAsync(
        Guid amendmentId,
        DecideProcurementPurchaseOrderAmendmentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderAmendmentDispatchDto> DispatchAsync(
        Guid amendmentId,
        DispatchProcurementPurchaseOrderAmendmentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementPurchaseOrderAmendmentAcknowledgementDto> AcknowledgeAsync(
        Guid dispatchId,
        AcknowledgeProcurementPurchaseOrderAmendmentRequest request,
        string correlationId,
        bool external,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementPurchaseOrderAmendmentNotFoundException(
    string code,
    string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementPurchaseOrderAmendmentValidationException(
    string code,
    string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementPurchaseOrderAmendmentConflictException(
    string code,
    string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementPurchaseOrderAmendmentAuthorizationException(
    string message) : UnauthorizedAccessException(message);

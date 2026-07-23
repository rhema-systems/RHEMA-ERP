using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementRfqControlService
{
    Task<ProcurementRfqControlDto> GetAsync(Guid rfqId, CancellationToken cancellationToken = default);
    Task EnsureDispatchReadyAsync(Guid rfqId, IReadOnlyCollection<Guid> supplierIds, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementRfqReceipt> RecordReceiptAsync(Guid rfqId, Guid quoteId, DateTime receivedAtUtc, string correlationId, CancellationToken cancellationToken = default);
    Task<bool> AreQuotesOpenAsync(Guid rfqId, CancellationToken cancellationToken = default);
    Task<ProcurementRfqOpeningRegisterDto> CompleteOpeningAsync(Guid rfqId, CompleteProcurementRfqOpeningRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementRfqEvaluationDto> SaveEvaluationAsync(Guid rfqId, SaveProcurementRfqEvaluationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementRfqEvaluationDto> SubmitEvaluationAsync(Guid rfqId, SubmitProcurementRfqEvaluationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementRfqEvaluationDto> DecideEvaluationAsync(Guid rfqId, DecideProcurementRfqEvaluationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CreatePurchaseOrdersFromRfqDto> GetApprovedAwardAsync(Guid rfqId, string correlationId, CancellationToken cancellationToken = default);
    Task RecordAwardHandoffAsync(Guid rfqId, IReadOnlyCollection<CreatedPurchaseOrderFromRfqDto> purchaseOrders, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementRfqControlNotFoundException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementRfqControlConflictException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementRfqControlValidationException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementRfqControlAuthorizationException(string message) : UnauthorizedAccessException(message);

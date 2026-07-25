using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementExceptionalSourcingControlService
{
    Task<bool> IsExceptionalAsync(Guid tenderId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingReadinessDto> GetReadinessAsync(Guid tenderId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingControlDto> GetAsync(Guid tenderId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingControlDto> PrepareAsync(Guid tenderId, PrepareProcurementExceptionalSourcingRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingControlDto> SubmitApprovalAsync(Guid tenderId, SubmitProcurementExceptionalApprovalRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingControlDto> DecideApprovalAsync(Guid tenderId, DecideProcurementExceptionalApprovalRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task EnsureBidSupplierAllowedAsync(Guid tenderId, Guid businessPartnerId, CancellationToken cancellationToken = default);
    Task EnsureNegotiationAllowedAsync(Guid tenderId, Guid bidId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingControlDto> RecordNegotiationAsync(Guid tenderId, RecordProcurementExceptionalNegotiationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingControlDto> RecordRecommendationAsync(Guid tenderId, RecordProcurementExceptionalRecommendationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingControlDto> RecordAwardAsync(Guid tenderId, RecordProcurementTenderAwardRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingControlDto> RecordContractAsync(Guid tenderId, RecordProcurementTenderContractRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingControlDto> RecordAcceptanceAsync(Guid tenderId, RecordProcurementTenderAcceptanceRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementExceptionalSourcingControlDto> RecordPostAwardFilingAsync(Guid tenderId, RecordProcurementPostAwardFilingRequest request, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementExceptionalSourcingNotFoundException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementExceptionalSourcingConflictException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementExceptionalSourcingValidationException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public sealed class ProcurementExceptionalSourcingAuthorizationException(string message) : UnauthorizedAccessException(message);

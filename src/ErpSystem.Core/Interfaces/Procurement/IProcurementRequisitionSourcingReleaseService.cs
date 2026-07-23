using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementRequisitionSourcingReleaseService
{
    Task<PurchaseRequisitionSourcingReadinessDto> GetReadinessAsync(Guid requisitionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseRequisitionSourcingReleaseDto>> GetHistoryAsync(Guid requisitionId, CancellationToken cancellationToken = default);
    Task<PurchaseRequisitionSourcingReleaseDto> ReleaseAsync(Guid requisitionId, string reason, string correlationId, CancellationToken cancellationToken = default);
    Task<PurchaseRequisitionSourcingReleaseDto> EnforceSourcingAsync(Guid requisitionId, string sourceType, string sourceReference, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementRequisitionSourcingNotFoundException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionSourcingBlockedException(PurchaseRequisitionSourcingReadinessDto readiness)
    : InvalidOperationException(readiness.Message)
{
    public PurchaseRequisitionSourcingReadinessDto Readiness { get; } = readiness;
}

public sealed class ProcurementRequisitionSourcingConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionSourcingValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionSourcingAuthorizationException(string message) : UnauthorizedAccessException(message);

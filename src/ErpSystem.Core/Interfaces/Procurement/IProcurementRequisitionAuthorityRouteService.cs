using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementRequisitionAuthorityRouteService
{
    Task<PurchaseRequisitionAuthorityReadinessDto> GetReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);
    Task<PurchaseRequisitionAuthorityReadinessDto> GetLinkedControlReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);

    Task<ProcurementAuthorityRouteDecisionDto> EnforceSubmissionAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementRequisitionAuthorityRoute> CaptureAsync(
        PurchaseRequisition requisition,
        ProcurementAuthorityRouteDecisionDto decision,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequisitionAuthorityReadinessDto> EnforceApprovalAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementRequisitionAuthorityRoute?> GetLatestRouteAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseRequisitionAuthorityRouteHistoryDto>> GetHistoryAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementRequisitionAuthorityNotFoundException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionAuthorityBlockedException(PurchaseRequisitionAuthorityReadinessDto readiness)
    : InvalidOperationException(readiness.Message)
{
    public PurchaseRequisitionAuthorityReadinessDto Readiness { get; } = readiness;
}

public sealed class ProcurementRequisitionAuthorityConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionAuthorityAuthorizationException(string message) : UnauthorizedAccessException(message);

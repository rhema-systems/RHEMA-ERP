using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementRequisitionSubmissionControlService
{
    Task<PurchaseRequisitionSubmissionReadinessDto> GetReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);
    Task<PurchaseRequisitionSubmissionReadinessDto> EnforceAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseRequisitionSubmissionControlHistoryDto>> GetHistoryAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementRequisitionSubmissionNotFoundException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionSubmissionBlockedException(
    PurchaseRequisitionSubmissionReadinessDto readiness) : InvalidOperationException(readiness.Message)
{
    public PurchaseRequisitionSubmissionReadinessDto Readiness { get; } = readiness;
}

public sealed class ProcurementRequisitionSubmissionAuthorizationException(string message) : UnauthorizedAccessException(message);

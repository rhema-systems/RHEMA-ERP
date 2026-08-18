using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementRequisitionBudgetControlService
{
    Task<PurchaseRequisitionBudgetReadinessDto> GetReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);
    Task<PurchaseRequisitionBudgetReadinessDto> GetLinkedControlReadinessAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequisitionBudgetReadinessDto> ReserveAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequisitionBudgetReleaseDto> ReleaseAsync(
        PurchaseRequisition requisition,
        string reason,
        string requiredPermissionCode,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseRequisitionBudgetControlHistoryDto>> GetHistoryAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementRequisitionBudgetNotFoundException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionBudgetValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionBudgetConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionBudgetAuthorizationException(string message) : UnauthorizedAccessException(message);

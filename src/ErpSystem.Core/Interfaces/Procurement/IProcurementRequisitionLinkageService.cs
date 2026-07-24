using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementRequisitionLinkageService
{
    Task<PurchaseRequisitionLinkageOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task PrepareAsync(
        PurchaseRequisition requisition,
        SavePurchaseRequisitionLinkageRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task RecordMutationAsync(
        PurchaseRequisition requisition,
        string action,
        PurchaseRequisitionLinkageDto? before,
        string correlationId,
        string? reason = null,
        CancellationToken cancellationToken = default);
    Task RecordExportAsync(
        PurchaseRequisition requisition,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseRequisitionLinkageHistoryDto>> GetHistoryAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);
    PurchaseRequisitionLinkageDto Map(PurchaseRequisition requisition);
}

public sealed class ProcurementRequisitionLinkageNotFoundException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionLinkageConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionLinkageValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementRequisitionLinkageAuthorizationException(string message) : UnauthorizedAccessException(message);

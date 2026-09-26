using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementAcceptedSupplyService
{
    Task<IReadOnlyList<ProcurementAcceptedReceiptLineDto>> GetGoodsReceiptLinesAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken = default);

    Task<ProcurementAcceptedSupplyOptionsDto> GetOptionsAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<ProcurementAcceptedSupplyResolutionDto> ResolveAsync(
        ProcurementAcceptedSupplyKind kind,
        Guid sourceId,
        Guid? purchaseOrderId,
        Guid? currentVendorInvoiceId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementAcceptedSupplyValidationException(
    string code,
    string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

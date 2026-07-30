using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementPurchaseOrderAmendmentStore
{
    bool HasRequiredTransaction { get; }

    Task<PurchaseOrder?> GetPurchaseOrderForUpdateAsync(
        Guid tenantId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task SetApprovedSourceMutationContextAsync(
        Guid amendmentId,
        CancellationToken cancellationToken = default);

    Task ClearApprovedSourceMutationContextAsync(
        CancellationToken cancellationToken = default);
}

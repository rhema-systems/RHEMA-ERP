using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementRequisitionSourcingReleaseStore
{
    bool UsesRelationalDatabase { get; }
    bool HasRequiredTransaction { get; }
    Task<PurchaseRequisition?> GetRequisitionForUpdateAsync(Guid tenantId, Guid requisitionId, CancellationToken cancellationToken = default);
}

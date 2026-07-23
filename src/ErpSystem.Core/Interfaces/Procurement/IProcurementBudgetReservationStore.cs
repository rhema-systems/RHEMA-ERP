using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementBudgetReservationStore
{
    bool HasRequiredTransaction { get; }

    Task<ProcurementBudget?> GetBudgetForUpdateAsync(
        Guid tenantId,
        Guid budgetId,
        CancellationToken cancellationToken = default);
}

using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned consumer boundary for post-acceptance supplier returns.
///
/// Procurement/Inventory owns source approval, dispatch, stock movement and valuation evidence.
/// Finance owns account resolution, fiscal-period controls, AP/tax documents, posting, reversal and
/// idempotency. Implementations must never mutate Procurement or Inventory domain entities.
/// </summary>
public interface ISupplierReturnFinanceAdapter
{
    Task<SupplierReturnFinanceOutcomeDto> ConsumeDispatchAsync(
        SupplierReturnDispatchFinanceDto dispatch,
        CancellationToken cancellationToken = default);

    Task<SupplierReturnFinanceOutcomeDto> ConsumeCommercialResolutionAsync(
        SupplierReturnCommercialResolutionFinanceDto resolution,
        CancellationToken cancellationToken = default);
}

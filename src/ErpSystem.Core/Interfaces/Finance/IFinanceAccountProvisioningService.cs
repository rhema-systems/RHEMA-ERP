using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned boundary for module-required GL accounts. It prevents other modules from writing
/// Account or AccountAccountingBook directly and keeps classification policy inside Finance.
/// </summary>
public interface IFinanceAccountProvisioningService
{
    Task<ProvisionedFinanceAccountDto> ProvisionAsync(
        ProvisionFinanceAccountDto request,
        CancellationToken cancellationToken = default);
}

using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    public interface IAccountingBookService
    {
        Task<IReadOnlyList<AccountingBookDto>> GetBooksAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

        Task EnsureTenantDefaultsAsync(CancellationToken cancellationToken = default);

        Task SyncAccountMappingsAsync(Account account, CancellationToken cancellationToken = default);
    }
}

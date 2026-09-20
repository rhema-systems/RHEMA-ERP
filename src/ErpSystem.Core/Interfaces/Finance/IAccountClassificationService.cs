using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IAccountClassificationService
{
    Task<IReadOnlyList<AccountClassificationDto>> GetAsync(Guid? accountingBookId = null, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<AccountClassificationWhereUsedDto> GetWhereUsedAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AccountClassificationDto> CreateAsync(SaveAccountClassificationDto request, CancellationToken cancellationToken = default);
    Task<AccountClassificationDto> UpdateAsync(Guid id, SaveAccountClassificationDto request, CancellationToken cancellationToken = default);
    Task<AccountClassificationDto> RetireAsync(Guid id, RetireAccountClassificationDto request, CancellationToken cancellationToken = default);
}

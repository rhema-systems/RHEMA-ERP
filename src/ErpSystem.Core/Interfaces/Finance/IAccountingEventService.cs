using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IAccountingEventService
{
    Task<AccountingEventDto> CreateAsync(CreateAccountingEventDto request, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> ReleaseAsync(Guid accountingEventId, ReleaseAccountingEventDto request, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> GetAsync(Guid accountingEventId, CancellationToken cancellationToken = default);
    Task<AccountingEventPostingDto> GetBookAsync(Guid accountingEventId, Guid accountingBookId, CancellationToken cancellationToken = default);
}

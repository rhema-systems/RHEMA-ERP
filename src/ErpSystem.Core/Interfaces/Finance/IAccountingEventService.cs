using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IAccountingEventService
{
    Task<AccountingEventDto> CreateAsync(CreateAccountingEventDto request, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> ApproveAsync(Guid accountingEventId, ReleaseAccountingEventDto request, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> RejectAsync(Guid accountingEventId, ReleaseAccountingEventDto request, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> ExecuteApprovedAsync(Guid accountingEventId, ReleaseAccountingEventDto request,
        IFinanceProducerExecutionParticipant? participant = null, CancellationToken cancellationToken = default);
    /// <summary>Existing combined Finance-only C6 path; staged producer requests cannot execute through it.</summary>
    Task<AccountingEventDto> ReleaseAsync(Guid accountingEventId, ReleaseAccountingEventDto request, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> GetAsync(Guid accountingEventId, CancellationToken cancellationToken = default);
    Task<AccountingEventPostingDto> GetBookAsync(Guid accountingEventId, Guid accountingBookId, CancellationToken cancellationToken = default);
}

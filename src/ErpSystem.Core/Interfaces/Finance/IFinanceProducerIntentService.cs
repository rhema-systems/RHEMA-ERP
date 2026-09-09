using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned producer boundary. Producers submit economics and stable source identity only; they never
/// enumerate books, calculate applicability fingerprints, or invoke the posting leaf per representation.
/// </summary>
public interface IFinanceProducerIntentService
{
    Task<AccountingEventDto> PrepareAsync(ProducerAccountingIntentDto intent, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> GetAsync(Guid accountingEventId, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> ApprovePreparedAsync(Guid accountingEventId,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> RejectPreparedAsync(Guid accountingEventId,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> ApproveAsync(Guid accountingEventId, ProducerAccountingIntentDto intent,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> RejectAsync(Guid accountingEventId, ProducerAccountingIntentDto intent,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default);
}

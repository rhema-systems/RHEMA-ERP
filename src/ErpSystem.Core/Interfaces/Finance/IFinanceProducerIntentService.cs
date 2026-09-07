using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned producer boundary. Producers submit economics and stable source identity only; they never
/// enumerate books, calculate applicability fingerprints, or invoke the posting leaf per representation.
/// </summary>
public interface IFinanceProducerIntentService
{
    Task<AccountingEventDto> PrepareAsync(ProducerAccountingIntentDto intent, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> ApproveAsync(Guid accountingEventId, ProducerAccountingIntentDto intent,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> RejectAsync(Guid accountingEventId, ProducerAccountingIntentDto intent,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default);
    Task<AccountingEventDto> ExecuteApprovedAsync(Guid accountingEventId, ProducerAccountingIntentDto intent,
        IFinanceProducerExecutionParticipant participant, CancellationToken cancellationToken = default);
}

/// <summary>
/// A narrowly scoped same-database participant invoked inside Finance's serializable C6 transaction.
/// Implementations must use the ambient DbContext transaction and must not commit independently.
/// </summary>
public interface IFinanceProducerExecutionParticipant
{
    string ParticipantIdentity { get; }
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

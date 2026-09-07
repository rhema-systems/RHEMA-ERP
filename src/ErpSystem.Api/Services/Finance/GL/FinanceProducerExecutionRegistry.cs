using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Finance-reviewed owner adapter registered in the application container. API callers can name a prepared
/// identity but cannot supply executable code, another DbContext, or an external-side-effect participant.
/// </summary>
public interface IFinanceProducerExecutionHandler
{
    string ParticipantIdentity { get; }
    ApplicationDbContext DbContext { get; }
    bool UsesExternalSideEffects { get; }
    bool ManagesTransactions { get; }
    Task ExecuteAsync(FinanceProducerExecutionContext context, CancellationToken cancellationToken);
}

public sealed record FinanceProducerExecutionContext(
    Guid TenantId,
    Guid AccountingEventId,
    string RequestFingerprint,
    string CanonicalIntentSnapshotJson);

public interface IFinanceProducerExecutionRegistry
{
    IFinanceProducerExecutionHandler Resolve(string participantIdentity, ApplicationDbContext db);
}

public sealed class FinanceProducerExecutionRegistry(IEnumerable<IFinanceProducerExecutionHandler> handlers)
    : IFinanceProducerExecutionRegistry
{
    public IFinanceProducerExecutionHandler Resolve(string participantIdentity, ApplicationDbContext db)
    {
        var canonical = participantIdentity.Trim().ToUpperInvariant();
        var matches = handlers.Where(handler => string.Equals(
            handler.ParticipantIdentity.Trim().ToUpperInvariant(), canonical, StringComparison.Ordinal)).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException("ACCOUNTING_EVENT_PARTICIPANT_UNREGISTERED: exactly one Finance-reviewed owner adapter is required.");
        var handler = matches[0];
        if (!ReferenceEquals(handler.DbContext, db))
            throw new InvalidOperationException("ACCOUNTING_EVENT_PARTICIPANT_CONTEXT_CONFLICT: the owner adapter must use Finance's exact scoped DbContext.");
        if (handler.UsesExternalSideEffects)
            throw new InvalidOperationException("ACCOUNTING_EVENT_PARTICIPANT_EXTERNAL_EFFECTS_UNSUPPORTED: staged execution supports same-database writes only.");
        if (handler.ManagesTransactions)
            throw new InvalidOperationException("ACCOUNTING_EVENT_PARTICIPANT_TRANSACTION_CONTROL_FORBIDDEN: Finance exclusively owns commit and rollback.");
        return handler;
    }
}

internal interface ITrustedAccountingEventExecutor
{
    Task<AccountingEventDto> ExecuteApprovedAsync(Guid accountingEventId, ReleaseAccountingEventDto request,
        IFinanceProducerExecutionHandler handler, CancellationToken cancellationToken = default);
}

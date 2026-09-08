using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Closed same-database handoff used only by reviewed owner services in this assembly. The owner stages its
/// mutation first, then supplies deterministic receipt evidence; Finance never receives executable owner code.
/// </summary>
internal interface IFinanceProducerApprovedExecution
{
    Task<AccountingEventDto> ExecuteInAmbientTransactionAsync(Guid accountingEventId,
        ProducerAccountingIntentDto intent, ProducerOwnerEffectReceiptDto receipt,
        CancellationToken cancellationToken = default);

    Task RecordFailureAfterRollbackAsync(Guid accountingEventId, ProducerAccountingIntentDto intent,
        ProducerOwnerEffectReceiptDto receipt, Exception failure, CancellationToken cancellationToken = default);
}

internal interface ITrustedAccountingEventExecutor
{
    Task<AccountingEventDto> PrepareGroupMemberInAmbientTransactionAsync(CreateAccountingEventDto request,
        CancellationToken cancellationToken = default);

    Task<AccountingEventDto> ValidatePreparedGroupMemberAsync(Guid accountingEventId,
        CreateAccountingEventDto request, CancellationToken cancellationToken = default);

    Task<AccountingEventDto> ExecuteApprovedGroupMemberInAmbientTransactionAsync(Guid accountingEventId,
        ReleaseAccountingEventDto request, Guid producerIntentGroupId, Guid approvedCheckerId,
        Guid expectedAmbientTransactionId, CancellationToken cancellationToken = default);

    Task<AccountingEventDto> ExecuteApprovedInAmbientTransactionAsync(Guid accountingEventId,
        ReleaseAccountingEventDto request, ProducerOwnerEffectReceiptDto receipt,
        CancellationToken cancellationToken = default);

    Task RecordApprovedFailureAfterRollbackAsync(Guid accountingEventId, ReleaseAccountingEventDto request,
        ProducerOwnerEffectReceiptDto receipt, Exception failure, CancellationToken cancellationToken = default);
}

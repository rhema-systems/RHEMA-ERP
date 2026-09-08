using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>Internal data-only group execution boundary. Owner code controls the shared transaction.</summary>
internal interface IFinanceProducerIntentGroupApprovedExecution
{
    Task<ProducerIntentGroupDto> ExecuteInAmbientTransactionAsync(Guid groupId,
        ProducerIntentGroupRequestDto request, ProducerOwnerEffectReceiptDto receipt,
        CancellationToken cancellationToken = default);

    Task RecordFailureAfterRollbackAsync(Guid groupId, ProducerIntentGroupRequestDto request,
        ProducerOwnerEffectReceiptDto receipt, Exception failure, CancellationToken cancellationToken = default);
}

internal sealed class ProducerIntentGroupMemberExecutionException(
    int memberOrder, Guid accountingEventId, Exception innerException)
    : InvalidOperationException($"PRODUCER_INTENT_GROUP_MEMBER_FAILED: member {memberOrder} ({accountingEventId:D}) failed: {innerException.Message}", innerException)
{
    public int MemberOrder { get; } = memberOrder;
    public Guid AccountingEventId { get; } = accountingEventId;
}

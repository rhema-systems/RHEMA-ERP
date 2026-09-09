using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinanceProducerIntentGroupService
{
    Task<ProducerIntentGroupDto> PrepareAsync(ProducerIntentGroupRequestDto request, CancellationToken cancellationToken = default);
    Task<ProducerIntentGroupDto> ApprovePreparedAsync(Guid groupId,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default);
    Task<ProducerIntentGroupDto> RejectPreparedAsync(Guid groupId,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default);
    Task<ProducerIntentGroupDto> ApproveAsync(Guid groupId, DecideProducerIntentGroupRequestDto request, CancellationToken cancellationToken = default);
    Task<ProducerIntentGroupDto> RejectAsync(Guid groupId, DecideProducerIntentGroupRequestDto request, CancellationToken cancellationToken = default);
    Task<ProducerIntentGroupDto> GetAsync(Guid groupId, CancellationToken cancellationToken = default);
}

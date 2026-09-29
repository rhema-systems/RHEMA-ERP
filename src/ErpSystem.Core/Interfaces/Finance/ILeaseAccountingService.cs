using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface ILeaseAccountingService
{
    Task<IEnumerable<LeaseContractListDto>> GetLeasesAsync();
    Task<LeaseContractDetailDto?> GetLeaseByIdAsync(Guid id);

    /// <summary>
    /// Preview the amortization schedule without persisting — used for UI preview
    /// </summary>
    Task<List<LeaseScheduleLineDto>> PreviewScheduleAsync(CreateLeaseContractDto dto);

    /// <summary>
    /// Create a lease contract in Draft status with pre-calculated PV and schedule
    /// </summary>
    Task<LeaseContractDetailDto> CreateLeaseAsync(CreateLeaseContractDto dto);

    /// <summary>
    /// Submit the immutable lease-recognition proposal to the shared Finance workflow.
    /// No GL or asset mutation occurs until an independent final approval.
    /// </summary>
    Task<LeaseContractDetailDto> ActivateLeaseAsync(
        Guid leaseId,
        ActivateLeaseDto? dto = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes an independently approved activation inside the shared approval transaction.
    /// This is not a public maker action.
    /// </summary>
    Task<LeaseContractDetailDto> CompleteApprovedActivationAsync(
        Guid leaseId,
        Guid approvedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepare the canonical AP draft for a single period. Normal AP maker/checker approval owns
    /// the only recognition journal and normal AP settlement owns payment.
    /// </summary>
    Task<LeaseContractDetailDto> PreparePeriodPayableAsync(
        Guid leaseId,
        Guid scheduleLineId,
        CancellationToken cancellationToken = default);
}

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
    /// Activate the lease: create the ROU FixedAsset + post recognition GL journal
    /// (DR ROU Asset, CR Lease Liability)
    /// </summary>
    Task<LeaseContractDetailDto> ActivateLeaseAsync(
        Guid leaseId,
        ActivateLeaseDto? dto = null,
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

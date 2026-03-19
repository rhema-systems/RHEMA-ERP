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
    Task<LeaseContractDetailDto> ActivateLeaseAsync(Guid leaseId);

    /// <summary>
    /// Post a single period's journal: DR Interest Expense + DR Lease Liability, CR Cash/Payable
    /// </summary>
    Task<LeaseContractDetailDto> PostPeriodJournalAsync(Guid leaseId, Guid scheduleLineId);
}

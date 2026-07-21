using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Manages training service bonds (binding service-obligation agreements tied to a nomination):
/// creation (auto on approval + manual), acceptance (self / HR-on-behalf), early-exit repayment,
/// waiver and settlement.
/// </summary>
public interface ITrainingServiceBondService
{
    Task<IEnumerable<TrainingServiceBondDto>> GetAllAsync(TrainingBondStatus? status = null, CancellationToken cancellationToken = default);
    Task<TrainingServiceBondDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrainingServiceBondDto?> GetByNominationAsync(Guid nominationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TrainingServiceBondDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TrainingServiceBondDto>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<TrainingServiceBondDto> CreateAsync(CreateTrainingServiceBondDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<TrainingServiceBondDto> UpdateAsync(Guid id, UpdateTrainingServiceBondDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    Task<TrainingServiceBondDto> AcceptAsync(AcceptTrainingServiceBondDto dto, Guid actingEmployeeId, bool onBehalf, Guid userId, CancellationToken cancellationToken = default);
    Task<TrainingServiceBondDto> RecordExitAsync(RecordBondExitDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<TrainingServiceBondDto> WaiveAsync(WaiveTrainingServiceBondDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<TrainingServiceBondDto> SettleAsync(SettleTrainingServiceBondDto dto, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Auto-create a Pending bond for an approved nomination when its program requires one (idempotent).</summary>
    Task EnsureBondForNominationAsync(Guid nominationId, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Cancel a still-pending bond when its nomination is rejected/withdrawn.</summary>
    Task CancelForNominationAsync(Guid nominationId, Guid userId, CancellationToken cancellationToken = default);
}

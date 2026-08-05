using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Read access to the training status-change audit trail (<c>TrainingStatusHistory</c>).
/// </summary>
public interface ITrainingStatusHistoryService
{
    /// <summary>Returns the status-transition timeline for a single entity, oldest change first.</summary>
    Task<IEnumerable<TrainingStatusHistoryDto>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
}

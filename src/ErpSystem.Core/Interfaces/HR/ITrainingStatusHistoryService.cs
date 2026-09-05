using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The training status-change audit trail (<c>TrainingStatusHistory</c>).
/// </summary>
public interface ITrainingStatusHistoryService
{
    /// <summary>Returns the status-transition timeline for a single entity, oldest change first.</summary>
    Task<IEnumerable<TrainingStatusHistoryDto>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages one audit row for a status change.
    /// </summary>
    /// <remarks>
    /// ⚠ This does NOT save. The row is added to the shared DbContext and committed by the caller's
    /// own <c>SaveChangesAsync</c>, which is what keeps the audit row and the change it describes in
    /// a single transaction — an audit trail that can commit while the change it records rolls back
    /// is worse than none. A caller that never saves silently records nothing.
    /// </remarks>
    Task RecordAsync(
        string entityType,
        Guid entityId,
        string? entityReference,
        int? fromStatus,
        string? fromStatusName,
        int toStatus,
        string toStatusName,
        Guid? changedByEmployeeId,
        string? reason,
        CancellationToken cancellationToken = default);
}

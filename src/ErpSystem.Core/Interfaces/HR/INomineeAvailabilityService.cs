using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Checks whether prospective training nominees have conflicting commitments (leave, travel, other
/// training) overlapping a proposed training window, so nominations can be made informed. Each source
/// is queried defensively — a missing/empty module simply contributes no conflicts.
/// </summary>
public interface INomineeAvailabilityService
{
    Task<List<NomineeConflictDto>> CheckAsync(
        IEnumerable<Guid> employeeIds,
        DateTime from,
        DateTime to,
        Guid? excludeScheduleId,
        CancellationToken cancellationToken = default);
}

using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The confirming-authority map: who signs off probation for a given unit and staff category
/// (FR-HR-032's "the head", stated explicitly per decision D-2 because the org data cannot supply it).
/// </summary>
public interface IProbationConfirmingAuthorityService
{
    Task<IEnumerable<ProbationConfirmingAuthorityDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ProbationConfirmingAuthorityDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Who confirms this employee's probation, and which rule decided it.
    /// </summary>
    /// <remarks>
    /// Returns an unresolved result rather than falling back to HR when nothing matches: a silent
    /// fallback would make an unconfigured tenant look configured.
    /// </remarks>
    Task<ResolvedConfirmingAuthorityDto> ResolveForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>The matching rule for an employee, or null. For callers that already hold the employee.</summary>
    Task<ProbationConfirmingAuthority?> ResolveInternalAsync(Guid tenantId, Employee employee, CancellationToken cancellationToken = default);

    Task<ProbationConfirmingAuthorityDto> CreateAsync(CreateProbationConfirmingAuthorityDto dto, CancellationToken cancellationToken = default);

    Task<ProbationConfirmingAuthorityDto> UpdateAsync(Guid id, UpdateProbationConfirmingAuthorityDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The exit register (area 9b) — separations of every type, from resignation to summary dismissal.
/// FRD §A1.10 and §3.A.2.
/// </summary>
/// <remarks>
/// Slice 1 covers the record itself: raise, amend while draft, read, cancel. Approval (FR-HR-092),
/// clearance (FR-HR-091/183), settlement (FR-HR-184/185) and the effect on the employee master
/// record each arrive as their own slice with their own rule, deliberately rather than as a status
/// field a client can set.
/// </remarks>
public interface ISeparationService
{
    Task<PagedResult<EmployeeSeparationListDto>> GetPagedAsync(
        EmployeeSeparationQueryDto query, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every separation on record for one employee, newest first. Usually zero or one.</summary>
    Task<IEnumerable<EmployeeSeparationListDto>> GetForEmployeeAsync(
        Guid employeeId, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto> CreateAsync(
        CreateEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto> UpdateAsync(
        Guid id, UpdateEmployeeSeparationDto dto, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto> CancelAsync(
        Guid id, CancelEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

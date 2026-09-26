using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Salary change requests (round 3, lane S). Raise → submit → the engine approves → the service
/// applies. See <see cref="Entities.HR.EmployeeSalaryChangeRequest"/> for the shape and the two
/// halves of applying.
/// </summary>
public interface IEmployeeSalaryChangeRequestService
{
    Task<IEnumerable<SalaryChangeRequestDto>> GetAllAsync(Guid? employeeId = null, SalaryChangeRequestStatus? status = null, CancellationToken cancellationToken = default);
    Task<SalaryChangeRequestDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <param name="requestedByEmployeeId">The caller's employee id — the record's actor, distinct from the engine's initiator user.</param>
    Task<SalaryChangeRequestDto> CreateAsync(CreateSalaryChangeRequestDto dto, Guid requestedByEmployeeId, CancellationToken cancellationToken = default);
    Task<SalaryChangeRequestDto> UpdateAsync(Guid id, UpdateSalaryChangeRequestDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SalaryChangeRequestDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    /// <param name="actorEmployeeId">The caller's employee id: neither the requester nor the subject may decide their own request, whatever the definition says.</param>
    Task<SalaryChangeRequestDto> ApproveAsync(Guid id, Guid? actorEmployeeId, CancellationToken cancellationToken = default);
    Task<SalaryChangeRequestDto> RejectAsync(Guid id, string? reason, Guid? actorEmployeeId, CancellationToken cancellationToken = default);
    Task<SalaryChangeRequestDto> RecallAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Re-run whatever applying still owes: HR's half if it never went through, payroll's half otherwise.</summary>
    Task<SalaryChangeRequestDto> RetryApplyAsync(Guid id, CancellationToken cancellationToken = default);
}

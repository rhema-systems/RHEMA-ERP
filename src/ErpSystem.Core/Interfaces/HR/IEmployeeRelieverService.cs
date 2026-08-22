using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Manages an employee's pre-defined relievers (configured on the employee profile). These
/// auto-populate the reliever slots on leave plans and requests.
/// </summary>
public interface IEmployeeRelieverService
{
    Task<IEnumerable<EmployeeRelieverDto>> GetForEmployeeAsync(Guid employeeId, bool activeOnly = false);
    Task<EmployeeRelieverDto> CreateAsync(CreateEmployeeRelieverDto dto);
    Task<EmployeeRelieverDto> UpdateAsync(Guid id, UpdateEmployeeRelieverDto dto);
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Whose roster a row sits on — the question the self-or-HR gate has to answer before an update
    /// or a delete, and one only the stored row can answer.
    /// </summary>
    /// <remarks>Added in areas 19-23 slice 7. Throws <c>ArgumentException</c> if there is no such row.</remarks>
    Task<Guid> GetOwnerEmployeeIdAsync(Guid id);
}

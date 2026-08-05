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
}

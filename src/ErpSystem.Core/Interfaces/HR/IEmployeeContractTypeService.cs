using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// CRUD for the tenant's contract-kind vocabulary — the list the contract row and the appointment
/// letter pick from. See <see cref="EmployeeContractTypeDto"/> for why it is not the
/// <c>EmploymentType</c> enum.
/// </summary>
public interface IEmployeeContractTypeService
{
    Task<IEnumerable<EmployeeContractTypeDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<EmployeeContractTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeContractTypeDto> CreateAsync(CreateEmployeeContractTypeDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeContractTypeDto> UpdateAsync(Guid id, UpdateEmployeeContractTypeDto dto, CancellationToken cancellationToken = default);

    /// <summary>Retires a kind. Never deletes — see the implementation.</summary>
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}

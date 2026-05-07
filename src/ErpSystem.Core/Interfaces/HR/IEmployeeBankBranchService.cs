using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Application service for Bank Branch reference data operations.
/// All operations are tenant-scoped.
/// </summary>
public interface IEmployeeBankBranchService
{
    /// <summary>Gets all branches for a bank.</summary>
    Task<IReadOnlyList<EmployeeBankBranchDto>> GetByBankAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default);

    /// <summary>Gets active branches for a bank.</summary>
    Task<IReadOnlyList<EmployeeBankBranchDto>> GetActiveByBankAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default);

    /// <summary>Gets a branch by identifier.</summary>
    Task<EmployeeBankBranchDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a branch by its code within a bank.</summary>
    Task<EmployeeBankBranchDto?> GetByCodeAsync(Guid tenantId, Guid bankId, string code, CancellationToken cancellationToken = default);

    /// <summary>Creates a new branch. Enforces unique Code per bank per tenant (when Code is provided).</summary>
    Task<EmployeeBankBranchDto> CreateAsync(Guid tenantId, CreateEmployeeBankBranchDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing branch.</summary>
    Task<EmployeeBankBranchDto> UpdateAsync(Guid tenantId, UpdateEmployeeBankBranchDto dto, CancellationToken cancellationToken = default);

    /// <summary>Activates a branch (idempotent).</summary>
    Task<EmployeeBankBranchDto> ActivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deactivates a branch (idempotent).</summary>
    Task<EmployeeBankBranchDto> DeactivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a branch. Blocked when the branch is referenced by any employee bank detail.</summary>
    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}

using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Application service for Bank (financial institution) reference data operations.
/// All operations are tenant-scoped.
/// </summary>
public interface IEmployeeBankService
{
    /// <summary>Gets all banks for a tenant.</summary>
    Task<IReadOnlyList<EmployeeBankDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Gets active banks only.</summary>
    Task<IReadOnlyList<EmployeeBankDto>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Gets a bank by identifier.</summary>
    Task<EmployeeBankDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a bank by its short code.</summary>
    Task<EmployeeBankDto?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);

    /// <summary>Gets a bank with its branches.</summary>
    Task<EmployeeBankDto> GetWithBranchesAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets all branches for a given bank.</summary>
    Task<IReadOnlyList<EmployeeBankBranchDto>> GetBranchesAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default);

    /// <summary>Creates a new bank. Enforces unique Code per tenant.</summary>
    Task<EmployeeBankDto> CreateAsync(Guid tenantId, CreateEmployeeBankDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing bank.</summary>
    Task<EmployeeBankDto> UpdateAsync(Guid tenantId, UpdateEmployeeBankDto dto, CancellationToken cancellationToken = default);

    /// <summary>Activates a bank (idempotent).</summary>
    Task<EmployeeBankDto> ActivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deactivates a bank (idempotent).</summary>
    Task<EmployeeBankDto> DeactivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a bank. Blocked when the bank is referenced by any employee bank detail.</summary>
    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}

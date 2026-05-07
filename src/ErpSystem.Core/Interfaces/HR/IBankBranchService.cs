using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Application service for Bank Branch reference data operations.
/// All operations are tenant-scoped.
/// </summary>
public interface IBankBranchService
{
    /// <summary>Gets all branches for a bank.</summary>
    Task<IReadOnlyList<BankBranchDto>> GetByBankAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default);

    /// <summary>Gets active branches for a bank.</summary>
    Task<IReadOnlyList<BankBranchDto>> GetActiveByBankAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default);

    /// <summary>Gets a branch by identifier.</summary>
    Task<BankBranchDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a branch by its code within a bank.</summary>
    Task<BankBranchDto?> GetByCodeAsync(Guid tenantId, Guid bankId, string code, CancellationToken cancellationToken = default);

    /// <summary>Creates a new branch. Enforces unique Code per bank per tenant (when Code is provided).</summary>
    Task<BankBranchDto> CreateAsync(Guid tenantId, CreateBankBranchDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing branch.</summary>
    Task<BankBranchDto> UpdateAsync(Guid tenantId, UpdateBankBranchDto dto, CancellationToken cancellationToken = default);

    /// <summary>Activates a branch (idempotent).</summary>
    Task<BankBranchDto> ActivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deactivates a branch (idempotent).</summary>
    Task<BankBranchDto> DeactivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a branch. Blocked when the branch is referenced by any employee bank detail.</summary>
    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}

using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Application service for Bank (financial institution) reference data operations.
/// All operations are tenant-scoped.
/// </summary>
public interface IBankService
{
    /// <summary>Gets all banks for a tenant.</summary>
    Task<IReadOnlyList<BankDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Gets active banks only.</summary>
    Task<IReadOnlyList<BankDto>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Gets a bank by identifier.</summary>
    Task<BankDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a bank by its short code.</summary>
    Task<BankDto?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);

    /// <summary>Gets a bank with its branches.</summary>
    Task<BankDto> GetWithBranchesAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets all branches for a given bank.</summary>
    Task<IReadOnlyList<BankBranchDto>> GetBranchesAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default);

    /// <summary>Creates a new bank. Enforces unique Code per tenant.</summary>
    Task<BankDto> CreateAsync(Guid tenantId, CreateBankDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing bank.</summary>
    Task<BankDto> UpdateAsync(Guid tenantId, UpdateBankDto dto, CancellationToken cancellationToken = default);

    /// <summary>Activates a bank (idempotent).</summary>
    Task<BankDto> ActivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deactivates a bank (idempotent).</summary>
    Task<BankDto> DeactivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a bank. Blocked when the bank is referenced by any employee bank detail.</summary>
    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}

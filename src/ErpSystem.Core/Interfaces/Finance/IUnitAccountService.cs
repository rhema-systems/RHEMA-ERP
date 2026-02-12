using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing Unit Accounts.
    /// Unit Accounts are hierarchical accounts for tracking non-financial quantities.
    /// </summary>
    public interface IUnitAccountService
    {
        /// <summary>
        /// Retrieves all unit accounts for the current tenant.
        /// </summary>
        Task<IReadOnlyList<UnitAccountDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves unit accounts in hierarchical structure for tree display.
        /// </summary>
        Task<IReadOnlyList<UnitAccountHierarchyDto>> GetHierarchyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves unit accounts filtered by unit type.
        /// </summary>
        Task<IReadOnlyList<UnitAccountDto>> GetByUnitTypeAsync(Guid unitTypeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves posting accounts only (accounts that can receive journal entries).
        /// </summary>
        Task<IReadOnlyList<UnitAccountDto>> GetPostingAccountsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single unit account by ID including balance history.
        /// </summary>
        Task<UnitAccountDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a unit account by account number.
        /// </summary>
        Task<UnitAccountDto?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new unit account.
        /// </summary>
        Task<UnitAccountDto> CreateAsync(CreateUnitAccountDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing unit account.
        /// </summary>
        Task<UnitAccountDto> UpdateAsync(Guid id, UpdateUnitAccountDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates a unit account.
        /// </summary>
        Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deactivates a unit account (prevents new postings).
        /// </summary>
        Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a unit account (soft delete).
        /// Fails if the account has posted transactions.
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Recalculates all balances for a unit account based on posted journal entries.
        /// </summary>
        Task RecalculateBalancesAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current balance for a unit account.
        /// </summary>
        Task<decimal> GetCurrentBalanceAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets period balances for a unit account.
        /// </summary>
        Task<IReadOnlyList<UnitAccountBalanceDto>> GetBalancesAsync(
            Guid id,
            Guid? fiscalYearId = null,
            CancellationToken cancellationToken = default);
    }
}

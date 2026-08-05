// FILE: src/ErpSystem.Core/Services/Finance/IAccountService.cs

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing General Ledger Accounts.
    /// 
    /// RESPONSIBILITIES:
    /// - Encapsulate all business logic for account creation, update, deletion,
    ///   and retrieval (including segmented accounts).
    /// - Enforce multi-tenancy using the current user/tenant context.
    /// - Coordinate with repositories / UnitOfWork to persist changes.
    /// - Provide a clean API surface for Controllers and other modules.
    /// 
    /// NOTES:
    /// - All methods are asynchronous and cancellable.
    /// - TenantId is resolved inside the implementation (e.g. via ICurrentUserService),
    ///   not supplied explicitly in the contract.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Retrieves a single account by its unique identifier.
        /// 
        /// - Respects tenant isolation.
        /// - Returns null if the account does not exist or belongs to another tenant.
        /// </summary>
        /// <param name="id">Account Id (GUID).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single account by its account number.
        /// 
        /// NOTES:
        /// - Account number is unique per tenant.
        /// - Supports both segmented and non-segmented account numbers.
        /// </summary>
        /// <param name="accountNumber">The full account number string.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task<AccountDto?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a list of all accounts for the current tenant.
        /// 
        /// USAGE:
        /// - Chart of Accounts screen.
        /// - Background jobs needing all accounts.
        /// 
        /// NOTE:
        /// - For large tenants, this may be replaced by a paged query in controllers.
        /// </summary>
        /// <param name="accountType">Optional account type filter.</param>
        /// <param name="status">Optional account status filter.</param>
        /// <param name="isMultiCurrency">Optional multi-currency filter.</param>
        /// <param name="coaType">Optional chart-of-accounts structure filter.</param>
        /// <param name="search">Optional search term for account code, number, or name.</param>
        /// <param name="take">Optional maximum number of records to return.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task<IReadOnlyList<AccountDto>> GetAllAsync(
            string? accountType = null,
            string? status = null,
            bool? isMultiCurrency = null,
            string? coaType = null,
            string? search = null,
            int? take = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all accounts filtered by account type (e.g., Asset, Liability, Revenue).
        /// <param name="dto">Account creation payload.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The created <see cref="AccountDto"/> including generated Id and codes.</returns>
        Task<AccountDto> CreateAsync(AccountCreateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing GL Account for the current tenant.
        /// 
        /// BUSINESS RULES:
        /// - Enforce that account exists and belongs to current tenant.
        /// - Restrict certain changes if transactions already exist
        ///   (e.g., disallow major segment changes or type changes).
        /// - Maintain audit information (UpdatedAt, UpdatedBy).
        /// </summary>
        /// <param name="dto">Account update payload, including Id.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The updated <see cref="AccountDto"/>.</returns>
        Task<AccountDto> UpdateAsync(AccountUpdateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes an account (marks as deleted) for the current tenant.
        /// 
        /// NOTES:
        /// - Does NOT physically remove data from the database.
        /// - Should check for dependent transactions and may disallow delete
        ///   if the account has posted entries (or instead mark as Inactive).
        /// </summary>
        /// <param name="id">Account Id.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks an account as active for new postings (if allowed by business rules).
        /// </summary>
        /// <param name="id">Account Id.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks an account as inactive for new postings (but preserves history).
        /// 
        /// USAGE:
        /// - Used when phasing out old accounts while keeping them for reporting.
        /// </summary>
        /// <param name="id">Account Id.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

        // Multi-Currency Management
        Task<CurrencyLinkDto> AddCurrencyLinkAsync(AddCurrencyLinkDto dto);
        Task<CurrencyLinkRemovalResultDto> RemoveCurrencyLinkAsync(RemoveCurrencyLinkDto dto);
        Task<CurrencyLinkDto> InactivateCurrencyLinkAsync(Guid accountId, string currencyCode);
        Task<CurrencyLinkDto> UpdateCurrencyLinkRatePolicyAsync(Guid accountId, string currencyCode, UpdateCurrencyLinkRatePolicyDto dto);
        Task<List<CurrencyLinkDto>> GetAccountCurrencyLinksAsync(Guid accountId, bool includeInactive = false);
    }
}

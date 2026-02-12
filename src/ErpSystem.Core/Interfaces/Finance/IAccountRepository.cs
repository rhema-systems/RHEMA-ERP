using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Repository contract for GL Accounts.
    /// Extends the generic repository with Finance-specific queries.
    /// </summary>
    public interface IAccountRepository : IGenericRepository<Account>
    {
        /// <summary>
        /// Returns true if an account number exists for the specified tenant.
        /// </summary>
        Task<bool> AccountNumberExistsAsync(Guid tenantId, string accountNumber);

        /// <summary>
        /// Returns all accounts of a given type (Asset, Liability, etc.) for a tenant.
        /// </summary>
        Task<IReadOnlyList<Account>> GetByTypeAsync(Guid tenantId, AccountType accountType);
    }
}
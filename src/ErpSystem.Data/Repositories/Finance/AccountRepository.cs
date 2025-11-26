using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Finance
{
    /// <summary>
    /// Concrete repository for GL Accounts.
    /// Uses GenericRepository for core CRUD and adds tenant-aware queries.
    /// </summary>
    public class AccountRepository : GenericRepository<Account>, IAccountRepository
    {
        public AccountRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<bool> AccountNumberExistsAsync(Guid tenantId, string accountNumber)
        {
            // _dbSet comes from GenericRepository<T>
            return await _dbSet.AnyAsync(a =>
                a.TenantId == tenantId &&
                !a.IsDeleted &&
                a.AccountNumber == accountNumber);
        }

        public async Task<IReadOnlyList<Account>> GetByTypeAsync(Guid tenantId, AccountType accountType)
        {
            return await _dbSet
                .Where(a => a.TenantId == tenantId &&
                            !a.IsDeleted &&
                            a.AccountType == accountType)
                .OrderBy(a => a.AccountNumber)
                .ToListAsync();
        }
    }
}

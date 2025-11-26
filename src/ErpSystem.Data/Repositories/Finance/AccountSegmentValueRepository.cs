using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Finance
{
    /// <summary>
    /// Repository for account-specific segment values.
    /// </summary>
    public class AccountSegmentValueRepository
        : GenericRepository<AccountSegmentValue>, IAccountSegmentValueRepository
    {
        public AccountSegmentValueRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<AccountSegmentValue>> GetByAccountAsync(Guid tenantId, Guid accountId)
        {
            return await _dbSet
                .Where(v => v.TenantId == tenantId &&
                            !v.IsDeleted &&
                            v.AccountId == accountId)
                .OrderBy(v => v.SegmentPosition)
                .ToListAsync();
        }
    }
}

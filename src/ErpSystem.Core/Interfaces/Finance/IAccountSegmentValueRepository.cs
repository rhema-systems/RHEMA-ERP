using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Repository for per-account segment values.
    /// </summary>
    public interface IAccountSegmentValueRepository : IGenericRepository<AccountSegmentValue>
    {
        /// <summary>
        /// Gets all segment values for a given account in a tenant.
        /// </summary>
        Task<IReadOnlyList<AccountSegmentValue>> GetByAccountAsync(Guid tenantId, Guid accountId);
    }
}

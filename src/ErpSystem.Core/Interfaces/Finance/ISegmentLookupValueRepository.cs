using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Repository for lookup values (e.g., Departments, Cost Centers) for segments.
    /// </summary>
    public interface ISegmentLookupValueRepository : IGenericRepository<SegmentLookupValue>
    {
        /// <summary>
        /// Gets all lookup values for a specific segment structure in a tenant.
        /// </summary>
        Task<IReadOnlyList<SegmentLookupValue>> GetBySegmentStructureAsync(Guid tenantId, Guid segmentStructureId);
    }
}

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
    /// Repository for segment lookup values (e.g. departments).
    /// </summary>
    public class SegmentLookupValueRepository
        : GenericRepository<SegmentLookupValue>, ISegmentLookupValueRepository
    {
        public SegmentLookupValueRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<SegmentLookupValue>> GetBySegmentStructureAsync(
            Guid tenantId, Guid segmentStructureId)
        {
            return await _dbSet
                .Where(v => v.TenantId == tenantId &&
                            !v.IsDeleted &&
                            v.SegmentStructureId == segmentStructureId)
                .OrderBy(v => v.DisplayOrder)
                .ThenBy(v => v.SegmentValue)
                .ToListAsync();
        }
    }
}

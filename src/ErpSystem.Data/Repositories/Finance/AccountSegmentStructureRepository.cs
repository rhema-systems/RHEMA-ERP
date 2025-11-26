using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Data.Repositories.Finance
{
    /// <summary>
    /// Repository for segment structures (configuration).
    /// </summary>
    public class AccountSegmentStructureRepository
        : GenericRepository<AccountSegmentStructure>, IAccountSegmentStructureRepository
    {
        public AccountSegmentStructureRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

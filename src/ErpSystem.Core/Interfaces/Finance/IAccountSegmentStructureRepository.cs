using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Repository for segment structure configuration (segment definitions).
    /// For now, it just inherits the generic repository.
    /// </summary>
    public interface IAccountSegmentStructureRepository : IGenericRepository<AccountSegmentStructure>
    {
    }
}

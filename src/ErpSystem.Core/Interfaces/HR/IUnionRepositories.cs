using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

public interface IUnionRepository : IGenericRepository<Union>
{
    Task<IEnumerable<Union>> GetAllWithCountsAsync();
    Task<Union?> GetByIdWithAgreementsAsync(Guid id);
    Task<IEnumerable<Union>> GetActiveAsync();
}

public interface ICollectiveBargainingAgreementRepository : IGenericRepository<CollectiveBargainingAgreement>
{
    Task<IEnumerable<CollectiveBargainingAgreement>> GetByUnionIdAsync(Guid unionId);
}

using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <remarks>
/// ⚠ Every method takes the tenant. Areas 19-23 slice 6 found all three reads fetching the whole
/// table across every tenant and letting the service filter the result in memory: correct, but it
/// materialised every other tenant's unions and their entire agreement graphs to throw them away.
/// The predicate belongs in the query.
/// </remarks>
public interface IUnionRepository : IGenericRepository<Union>
{
    Task<IEnumerable<Union>> GetAllWithCountsAsync(Guid tenantId);
    Task<Union?> GetByIdWithAgreementsAsync(Guid id, Guid tenantId);

    /// <remarks>
    /// ⚠ Loads the agreements, which it did not before slice 6. Without them <c>AgreementCount</c>
    /// came back 0 from this endpoint and non-zero from <c>GetAllWithCountsAsync</c> — the same DTO,
    /// the same union, the same field, two answers. Measured on a probe fixture with two agreements:
    /// <c>/unions</c> said 2 and <c>/unions/active</c> said 0.
    /// </remarks>
    Task<IEnumerable<Union>> GetActiveAsync(Guid tenantId);
}

public interface ICollectiveBargainingAgreementRepository : IGenericRepository<CollectiveBargainingAgreement>
{
    Task<IEnumerable<CollectiveBargainingAgreement>> GetByUnionIdAsync(Guid unionId, Guid tenantId);

    /// <summary>One agreement with its union loaded, so the mapped DTO can name it.</summary>
    Task<CollectiveBargainingAgreement?> GetByIdWithUnionAsync(Guid id, Guid tenantId);
}

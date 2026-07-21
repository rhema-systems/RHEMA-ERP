using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

public interface IUnionService
{
    Task<IEnumerable<UnionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<UnionDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<UnionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UnionDto> CreateAsync(CreateUnionDto createDto, CancellationToken cancellationToken = default);
    Task<UnionDto> UpdateAsync(UpdateUnionDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Collective bargaining agreements
    Task<CollectiveBargainingAgreementDto> AddAgreementAsync(CreateCollectiveBargainingAgreementDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<CollectiveBargainingAgreementDto>> GetAgreementsAsync(Guid unionId, CancellationToken cancellationToken = default);
    Task<CollectiveBargainingAgreementDto> UpdateAgreementAsync(UpdateCollectiveBargainingAgreementDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAgreementAsync(Guid agreementId, CancellationToken cancellationToken = default);
}

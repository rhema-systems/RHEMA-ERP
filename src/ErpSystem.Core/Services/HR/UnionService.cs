using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class UnionService : IUnionService
{
    private readonly IUnionRepository _unionRepository;
    private readonly ICollectiveBargainingAgreementRepository _agreementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnionService> _logger;

    public UnionService(
        IUnionRepository unionRepository,
        ICollectiveBargainingAgreementRepository agreementRepository,
        IUnitOfWork unitOfWork,
        ILogger<UnionService> logger)
    {
        _unionRepository = unionRepository;
        _agreementRepository = agreementRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<UnionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unionRepository.GetAllWithCountsAsync();
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<UnionDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unionRepository.GetActiveAsync();
        return entities.ToDtoList();
    }

    public async Task<UnionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _unionRepository.GetByIdWithAgreementsAsync(id);
        if (entity == null) throw new ArgumentException($"Union with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<UnionDto> CreateAsync(CreateUnionDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        await _unionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union created: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<UnionDto> UpdateAsync(UpdateUnionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _unionRepository.GetByIdAsync(updateDto.Id);
        if (entity == null) throw new ArgumentException($"Union with ID '{updateDto.Id}' not found.");
        updateDto.UpdateEntity(entity);
        await _unionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union updated: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _unionRepository.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException($"Union with ID '{id}' not found.");
        await _unionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union deleted: {Id}", id);
        return true;
    }

    public async Task<CollectiveBargainingAgreementDto> AddAgreementAsync(CreateCollectiveBargainingAgreementDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        await _agreementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA added to union: {UnionId}", createDto.UnionId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<CollectiveBargainingAgreementDto>> GetAgreementsAsync(Guid unionId, CancellationToken cancellationToken = default)
    {
        var entities = await _agreementRepository.GetByUnionIdAsync(unionId);
        return entities.ToDtoList();
    }

    public async Task<CollectiveBargainingAgreementDto> UpdateAgreementAsync(UpdateCollectiveBargainingAgreementDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _agreementRepository.GetByIdAsync(updateDto.Id);
        if (entity == null) throw new ArgumentException("Agreement not found");
        updateDto.UpdateEntity(entity);
        await _agreementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAgreementAsync(Guid agreementId, CancellationToken cancellationToken = default)
    {
        var entity = await _agreementRepository.GetByIdAsync(agreementId);
        if (entity == null) throw new ArgumentException("Agreement not found");
        await _agreementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA deleted: {Id}", agreementId);
        return true;
    }
}

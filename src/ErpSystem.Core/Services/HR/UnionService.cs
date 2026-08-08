using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class UnionService : IUnionService
{
    private readonly IUnionRepository _unionRepository;
    private readonly ICollectiveBargainingAgreementRepository _agreementRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnionService> _logger;

    public UnionService(
        IUnionRepository unionRepository,
        ICollectiveBargainingAgreementRepository agreementRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<UnionService> logger)
    {
        _unionRepository = unionRepository;
        _agreementRepository = agreementRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<Union> GetOwnedUnionAsync(Guid id)
    {
        var entity = await _unionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Union with ID '{id}' not found.");
        return entity;
    }

    private async Task<CollectiveBargainingAgreement> GetOwnedAgreementAsync(Guid id)
    {
        var entity = await _agreementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Agreement not found");
        return entity;
    }

    public async Task<IEnumerable<UnionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _unionRepository.GetAllWithCountsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<UnionDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _unionRepository.GetActiveAsync();
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<UnionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unionRepository.GetByIdWithAgreementsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Union with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<UnionDto> CreateAsync(CreateUnionDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        if (!string.IsNullOrWhiteSpace(createDto.Code))
        {
            // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
            var duplicate = await _unionRepository.GetQueryable()
                .AnyAsync(u => u.TenantId == tenantId && u.Code == createDto.Code, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"A union with code '{createDto.Code}' already exists.");
        }

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        await _unionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union created: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<UnionDto> UpdateAsync(UpdateUnionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedUnionAsync(updateDto.Id);

        if (!string.IsNullOrWhiteSpace(updateDto.Code) && updateDto.Code != entity.Code)
        {
            var duplicate = await _unionRepository.GetQueryable()
                .AnyAsync(u => u.TenantId == entity.TenantId && u.Code == updateDto.Code && u.Id != updateDto.Id, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"A union with code '{updateDto.Code}' already exists.");
        }

        updateDto.UpdateEntity(entity);
        await _unionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union updated: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedUnionAsync(id);
        await _unionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union deleted: {Id}", id);
        return true;
    }

    public async Task<CollectiveBargainingAgreementDto> AddAgreementAsync(CreateCollectiveBargainingAgreementDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedUnionAsync(createDto.UnionId);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        await _agreementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA added to union: {UnionId}", createDto.UnionId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<CollectiveBargainingAgreementDto>> GetAgreementsAsync(Guid unionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedUnionAsync(unionId);

        var entities = await _agreementRepository.GetByUnionIdAsync(unionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<CollectiveBargainingAgreementDto> UpdateAgreementAsync(UpdateCollectiveBargainingAgreementDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAgreementAsync(updateDto.Id);
        updateDto.UpdateEntity(entity);
        await _agreementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAgreementAsync(Guid agreementId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAgreementAsync(agreementId);
        await _agreementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA deleted: {Id}", agreementId);
        return true;
    }
}

using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class OrientationCategoryService : IOrientationCategoryService
{
    private readonly IOrientationCategoryRepository _categoryRepository;
    private readonly IOrientationProgramRepository _programRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrientationCategoryService> _logger;

    public OrientationCategoryService(
        IOrientationCategoryRepository categoryRepository,
        IOrientationProgramRepository programRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<OrientationCategoryService> logger)
    {
        _categoryRepository = categoryRepository;
        _programRepository = programRepository;
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

    private async Task<OrientationCategory> GetOwnedCategoryAsync(Guid id)
    {
        var entity = await _categoryRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation category with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationCategory> GetOwnedCategoryWithSubsAsync(Guid id)
    {
        var entity = await _categoryRepository.GetWithSubCategoriesAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation category with ID '{id}' not found.");
        return entity;
    }

    public async Task<OrientationCategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCategoryWithSubsAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationCategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _categoryRepository.GetAllAsync()).Where(c => c.TenantId == tenantId);
        return entities.Select(c => c.ToDto());
    }

    public async Task<IEnumerable<OrientationCategoryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _categoryRepository.GetActiveAsync()).Where(c => c.TenantId == tenantId);
        return entities.Select(c => c.ToDto());
    }

    public async Task<IEnumerable<OrientationCategoryDto>> GetRootCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _categoryRepository.GetRootCategoriesAsync()).Where(c => c.TenantId == tenantId);
        return entities.Select(c => c.ToDto());
    }

    public async Task<IEnumerable<OrientationCategoryDto>> GetByParentAsync(Guid? parentCategoryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _categoryRepository.GetByParentAsync(parentCategoryId)).Where(c => c.TenantId == tenantId);
        return entities.Select(c => c.ToDto());
    }

    public async Task<IEnumerable<OrientationCategoryLookupDto>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _categoryRepository.GetActiveAsync()).Where(c => c.TenantId == tenantId);
        return entities.Select(c => c.ToLookupDto());
    }

    public async Task<OrientationCategoryDto> CreateAsync(CreateOrientationCategoryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _categoryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation category created: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<OrientationCategoryDto> UpdateAsync(UpdateOrientationCategoryDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCategoryAsync(updateDto.Id);

        if (updateDto.ParentCategoryId == updateDto.Id)
            throw new InvalidOperationException("A category cannot be its own parent.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _categoryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation category updated: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCategoryAsync(id);
        var tenantId = GetTenantId();

        var hasPrograms = await _programRepository.ExistsAsync(p => p.TenantId == tenantId && p.CategoryId == id && !p.IsDeleted);
        if (hasPrograms)
            throw new InvalidOperationException("Cannot delete a category that still has programs assigned to it.");

        var children = (await _categoryRepository.GetByParentAsync(id)).Where(c => c.TenantId == tenantId);
        if (children.Any())
            throw new InvalidOperationException("Cannot delete a category that has sub-categories.");

        await _categoryRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation category deleted: {Name}", entity.Name);
        return true;
    }
}

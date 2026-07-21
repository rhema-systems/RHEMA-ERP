using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class OrientationCategoryService : IOrientationCategoryService
{
    private readonly IOrientationCategoryRepository _categoryRepository;
    private readonly IOrientationProgramRepository _programRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrientationCategoryService> _logger;

    public OrientationCategoryService(
        IOrientationCategoryRepository categoryRepository,
        IOrientationProgramRepository programRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrientationCategoryService> logger)
    {
        _categoryRepository = categoryRepository;
        _programRepository = programRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OrientationCategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _categoryRepository.GetWithSubCategoriesAsync(id)
            ?? throw new ArgumentException($"Orientation category with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationCategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _categoryRepository.GetAllAsync();
        return entities.Select(c => c.ToDto());
    }

    public async Task<IEnumerable<OrientationCategoryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _categoryRepository.GetActiveAsync();
        return entities.Select(c => c.ToDto());
    }

    public async Task<IEnumerable<OrientationCategoryDto>> GetRootCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _categoryRepository.GetRootCategoriesAsync();
        return entities.Select(c => c.ToDto());
    }

    public async Task<IEnumerable<OrientationCategoryDto>> GetByParentAsync(Guid? parentCategoryId, CancellationToken cancellationToken = default)
    {
        var entities = await _categoryRepository.GetByParentAsync(parentCategoryId);
        return entities.Select(c => c.ToDto());
    }

    public async Task<IEnumerable<OrientationCategoryLookupDto>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _categoryRepository.GetActiveAsync();
        return entities.Select(c => c.ToLookupDto());
    }

    public async Task<OrientationCategoryDto> CreateAsync(CreateOrientationCategoryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _categoryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation category created: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<OrientationCategoryDto> UpdateAsync(UpdateOrientationCategoryDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _categoryRepository.GetByIdAsync(updateDto.Id)
            ?? throw new ArgumentException($"Orientation category with ID '{updateDto.Id}' not found.");

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
        var entity = await _categoryRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Orientation category with ID '{id}' not found.");

        var hasPrograms = await _programRepository.ExistsAsync(p => p.CategoryId == id && !p.IsDeleted);
        if (hasPrograms)
            throw new InvalidOperationException("Cannot delete a category that still has programs assigned to it.");

        var children = await _categoryRepository.GetByParentAsync(id);
        if (children.Any())
            throw new InvalidOperationException("Cannot delete a category that has sub-categories.");

        await _categoryRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation category deleted: {Name}", entity.Name);
        return true;
    }
}

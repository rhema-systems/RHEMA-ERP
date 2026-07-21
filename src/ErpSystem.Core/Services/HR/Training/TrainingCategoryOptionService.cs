using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingCategoryOptionService : ITrainingCategoryOptionService
{
    private readonly IGenericRepository<TrainingCategoryOption> _optionRepository;
    private readonly IGenericRepository<TrainingProgram> _programRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingCategoryOptionService> _logger;

    public TrainingCategoryOptionService(
        IGenericRepository<TrainingCategoryOption> optionRepository,
        IGenericRepository<TrainingProgram> programRepository,
        IUnitOfWork unitOfWork,
        ILogger<TrainingCategoryOptionService> logger)
    {
        _optionRepository = optionRepository;
        _programRepository = programRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<TrainingCategoryOptionDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = _optionRepository.GetQueryable();
        if (activeOnly)
            query = query.Where(o => o.IsActive);

        var options = await query
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Name)
            .ToListAsync(cancellationToken);

        var counts = await _programRepository.GetQueryable()
            .Where(p => p.CategoryOptionId != null)
            .GroupBy(p => p.CategoryOptionId!.Value)
            .Select(g => new { OptionId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dtos = options.Select(o => o.ToDto()).ToList();
        foreach (var dto in dtos)
            dto.ProgramsCount = counts.FirstOrDefault(c => c.OptionId == dto.Id)?.Count ?? 0;

        return dtos;
    }

    public async Task<TrainingCategoryOptionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _optionRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Training category with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<TrainingCategoryOptionDto> CreateAsync(CreateTrainingCategoryOptionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var duplicate = await _optionRepository.GetQueryable()
            .AnyAsync(o => o.Code == dto.Code, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A training category with code '{dto.Code}' already exists.");

        var entity = dto.ToEntity(tenantId, createdByUserId);
        await _optionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training category created: {Code} - {Name}", entity.Code, entity.Name);
        return entity.ToDto();
    }

    public async Task<TrainingCategoryOptionDto> UpdateAsync(Guid id, UpdateTrainingCategoryOptionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _optionRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Training category with ID '{id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        await _optionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training category updated: {Id}", id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _optionRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Training category with ID '{id}' not found.");

        var inUse = await _programRepository.GetQueryable()
            .AnyAsync(p => p.CategoryOptionId == id, cancellationToken);
        if (inUse)
            throw new InvalidOperationException("This category cannot be deleted because it is assigned to one or more training programs. Deactivate it instead.");

        await _optionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training category deleted: {Id}", id);
        return true;
    }
}

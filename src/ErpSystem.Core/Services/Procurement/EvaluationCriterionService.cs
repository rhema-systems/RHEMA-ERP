using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class EvaluationCriterionService : IEvaluationCriterionService
{
    private readonly IEvaluationCriterionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EvaluationCriterionService> _logger;

    public EvaluationCriterionService(
        IEvaluationCriterionRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<EvaluationCriterionService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<EvaluationCriterionDto?> GetByIdAsync(Guid id)
    {
        var criterion = await _repository.GetByIdAsync(id);
        return criterion == null ? null : MapToDto(criterion);
    }

    public async Task<IEnumerable<EvaluationCriterionDto>> GetAllAsync()
    {
        var criteria = await _repository.GetAllAsync();
        return criteria.Select(MapToDto);
    }

    public async Task<IEnumerable<EvaluationCriterionDto>> GetActiveAsync()
    {
        var criteria = await _repository.GetActiveAsync();
        return criteria.Select(MapToDto);
    }

    public async Task<IEnumerable<EvaluationCriterionDto>> GetByCategoryAsync(string category)
    {
        var criteria = await _repository.GetByCategoryAsync(category);
        return criteria.Select(MapToDto);
    }

    public async Task<EvaluationCriterionDto> CreateAsync(CreateEvaluationCriterionDto dto)
    {
        // Check if code already exists
        var existing = await _repository.GetByCodeAsync(dto.CriterionCode);
        if (existing != null)
        {
            throw new InvalidOperationException($"Evaluation criterion with code '{dto.CriterionCode}' already exists");
        }

        var criterion = new EvaluationCriterion
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUserProvider.TenantId,
            CriterionName = dto.CriterionName,
            CriterionCode = dto.CriterionCode,
            Category = dto.Category,
            EvaluationType = dto.EvaluationType,
            Description = dto.Description,
            MaxScore = dto.MaxScore,
            Weight = dto.Weight,
            IsActive = dto.IsActive,
            DisplayOrder = dto.DisplayOrder,
            CreatedById = _currentUserProvider.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(criterion);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Evaluation criterion created: {CriterionName} ({CriterionCode})", 
            criterion.CriterionName, criterion.CriterionCode);

        return MapToDto(criterion);
    }

    public async Task<EvaluationCriterionDto> UpdateAsync(Guid id, UpdateEvaluationCriterionDto dto)
    {
        var criterion = await _repository.GetByIdAsync(id);
        if (criterion == null)
        {
            throw new InvalidOperationException($"Evaluation criterion with ID '{id}' not found");
        }

        criterion.CriterionName = dto.CriterionName;
        criterion.EvaluationType = dto.EvaluationType;
        criterion.Description = dto.Description;
        criterion.MaxScore = dto.MaxScore;
        criterion.Weight = dto.Weight;
        criterion.IsActive = dto.IsActive;
        criterion.DisplayOrder = dto.DisplayOrder;
        criterion.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(criterion);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Evaluation criterion updated: {CriterionName} ({CriterionCode})", 
            criterion.CriterionName, criterion.CriterionCode);

        return MapToDto(criterion);
    }

    public async Task DeleteAsync(Guid id)
    {
        var criterion = await _repository.GetByIdAsync(id);
        if (criterion == null)
        {
            throw new InvalidOperationException($"Evaluation criterion with ID '{id}' not found");
        }

        await _repository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Evaluation criterion deleted: {CriterionName} ({CriterionCode})", 
            criterion.CriterionName, criterion.CriterionCode);
    }

    private static EvaluationCriterionDto MapToDto(EvaluationCriterion criterion)
    {
        return new EvaluationCriterionDto
        {
            Id = criterion.Id,
            CriterionName = criterion.CriterionName,
            CriterionCode = criterion.CriterionCode,
            Category = criterion.Category,
            EvaluationType = criterion.EvaluationType,
            Description = criterion.Description,
            MaxScore = criterion.MaxScore,
            Weight = criterion.Weight,
            IsActive = criterion.IsActive,
            DisplayOrder = criterion.DisplayOrder,
            CreatedAt = criterion.CreatedAt,
            CreatedByName = criterion.CreatedBy?.FullName
        };
    }
}


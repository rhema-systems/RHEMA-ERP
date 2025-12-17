using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class EvaluationTemplateService : IEvaluationTemplateService
{
    private readonly IEvaluationTemplateRepository _templateRepository;
    private readonly IEvaluationTemplateCriterionRepository _criterionRepository;
    private readonly IEvaluationCriterionRepository _evaluationCriterionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EvaluationTemplateService> _logger;

    public EvaluationTemplateService(
        IEvaluationTemplateRepository templateRepository,
        IEvaluationTemplateCriterionRepository criterionRepository,
        IEvaluationCriterionRepository evaluationCriterionRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<EvaluationTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _criterionRepository = criterionRepository;
        _evaluationCriterionRepository = evaluationCriterionRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<EvaluationTemplateDto?> GetByIdAsync(Guid id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        return template == null ? null : MapToDto(template);
    }

    public async Task<EvaluationTemplateDto?> GetByIdWithCriteriaAsync(Guid id)
    {
        var template = await _templateRepository.GetByIdWithCriteriaAsync(id);
        return template == null ? null : MapToDto(template);
    }

    public async Task<IEnumerable<EvaluationTemplateDto>> GetAllAsync()
    {
        var templates = await _templateRepository.GetAllAsync();
        return templates.Select(MapToDto);
    }

    public async Task<IEnumerable<EvaluationTemplateDto>> GetActiveAsync()
    {
        var templates = await _templateRepository.GetActiveAsync();
        return templates.Select(MapToDto);
    }

    public async Task<IEnumerable<EvaluationTemplateDto>> GetByCategoryAsync(string category)
    {
        var templates = await _templateRepository.GetByCategoryAsync(category);
        return templates.Select(MapToDto);
    }

    public async Task<IEnumerable<EvaluationTemplateDto>> GetByTenderTypeAsync(string tenderType)
    {
        var templates = await _templateRepository.GetByTenderTypeAsync(tenderType);
        return templates.Select(MapToDto);
    }

    public async Task<IEnumerable<EvaluationTemplateListItemDto>> GetActiveForDropdownAsync()
    {
        var templates = await _templateRepository.GetActiveAsync();
        return templates.Select(t => new EvaluationTemplateListItemDto
        {
            Id = t.Id,
            TemplateName = t.TemplateName,
            TemplateCode = t.TemplateCode,
            Category = t.Category,
            TenderType = t.TenderType,
            IsDefault = t.IsDefault,
            CriteriaCount = t.TemplateCriteria?.Count(c => !c.IsDeleted) ?? 0
        });
    }

    public async Task<EvaluationTemplateDto?> GetDefaultAsync(string category, string tenderType)
    {
        var template = await _templateRepository.GetDefaultAsync(category, tenderType);
        return template == null ? null : MapToDto(template);
    }

    public async Task<EvaluationTemplateDto> CreateAsync(CreateEvaluationTemplateDto dto)
    {
        // Check if code already exists
        var existing = await _templateRepository.GetByCodeAsync(dto.TemplateCode);
        if (existing != null)
        {
            throw new InvalidOperationException($"Evaluation template with code '{dto.TemplateCode}' already exists");
        }

        // Validate criteria weights sum to 100 if any criteria provided
        if (dto.Criteria.Any())
        {
            var totalWeight = dto.Criteria.Sum(c => c.Weight);
            if (Math.Abs(totalWeight - 100) > 0.01m)
            {
                throw new InvalidOperationException($"Criteria weights must sum to 100. Current total: {totalWeight}");
            }
        }

        var template = new EvaluationTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUserProvider.TenantId,
            TemplateName = dto.TemplateName,
            TemplateCode = dto.TemplateCode,
            Description = dto.Description,
            Category = dto.Category,
            TenderType = dto.TenderType,
            IsDefault = dto.IsDefault,
            IsActive = dto.IsActive,
            PassingScore = dto.PassingScore,
            ScoringMethod = dto.ScoringMethod,
            DisplayOrder = dto.DisplayOrder,
            CreatedById = _currentUserProvider.UserId,
            CreatedAt = DateTime.UtcNow
        };

        // If setting as default, unset other defaults for same category/tender type
        if (dto.IsDefault)
        {
            await UnsetOtherDefaultsAsync(dto.Category, dto.TenderType, template.Id);
        }

        await _templateRepository.CreateAsync(template);
        await _unitOfWork.SaveChangesAsync();

        // Add criteria
        foreach (var criterionDto in dto.Criteria)
        {
            var criterion = new EvaluationTemplateCriterion
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                EvaluationTemplateId = template.Id,
                EvaluationCriterionId = criterionDto.EvaluationCriterionId,
                Weight = criterionDto.Weight,
                MaxScore = criterionDto.MaxScore,
                IsMandatory = criterionDto.IsMandatory,
                MinimumScore = criterionDto.MinimumScore,
                DisplayOrder = criterionDto.DisplayOrder,
                CreatedAt = DateTime.UtcNow
            };
            await _criterionRepository.CreateAsync(criterion);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Evaluation template created: {TemplateName} ({TemplateCode})",
            template.TemplateName, template.TemplateCode);

        // Reload with criteria
        var result = await _templateRepository.GetByIdWithCriteriaAsync(template.Id);
        return MapToDto(result!);
    }

    public async Task<EvaluationTemplateDto> UpdateAsync(Guid id, UpdateEvaluationTemplateDto dto)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
        {
            throw new InvalidOperationException($"Evaluation template with ID '{id}' not found");
        }

        // Validate criteria weights sum to 100 if any criteria provided
        if (dto.Criteria.Any())
        {
            var totalWeight = dto.Criteria.Sum(c => c.Weight);
            if (Math.Abs(totalWeight - 100) > 0.01m)
            {
                throw new InvalidOperationException($"Criteria weights must sum to 100. Current total: {totalWeight}");
            }
        }

        template.TemplateName = dto.TemplateName;
        template.Description = dto.Description;
        template.Category = dto.Category;
        template.TenderType = dto.TenderType;
        template.IsDefault = dto.IsDefault;
        template.IsActive = dto.IsActive;
        template.PassingScore = dto.PassingScore;
        template.ScoringMethod = dto.ScoringMethod;
        template.DisplayOrder = dto.DisplayOrder;
        template.UpdatedAt = DateTime.UtcNow;

        // If setting as default, unset other defaults for same category/tender type
        if (dto.IsDefault)
        {
            await UnsetOtherDefaultsAsync(dto.Category, dto.TenderType, template.Id);
        }

        await _templateRepository.UpdateAsync(template);

        // Delete existing criteria and add new ones
        await _criterionRepository.DeleteByTemplateIdAsync(id);
        await _unitOfWork.SaveChangesAsync();

        foreach (var criterionDto in dto.Criteria)
        {
            var criterion = new EvaluationTemplateCriterion
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                EvaluationTemplateId = template.Id,
                EvaluationCriterionId = criterionDto.EvaluationCriterionId,
                Weight = criterionDto.Weight,
                MaxScore = criterionDto.MaxScore,
                IsMandatory = criterionDto.IsMandatory,
                MinimumScore = criterionDto.MinimumScore,
                DisplayOrder = criterionDto.DisplayOrder,
                CreatedAt = DateTime.UtcNow
            };
            await _criterionRepository.CreateAsync(criterion);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Evaluation template updated: {TemplateName} ({TemplateCode})",
            template.TemplateName, template.TemplateCode);

        var result = await _templateRepository.GetByIdWithCriteriaAsync(template.Id);
        return MapToDto(result!);
    }

    public async Task DeleteAsync(Guid id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
        {
            throw new InvalidOperationException($"Evaluation template with ID '{id}' not found");
        }

        // Delete criteria first
        await _criterionRepository.DeleteByTemplateIdAsync(id);
        await _templateRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Evaluation template deleted: {TemplateName} ({TemplateCode})",
            template.TemplateName, template.TemplateCode);
    }

    public async Task<bool> ValidateCriteriaWeightsAsync(Guid templateId)
    {
        var criteria = await _criterionRepository.GetByTemplateIdAsync(templateId);
        var totalWeight = criteria.Sum(c => c.Weight);
        return Math.Abs(totalWeight - 100) <= 0.01m;
    }

    private async Task UnsetOtherDefaultsAsync(string category, string tenderType, Guid excludeId)
    {
        var templates = await _templateRepository.GetByCategoryAsync(category);
        foreach (var t in templates.Where(t => t.Id != excludeId && t.TenderType == tenderType && t.IsDefault))
        {
            t.IsDefault = false;
            await _templateRepository.UpdateAsync(t);
        }
    }

    private static EvaluationTemplateDto MapToDto(EvaluationTemplate template)
    {
        var criteria = template.TemplateCriteria?.Where(c => !c.IsDeleted).ToList() ?? new List<EvaluationTemplateCriterion>();

        return new EvaluationTemplateDto
        {
            Id = template.Id,
            TemplateName = template.TemplateName,
            TemplateCode = template.TemplateCode,
            Description = template.Description,
            Category = template.Category,
            TenderType = template.TenderType,
            IsDefault = template.IsDefault,
            IsActive = template.IsActive,
            PassingScore = template.PassingScore,
            ScoringMethod = template.ScoringMethod,
            DisplayOrder = template.DisplayOrder,
            CreatedAt = template.CreatedAt,
            CreatedByName = template.CreatedBy?.FullName,
            CriteriaCount = criteria.Count,
            TotalWeight = criteria.Sum(c => c.Weight),
            Criteria = criteria.OrderBy(c => c.DisplayOrder).Select(c => new EvaluationTemplateCriterionDto
            {
                Id = c.Id,
                EvaluationTemplateId = c.EvaluationTemplateId,
                EvaluationCriterionId = c.EvaluationCriterionId,
                CriterionName = c.EvaluationCriterion?.CriterionName ?? string.Empty,
                CriterionCode = c.EvaluationCriterion?.CriterionCode ?? string.Empty,
                Category = c.EvaluationCriterion?.Category ?? "General",
                CriterionDescription = c.EvaluationCriterion?.Description,
                Weight = c.Weight,
                MaxScore = c.MaxScore,
                IsMandatory = c.IsMandatory,
                MinimumScore = c.MinimumScore,
                DisplayOrder = c.DisplayOrder
            }).ToList()
        };
    }
}

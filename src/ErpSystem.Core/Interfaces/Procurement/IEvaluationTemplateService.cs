using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Evaluation template service interface
/// </summary>
public interface IEvaluationTemplateService
{
    /// <summary>
    /// Get evaluation template by ID
    /// </summary>
    Task<EvaluationTemplateDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get evaluation template by ID with all criteria
    /// </summary>
    Task<EvaluationTemplateDto?> GetByIdWithCriteriaAsync(Guid id);

    /// <summary>
    /// Get all evaluation templates
    /// </summary>
    Task<IEnumerable<EvaluationTemplateDto>> GetAllAsync();

    /// <summary>
    /// Get all active evaluation templates
    /// </summary>
    Task<IEnumerable<EvaluationTemplateDto>> GetActiveAsync();

    /// <summary>
    /// Get evaluation templates by category
    /// </summary>
    Task<IEnumerable<EvaluationTemplateDto>> GetByCategoryAsync(string category);

    /// <summary>
    /// Get evaluation templates by tender type
    /// </summary>
    Task<IEnumerable<EvaluationTemplateDto>> GetByTenderTypeAsync(string tenderType);

    /// <summary>
    /// Get active templates for dropdown (minimal data)
    /// </summary>
    Task<IEnumerable<EvaluationTemplateListItemDto>> GetActiveForDropdownAsync();

    /// <summary>
    /// Get the default template for a category and tender type
    /// </summary>
    Task<EvaluationTemplateDto?> GetDefaultAsync(string category, string tenderType);

    /// <summary>
    /// Create a new evaluation template
    /// </summary>
    Task<EvaluationTemplateDto> CreateAsync(CreateEvaluationTemplateDto dto);

    /// <summary>
    /// Update an existing evaluation template
    /// </summary>
    Task<EvaluationTemplateDto> UpdateAsync(Guid id, UpdateEvaluationTemplateDto dto);

    /// <summary>
    /// Delete an evaluation template
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Validate that all criteria weights sum to 100
    /// </summary>
    Task<bool> ValidateCriteriaWeightsAsync(Guid templateId);
}


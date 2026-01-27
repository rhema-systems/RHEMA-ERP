using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Evaluation criterion service interface
/// </summary>
public interface IEvaluationCriterionService
{
    Task<EvaluationCriterionDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<EvaluationCriterionDto>> GetAllAsync();
    Task<IEnumerable<EvaluationCriterionDto>> GetActiveAsync();
    Task<IEnumerable<EvaluationCriterionDto>> GetByCategoryAsync(string category);
    Task<EvaluationCriterionDto> CreateAsync(CreateEvaluationCriterionDto dto);
    Task<EvaluationCriterionDto> UpdateAsync(Guid id, UpdateEvaluationCriterionDto dto);
    Task DeleteAsync(Guid id);
}


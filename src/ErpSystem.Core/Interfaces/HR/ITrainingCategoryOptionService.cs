using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// CRUD for user-configurable training-program categories (<c>TrainingCategoryOption</c>).
/// </summary>
public interface ITrainingCategoryOptionService
{
    Task<IEnumerable<TrainingCategoryOptionDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<TrainingCategoryOptionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrainingCategoryOptionDto> CreateAsync(CreateTrainingCategoryOptionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<TrainingCategoryOptionDto> UpdateAsync(Guid id, UpdateTrainingCategoryOptionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

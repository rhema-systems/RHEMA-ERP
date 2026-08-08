using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// CRUD for the optional program-grouping / curriculum layer (<c>TrainingProgramGroup</c>).
/// </summary>
public interface ITrainingProgramGroupService
{
    Task<IEnumerable<TrainingProgramGroupDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<TrainingProgramGroupDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrainingProgramGroupDto> CreateAsync(CreateTrainingProgramGroupDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<TrainingProgramGroupDto> UpdateAsync(Guid id, UpdateTrainingProgramGroupDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

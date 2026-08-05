using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// CRUD for tenant-configurable talent pool types (<c>TalentPoolTypeDefinition</c>).
/// </summary>
public interface ITalentPoolTypeDefinitionService
{
    Task<IEnumerable<TalentPoolTypeDefinitionDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<TalentPoolTypeDefinitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TalentPoolTypeDefinitionDto> CreateAsync(CreateTalentPoolTypeDefinitionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<TalentPoolTypeDefinitionDto> UpdateAsync(UpdateTalentPoolTypeDefinitionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

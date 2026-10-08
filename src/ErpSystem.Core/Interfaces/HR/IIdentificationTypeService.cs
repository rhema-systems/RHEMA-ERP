using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

public interface IIdentificationTypeService
{
    Task<IdentificationTypeDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<IdentificationTypeDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<IdentificationTypeDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    /// <summary>Active types for an explicit tenant — the anonymous careers catalogue has no user to take the tenant from (round 3, lane C1).</summary>
    Task<IEnumerable<IdentificationTypeDto>> GetActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<IdentificationTypeDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IdentificationTypeDto> CreateAsync(CreateIdentificationTypeDto createDto, CancellationToken cancellationToken = default);
    Task<IdentificationTypeDto> UpdateAsync(UpdateIdentificationTypeDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IdentificationTypeModuleAvailabilityDto> GetModuleAvailabilityAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IdentificationTypeModuleAvailabilityDto> UpdateModuleAvailabilityAsync(
        Guid id,
        UpdateIdentificationTypeModulesDto updateDto,
        CancellationToken cancellationToken = default);
}

using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>The tenant's disability catalogue (round 3, lane P2; register row E-5) — the relationship-type shape.</summary>
public interface IDisabilityTypeService
{
    Task<IEnumerable<DisabilityTypeDto>> GetAllAsync(bool activeOnly = false, IEnumerable<DisabilityCategory>? categories = null, CancellationToken cancellationToken = default);
    Task<DisabilityTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DisabilityTypeDto> CreateAsync(CreateDisabilityTypeDto dto, CancellationToken cancellationToken = default);
    Task<DisabilityTypeDto> UpdateAsync(Guid id, UpdateDisabilityTypeDto dto, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Refused with a count while any employee or dependant names the row.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>The rule every writer of a <c>DisabilityTypeId</c> applies: null passes; otherwise one of the tenant's LIVE rows.</summary>
    Task EnsureUsableAsync(Guid? disabilityTypeId, bool hasDisability, CancellationToken cancellationToken = default);
}

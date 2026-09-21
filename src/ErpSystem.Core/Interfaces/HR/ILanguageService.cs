using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>The language catalogue (round 3, lane C1). Same shape as the relationship-type lookup.</summary>
public interface ILanguageService
{
    Task<IEnumerable<LanguageDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);

    /// <summary>Active rows for an explicit tenant — the anonymous careers catalogue has no user to take the tenant from.</summary>
    Task<IEnumerable<LanguageDto>> GetActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<LanguageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LanguageDto> CreateAsync(CreateLanguageDto dto, CancellationToken cancellationToken = default);
    Task<LanguageDto> UpdateAsync(Guid id, UpdateLanguageDto dto, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Refused with a count while any candidate language row still names the language; retire instead.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

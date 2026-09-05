using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Manages the current tenant's single <c>CompanyHrPolicySettings</c> record.
/// </summary>
public interface ICompanyHrPolicySettingsService
{
    /// <summary>
    /// Returns the tenant's settings. When no row is persisted yet, returns a
    /// coded-defaults DTO (nothing is created until the first save).
    /// </summary>
    Task<CompanyHrPolicySettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the named tenant's settings, or coded defaults when no row is persisted.
    /// </summary>
    /// <remarks>
    /// ⚠ For callers with no authenticated tenant — the scheduled reminder sweeps, which run per
    /// tenant with no HTTP context. <see cref="GetAsync"/> throws in that case, which is what stopped
    /// the probation sweep every night it ran on the timer.
    /// </remarks>
    Task<CompanyHrPolicySettingsDto> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts the tenant's settings from the supplied command and returns the saved state.
    /// </summary>
    Task<CompanyHrPolicySettingsDto> UpdateAsync(UpdateCompanyHrPolicySettingsDto dto, CancellationToken cancellationToken = default);
}

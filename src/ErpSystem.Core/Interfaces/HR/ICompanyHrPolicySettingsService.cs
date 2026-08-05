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
    /// Upserts the tenant's settings from the supplied command and returns the saved state.
    /// </summary>
    Task<CompanyHrPolicySettingsDto> UpdateAsync(UpdateCompanyHrPolicySettingsDto dto, CancellationToken cancellationToken = default);
}

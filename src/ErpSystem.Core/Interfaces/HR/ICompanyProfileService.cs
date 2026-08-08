using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Manages the current tenant's single <c>CompanyProfile</c> record (legal-employer master data).
/// </summary>
public interface ICompanyProfileService
{
    /// <summary>
    /// Returns the tenant's company profile. When no row is persisted yet, returns a DTO populated
    /// with defaults resolved from the Tenant record and configuration (nothing is created until the
    /// first save).
    /// </summary>
    Task<CompanyProfileDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Upserts the tenant's company profile from the supplied command and returns the saved state.</summary>
    Task<CompanyProfileDto> UpdateAsync(UpdateCompanyProfileDto dto, CancellationToken cancellationToken = default);
}

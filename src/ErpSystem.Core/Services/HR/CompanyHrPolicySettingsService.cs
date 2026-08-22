using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Manages the tenant's single <see cref="CompanyHrPolicySettings"/> record. Read returns
/// coded defaults when nothing is persisted; update performs a get-or-create upsert scoped
/// to the current tenant.
/// </summary>
public class CompanyHrPolicySettingsService : ICompanyHrPolicySettingsService
{
    private readonly IGenericRepository<CompanyHrPolicySettings> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<CompanyHrPolicySettingsService> _logger;

    public CompanyHrPolicySettingsService(
        IGenericRepository<CompanyHrPolicySettings> repository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<CompanyHrPolicySettingsService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    public async Task<CompanyHrPolicySettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetQueryable()
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is not null)
            return entity.ToDto();

        // Nothing persisted yet — surface coded defaults without creating a row.
        return new CompanyHrPolicySettings { TenantId = tenantId }.ToDto();
    }

    public async Task<CompanyHrPolicySettingsDto> UpdateAsync(UpdateCompanyHrPolicySettingsDto dto, CancellationToken cancellationToken = default)
    {
        ValidateRetirementAges(dto);
        ValidateConsistency(dto);

        var tenantId = GetTenantId();
        var entity = await _repository.GetQueryable()
            .Where(s => !s.IsDeleted && s.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new CompanyHrPolicySettings { TenantId = tenantId };
            entity.ApplyUpdate(dto);
            await _repository.AddAsync(entity);
            _logger.LogInformation("Company HR policy settings created for tenant {TenantId}", entity.TenantId);
        }
        else
        {
            entity.ApplyUpdate(dto);
            await _repository.UpdateAsync(entity);
            _logger.LogInformation("Company HR policy settings updated for tenant {TenantId}", entity.TenantId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    private static void ValidateRetirementAges(UpdateCompanyHrPolicySettingsDto dto)
    {
        if (dto.VoluntaryRetirementAge > dto.CompulsoryRetirementAge)
            throw new InvalidOperationException(
                "Voluntary retirement age cannot exceed the compulsory retirement age.");

        if (dto.UseGenderSpecificRetirementAge &&
            dto.MaleRetirementAge is null && dto.FemaleRetirementAge is null)
            throw new InvalidOperationException(
                "Gender-specific retirement is enabled but no male/female retirement age was provided.");
    }

    /// <summary>
    /// The three rules that stop a saved setting meaning something other than what it says.
    /// </summary>
    /// <remarks>
    /// Each of these was reachable before: the value saved cleanly, the screen read it back, and the
    /// engine behaved as though a different value had been set.
    /// </remarks>
    private static void ValidateConsistency(UpdateCompanyHrPolicySettingsDto dto)
    {
        // All four weights at zero saves fine and then silently loses to FitScoreWeights.Default in
        // SuccessionCandidateSearchService — so the screen would show 0/0/0/0 while succession ranked
        // candidates on 35/30/20/15. The weights are relative and need not sum to anything; they
        // just cannot all be nothing.
        if (dto.FitWeightPerformance + dto.FitWeightCompetency
            + dto.FitWeightPotential + dto.FitWeightTenure <= 0)
            throw new InvalidOperationException(
                "At least one succession fit weight must be greater than zero, "
                + "otherwise candidate ranking silently falls back to the built-in weights.");

        // Free text with a length cap and nothing else. The long-service reader drops unparseable
        // entries rather than throwing — correct of it — but that means "ten,fifteen" saves happily
        // and reads back as no milestones at all.
        if (!string.IsNullOrWhiteSpace(dto.LongServiceMilestoneYears))
        {
            var parts = dto.LongServiceMilestoneYears
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0 || parts.Any(p => !int.TryParse(p, out var n) || n is <= 0 or > 100))
                throw new InvalidOperationException(
                    "Long-service milestone years must be a comma-separated list of whole years "
                    + "between 1 and 100, for example \"5,10,15,20,25\".");
        }

        // Stored uppercased and used wherever no explicit currency is set. A two- or four-character
        // value is not an ISO 4217 code, and the column would truncate a longer one.
        var currency = (dto.DefaultCurrencyCode ?? string.Empty).Trim();
        if (currency.Length != 3 || !currency.All(char.IsLetter))
            throw new InvalidOperationException(
                "Default currency code must be a three-letter ISO 4217 code, for example \"GHS\".");
    }
}

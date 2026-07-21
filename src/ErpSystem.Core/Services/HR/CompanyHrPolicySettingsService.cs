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

    public async Task<CompanyHrPolicySettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetQueryable()
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is not null)
            return entity.ToDto();

        // Nothing persisted yet — surface coded defaults without creating a row.
        return new CompanyHrPolicySettings { TenantId = _currentUser.TenantId }.ToDto();
    }

    public async Task<CompanyHrPolicySettingsDto> UpdateAsync(UpdateCompanyHrPolicySettingsDto dto, CancellationToken cancellationToken = default)
    {
        ValidateRetirementAges(dto);

        var entity = await _repository.GetQueryable()
            .Where(s => !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new CompanyHrPolicySettings { TenantId = _currentUser.TenantId };
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
}

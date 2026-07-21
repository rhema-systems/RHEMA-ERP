using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Manages the tenant's single <see cref="CompanyProfile"/> record. Read returns the saved row or
/// Tenant/config-resolved defaults (via <see cref="ICompanyProfileProvider"/>); update performs a
/// get-or-create upsert scoped to the current tenant.
/// </summary>
public class CompanyProfileService : ICompanyProfileService
{
    private readonly IGenericRepository<CompanyProfile> _repository;
    private readonly ICompanyProfileProvider _provider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<CompanyProfileService> _logger;

    public CompanyProfileService(
        IGenericRepository<CompanyProfile> repository,
        ICompanyProfileProvider provider,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<CompanyProfileService> logger)
    {
        _repository = repository;
        _provider = provider;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<CompanyProfileDto> GetAsync(CancellationToken cancellationToken = default)
    {
        // Provider returns the saved row or a Tenant/config-resolved defaults instance.
        var profile = await _provider.GetAsync(cancellationToken);
        return profile.ToDto();
    }

    public async Task<CompanyProfileDto> UpdateAsync(UpdateCompanyProfileDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.LegalName))
            throw new InvalidOperationException("Legal name is required.");

        var entity = await _repository.GetQueryable()
            .Where(p => !p.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new CompanyProfile { TenantId = _currentUser.TenantId };
            entity.ApplyUpdate(dto);
            await _repository.AddAsync(entity);
            _logger.LogInformation("Company profile created for tenant {TenantId}", entity.TenantId);
        }
        else
        {
            entity.ApplyUpdate(dto);
            await _repository.UpdateAsync(entity);
            _logger.LogInformation("Company profile updated for tenant {TenantId}", entity.TenantId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

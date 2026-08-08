using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Entities.HR.StaffTravel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 8: CONFIGURATION SERVICE
// ============================================================================

#region Staff Travel Configuration Service

public class StaffTravelConfigurationService : IStaffTravelConfigurationService
{
    private readonly IStaffTravelCurrencyExchangeRateRepository _rateRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelConfigurationService> _logger;

    public StaffTravelConfigurationService(
        IStaffTravelCurrencyExchangeRateRepository rateRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelConfigurationService> logger)
    {
        _rateRepository = rateRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
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

    private async Task<StaffTravelCurrencyExchangeRate> GetOwnedRateAsync(Guid id)
    {
        var entity = await _rateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Currency exchange rate with ID '{id}' not found.");
        return entity;
    }

    public async Task<StaffTravelCurrencyExchangeRateDto> GetRateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRateAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffTravelCurrencyExchangeRateDto?> GetLatestRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _rateRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId
                     && r.FromCurrency == fromCurrency
                     && r.ToCurrency == toCurrency
                     && !r.IsDeleted)
            .OrderByDescending(r => r.RateDate)
            .FirstOrDefaultAsync(cancellationToken);
        return entity?.ToDto();
    }

    public async Task<StaffTravelCurrencyExchangeRateDto?> GetRateOnDateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _rateRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId
                     && r.FromCurrency == fromCurrency
                     && r.ToCurrency == toCurrency
                     && r.RateDate <= date
                     && !r.IsDeleted)
            .OrderByDescending(r => r.RateDate)
            .FirstOrDefaultAsync(cancellationToken);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffTravelCurrencyExchangeRateDto>> GetRatesByDateAsync(DateOnly rateDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _rateRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.RateDate == rateDate && !r.IsDeleted)
            .OrderBy(r => r.FromCurrency)
            .ThenBy(r => r.ToCurrency)
            .ToListAsync(cancellationToken);
        return entities.Select(r => r.ToDto()).ToList();
    }

    public async Task<StaffTravelCurrencyExchangeRateDto> CreateRateAsync(CreateStaffTravelCurrencyExchangeRateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _rateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelCurrencyExchangeRateDto> UpdateRateAsync(UpdateStaffTravelCurrencyExchangeRateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRateAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _rateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteRateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRateAsync(id);
        await _rateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

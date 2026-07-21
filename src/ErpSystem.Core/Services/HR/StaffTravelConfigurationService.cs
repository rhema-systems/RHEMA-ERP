using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 8: CONFIGURATION SERVICE
// ============================================================================

#region Staff Travel Configuration Service

public class StaffTravelConfigurationService : IStaffTravelConfigurationService
{
    private readonly IStaffTravelCurrencyExchangeRateRepository _rateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelConfigurationService> _logger;

    public StaffTravelConfigurationService(
        IStaffTravelCurrencyExchangeRateRepository rateRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelConfigurationService> logger)
    {
        _rateRepository = rateRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffTravelCurrencyExchangeRateDto> GetRateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _rateRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Currency exchange rate with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffTravelCurrencyExchangeRateDto?> GetLatestRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        var entity = await _rateRepository.GetLatestRateAsync(fromCurrency, toCurrency);
        return entity?.ToDto();
    }

    public async Task<StaffTravelCurrencyExchangeRateDto?> GetRateOnDateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        var entity = await _rateRepository.GetRateOnDateAsync(fromCurrency, toCurrency, date);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffTravelCurrencyExchangeRateDto>> GetRatesByDateAsync(DateOnly rateDate, CancellationToken cancellationToken = default)
        => (await _rateRepository.GetByDateAsync(rateDate)).Select(r => r.ToDto()).ToList();

    public async Task<StaffTravelCurrencyExchangeRateDto> CreateRateAsync(CreateStaffTravelCurrencyExchangeRateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _rateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelCurrencyExchangeRateDto> UpdateRateAsync(UpdateStaffTravelCurrencyExchangeRateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _rateRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Currency exchange rate with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _rateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteRateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _rateRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Currency exchange rate with ID '{id}' not found.");

        await _rateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

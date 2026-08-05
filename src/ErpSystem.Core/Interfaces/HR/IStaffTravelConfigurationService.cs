using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 8: CONFIGURATION SERVICE
// ============================================================================

#region Staff Travel Configuration Service

public interface IStaffTravelConfigurationService
{
    // Currency exchange rates
    Task<StaffTravelCurrencyExchangeRateDto> GetRateByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffTravelCurrencyExchangeRateDto?> GetLatestRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
    Task<StaffTravelCurrencyExchangeRateDto?> GetRateOnDateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelCurrencyExchangeRateDto>> GetRatesByDateAsync(DateOnly rateDate, CancellationToken cancellationToken = default);
    Task<StaffTravelCurrencyExchangeRateDto> CreateRateAsync(CreateStaffTravelCurrencyExchangeRateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelCurrencyExchangeRateDto> UpdateRateAsync(UpdateStaffTravelCurrencyExchangeRateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRateAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

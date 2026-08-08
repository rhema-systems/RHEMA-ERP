using ErpSystem.Core.Entities.HR.StaffTravel;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 8: CONFIGURATION
// ============================================================================

#region Staff Travel Currency Exchange Rate

public interface IStaffTravelCurrencyExchangeRateRepository : IGenericRepository<StaffTravelCurrencyExchangeRate>
{
    /// <summary>Returns the most recent rate for a currency pair (by rate date).</summary>
    Task<StaffTravelCurrencyExchangeRate?> GetLatestRateAsync(string fromCurrency, string toCurrency);

    /// <summary>Returns the rate for a currency pair effective on (or most recently before) the given date.</summary>
    Task<StaffTravelCurrencyExchangeRate?> GetRateOnDateAsync(string fromCurrency, string toCurrency, DateOnly date);

    /// <summary>Returns all rates captured for a given date.</summary>
    Task<IEnumerable<StaffTravelCurrencyExchangeRate>> GetByDateAsync(DateOnly rateDate);
}

#endregion

using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 8: CONFIGURATION
// ============================================================================

#region Staff Travel Currency Exchange Rate Repository

public class StaffTravelCurrencyExchangeRateRepository
    : GenericRepository<StaffTravelCurrencyExchangeRate>, IStaffTravelCurrencyExchangeRateRepository
{
    public StaffTravelCurrencyExchangeRateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelCurrencyExchangeRate?> GetLatestRateAsync(string fromCurrency, string toCurrency)
    {
        return await _dbSet
            .Where(r => r.FromCurrency == fromCurrency && r.ToCurrency == toCurrency && !r.IsDeleted)
            .OrderByDescending(r => r.RateDate)
            .FirstOrDefaultAsync();
    }

    public async Task<StaffTravelCurrencyExchangeRate?> GetRateOnDateAsync(string fromCurrency, string toCurrency, DateOnly date)
    {
        return await _dbSet
            .Where(r => r.FromCurrency == fromCurrency && r.ToCurrency == toCurrency
                     && r.RateDate <= date && !r.IsDeleted)
            .OrderByDescending(r => r.RateDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<StaffTravelCurrencyExchangeRate>> GetByDateAsync(DateOnly rateDate)
    {
        return await _dbSet
            .Where(r => r.RateDate == rateDate && !r.IsDeleted)
            .OrderBy(r => r.FromCurrency)
            .ThenBy(r => r.ToCurrency)
            .ToListAsync();
    }
}

#endregion

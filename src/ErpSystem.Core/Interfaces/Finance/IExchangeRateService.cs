using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing Exchange Rates.
    /// Handles currency conversion rates for multi-currency transactions.
    /// </summary>
    public interface IExchangeRateService
    {
        /// <summary>
        /// Retrieves exchange rates with optional filtering.
        /// </summary>
        /// <param name="baseCurrency">Optional base currency filter</param>
        /// <param name="targetCurrency">Optional target currency filter</param>
        /// <param name="effectiveDate">Optional effective date filter</param>
        /// <param name="rateType">Optional rate type filter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<IReadOnlyList<ExchangeRateDto>> GetExchangeRatesAsync(
            string? baseCurrency = null,
            string? targetCurrency = null,
            DateTime? effectiveDate = null,
            string? rateType = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single exchange rate by ID.
        /// </summary>
        Task<ExchangeRateDto?> GetExchangeRateByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the current/active exchange rate for a currency pair.
        /// </summary>
        /// <param name="targetCurrencyCode">Target currency code</param>
        /// <param name="baseCurrencyCode">Base currency code (defaults to tenant base currency)</param>
        /// <param name="effectiveDate">Date for which to get the rate (defaults to today)</param>
        Task<ExchangeRateDto?> GetCurrentRateAsync(
            string targetCurrencyCode,
            string? baseCurrencyCode = null,
            DateTime? effectiveDate = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new exchange rate.
        /// </summary>
        Task<ExchangeRateDto> CreateExchangeRateAsync(CreateExchangeRateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing exchange rate.
        /// </summary>
        Task<ExchangeRateDto> UpdateExchangeRateAsync(Guid id, UpdateExchangeRateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an exchange rate (soft delete).
        /// Cannot delete if rate has been used in transactions.
        /// </summary>
        Task DeleteExchangeRateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk uploads exchange rates from external source.
        /// </summary>
        Task<IReadOnlyList<ExchangeRateDto>> BulkUploadRatesAsync(
            List<CreateExchangeRateDto> rates,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes exchange rate trends over a period.
        /// </summary>
        Task<IReadOnlyList<TrendAnalysisDto>> GetTrendsAsync(
            string? sourceCurrency,
            string targetCurrency,
            DateTime startDate,
            DateTime endDate,
            string groupBy = "daily",
            int movingAverageWindow = 7,
            CancellationToken cancellationToken = default);
    }
}

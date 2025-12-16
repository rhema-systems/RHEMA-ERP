using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing Currencies.
    /// Handles multi-currency configuration and management.
    /// </summary>
    public interface ICurrencyService
    {
        /// <summary>
        /// Retrieves all currencies for the current tenant.
        /// </summary>
        Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single currency by its ISO currency code.
        /// </summary>
        /// <param name="currencyCode">ISO 4217 three-letter code (e.g., "USD", "GHS")</param>
        Task<CurrencyDto?> GetCurrencyByCodeAsync(string currencyCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new currency.
        /// </summary>
        Task<CurrencyDto> CreateCurrencyAsync(CreateCurrencyDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing currency.
        /// </summary>
        Task<CurrencyDto> UpdateCurrencyAsync(string currencyCode, UpdateCurrencyDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a currency (soft delete).
        /// Cannot delete if currency has transaction history or is base currency.
        /// </summary>
        Task DeleteCurrencyAsync(string currencyCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates or deactivates a currency.
        /// </summary>
        Task<CurrencyDto> ToggleCurrencyStatusAsync(string currencyCode, bool isActive, CancellationToken cancellationToken = default);
    }
}

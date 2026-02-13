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
        // Retrieval
        Task<IReadOnlyList<CurrencyDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CurrencyDto>> GetActiveAsync(CancellationToken cancellationToken = default);
        Task<CurrencyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<CurrencyDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<CurrencyDto?> GetBaseCurrencyAsync(CancellationToken cancellationToken = default);

        // CRUD
        Task<CurrencyDto> CreateAsync(CreateCurrencyDto dto, CancellationToken cancellationToken = default);
        Task<CurrencyDto> UpdateAsync(Guid id, UpdateCurrencyDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        
        // Operations
        Task<bool> SetBaseCurrencyAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> UpdateExchangeRateAsync(Guid id, decimal rate, CancellationToken cancellationToken = default);
        Task<decimal> ConvertAsync(decimal amount, string fromCurrencyCode, string toCurrencyCode, CancellationToken cancellationToken = default);
        Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

        // Legacy/Alias (kept for compatibility or remove if unused)
        Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken = default);
        Task<CurrencyDto?> GetCurrencyByCodeAsync(string currencyCode, CancellationToken cancellationToken = default);
        Task<CurrencyDto> CreateCurrencyAsync(CreateCurrencyDto dto, CancellationToken cancellationToken = default);
        Task<CurrencyDto> UpdateCurrencyAsync(string currencyCode, UpdateCurrencyDto dto, CancellationToken cancellationToken = default);
        Task DeleteCurrencyAsync(string currencyCode, CancellationToken cancellationToken = default);
        Task<CurrencyDto> ToggleCurrencyStatusAsync(string currencyCode, bool isActive, CancellationToken cancellationToken = default);
    }
}

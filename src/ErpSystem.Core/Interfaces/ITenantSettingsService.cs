using ErpSystem.Core.DTOs.Finance;
using System.Threading.Tasks;

namespace ErpSystem.Core.Interfaces
{
    /// <summary>
    /// Service for retrieving tenant-specific configuration settings.
    /// Provides centralized access to tenant preferences like base currency, company name, etc.
    /// </summary>
    public interface ITenantSettingsService
    {
        /// <summary>
        /// Gets the full base-currency reference for the current tenant from the finance multi-currency setup.
        /// </summary>
        Task<BaseCurrencyReferenceDto> GetBaseCurrencyReferenceAsync();

        /// <summary>
        /// Gets the base currency code for the current tenant.
        /// Returns ISO 4217 three-letter code (e.g., "GHS", "USD", "EUR").
        /// </summary>
        Task<string> GetBaseCurrencyAsync();

        /// <summary>
        /// Gets the display name for the base currency (e.g., "Ghana Cedis", "US Dollar").
        /// </summary>
        Task<string> GetBaseCurrencyNameAsync();

        /// <summary>
        /// Gets the currency symbol for display (e.g., "₵", "$", "€").
        /// </summary>
        Task<string> GetCurrencySymbolAsync();

        /// <summary>
        /// Gets the number of decimal places for currency amounts (typically 2).
        /// </summary>
        Task<int> GetCurrencyDecimalPlacesAsync();

        /// <summary>
        /// Gets the company/tenant name for display in reports.
        /// </summary>
        Task<string> GetCompanyNameAsync();
    }
}

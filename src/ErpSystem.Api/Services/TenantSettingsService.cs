using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace ErpSystem.Api.Services
{
    /// <summary>
    /// Service for retrieving tenant-specific configuration settings.
    /// Provides centralized access to tenant preferences with fallback defaults.
    /// </summary>
    public class TenantSettingsService : ITenantSettingsService
    {
        private static readonly BaseCurrencyReferenceDto DefaultBaseCurrency = new()
        {
            CurrencyCode = "GHS",
            CurrencyName = "Ghana Cedi",
            CurrencySymbol = "₵",
            DecimalPlaces = 2
        };

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public TenantSettingsService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<BaseCurrencyReferenceDto> GetBaseCurrencyReferenceAsync()
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
            {
                return CloneBaseCurrency(DefaultBaseCurrency);
            }

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            var currency = await _context.Currencies
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsBaseCurrency && !c.IsDeleted)
                .OrderByDescending(c => c.IsActive)
                .ThenByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .FirstOrDefaultAsync();

            return BuildBaseCurrencyReference(currency, tenant);
        }

        public async Task<string> GetBaseCurrencyAsync()
        {
            return (await GetBaseCurrencyReferenceAsync()).CurrencyCode;
        }

        public async Task<string> GetBaseCurrencyNameAsync()
        {
            return (await GetBaseCurrencyReferenceAsync()).CurrencyName;
        }

        public async Task<string> GetCurrencySymbolAsync()
        {
            return (await GetBaseCurrencyReferenceAsync()).CurrencySymbol;
        }

        public async Task<int> GetCurrencyDecimalPlacesAsync()
        {
            return (await GetBaseCurrencyReferenceAsync()).DecimalPlaces;
        }

        public async Task<string> GetCompanyNameAsync()
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                return "RHEMA ERP";

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            return tenant?.Name ?? "RHEMA ERP";
        }

        private static BaseCurrencyReferenceDto BuildBaseCurrencyReference(Currency? currency, Tenant? tenant)
        {
            var tenantCurrencyCode = NormalizeCurrencyCode(tenant?.BaseCurrency);
            var resolvedCode = NormalizeCurrencyCode(currency?.CurrencyCode)
                ?? tenantCurrencyCode
                ?? DefaultBaseCurrency.CurrencyCode;

            var resolvedName = string.IsNullOrWhiteSpace(currency?.CurrencyName)
                ? (!string.IsNullOrWhiteSpace(tenant?.BaseCurrencyName)
                    ? tenant.BaseCurrencyName!
                    : DefaultBaseCurrency.CurrencyName)
                : currency.CurrencyName;

            var resolvedSymbol = string.IsNullOrWhiteSpace(currency?.CurrencySymbol)
                ? (!string.IsNullOrWhiteSpace(tenant?.CurrencySymbol)
                    ? tenant.CurrencySymbol!
                    : DefaultBaseCurrency.CurrencySymbol)
                : currency.CurrencySymbol!;

            var resolvedDecimalPlaces = currency?.DecimalPlaces >= 0
                ? currency.DecimalPlaces
                : tenant?.CurrencyDecimalPlaces > 0
                    ? tenant.CurrencyDecimalPlaces
                    : DefaultBaseCurrency.DecimalPlaces;

            return new BaseCurrencyReferenceDto
            {
                CurrencyCode = resolvedCode,
                CurrencyName = resolvedName,
                CurrencySymbol = resolvedSymbol,
                DecimalPlaces = resolvedDecimalPlaces
            };
        }

        private static BaseCurrencyReferenceDto CloneBaseCurrency(BaseCurrencyReferenceDto currency)
        {
            return new BaseCurrencyReferenceDto
            {
                CurrencyCode = currency.CurrencyCode,
                CurrencyName = currency.CurrencyName,
                CurrencySymbol = currency.CurrencySymbol,
                DecimalPlaces = currency.DecimalPlaces
            };
        }

        private static string? NormalizeCurrencyCode(string? currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                return null;
            }

            var normalized = currencyCode.Trim().ToUpperInvariant();
            return normalized.Length == 3 ? normalized : null;
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Finance.Settings
{
    public class FinanceSettingsService : IFinanceSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantSettingsService _tenantSettingsService;

        public FinanceSettingsService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            ITenantSettingsService tenantSettingsService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _tenantSettingsService = tenantSettingsService;
        }

        public async Task<FinanceSettingsDto> GetSettingsAsync()
        {
            var tenantId = _currentUserService.TenantId 
                ?? throw new InvalidOperationException("Tenant context is required.");

            var baseCurrency = await _tenantSettingsService.GetBaseCurrencyReferenceAsync();

            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            // Create default settings if none exist
            if (settings == null)
            {
                settings = new FinanceSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CoaType = "Segmented",
                    CoaConfigurationLocked = false,
                    BaseCurrency = baseCurrency.CurrencyCode
                };

                _context.FinanceSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return MapToDto(settings, baseCurrency);
        }

        public async Task<FinanceSettingsDto> UpdateSettingsAsync(UpdateFinanceSettingsDto dto)
        {
            var tenantId = _currentUserService.TenantId 
                ?? throw new InvalidOperationException("Tenant context is required.");

            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (settings == null)
            {
                // Create if doesn't exist
                settings = new FinanceSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId
                };
                _context.FinanceSettings.Add(settings);
            }

            // Check if COA type can be changed
            if (dto.CoaType != null && dto.CoaType != settings.CoaType)
            {
                if (settings.CoaConfigurationLocked)
                {
                    throw new InvalidOperationException(
                        "Cannot change COA type. Segmented structure is enforced.");
                }

                if (dto.CoaType == "Standard")
                {
                    throw new InvalidOperationException(
                        "Standard Chart of Accounts structure is no longer supported. Please use Segmented.");
                }

                // Check if any accounts exist
                var accountsExist = await _context.Accounts.AnyAsync(a => a.TenantId == tenantId);
                if (accountsExist)
                {
                    // Lock the configuration
                    settings.CoaConfigurationLocked = true;
                    throw new InvalidOperationException(
                        "Cannot change COA type after accounts have been created.");
                }

                settings.CoaType = dto.CoaType;
            }

            // Update other settings
            if (dto.BaseCurrency != null)
            {
                var requestedBaseCurrency = dto.BaseCurrency.Trim().ToUpperInvariant();
                var targetCurrency = await _context.Currencies
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CurrencyCode == requestedBaseCurrency && !c.IsDeleted);

                if (targetCurrency == null)
                {
                    throw new InvalidOperationException($"Currency '{requestedBaseCurrency}' was not found in the finance multi-currency setup.");
                }

                var existingBaseCurrencies = await _context.Currencies
                    .Where(c => c.TenantId == tenantId && c.IsBaseCurrency && c.Id != targetCurrency.Id && !c.IsDeleted)
                    .ToListAsync();

                foreach (var existingBaseCurrency in existingBaseCurrencies)
                {
                    existingBaseCurrency.IsBaseCurrency = false;
                }

                targetCurrency.IsBaseCurrency = true;
                targetCurrency.IsActive = true;
                settings.BaseCurrency = targetCurrency.CurrencyCode;

                await SyncTenantCurrencySettingsAsync(tenantId, targetCurrency);
            }

            if (dto.AccountSeparator != null)
                settings.AccountSeparator = dto.AccountSeparator;

            if (dto.RetainedEarningsAccountId.HasValue)
                settings.RetainedEarningsAccountId = dto.RetainedEarningsAccountId;

            if (dto.UnrealizedGainLossAccountId.HasValue)
                settings.UnrealizedGainLossAccountId = dto.UnrealizedGainLossAccountId;

            if (dto.RealizedGainLossAccountId.HasValue)
                settings.RealizedGainLossAccountId = dto.RealizedGainLossAccountId;

            if (dto.SuspenseAccountId.HasValue)
                settings.SuspenseAccountId = dto.SuspenseAccountId;

            if (dto.ControlAccountArId.HasValue) settings.ControlAccountArId = dto.ControlAccountArId;
            if (dto.ControlAccountApId.HasValue) settings.ControlAccountApId = dto.ControlAccountApId;
            if (dto.ControlAccountInventoryId.HasValue) settings.ControlAccountInventoryId = dto.ControlAccountInventoryId;
            if (dto.ControlAccountPayrollId.HasValue) settings.ControlAccountPayrollId = dto.ControlAccountPayrollId;
            if (dto.ControlAccountTaxId.HasValue) settings.ControlAccountTaxId = dto.ControlAccountTaxId;
            if (dto.ControlAccountGRVAccrualId.HasValue) settings.ControlAccountGRVAccrualId = dto.ControlAccountGRVAccrualId;
            if (dto.DiscountAllowedAccountId.HasValue) settings.DiscountAllowedAccountId = dto.DiscountAllowedAccountId;
            if (dto.DiscountReceivedAccountId.HasValue) settings.DiscountReceivedAccountId = dto.DiscountReceivedAccountId;
            if (dto.MigrationClearingAccountId.HasValue) settings.MigrationClearingAccountId = dto.MigrationClearingAccountId;
            if (dto.OpeningBalanceAutoRoutingEnabled.HasValue) settings.OpeningBalanceAutoRoutingEnabled = dto.OpeningBalanceAutoRoutingEnabled.Value;

            await _context.SaveChangesAsync();

            var baseCurrency = await _tenantSettingsService.GetBaseCurrencyReferenceAsync();
            return MapToDto(settings, baseCurrency);
        }

        public async Task<bool> CanChangeCOATypeAsync()
        {
            var tenantId = _currentUserService.TenantId 
                ?? throw new InvalidOperationException("Tenant context is required.");

            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (settings?.CoaConfigurationLocked == true)
                return false;

            var accountsExist = await _context.Accounts.AnyAsync(a => a.TenantId == tenantId);
            return !accountsExist;
        }

        private async Task SyncTenantCurrencySettingsAsync(Guid tenantId, Currency currency)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
            {
                return;
            }

            tenant.BaseCurrency = currency.CurrencyCode;
            tenant.BaseCurrencyName = currency.CurrencyName;
            tenant.CurrencySymbol = currency.CurrencySymbol;
            tenant.CurrencyDecimalPlaces = currency.DecimalPlaces;
        }

        private static FinanceSettingsDto MapToDto(FinanceSettings settings, BaseCurrencyReferenceDto baseCurrency)
        {
            return new FinanceSettingsDto
            {
                Id = settings.Id,
                TenantId = settings.TenantId,
                CoaType = settings.CoaType,
                CoaConfigurationLocked = settings.CoaConfigurationLocked,
                BaseCurrency = baseCurrency.CurrencyCode,
                BaseCurrencyName = baseCurrency.CurrencyName,
                BaseCurrencySymbol = baseCurrency.CurrencySymbol,
                BaseCurrencyDecimalPlaces = baseCurrency.DecimalPlaces,
                AccountSeparator = settings.AccountSeparator,
                RetainedEarningsAccountId = settings.RetainedEarningsAccountId,
                UnrealizedGainLossAccountId = settings.UnrealizedGainLossAccountId,
                RealizedGainLossAccountId = settings.RealizedGainLossAccountId,
                SuspenseAccountId = settings.SuspenseAccountId,
                ControlAccountArId = settings.ControlAccountArId,
                ControlAccountApId = settings.ControlAccountApId,
                ControlAccountInventoryId = settings.ControlAccountInventoryId,
                ControlAccountPayrollId = settings.ControlAccountPayrollId,
                ControlAccountTaxId = settings.ControlAccountTaxId,
                ControlAccountGRVAccrualId = settings.ControlAccountGRVAccrualId,
                DiscountAllowedAccountId = settings.DiscountAllowedAccountId,
                DiscountReceivedAccountId = settings.DiscountReceivedAccountId,
                MigrationClearingAccountId = settings.MigrationClearingAccountId,
                OpeningBalanceAutoRoutingEnabled = settings.OpeningBalanceAutoRoutingEnabled
            };
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;

namespace ErpSystem.Api.Services.Finance.Settings
{
    public class FinanceSettingsService : IFinanceSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly IFinanceAuditService? _financeAuditService;

        public FinanceSettingsService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            ITenantSettingsService tenantSettingsService,
            IFinanceAuditService? financeAuditService = null)
        {
            _context = context;
            _currentUserService = currentUserService;
            _tenantSettingsService = tenantSettingsService;
            _financeAuditService = financeAuditService;
        }

        public async Task<FinanceSettingsDto> GetSettingsAsync()
        {
            var tenantId = _currentUserService.GetRequiredFinanceTenantId();

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

            var transactionsExist = await HasAccountingActivityAsync(tenantId);
            return MapToDto(settings, baseCurrency, transactionsExist);
        }

        public async Task<FinanceSettingsDto> UpdateSettingsAsync(UpdateFinanceSettingsDto dto)
        {
            var tenantId = _currentUserService.GetRequiredFinanceTenantId();

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

            var beforeFxMappings = new
            {
                settings.UnrealizedFxGainAccountId,
                settings.UnrealizedFxLossAccountId,
                settings.RealizedFxGainAccountId,
                settings.RealizedFxLossAccountId
            };

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
                var existingBaseCurrency = NormalizeCurrencyCode(settings.BaseCurrency);
                var hasAccountingActivity = await HasAccountingActivityAsync(tenantId);
                if (!string.Equals(existingBaseCurrency, requestedBaseCurrency, StringComparison.OrdinalIgnoreCase)
                    && hasAccountingActivity)
                {
                    settings.FunctionalCurrencyLocked = true;
                    settings.FunctionalCurrencyLockedAt ??= DateTime.UtcNow;
                    settings.FunctionalCurrencyLockedReason ??= "Functional currency locked because accounting activity exists.";
                    await _context.SaveChangesAsync();

                    await RecordFinanceSettingsAuditAsync(
                        FinanceAuditEvents.FunctionalCurrencyChangeRejected,
                        tenantId,
                        settings,
                        beforeValues: new
                        {
                            settings.BaseCurrency,
                            settings.FunctionalCurrencyLocked,
                            settings.FunctionalCurrencyLockedAt
                        },
                        afterValues: new
                        {
                            RequestedBaseCurrency = requestedBaseCurrency,
                            AccountingActivityExists = true
                        },
                        reason: "Functional currency cannot be changed after accounting activity exists.");

                    throw new InvalidOperationException(
                        "Cannot change functional currency after accounting activity exists. Use a controlled functional-currency migration process.");
                }

                var targetCurrency = await _context.Currencies
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CurrencyCode == requestedBaseCurrency && !c.IsDeleted);

                if (targetCurrency == null)
                {
                    throw new InvalidOperationException($"Currency '{requestedBaseCurrency}' was not found in the finance multi-currency setup.");
                }

                var existingBaseCurrencies = await _context.Currencies
                    .Where(c => c.TenantId == tenantId && c.IsBaseCurrency && c.Id != targetCurrency.Id && !c.IsDeleted)
                    .ToListAsync();

                foreach (var baseCurrencyToClear in existingBaseCurrencies)
                {
                    baseCurrencyToClear.IsBaseCurrency = false;
                }

                targetCurrency.IsBaseCurrency = true;
                targetCurrency.IsActive = true;
                settings.BaseCurrency = targetCurrency.CurrencyCode;
                settings.FunctionalCurrencyLocked = hasAccountingActivity;
                if (hasAccountingActivity)
                {
                    settings.FunctionalCurrencyLockedAt ??= DateTime.UtcNow;
                    settings.FunctionalCurrencyLockedReason ??= "Functional currency locked because accounting activity exists.";
                }

                await SyncTenantCurrencySettingsAsync(tenantId, targetCurrency);

                await RecordFinanceSettingsAuditAsync(
                    FinanceAuditEvents.FunctionalCurrencyConfigured,
                    tenantId,
                    settings,
                    beforeValues: new
                    {
                        BaseCurrency = existingBaseCurrency
                    },
                    afterValues: new
                    {
                        BaseCurrency = targetCurrency.CurrencyCode,
                        targetCurrency.CurrencyName,
                        targetCurrency.CurrencySymbol,
                        settings.FunctionalCurrencyLocked
                    });
            }

            if (dto.AccountSeparator != null)
                settings.AccountSeparator = dto.AccountSeparator;

            if (dto.RetainedEarningsAccountId.HasValue)
                settings.RetainedEarningsAccountId = dto.RetainedEarningsAccountId;

            if (dto.UnrealizedGainLossAccountId.HasValue)
                settings.UnrealizedGainLossAccountId = dto.UnrealizedGainLossAccountId;

            if (dto.UnrealizedFxGainAccountId.HasValue)
                settings.UnrealizedFxGainAccountId = dto.UnrealizedFxGainAccountId;

            if (dto.UnrealizedFxLossAccountId.HasValue)
                settings.UnrealizedFxLossAccountId = dto.UnrealizedFxLossAccountId;

            if (dto.RealizedGainLossAccountId.HasValue)
                settings.RealizedGainLossAccountId = dto.RealizedGainLossAccountId;

            if (dto.RealizedFxGainAccountId.HasValue)
                settings.RealizedFxGainAccountId = dto.RealizedFxGainAccountId;

            if (dto.RealizedFxLossAccountId.HasValue)
                settings.RealizedFxLossAccountId = dto.RealizedFxLossAccountId;

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

            var afterFxMappings = new
            {
                settings.UnrealizedFxGainAccountId,
                settings.UnrealizedFxLossAccountId,
                settings.RealizedFxGainAccountId,
                settings.RealizedFxLossAccountId
            };

            if (!Equals(beforeFxMappings.UnrealizedFxGainAccountId, afterFxMappings.UnrealizedFxGainAccountId)
                || !Equals(beforeFxMappings.UnrealizedFxLossAccountId, afterFxMappings.UnrealizedFxLossAccountId)
                || !Equals(beforeFxMappings.RealizedFxGainAccountId, afterFxMappings.RealizedFxGainAccountId)
                || !Equals(beforeFxMappings.RealizedFxLossAccountId, afterFxMappings.RealizedFxLossAccountId))
            {
                await RecordFinanceSettingsAuditAsync(
                    FinanceAuditEvents.FxAccountMappingChanged,
                    tenantId,
                    settings,
                    beforeValues: beforeFxMappings,
                    afterValues: afterFxMappings);
            }

            var baseCurrency = await _tenantSettingsService.GetBaseCurrencyReferenceAsync();
            return MapToDto(settings, baseCurrency, await HasAccountingActivityAsync(tenantId));
        }

        public async Task<bool> CanChangeCOATypeAsync()
        {
            var tenantId = _currentUserService.GetRequiredFinanceTenantId();

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

        private async Task<bool> HasAccountingActivityAsync(Guid tenantId)
        {
            return await _context.FinancePostingEvents.AnyAsync(e => e.TenantId == tenantId && !e.IsDeleted)
                || await _context.JournalEntries.AnyAsync(j => j.TenantId == tenantId && !j.IsDeleted)
                || await _context.AccountTransactions.AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted);
        }

        private async Task RecordFinanceSettingsAuditAsync(
            string eventType,
            Guid tenantId,
            FinanceSettings settings,
            object? beforeValues = null,
            object? afterValues = null,
            string? reason = null)
        {
            if (_financeAuditService == null)
            {
                return;
            }

            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = tenantId,
                SourceModule = "FX",
                SourceDocumentType = "FinanceSettings",
                SourceDocumentId = settings.Id,
                BeforeValues = beforeValues,
                AfterValues = afterValues,
                Reason = reason,
                Resource = "Finance.Settings",
                ResourceId = settings.Id.ToString()
            });
        }

        private static string NormalizeCurrencyCode(string? currencyCode)
        {
            return string.IsNullOrWhiteSpace(currencyCode)
                ? string.Empty
                : currencyCode.Trim().ToUpperInvariant();
        }

        private static FinanceSettingsDto MapToDto(FinanceSettings settings, BaseCurrencyReferenceDto baseCurrency, bool transactionsExist)
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
                FunctionalCurrencyLocked = settings.FunctionalCurrencyLocked || transactionsExist,
                FunctionalCurrencyLockedAt = settings.FunctionalCurrencyLockedAt,
                FunctionalCurrencyLockedReason = settings.FunctionalCurrencyLockedReason,
                AccountSeparator = settings.AccountSeparator,
                RetainedEarningsAccountId = settings.RetainedEarningsAccountId,
                UnrealizedGainLossAccountId = settings.UnrealizedGainLossAccountId,
                UnrealizedFxGainAccountId = settings.UnrealizedFxGainAccountId,
                UnrealizedFxLossAccountId = settings.UnrealizedFxLossAccountId,
                RealizedGainLossAccountId = settings.RealizedGainLossAccountId,
                RealizedFxGainAccountId = settings.RealizedFxGainAccountId,
                RealizedFxLossAccountId = settings.RealizedFxLossAccountId,
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
                OpeningBalanceAutoRoutingEnabled = settings.OpeningBalanceAutoRoutingEnabled,
                TransactionsExist = transactionsExist
            };
        }
    }
}

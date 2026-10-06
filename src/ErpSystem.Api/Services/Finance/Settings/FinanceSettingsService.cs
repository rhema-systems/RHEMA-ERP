using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using ErpSystem.Core.Finance;

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

            // Validate both requested write-off mappings before mutating any settings. Omitted
            // values preserve the existing mapping, as with the other partial updates.
            if (dto.CustomerAdvanceAccountId.HasValue)
                await ValidateReturnAccountAsync(tenantId, dto.CustomerAdvanceAccountId.Value, AccountType.Liability, "Customer advance account");
            if (dto.WriteOffExpenseAccountId.HasValue)
                await ValidateWriteOffAccountAsync(tenantId, dto.WriteOffExpenseAccountId.Value,
                    AccountType.Expense, "Write-off Expense Account");
            if (dto.WriteOffRecoveryAccountId.HasValue)
                await ValidateWriteOffAccountAsync(tenantId, dto.WriteOffRecoveryAccountId.Value,
                    AccountType.Revenue, "Write-off Recovery Account");
            if (dto.ReturnToVendorClearingAccountId.HasValue)
                await ValidateReturnAccountAsync(tenantId, dto.ReturnToVendorClearingAccountId.Value, AccountType.Asset, "Return-to-vendor clearing account");
            if (dto.PurchaseReturnVarianceAccountId.HasValue)
                await ValidateReturnAccountAsync(tenantId, dto.PurchaseReturnVarianceAccountId.Value, AccountType.Expense, "Purchase-return cost variance account");
            if (dto.InvoiceRoundingGainAccountId.HasValue)
                await ValidateWriteOffAccountAsync(tenantId, dto.InvoiceRoundingGainAccountId.Value, AccountType.Revenue, "Invoice rounding gain account");
            if (dto.InvoiceRoundingLossAccountId.HasValue)
                await ValidateWriteOffAccountAsync(tenantId, dto.InvoiceRoundingLossAccountId.Value, AccountType.Expense, "Invoice rounding loss account");

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

            var effectiveInvoiceRoundingEnabled = dto.InvoiceRoundingEnabled ?? settings.InvoiceRoundingEnabled;
            if (effectiveInvoiceRoundingEnabled)
            {
                var gainAccountId = dto.InvoiceRoundingGainAccountId ?? settings.InvoiceRoundingGainAccountId
                    ?? throw new InvalidOperationException("Invoice rounding gain account is required before activation.");
                var lossAccountId = dto.InvoiceRoundingLossAccountId ?? settings.InvoiceRoundingLossAccountId
                    ?? throw new InvalidOperationException("Invoice rounding loss account is required before activation.");
                await ValidateWriteOffAccountAsync(
                    tenantId, gainAccountId, AccountType.Revenue, "Invoice rounding gain account");
                await ValidateWriteOffAccountAsync(
                    tenantId, lossAccountId, AccountType.Expense, "Invoice rounding loss account");
            }

            var accountingActivityExists = await HasAccountingActivityAsync(tenantId);
            if (accountingActivityExists && HasPrecisionAccountingChange(dto, settings))
            {
                throw new InvalidOperationException(
                    "Accounting precision and rounding policy cannot be changed after posted usage. Create a governed effective-dated policy transition; report display decimals may still be changed.");
            }

            var beforeFxMappings = new
            {
                settings.UnrealizedFxGainAccountId,
                settings.UnrealizedFxLossAccountId,
                settings.RealizedFxGainAccountId,
                settings.RealizedFxLossAccountId
            };
            var beforeWriteOffMappings = new
            {
                settings.WriteOffExpenseAccountId,
                settings.WriteOffRecoveryAccountId
            };
            var beforeFxPolicy = new
            {
                settings.DirectionalExchangeRatePolicyEnabled,
                settings.DefaultTransactionQuoteSide,
                settings.ArInvoiceQuoteSide,
                settings.ArSettlementQuoteSide,
                settings.ApInvoiceQuoteSide,
                settings.ApSettlementQuoteSide,
                settings.ClosingQuoteSide,
                settings.RequireExchangeRateOverrideApproval
            };
            var beforeControlPolicy = new
            {
                settings.SupplierAdvanceAccountId,
                settings.CustomerAdvanceAccountId,
                settings.ReversalDatePolicy,
                settings.MinimumReversalReasonLength,
                settings.EnforceFinanceAccessScopes,
                settings.RequireDepreciationBeforePeriodClose
            };
            var beforePrecisionPolicy = PrecisionPolicyAuditValues(settings);

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
            if (dto.SupplierAdvanceAccountId.HasValue) settings.SupplierAdvanceAccountId = dto.SupplierAdvanceAccountId;
            if (dto.CustomerAdvanceAccountId.HasValue) settings.CustomerAdvanceAccountId = dto.CustomerAdvanceAccountId;
            if (dto.ControlAccountInventoryId.HasValue) settings.ControlAccountInventoryId = dto.ControlAccountInventoryId;
            var rtvClearing = dto.ReturnToVendorClearingAccountId ?? settings.ReturnToVendorClearingAccountId;
            var rtvVariance = dto.PurchaseReturnVarianceAccountId ?? settings.PurchaseReturnVarianceAccountId;
            if (rtvClearing.HasValue && (rtvClearing == settings.ControlAccountInventoryId || rtvClearing == rtvVariance) ||
                rtvVariance.HasValue && rtvVariance == settings.ControlAccountInventoryId)
                throw new InvalidOperationException("Inventory, return clearing and purchase-return cost variance must use distinct GL accounts.");
            if (dto.ReturnToVendorClearingAccountId.HasValue) settings.ReturnToVendorClearingAccountId = dto.ReturnToVendorClearingAccountId;
            if (dto.PurchaseReturnVarianceAccountId.HasValue) settings.PurchaseReturnVarianceAccountId = dto.PurchaseReturnVarianceAccountId;
            if (dto.ControlAccountPayrollId.HasValue) settings.ControlAccountPayrollId = dto.ControlAccountPayrollId;
            if (dto.ControlAccountTaxId.HasValue) settings.ControlAccountTaxId = dto.ControlAccountTaxId;
            if (dto.ControlAccountGRVAccrualId.HasValue) settings.ControlAccountGRVAccrualId = dto.ControlAccountGRVAccrualId;
            if (dto.DiscountAllowedAccountId.HasValue) settings.DiscountAllowedAccountId = dto.DiscountAllowedAccountId;
            if (dto.DiscountReceivedAccountId.HasValue) settings.DiscountReceivedAccountId = dto.DiscountReceivedAccountId;
            if (dto.MigrationClearingAccountId.HasValue) settings.MigrationClearingAccountId = dto.MigrationClearingAccountId;
            if (dto.WriteOffExpenseAccountId.HasValue) settings.WriteOffExpenseAccountId = dto.WriteOffExpenseAccountId;
            if (dto.WriteOffRecoveryAccountId.HasValue) settings.WriteOffRecoveryAccountId = dto.WriteOffRecoveryAccountId;
            if (dto.OpeningBalanceAutoRoutingEnabled.HasValue) settings.OpeningBalanceAutoRoutingEnabled = dto.OpeningBalanceAutoRoutingEnabled.Value;
            if (dto.BankDepositPolicy.HasValue) settings.BankDepositPolicy = dto.BankDepositPolicy.Value;
            if (dto.RequireBankDepositPrimaryEvidence.HasValue)
                settings.RequireBankDepositPrimaryEvidence = dto.RequireBankDepositPrimaryEvidence.Value;
            if (dto.AutoPostBankDepositAfterConfirmation.HasValue)
                settings.AutoPostBankDepositAfterConfirmation = dto.AutoPostBankDepositAfterConfirmation.Value;
            if (dto.MaximumDepositDeductionAmount.HasValue)
            {
                if (dto.MaximumDepositDeductionAmount.Value < 0m)
                    throw new InvalidOperationException("Maximum deposit deduction amount cannot be negative.");
                settings.MaximumDepositDeductionAmount = dto.MaximumDepositDeductionAmount;
            }
            if (dto.MaximumDepositDeductionPercentage.HasValue)
            {
                if (dto.MaximumDepositDeductionPercentage.Value is < 0m or > 100m)
                    throw new InvalidOperationException("Maximum deposit deduction percentage must be between 0 and 100.");
                settings.MaximumDepositDeductionPercentage = dto.MaximumDepositDeductionPercentage;
            }
            if (dto.BankStatementMatchDateToleranceDays.HasValue)
                settings.BankStatementMatchDateToleranceDays = Math.Clamp(dto.BankStatementMatchDateToleranceDays.Value, 0, 30);
            if (dto.ChequeClearingPeriodDays.HasValue)
                settings.ChequeClearingPeriodDays = Math.Clamp(dto.ChequeClearingPeriodDays.Value, 0, 90);
            if (dto.CashTillVarianceApprovalThreshold.HasValue)
            {
                if (dto.CashTillVarianceApprovalThreshold.Value < 0m)
                    throw new InvalidOperationException("Cash-till variance approval threshold cannot be negative.");
                settings.CashTillVarianceApprovalThreshold = dto.CashTillVarianceApprovalThreshold.Value;
            }
            if (dto.RequireIndependentCashTillClosure.HasValue)
                settings.RequireIndependentCashTillClosure = dto.RequireIndependentCashTillClosure.Value;
            if (dto.ReturnedChequeBankChargeAccountId.HasValue)
                settings.ReturnedChequeBankChargeAccountId = dto.ReturnedChequeBankChargeAccountId;
            if (dto.DefaultReturnedChequeChargeTreatment.HasValue)
                settings.DefaultReturnedChequeChargeTreatment = dto.DefaultReturnedChequeChargeTreatment.Value;
            if (dto.DefaultTransactionQuoteSide != null)
                settings.DefaultTransactionQuoteSide = ParseQuoteSide(dto.DefaultTransactionQuoteSide);
            if (dto.ArInvoiceQuoteSide != null)
                settings.ArInvoiceQuoteSide = ParseQuoteSide(dto.ArInvoiceQuoteSide);
            if (dto.ArSettlementQuoteSide != null)
                settings.ArSettlementQuoteSide = ParseQuoteSide(dto.ArSettlementQuoteSide);
            if (dto.ApInvoiceQuoteSide != null)
                settings.ApInvoiceQuoteSide = ParseQuoteSide(dto.ApInvoiceQuoteSide);
            if (dto.ApSettlementQuoteSide != null)
                settings.ApSettlementQuoteSide = ParseQuoteSide(dto.ApSettlementQuoteSide);
            if (dto.ClosingQuoteSide != null)
                settings.ClosingQuoteSide = ParseQuoteSide(dto.ClosingQuoteSide);
            if (dto.DirectionalExchangeRatePolicyEnabled == true &&
                !beforeFxPolicy.DirectionalExchangeRatePolicyEnabled)
            {
                await EnsureDirectionalRateReadinessAsync(tenantId, settings);
            }
            if (dto.DirectionalExchangeRatePolicyEnabled.HasValue)
                settings.DirectionalExchangeRatePolicyEnabled = dto.DirectionalExchangeRatePolicyEnabled.Value;
            if (dto.RequireExchangeRateOverrideApproval.HasValue)
            {
                if (!dto.RequireExchangeRateOverrideApproval.Value)
                    throw new InvalidOperationException("Exchange-rate policy overrides must retain approval and reason controls.");
                settings.RequireExchangeRateOverrideApproval = true;
            }

            if (dto.ReversalDatePolicy.HasValue)
            {
                if (!Enum.IsDefined(dto.ReversalDatePolicy.Value))
                    throw new InvalidOperationException("A valid Finance reversal-date policy is required.");
                settings.ReversalDatePolicy = dto.ReversalDatePolicy.Value;
            }

            if (dto.MinimumReversalReasonLength.HasValue)
            {
                if (dto.MinimumReversalReasonLength.Value is < 10 or > 500)
                {
                    throw new InvalidOperationException(
                        "Minimum Finance reversal reason length must be between 10 and 500 characters.");
                }
                settings.MinimumReversalReasonLength = dto.MinimumReversalReasonLength.Value;
            }

            if (dto.EnforceFinanceAccessScopes.HasValue)
            {
                if (dto.EnforceFinanceAccessScopes.Value && !settings.EnforceFinanceAccessScopes)
                {
                    // Enabling fail-closed data scopes before grants exist would lock every
                    // non-administrator out of Finance. Require deliberate scope preparation first.
                    var now = DateTime.UtcNow;
                    var preparedGrantExists = await _context.Set<FinanceAccessScopeGrant>().AnyAsync(item =>
                        item.TenantId == tenantId &&
                        item.IsActive &&
                        !item.IsDeleted &&
                        item.EffectiveFrom <= now &&
                        (!item.EffectiveTo.HasValue || item.EffectiveTo >= now) &&
                        (item.ScopeType == FinanceAccessScopeType.Tenant ||
                         (item.ScopeType == FinanceAccessScopeType.BankAccount && item.ScopeValue != null)));
                    if (!preparedGrantExists)
                    {
                        throw new InvalidOperationException(
                            "Create and review at least one active Finance access-scope grant before enabling enforcement.");
                    }
                }
                settings.EnforceFinanceAccessScopes = dto.EnforceFinanceAccessScopes.Value;
            }

            if (dto.RequireDepreciationBeforePeriodClose.HasValue)
            {
                // TDC's default remains mandatory. The explicit setting is retained because
                // FIN-LIM-0034 calls for a configurable close blocker, not an unchangeable flag.
                // Every change is included in the Finance control-policy audit below.
                settings.RequireDepreciationBeforePeriodClose = dto.RequireDepreciationBeforePeriodClose.Value;
            }

            if (dto.WhtStatutoryYearStartMonth.HasValue || dto.WhtStatutoryYearStartDay.HasValue)
            {
                var month = dto.WhtStatutoryYearStartMonth ?? settings.WhtStatutoryYearStartMonth;
                var day = dto.WhtStatutoryYearStartDay ?? settings.WhtStatutoryYearStartDay;
                if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(2001, month))
                    throw new InvalidOperationException("The WHT statutory-year start must be a valid month and day.");
                settings.WhtStatutoryYearStartMonth = month;
                settings.WhtStatutoryYearStartDay = day;
            }

            // Procurement invoice matching consumes the same tenant Finance policy row. Keeping
            // these bounds in this service prevents report/UI integration from bypassing the
            // authoritative validation used by invoice readiness controls.
            if (dto.ApInvoicePriceTolerancePercent.HasValue)
            {
                if (dto.ApInvoicePriceTolerancePercent.Value is < 0 or > 100)
                    throw new InvalidOperationException("AP invoice price tolerance must be between 0 and 100 percent.");
                settings.ApInvoicePriceTolerancePercent = dto.ApInvoicePriceTolerancePercent.Value;
            }
            if (dto.ApInvoiceQuantityTolerancePercent.HasValue)
            {
                if (dto.ApInvoiceQuantityTolerancePercent.Value is < 0 or > 100)
                    throw new InvalidOperationException("AP invoice quantity tolerance must be between 0 and 100 percent.");
                settings.ApInvoiceQuantityTolerancePercent = dto.ApInvoiceQuantityTolerancePercent.Value;
            }

            var baseCurrency = await _tenantSettingsService.GetBaseCurrencyReferenceAsync();
            ApplyPrecisionSettings(dto, settings, baseCurrency.DecimalPlaces);

            var afterPrecisionPolicy = PrecisionPolicyAuditValues(settings);
            var precisionPolicyChanged = !Equals(beforePrecisionPolicy, afterPrecisionPolicy);
            if (precisionPolicyChanged && _financeAuditService == null)
            {
                throw new InvalidOperationException(
                    "Finance precision and rounding changes require the Finance audit service.");
            }

            await using var precisionAuditTransaction = precisionPolicyChanged && _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;
            await _context.SaveChangesAsync();

            var afterWriteOffMappings = new
            {
                settings.WriteOffExpenseAccountId,
                settings.WriteOffRecoveryAccountId
            };
            if (!Equals(beforeWriteOffMappings, afterWriteOffMappings))
            {
                await RecordFinanceSettingsAuditAsync(
                    FinanceAuditEvents.FinanceControlPolicyChanged,
                    tenantId,
                    settings,
                    beforeValues: beforeWriteOffMappings,
                    afterValues: afterWriteOffMappings,
                    reason: "Write-off expense and recovery account mappings changed.",
                    sourceModule: "Finance");
            }

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

            var afterFxPolicy = new
            {
                settings.DirectionalExchangeRatePolicyEnabled,
                settings.DefaultTransactionQuoteSide,
                settings.ArInvoiceQuoteSide,
                settings.ArSettlementQuoteSide,
                settings.ApInvoiceQuoteSide,
                settings.ApSettlementQuoteSide,
                settings.ClosingQuoteSide,
                settings.RequireExchangeRateOverrideApproval
            };

            if (!Equals(beforeFxPolicy, afterFxPolicy))
            {
                await RecordFinanceSettingsAuditAsync(
                    FinanceAuditEvents.ExchangeRatePolicyChanged,
                    tenantId,
                    settings,
                    beforeValues: beforeFxPolicy,
                    afterValues: afterFxPolicy);
            }

            var afterControlPolicy = new
            {
                settings.SupplierAdvanceAccountId,
                settings.CustomerAdvanceAccountId,
                settings.ReversalDatePolicy,
                settings.MinimumReversalReasonLength,
                settings.EnforceFinanceAccessScopes,
                settings.RequireDepreciationBeforePeriodClose
            };

            if (!Equals(beforeControlPolicy, afterControlPolicy))
            {
                await RecordFinanceSettingsAuditAsync(
                    FinanceAuditEvents.FinanceControlPolicyChanged,
                    tenantId,
                    settings,
                    beforeValues: beforeControlPolicy,
                    afterValues: afterControlPolicy);
            }

            if (precisionPolicyChanged)
            {
                await RecordFinanceSettingsAuditAsync(
                    FinanceAuditEvents.FinanceControlPolicyChanged,
                    tenantId,
                    settings,
                    beforeValues: beforePrecisionPolicy,
                    afterValues: afterPrecisionPolicy,
                    reason: "Finance precision and rounding governance changed.",
                    sourceModule: "Finance");
            }

            if (precisionAuditTransaction != null)
                await precisionAuditTransaction.CommitAsync();

            return MapToDto(settings, baseCurrency, await HasAccountingActivityAsync(tenantId));
        }

        private async Task EnsureDirectionalRateReadinessAsync(Guid tenantId, FinanceSettings settings)
        {
            var baseCurrency = await _tenantSettingsService.GetBaseCurrencyReferenceAsync();
            var functionalCurrency = baseCurrency.CurrencyCode.Trim().ToUpperInvariant();
            var activeForeignCurrencies = await _context.Currencies
                .AsNoTracking()
                .Where(currency => currency.TenantId == tenantId
                    && !currency.IsDeleted
                    && currency.IsActive
                    && !currency.IsBaseCurrency
                    && currency.CurrencyCode != functionalCurrency)
                .Select(currency => currency.CurrencyCode)
                .Distinct()
                .OrderBy(code => code)
                .ToListAsync();

            var requiredQuoteSides = new[]
            {
                settings.DefaultTransactionQuoteSide,
                settings.ArInvoiceQuoteSide,
                settings.ArSettlementQuoteSide,
                settings.ApInvoiceQuoteSide,
                settings.ApSettlementQuoteSide
            }.Distinct().ToList();
            var effectiveDate = DateTime.UtcNow.Date;
            var missing = new List<string>();

            foreach (var currencyCode in activeForeignCurrencies)
            {
                foreach (var quoteSide in requiredQuoteSides)
                {
                    var exists = await _context.ExchangeRates.AsNoTracking().AnyAsync(rate =>
                        rate.TenantId == tenantId
                        && !rate.IsDeleted
                        && rate.IsActive
                        && rate.BaseCurrencyCode == functionalCurrency
                        && rate.TargetCurrencyCode == currencyCode
                        && rate.RateType == ExchangeRateType.Daily
                        && rate.QuoteSide == quoteSide
                        && rate.Rate > 0m
                        && (rate.ApprovalStatus == RateApprovalStatus.Approved
                            || rate.ApprovalStatus == RateApprovalStatus.AutoApproved)
                        && rate.EffectiveDate.Date <= effectiveDate
                        && (!rate.EndDate.HasValue || rate.EndDate.Value.Date >= effectiveDate));
                    if (!exists)
                        missing.Add($"{currencyCode} {quoteSide}");
                }
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Directional exchange-rate policy cannot be enabled. Load and approve active Daily rates for: "
                    + string.Join(", ", missing) + ".");
            }
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
                || await _context.JournalEntries.AnyAsync(j => j.TenantId == tenantId && !j.IsDeleted &&
                    (j.PostingStatus == "Posted" || j.PostingStatus == "Reversed"))
                || await _context.AccountTransactions.AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted);
        }

        private async Task ValidateWriteOffAccountAsync(Guid tenantId, Guid accountId, AccountType accountType, string label)
        {
            var valid = accountId != Guid.Empty && await _context.Accounts.AsNoTracking().AnyAsync(account =>
                account.Id == accountId && account.TenantId == tenantId && !account.IsDeleted &&
                account.Status == AccountStatus.Active && account.AllowDirectPosting && !account.IsControlAccount &&
                account.AccountType == accountType);
            if (!valid)
                throw new InvalidOperationException($"{label} must be an active {accountType} posting account belonging to the current tenant.");
        }

        private async Task ValidateReturnAccountAsync(Guid tenantId, Guid accountId, AccountType type, string label)
        {
            if (!await _context.Accounts.AsNoTracking().AnyAsync(account => account.Id == accountId &&
                account.TenantId == tenantId && !account.IsDeleted && account.Status == AccountStatus.Active && account.AccountType == type &&
                account.AllowDirectPosting && !account.IsControlAccount))
                throw new InvalidOperationException($"{label} must be an active {type} posting account belonging to this tenant.");
        }

        private async Task RecordFinanceSettingsAuditAsync(
            string eventType,
            Guid tenantId,
            FinanceSettings settings,
            object? beforeValues = null,
            object? afterValues = null,
            string? reason = null,
            string sourceModule = "FX")
        {
            if (_financeAuditService == null)
            {
                return;
            }

            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = tenantId,
                SourceModule = sourceModule,
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

        private static ExchangeRateQuoteSide ParseQuoteSide(string value)
        {
            if (!Enum.TryParse<ExchangeRateQuoteSide>(value.Trim(), ignoreCase: true, out var quoteSide))
                throw new InvalidOperationException("Exchange-rate quote side must be Mid, Buying, or Selling.");

            return quoteSide;
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
                WhtStatutoryYearStartMonth = settings.WhtStatutoryYearStartMonth,
                WhtStatutoryYearStartDay = settings.WhtStatutoryYearStartDay,
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
                SupplierAdvanceAccountId = settings.SupplierAdvanceAccountId,
                CustomerAdvanceAccountId = settings.CustomerAdvanceAccountId,
                ControlAccountInventoryId = settings.ControlAccountInventoryId,
                ControlAccountCOGSId = settings.ControlAccountCOGSId,
                ReturnToVendorClearingAccountId = settings.ReturnToVendorClearingAccountId,
                PurchaseReturnVarianceAccountId = settings.PurchaseReturnVarianceAccountId,
                ControlAccountPayrollId = settings.ControlAccountPayrollId,
                ControlAccountTaxId = settings.ControlAccountTaxId,
                ControlAccountGRVAccrualId = settings.ControlAccountGRVAccrualId,
                DiscountAllowedAccountId = settings.DiscountAllowedAccountId,
                DiscountReceivedAccountId = settings.DiscountReceivedAccountId,
                MigrationClearingAccountId = settings.MigrationClearingAccountId,
                WriteOffExpenseAccountId = settings.WriteOffExpenseAccountId,
                WriteOffRecoveryAccountId = settings.WriteOffRecoveryAccountId,
                OpeningBalanceAutoRoutingEnabled = settings.OpeningBalanceAutoRoutingEnabled,
                BankDepositPolicy = settings.BankDepositPolicy,
                RequireBankDepositPrimaryEvidence = settings.RequireBankDepositPrimaryEvidence,
                AutoPostBankDepositAfterConfirmation = settings.AutoPostBankDepositAfterConfirmation,
                MaximumDepositDeductionAmount = settings.MaximumDepositDeductionAmount,
                MaximumDepositDeductionPercentage = settings.MaximumDepositDeductionPercentage,
                BankStatementMatchDateToleranceDays = settings.BankStatementMatchDateToleranceDays,
                ChequeClearingPeriodDays = settings.ChequeClearingPeriodDays,
                CashTillVarianceApprovalThreshold = settings.CashTillVarianceApprovalThreshold,
                RequireIndependentCashTillClosure = settings.RequireIndependentCashTillClosure,
                ReturnedChequeBankChargeAccountId = settings.ReturnedChequeBankChargeAccountId,
                DefaultReturnedChequeChargeTreatment = settings.DefaultReturnedChequeChargeTreatment,
                DirectionalExchangeRatePolicyEnabled = settings.DirectionalExchangeRatePolicyEnabled,
                DefaultTransactionQuoteSide = settings.DefaultTransactionQuoteSide.ToString(),
                ArInvoiceQuoteSide = settings.ArInvoiceQuoteSide.ToString(),
                ArSettlementQuoteSide = settings.ArSettlementQuoteSide.ToString(),
                ApInvoiceQuoteSide = settings.ApInvoiceQuoteSide.ToString(),
                ApSettlementQuoteSide = settings.ApSettlementQuoteSide.ToString(),
                ClosingQuoteSide = settings.ClosingQuoteSide.ToString(),
                RequireExchangeRateOverrideApproval = settings.RequireExchangeRateOverrideApproval,
                ReversalDatePolicy = settings.ReversalDatePolicy,
                MinimumReversalReasonLength = settings.MinimumReversalReasonLength,
                EnforceFinanceAccessScopes = settings.EnforceFinanceAccessScopes,
                RequireDepreciationBeforePeriodClose = settings.RequireDepreciationBeforePeriodClose,
                UnitPriceDecimalPlaces = settings.UnitPriceDecimalPlaces,
                ExchangeRateInputDecimalPlaces = settings.ExchangeRateInputDecimalPlaces,
                ExchangeRateDisplayDecimalPlaces = settings.ExchangeRateDisplayDecimalPlaces,
                TaxPercentageDecimalPlaces = settings.TaxPercentageDecimalPlaces,
                TaxRoundingMethod = settings.TaxRoundingMethod,
                TaxRoundingScope = settings.TaxRoundingScope,
                TaxRoundingIncrement = settings.TaxRoundingIncrement,
                InvoiceRoundingEnabled = settings.InvoiceRoundingEnabled,
                InvoiceRoundingIncrement = settings.InvoiceRoundingIncrement,
                InvoiceRoundingMethod = settings.InvoiceRoundingMethod,
                InvoiceRoundingGainAccountId = settings.InvoiceRoundingGainAccountId,
                InvoiceRoundingLossAccountId = settings.InvoiceRoundingLossAccountId,
                SettlementToleranceAmount = settings.SettlementToleranceAmount,
                SettlementTolerancePercentage = settings.SettlementTolerancePercentage,
                ReportDisplayDecimalPlaces = settings.ReportDisplayDecimalPlaces,
                PrecisionAccountingPolicyLocked = transactionsExist,
                ApInvoicePriceTolerancePercent = settings.ApInvoicePriceTolerancePercent,
                ApInvoiceQuantityTolerancePercent = settings.ApInvoiceQuantityTolerancePercent,
                TransactionsExist = transactionsExist
            };
        }

        private static void ApplyPrecisionSettings(
            UpdateFinanceSettingsDto dto,
            FinanceSettings settings,
            int currencyDecimalPlaces)
        {
            var unitPricePlaces = dto.UnitPriceDecimalPlaces ?? settings.UnitPriceDecimalPlaces;
            _ = PrecisionRoundingPolicy.RoundUnitPrice(0m, unitPricePlaces);

            var rateInputPlaces = dto.ExchangeRateInputDecimalPlaces ?? settings.ExchangeRateInputDecimalPlaces;
            var rateDisplayPlaces = dto.ExchangeRateDisplayDecimalPlaces ?? settings.ExchangeRateDisplayDecimalPlaces;
            _ = PrecisionRoundingPolicy.RoundExchangeRate(1m, rateInputPlaces);
            _ = PrecisionRoundingPolicy.RoundExchangeRate(1m, rateDisplayPlaces);

            var taxPercentagePlaces = dto.TaxPercentageDecimalPlaces ?? settings.TaxPercentageDecimalPlaces;
            _ = PrecisionRoundingPolicy.RoundPercentage(0m, taxPercentagePlaces);

            var reportPlaces = dto.ReportDisplayDecimalPlaces ?? settings.ReportDisplayDecimalPlaces;
            if (reportPlaces is < 0 or > CurrencyMinorUnitPolicy.MaximumDecimalPlaces)
                throw new InvalidOperationException("Report display precision must be between 0 and 4 decimal places.");

            var taxIncrement = dto.TaxRoundingIncrementSpecified
                ? dto.TaxRoundingIncrement
                : settings.TaxRoundingIncrement;
            if (taxIncrement.HasValue && taxIncrement.Value <= 0m)
                throw new InvalidOperationException("Tax rounding increment must be greater than zero.");
            var currencyMinorUnit = CurrencyMinorUnitPolicy.MinorUnit(currencyDecimalPlaces);
            if (taxIncrement.HasValue &&
                (taxIncrement.Value < currencyMinorUnit || taxIncrement.Value % currencyMinorUnit != 0m))
            {
                throw new InvalidOperationException(
                    $"Tax rounding increment must be a whole multiple of the currency minor unit {currencyMinorUnit}.");
            }

            var taxMethod = dto.TaxRoundingMethod ?? settings.TaxRoundingMethod;
            var taxScope = dto.TaxRoundingScope ?? settings.TaxRoundingScope;
            var invoiceMethod = dto.InvoiceRoundingMethod ?? settings.InvoiceRoundingMethod;
            if (!Enum.IsDefined(taxMethod) || !Enum.IsDefined(taxScope) || !Enum.IsDefined(invoiceMethod))
                throw new InvalidOperationException("Rounding method and scope values must be defined governance options.");
            var invoiceEnabled = dto.InvoiceRoundingEnabled ?? settings.InvoiceRoundingEnabled;
            if (invoiceEnabled)
            {
                var invoiceIncrement = dto.InvoiceRoundingIncrement ?? settings.InvoiceRoundingIncrement;
                if (!invoiceIncrement.HasValue || invoiceIncrement.Value <= 0m)
                    throw new InvalidOperationException(
                        "Invoice/cash rounding requires a positive increment before activation.");
                if (invoiceIncrement.Value < currencyMinorUnit || invoiceIncrement.Value % currencyMinorUnit != 0m)
                    throw new InvalidOperationException(
                        $"Invoice/cash rounding increment must be a whole multiple of the base-currency minor unit {currencyMinorUnit}.");
                if (!(dto.InvoiceRoundingGainAccountId ?? settings.InvoiceRoundingGainAccountId).HasValue
                    || !(dto.InvoiceRoundingLossAccountId ?? settings.InvoiceRoundingLossAccountId).HasValue)
                    throw new InvalidOperationException(
                        "Invoice/cash rounding requires both gain and loss accounts before activation.");
            }

            var settlementAmount = dto.SettlementToleranceAmount ?? settings.SettlementToleranceAmount;
            var settlementPercentage = dto.SettlementTolerancePercentage ?? settings.SettlementTolerancePercentage;
            _ = PrecisionRoundingPolicy.IsWithinSettlementTolerance(0m, 0m, settlementAmount, settlementPercentage);

            settings.UnitPriceDecimalPlaces = unitPricePlaces;
            settings.ExchangeRateInputDecimalPlaces = rateInputPlaces;
            settings.ExchangeRateDisplayDecimalPlaces = rateDisplayPlaces;
            settings.TaxPercentageDecimalPlaces = taxPercentagePlaces;
            if (dto.TaxRoundingMethod.HasValue) settings.TaxRoundingMethod = dto.TaxRoundingMethod.Value;
            if (dto.TaxRoundingScope.HasValue) settings.TaxRoundingScope = dto.TaxRoundingScope.Value;
            if (dto.TaxRoundingIncrementSpecified) settings.TaxRoundingIncrement = dto.TaxRoundingIncrement;
            settings.InvoiceRoundingEnabled = invoiceEnabled;
            if (dto.InvoiceRoundingIncrementSpecified) settings.InvoiceRoundingIncrement = dto.InvoiceRoundingIncrement;
            if (dto.InvoiceRoundingMethod.HasValue) settings.InvoiceRoundingMethod = dto.InvoiceRoundingMethod.Value;
            if (dto.InvoiceRoundingGainAccountIdSpecified) settings.InvoiceRoundingGainAccountId = dto.InvoiceRoundingGainAccountId;
            if (dto.InvoiceRoundingLossAccountIdSpecified) settings.InvoiceRoundingLossAccountId = dto.InvoiceRoundingLossAccountId;
            settings.SettlementToleranceAmount = settlementAmount;
            settings.SettlementTolerancePercentage = settlementPercentage;
            settings.ReportDisplayDecimalPlaces = reportPlaces;
        }

        private static bool HasPrecisionAccountingChange(UpdateFinanceSettingsDto dto, FinanceSettings settings) =>
            dto.UnitPriceDecimalPlaces.HasValue && dto.UnitPriceDecimalPlaces.Value != settings.UnitPriceDecimalPlaces ||
            dto.ExchangeRateInputDecimalPlaces.HasValue && dto.ExchangeRateInputDecimalPlaces.Value != settings.ExchangeRateInputDecimalPlaces ||
            dto.TaxPercentageDecimalPlaces.HasValue && dto.TaxPercentageDecimalPlaces.Value != settings.TaxPercentageDecimalPlaces ||
            dto.TaxRoundingMethod.HasValue && dto.TaxRoundingMethod.Value != settings.TaxRoundingMethod ||
            dto.TaxRoundingScope.HasValue && dto.TaxRoundingScope.Value != settings.TaxRoundingScope ||
            dto.TaxRoundingIncrementSpecified && dto.TaxRoundingIncrement != settings.TaxRoundingIncrement ||
            dto.InvoiceRoundingEnabled.HasValue && dto.InvoiceRoundingEnabled.Value != settings.InvoiceRoundingEnabled ||
            dto.InvoiceRoundingIncrementSpecified && dto.InvoiceRoundingIncrement != settings.InvoiceRoundingIncrement ||
            dto.InvoiceRoundingMethod.HasValue && dto.InvoiceRoundingMethod.Value != settings.InvoiceRoundingMethod ||
            dto.InvoiceRoundingGainAccountIdSpecified && dto.InvoiceRoundingGainAccountId != settings.InvoiceRoundingGainAccountId ||
            dto.InvoiceRoundingLossAccountIdSpecified && dto.InvoiceRoundingLossAccountId != settings.InvoiceRoundingLossAccountId ||
            dto.SettlementToleranceAmount.HasValue && dto.SettlementToleranceAmount.Value != settings.SettlementToleranceAmount ||
            dto.SettlementTolerancePercentage.HasValue && dto.SettlementTolerancePercentage.Value != settings.SettlementTolerancePercentage;

        private static object PrecisionPolicyAuditValues(FinanceSettings settings) => new
        {
            settings.UnitPriceDecimalPlaces,
            settings.ExchangeRateInputDecimalPlaces,
            settings.ExchangeRateDisplayDecimalPlaces,
            settings.TaxPercentageDecimalPlaces,
            settings.TaxRoundingMethod,
            settings.TaxRoundingScope,
            settings.TaxRoundingIncrement,
            settings.InvoiceRoundingEnabled,
            settings.InvoiceRoundingIncrement,
            settings.InvoiceRoundingMethod,
            settings.InvoiceRoundingGainAccountId,
            settings.InvoiceRoundingLossAccountId,
            settings.SettlementToleranceAmount,
            settings.SettlementTolerancePercentage,
            settings.ReportDisplayDecimalPlaces
        };
    }
}

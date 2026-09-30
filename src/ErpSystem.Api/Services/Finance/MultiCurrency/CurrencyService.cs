using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Api.Services.Finance.MultiCurrency
{
    public class CurrencyService : ICurrencyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly IExchangeRateService _exchangeRateService;
        private readonly ILogger<CurrencyService> _logger;

        public CurrencyService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ITenantSettingsService tenantSettingsService,
            IExchangeRateService exchangeRateService,
            ILogger<CurrencyService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _tenantSettingsService = tenantSettingsService;
            _exchangeRateService = exchangeRateService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";

        // Retrieval
        public async Task<IReadOnlyList<CurrencyDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await GetCurrenciesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
        {
            var currencies = await _unitOfWork.Repository<Currency>()
                .GetQueryable(c => c.TenantId == TenantId)
                .OrderBy(c => c.CurrencyCode)
                .ToListAsync(cancellationToken);

            return currencies.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<CurrencyDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            var currencies = await _unitOfWork.Repository<Currency>()
                .GetQueryable(c => c.TenantId == TenantId && c.IsActive && !c.IsDeleted)
                .OrderBy(c => c.CurrencyCode)
                .ToListAsync(cancellationToken);

            return currencies.Select(MapToDto).ToList();
        }

        public async Task<CurrencyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == id);

            return currency == null ? null : MapToDto(currency);
        }

        public async Task<CurrencyDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return await GetCurrencyByCodeAsync(code, cancellationToken);
        }

        public async Task<CurrencyDto?> GetCurrencyByCodeAsync(string currencyCode, CancellationToken cancellationToken = default)
        {
            var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.CurrencyCode == currencyCode);

            return currency == null ? null : MapToDto(currency);
        }

        public async Task<CurrencyDto?> GetBaseCurrencyAsync(CancellationToken cancellationToken = default)
        {
            var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.IsBaseCurrency && c.IsActive && !c.IsDeleted);

            return currency == null ? null : MapToDto(currency);
        }

        // CRUD Implementation
        public async Task<CurrencyDto> CreateAsync(CreateCurrencyDto dto, CancellationToken cancellationToken = default)
        {
            return await CreateCurrencyAsync(dto, cancellationToken);
        }

        public async Task<CurrencyDto> CreateCurrencyAsync(CreateCurrencyDto dto, CancellationToken cancellationToken = default)
        {
            var currencyCode = NormalizeCurrencyCode(dto.CurrencyCode, "Currency code");
            var shouldCreateInitialRate = dto.CreateInitialExchangeRate && !dto.IsBaseCurrency;

            if (dto.IsBaseCurrency)
            {
                throw new InvalidOperationException(
                    "Create the currency as a normal active currency, then select it as the functional currency through Finance Settings. " +
                    "The currency register cannot bypass the governed functional-currency change control.");
            }

            if (!await IsCodeUniqueAsync(currencyCode, null, cancellationToken))
                throw new InvalidOperationException($"Currency with code '{currencyCode}' already exists.");

            if (shouldCreateInitialRate && (!dto.InitialExchangeRate.HasValue || dto.InitialExchangeRate.Value <= 0))
                throw new InvalidOperationException("Initial exchange rate must be greater than zero.");

            Currency? currency = null;
            await _unitOfWork.ExecuteInTransactionAsync(async operationToken =>
            {
                // Recheck inside the serializable, retryable transaction so concurrent requests
                // cannot create the same tenant currency after the optimistic preflight above.
                if (!await IsCodeUniqueAsync(currencyCode, null, operationToken))
                    throw new InvalidOperationException($"Currency with code '{currencyCode}' already exists.");

                var now = DateTime.UtcNow;
                currency = new Currency
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    CurrencyCode = currencyCode,
                    NumericCode = dto.NumericCode.Trim(),
                    CurrencyName = dto.CurrencyName.Trim(),
                    CurrencySymbol = string.IsNullOrWhiteSpace(dto.CurrencySymbol) ? null : dto.CurrencySymbol.Trim(),
                    PluralName = string.IsNullOrWhiteSpace(dto.PluralName) ? null : dto.PluralName.Trim(),
                    DecimalPlaces = dto.DecimalPlaces,
                    RoundingMethod = dto.RoundingMethod,
                    RoundingPrecision = dto.RoundingPrecision,
                    SymbolPosition = dto.SymbolPosition,
                    DecimalSeparator = dto.DecimalSeparator,
                    ThousandsSeparator = dto.ThousandsSeparator,
                    DigitGrouping = dto.DigitGrouping,
                    IsBaseCurrency = false,
                    CurrencyClassification = dto.CurrencyClassification,
                    GeographicRegion = dto.GeographicRegion,
                    AutoRetrieveExchangeRate = dto.AutoRetrieveExchangeRate,
                    ExchangeRateUpdateFrequency = dto.ExchangeRateUpdateFrequency,
                    IsActive = dto.IsActive,
                    ActivationDate = now,
                    CountryCode = string.IsNullOrWhiteSpace(dto.CountryCode) ? null : dto.CountryCode.Trim().ToUpperInvariant(),
                    CountryName = string.IsNullOrWhiteSpace(dto.CountryName) ? null : dto.CountryName.Trim(),
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                await _unitOfWork.Repository<Currency>().AddAsync(currency);

                if (shouldCreateInitialRate)
                {
                    // Persist inside the same transaction so the exchange-rate validation can see
                    // the new currency. The outer unit of work remains responsible for commit.
                    await _unitOfWork.SaveChangesAsync(operationToken);

                    var baseCurrencyCode = await ResolveInitialRateBaseCurrencyCodeAsync(currency.CurrencyCode, operationToken);
                    await _exchangeRateService.CreateExchangeRateAsync(new CreateExchangeRateDto
                    {
                        BaseCurrencyCode = baseCurrencyCode,
                        TargetCurrencyCode = currency.CurrencyCode,
                        Rate = dto.InitialExchangeRate!.Value,
                        EffectiveDate = (dto.InitialExchangeRateDate ?? now).Date,
                        RateType = dto.InitialExchangeRateType,
                        RateSource = dto.InitialExchangeRateSource,
                        SourceReference = dto.InitialExchangeRateSourceReference,
                        IsActive = true,
                        ApprovalStatus = "Approved"
                    }, operationToken);
                }
            }, cancellationToken);

            _logger.LogInformation("Currency {CurrencyCode} created by {User}", currency!.CurrencyCode, UserName);
            return MapToDto(currency);
        }

        public async Task<CurrencyDto> UpdateAsync(Guid id, UpdateCurrencyDto dto, CancellationToken cancellationToken = default)
        {
            var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == id);

            if (currency == null)
                throw new ArgumentException($"Currency with ID '{id}' not found.");

            // Basic update logic sharing code with UpdateCurrencyAsync
            currency.CurrencyName = dto.CurrencyName;
            currency.CurrencySymbol = dto.CurrencySymbol;
            currency.PluralName = dto.PluralName;
            currency.RoundingMethod = dto.RoundingMethod;
            currency.RoundingPrecision = dto.RoundingPrecision;
            currency.SymbolPosition = dto.SymbolPosition;
            currency.ThousandsSeparator = dto.ThousandsSeparator;
            currency.DigitGrouping = dto.DigitGrouping;
            currency.CurrencyClassification = dto.CurrencyClassification;
            currency.GeographicRegion = dto.GeographicRegion;
            currency.AutoRetrieveExchangeRate = dto.AutoRetrieveExchangeRate;
            currency.ExchangeRateUpdateFrequency = dto.ExchangeRateUpdateFrequency;
            currency.IsActive = dto.IsActive;
            currency.UpdatedAt = DateTime.UtcNow;
            currency.UpdatedBy = UserName;
            
            // Should verify unique code if code update was allowed (but DTO lacks code)

            await _unitOfWork.Repository<Currency>().UpdateAsync(currency);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Currency {CurrencyCode} updated by {User}", currency.CurrencyCode, UserName);

            return MapToDto(currency);
        }

        public async Task<CurrencyDto> UpdateCurrencyAsync(string currencyCode, UpdateCurrencyDto dto, CancellationToken cancellationToken = default)
        {
             var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.CurrencyCode == currencyCode);

            if (currency == null)
                throw new ArgumentException($"Currency with code '{currencyCode}' not found.");
            
            return await UpdateAsync(currency.Id, dto, cancellationToken);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == id);

            if (currency == null) return false;

            await DeleteCurrencyInternalAsync(currency, cancellationToken);
            return true;
        }

        public async Task DeleteCurrencyAsync(string currencyCode, CancellationToken cancellationToken = default)
        {
            var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.CurrencyCode == currencyCode);

            if (currency == null) return;

            await DeleteCurrencyInternalAsync(currency, cancellationToken);
        }

        private async Task DeleteCurrencyInternalAsync(Currency currency, CancellationToken cancellationToken)
        {
            if (currency.IsBaseCurrency)
                throw new InvalidOperationException("Cannot delete the base currency.");

            var hasTransactions = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.TransactionCurrency == currency.CurrencyCode && !t.IsDeleted)
                .AnyAsync(cancellationToken);

            if (hasTransactions)
                throw new InvalidOperationException($"Cannot delete currency '{currency.CurrencyCode}' because it has transaction history.");

            var hasAccountLinks = await _unitOfWork.Repository<AccountCurrencyLink>()
                .GetQueryable(l => l.LinkedCurrencyCode == currency.CurrencyCode && !l.IsDeleted)
                .AnyAsync(cancellationToken);

            if (hasAccountLinks)
                throw new InvalidOperationException($"Cannot delete currency '{currency.CurrencyCode}' because it is linked to accounts.");

            // Soft Delete
            currency.IsDeleted = true;
            currency.DeletedAt = DateTime.UtcNow;
            currency.DeletedBy = UserName;

            await _unitOfWork.Repository<Currency>().UpdateAsync(currency);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Currency {CurrencyCode} deleted by {User}", currency.CurrencyCode, UserName);
        }

        public async Task<CurrencyDto> ToggleCurrencyStatusAsync(string currencyCode, bool isActive, CancellationToken cancellationToken = default)
        {
            var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.CurrencyCode == currencyCode);

            if (currency == null)
                throw new ArgumentException($"Currency with code '{currencyCode}' not found.");

            if (!isActive && currency.IsBaseCurrency)
                throw new InvalidOperationException("Cannot deactivate the base currency.");

            currency.IsActive = isActive;
            currency.UpdatedAt = DateTime.UtcNow;
            currency.UpdatedBy = UserName;

            if (isActive)
            {
                if (currency.ActivationDate == default)
                    currency.ActivationDate = DateTime.UtcNow;
                currency.DeactivationDate = null;
            }
            else
            {
                currency.DeactivationDate = DateTime.UtcNow;
            }

            await _unitOfWork.Repository<Currency>().UpdateAsync(currency);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Currency {CurrencyCode} {Status} by {User}", 
                currencyCode, isActive ? "activated" : "deactivated", UserName);

            return MapToDto(currency);
        }

        public async Task<bool> SetBaseCurrencyAsync(Guid id, CancellationToken cancellationToken = default)
        {
             var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == id && !c.IsDeleted);
            
            if (currency == null) return false;
            if (currency.IsBaseCurrency) return true;

            throw new InvalidOperationException(
                "Functional currency can only be changed through Finance Settings, where accounting-history locks, " +
                "currency activation and audit evidence are enforced.");
        }

        public async Task<bool> UpdateExchangeRateAsync(Guid id, decimal rate, CancellationToken cancellationToken = default)
        {
             var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == id && !c.IsDeleted);
             if (currency == null) return false;

             if (rate <= 0m)
                 throw new InvalidOperationException("Exchange rate must be greater than zero.");

             throw new InvalidOperationException(
                 "The legacy currency quick-rate endpoint is retired because it cannot capture an effective date, quote side, source evidence or approval. " +
                 "Create a governed exchange-rate submission in Finance > Multi-Currency > Exchange Rates.");
        }

        public async Task<decimal> ConvertAsync(decimal amount, string fromCode, string toCode, CancellationToken cancellationToken = default)
        {
            var fromCurrency = NormalizeCurrencyCode(fromCode, "Source currency");
            var toCurrency = NormalizeCurrencyCode(toCode, "Target currency");

            var activeCurrencies = await _unitOfWork.Repository<Currency>()
                .GetQueryable(currency => currency.TenantId == TenantId
                    && !currency.IsDeleted
                    && currency.IsActive
                    && (currency.CurrencyCode == fromCurrency || currency.CurrencyCode == toCurrency))
                .Select(currency => new { currency.CurrencyCode, currency.DecimalPlaces })
                .ToListAsync(cancellationToken);
            if (!activeCurrencies.Any(currency => string.Equals(currency.CurrencyCode, fromCurrency, StringComparison.OrdinalIgnoreCase))
                || !activeCurrencies.Any(currency => string.Equals(currency.CurrencyCode, toCurrency, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Both {fromCurrency} and {toCurrency} must be active tenant currencies before conversion.");
            }
            if (fromCurrency == toCurrency) return amount;
            var targetDecimalPlaces = activeCurrencies.Single(currency =>
                string.Equals(currency.CurrencyCode, toCurrency, StringComparison.OrdinalIgnoreCase)).DecimalPlaces;

            var date = DateTime.UtcNow.Date;

            // The governed public contract is directional: one unit of BaseCurrencyCode
            // equals Rate units of TargetCurrencyCode. Prefer that direct path, then its
            // mathematical inverse, before triangulating through the functional currency.
            var directQuote = await _exchangeRateService.GetCurrentRateAsync(
                toCurrency,
                fromCurrency,
                date,
                ExchangeRateType.Daily.ToString(),
                ExchangeRateQuoteSide.Mid.ToString(),
                cancellationToken);
            if (directQuote is { Rate: > 0m })
            {
                return RoundConvertedAmount(amount * directQuote.Rate, targetDecimalPlaces);
            }

            var inverseQuote = await _exchangeRateService.GetCurrentRateAsync(
                fromCurrency,
                toCurrency,
                date,
                ExchangeRateType.Daily.ToString(),
                ExchangeRateQuoteSide.Mid.ToString(),
                cancellationToken);
            if (inverseQuote is { Rate: > 0m })
            {
                return RoundConvertedAmount(amount / inverseQuote.Rate, targetDecimalPlaces);
            }

            var functionalCurrency = NormalizeCurrencyCode(
                await _tenantSettingsService.GetBaseCurrencyAsync(),
                "Functional currency");
            if (fromCurrency == functionalCurrency || toCurrency == functionalCurrency)
            {
                throw MissingApprovedRate(fromCurrency, toCurrency, date);
            }

            var fromFunctionalQuote = await _exchangeRateService.GetCurrentRateAsync(
                fromCurrency,
                functionalCurrency,
                date,
                ExchangeRateType.Daily.ToString(),
                ExchangeRateQuoteSide.Mid.ToString(),
                cancellationToken);
            var toFunctionalQuote = await _exchangeRateService.GetCurrentRateAsync(
                toCurrency,
                functionalCurrency,
                date,
                ExchangeRateType.Daily.ToString(),
                ExchangeRateQuoteSide.Mid.ToString(),
                cancellationToken);
            if (fromFunctionalQuote is not { Rate: > 0m } || toFunctionalQuote is not { Rate: > 0m })
            {
                throw MissingApprovedRate(fromCurrency, toCurrency, date);
            }

            // Both legs are functional -> currency quotes. Convert the source amount back
            // to functional currency, then out to the requested target currency.
            return RoundConvertedAmount(
                amount * toFunctionalQuote.Rate / fromFunctionalQuote.Rate,
                targetDecimalPlaces);
        }

        private static decimal RoundConvertedAmount(decimal amount, int decimalPlaces) =>
            decimal.Round(amount, Math.Clamp(decimalPlaces, 0, 28), MidpointRounding.AwayFromZero);

        private static InvalidOperationException MissingApprovedRate(string fromCurrency, string toCurrency, DateTime date) =>
            new($"No active approved Daily/Mid exchange-rate path exists for {fromCurrency}/{toCurrency} on {date:yyyy-MM-dd}. Conversion was not performed.");

        public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            var currencyCode = NormalizeCurrencyCode(code, "Currency code");
            return !await _unitOfWork.Repository<Currency>()
                .GetQueryable(c => c.TenantId == TenantId && c.CurrencyCode == currencyCode && c.Id != excludeId && !c.IsDeleted)
                .AnyAsync(cancellationToken);
        }

        private async Task<string> ResolveInitialRateBaseCurrencyCodeAsync(string targetCurrencyCode, CancellationToken cancellationToken)
        {
            var baseCurrencyCode = NormalizeCurrencyCode(await _tenantSettingsService.GetBaseCurrencyAsync(), "Base currency");

            if (baseCurrencyCode == targetCurrencyCode)
                throw new InvalidOperationException("Initial exchange rates are only created for non-base currencies.");

            var baseCurrencyExists = await _unitOfWork.Repository<Currency>()
                .GetQueryable(c => c.TenantId == TenantId
                    && c.CurrencyCode == baseCurrencyCode
                    && c.IsActive
                    && !c.IsDeleted)
                .AnyAsync(cancellationToken);

            if (!baseCurrencyExists)
            {
                throw new InvalidOperationException(
                    $"Base currency '{baseCurrencyCode}' must be configured before creating an initial exchange rate.");
            }

            return baseCurrencyCode;
        }

        private static string NormalizeCurrencyCode(string currencyCode, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
                throw new InvalidOperationException($"{fieldName} is required.");

            var normalized = currencyCode.Trim().ToUpperInvariant();
            if (normalized.Length != 3)
                throw new InvalidOperationException($"{fieldName} must be a three-letter ISO 4217 code.");

            return normalized;
        }

        private CurrencyDto MapToDto(Currency currency)
        {
            var accountLinkageCount = _unitOfWork.Repository<AccountCurrencyLink>()
                .GetQueryable(l => l.LinkedCurrencyCode == currency.CurrencyCode && !l.IsDeleted)
                .Count();

            var transactionCount = _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.TransactionCurrency == currency.CurrencyCode && !t.IsDeleted)
                .Count();

            return new CurrencyDto
            {
                Id = currency.Id,
                TenantId = currency.TenantId,
                CurrencyCode = currency.CurrencyCode,
                NumericCode = currency.NumericCode,
                CurrencyName = currency.CurrencyName,
                CurrencySymbol = currency.CurrencySymbol,
                PluralName = currency.PluralName,
                DecimalPlaces = currency.DecimalPlaces,
                RoundingMethod = currency.RoundingMethod,
                RoundingPrecision = currency.RoundingPrecision,
                SymbolPosition = currency.SymbolPosition,
                DecimalSeparator = currency.DecimalSeparator,
                ThousandsSeparator = currency.ThousandsSeparator,
                DigitGrouping = currency.DigitGrouping,
                IsBaseCurrency = currency.IsBaseCurrency,
                CurrencyClassification = currency.CurrencyClassification,
                GeographicRegion = currency.GeographicRegion,
                AutoRetrieveExchangeRate = currency.AutoRetrieveExchangeRate,
                ExchangeRateUpdateFrequency = currency.ExchangeRateUpdateFrequency,
                IsActive = currency.IsActive,
                ActivationDate = currency.ActivationDate,
                DeactivationDate = currency.DeactivationDate,
                HasAccountLinkages = accountLinkageCount > 0,
                AccountLinkageCount = accountLinkageCount,
                HasTransactionHistory = transactionCount > 0,
                TransactionCount = transactionCount,
                CountryCode = currency.CountryCode,
                CountryName = currency.CountryName,
                CreatedAt = currency.CreatedAt,
                UpdatedAt = currency.UpdatedAt,
                CreatedBy = currency.CreatedBy,
                UpdatedBy = currency.UpdatedBy
            };
        }
    }
}

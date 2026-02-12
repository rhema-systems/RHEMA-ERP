using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Api.Services.Finance.MultiCurrency
{
    public class CurrencyService : ICurrencyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<CurrencyService> _logger;

        public CurrencyService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<CurrencyService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;
        private string UserName => _currentUserService.UserName ?? "system";

        public async Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
        {
            var currencies = await _unitOfWork.Repository<Currency>()
                .GetQueryable(c => c.TenantId == TenantId)
                .OrderBy(c => c.CurrencyCode)
                .ToListAsync(cancellationToken);

            return currencies.Select(MapToDto).ToList();
        }

        public async Task<CurrencyDto?> GetCurrencyByCodeAsync(string currencyCode, CancellationToken cancellationToken = default)
        {
            var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.CurrencyCode == currencyCode);

            return currency == null ? null : MapToDto(currency);
        }

        public async Task<CurrencyDto> CreateCurrencyAsync(CreateCurrencyDto dto, CancellationToken cancellationToken = default)
        {
            var exists = await _unitOfWork.Repository<Currency>()
                .GetQueryable(c => c.TenantId == TenantId && c.CurrencyCode == dto.CurrencyCode)
                .AnyAsync(cancellationToken);

            if (exists)
                throw new InvalidOperationException($"Currency with code '{dto.CurrencyCode}' already exists.");

            if (dto.IsBaseCurrency)
            {
                var existingBaseCurrency = await _unitOfWork.Repository<Currency>()
                    .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.IsBaseCurrency);

                if (existingBaseCurrency != null)
                {
                    existingBaseCurrency.IsBaseCurrency = false;
                    await _unitOfWork.Repository<Currency>().UpdateAsync(existingBaseCurrency);
                }
            }

            var now = DateTime.UtcNow;
            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CurrencyCode = dto.CurrencyCode.ToUpper(),
                NumericCode = dto.NumericCode,
                CurrencyName = dto.CurrencyName,
                CurrencySymbol = dto.CurrencySymbol,
                PluralName = dto.PluralName,
                DecimalPlaces = dto.DecimalPlaces,
                RoundingMethod = dto.RoundingMethod,
                RoundingPrecision = dto.RoundingPrecision,
                SymbolPosition = dto.SymbolPosition,
                DecimalSeparator = dto.DecimalSeparator,
                ThousandsSeparator = dto.ThousandsSeparator,
                DigitGrouping = dto.DigitGrouping,
                IsBaseCurrency = dto.IsBaseCurrency,
                CurrencyClassification = dto.CurrencyClassification,
                GeographicRegion = dto.GeographicRegion,
                AutoRetrieveExchangeRate = dto.AutoRetrieveExchangeRate,
                ExchangeRateUpdateFrequency = dto.ExchangeRateUpdateFrequency,
                DefaultRateType = dto.DefaultRateType,
                IsActive = dto.IsActive,
                ActivationDate = now,
                CountryCode = dto.CountryCode,
                CountryName = dto.CountryName,
                CreatedAt = now,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<Currency>().AddAsync(currency);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Currency {CurrencyCode} created by {User}", currency.CurrencyCode, UserName);

            return MapToDto(currency);
        }

        public async Task<CurrencyDto> UpdateCurrencyAsync(string currencyCode, UpdateCurrencyDto dto, CancellationToken cancellationToken = default)
        {
            var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.CurrencyCode == currencyCode);

            if (currency == null)
                throw new ArgumentException($"Currency with code '{currencyCode}' not found.");

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
            currency.DefaultRateType = dto.DefaultRateType;
            currency.IsActive = dto.IsActive;
            currency.UpdatedAt = DateTime.UtcNow;
            currency.UpdatedBy = UserName;

            await _unitOfWork.Repository<Currency>().UpdateAsync(currency);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Currency {CurrencyCode} updated by {User}", currencyCode, UserName);

            return MapToDto(currency);
        }

        public async Task DeleteCurrencyAsync(string currencyCode, CancellationToken cancellationToken = default)
        {
            var currency = await _unitOfWork.Repository<Currency>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.CurrencyCode == currencyCode);

            if (currency == null)
                return;

            if (currency.IsBaseCurrency)
                throw new InvalidOperationException("Cannot delete the base currency.");

            var hasTransactions = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.TransactionCurrency == currencyCode && !t.IsDeleted)
                .AnyAsync(cancellationToken);

            if (hasTransactions)
                throw new InvalidOperationException($"Cannot delete currency '{currencyCode}' because it has transaction history.");

            var hasAccountLinks = await _unitOfWork.Repository<AccountCurrencyLink>()
                .GetQueryable(l => l.LinkedCurrencyCode == currencyCode && !l.IsDeleted)
                .AnyAsync(cancellationToken);

            if (hasAccountLinks)
                throw new InvalidOperationException($"Cannot delete currency '{currencyCode}' because it is linked to accounts.");

            await _unitOfWork.Repository<Currency>().DeleteAsync(currency);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Currency {CurrencyCode} deleted by {User}", currencyCode, UserName);
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
                DefaultRateType = currency.DefaultRateType,
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

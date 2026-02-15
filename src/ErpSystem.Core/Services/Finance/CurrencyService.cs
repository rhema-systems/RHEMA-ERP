using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Finance;

public class CurrencyService : ICurrencyService
{
    private readonly ICurrencyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(
        ICurrencyRepository repository,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        ILogger<CurrencyService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<CurrencyDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<CurrencyDto?> GetByCodeAsync(string code)
    {
        var entity = await _repository.GetByCodeAsync(code);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<IEnumerable<CurrencyDto>> GetAllAsync()
    {
        var entities = await _repository.GetAllAsync();
        return entities.Where(e => !e.IsDeleted).Select(MapToDto);
    }

    public async Task<IEnumerable<CurrencyDto>> GetActiveAsync()
    {
        var entities = await _repository.GetActiveAsync();
        return entities.Select(MapToDto);
    }

    public async Task<CurrencyDto?> GetBaseCurrencyAsync()
    {
        var entity = await _repository.GetBaseCurrencyAsync();
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<CurrencyDto> CreateAsync(CreateCurrencyDto dto)
    {
        // Check for unique code
        if (!await IsCodeUniqueAsync(dto.Code))
        {
            throw new InvalidOperationException($"Currency with code '{dto.Code}' already exists.");
        }

        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot create currency without a valid tenant context.");
        }

        var entity = new Currency
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = dto.Code.ToUpperInvariant(),
            Name = dto.Name,
            Symbol = dto.Symbol,
            DecimalPlaces = dto.DecimalPlaces,
            ExchangeRate = dto.ExchangeRate,
            ExchangeRateDate = DateTime.UtcNow,
            IsBaseCurrency = dto.IsBaseCurrency,
            IsActive = true,
            DisplayOrder = dto.DisplayOrder,
            FormatString = dto.FormatString,
            Country = dto.Country,
            CreatedAt = DateTime.UtcNow
        };

        // If this is set as base currency, unset other base currencies
        if (dto.IsBaseCurrency)
        {
            await UnsetOtherBaseCurrenciesAsync(entity.Id);
            entity.ExchangeRate = 1; // Base currency always has exchange rate of 1
        }

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Created currency {Code} with ID {Id}", entity.Code, entity.Id);

        return MapToDto(entity);
    }

    public async Task<CurrencyDto> UpdateAsync(Guid id, UpdateCurrencyDto dto)
    {
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Currency with ID {id} not found.");

        entity.Name = dto.Name;
        entity.Symbol = dto.Symbol;
        entity.DecimalPlaces = dto.DecimalPlaces;
        entity.ExchangeRate = dto.ExchangeRate;
        entity.ExchangeRateDate = DateTime.UtcNow;
        entity.IsBaseCurrency = dto.IsBaseCurrency;
        entity.IsActive = dto.IsActive;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.FormatString = dto.FormatString;
        entity.Country = dto.Country;
        entity.UpdatedAt = DateTime.UtcNow;

        // If this is set as base currency, unset other base currencies
        if (dto.IsBaseCurrency)
        {
            await UnsetOtherBaseCurrenciesAsync(entity.Id);
            entity.ExchangeRate = 1; // Base currency always has exchange rate of 1
        }

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Updated currency {Code} with ID {Id}", entity.Code, entity.Id);

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        if (entity.IsBaseCurrency)
        {
            throw new InvalidOperationException("Cannot delete the base currency. Set another currency as base first.");
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Deleted currency {Code} with ID {Id}", entity.Code, entity.Id);

        return true;
    }

    public async Task<bool> SetBaseCurrencyAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        await UnsetOtherBaseCurrenciesAsync(id);

        entity.IsBaseCurrency = true;
        entity.ExchangeRate = 1;
        entity.ExchangeRateDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UpdateExchangeRateAsync(Guid id, decimal exchangeRate)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        if (entity.IsBaseCurrency)
        {
            throw new InvalidOperationException("Cannot change exchange rate of base currency.");
        }

        entity.ExchangeRate = exchangeRate;
        entity.ExchangeRateDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated exchange rate for currency {Code} to {Rate}", entity.Code, exchangeRate);
        return true;
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        return await _repository.IsCodeUniqueAsync(code, excludeId);
    }

    public async Task<decimal> ConvertAsync(decimal amount, string fromCurrencyCode, string toCurrencyCode)
    {
        if (fromCurrencyCode == toCurrencyCode)
        {
            return amount;
        }

        var fromCurrency = await _repository.GetByCodeAsync(fromCurrencyCode)
            ?? throw new InvalidOperationException($"Currency '{fromCurrencyCode}' not found.");

        var toCurrency = await _repository.GetByCodeAsync(toCurrencyCode)
            ?? throw new InvalidOperationException($"Currency '{toCurrencyCode}' not found.");

        // Convert to base currency first, then to target currency
        var amountInBase = amount / fromCurrency.ExchangeRate;
        var convertedAmount = amountInBase * toCurrency.ExchangeRate;

        return Math.Round(convertedAmount, toCurrency.DecimalPlaces);
    }

    private async Task UnsetOtherBaseCurrenciesAsync(Guid excludeId)
    {
        var allCurrencies = await _repository.GetAllAsync();
        foreach (var currency in allCurrencies.Where(c => c.Id != excludeId && c.IsBaseCurrency))
        {
            currency.IsBaseCurrency = false;
            currency.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(currency);
        }
    }

    private static CurrencyDto MapToDto(Currency entity)
    {
        return new CurrencyDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Symbol = entity.Symbol,
            DecimalPlaces = entity.DecimalPlaces,
            ExchangeRate = entity.ExchangeRate,
            ExchangeRateDate = entity.ExchangeRateDate,
            IsBaseCurrency = entity.IsBaseCurrency,
            IsActive = entity.IsActive,
            DisplayOrder = entity.DisplayOrder,
            FormatString = entity.FormatString,
            Country = entity.Country,
            CreatedAt = entity.CreatedAt
        };
    }
}

using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    private async Task<string> GetTenantBaseCurrencyCodeAsync()
    {
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        return string.IsNullOrWhiteSpace(baseCurrencyCode)
            ? "USD"
            : baseCurrencyCode.Trim().ToUpperInvariant();
    }

    private async Task<string> GetProjectBaseCurrencyCodeAsync(Project? project = null)
    {
        if (!string.IsNullOrWhiteSpace(project?.BaseCurrencyCode))
        {
            return project.BaseCurrencyCode.Trim().ToUpperInvariant();
        }

        return await GetTenantBaseCurrencyCodeAsync();
    }

    private async Task<string> ResolveProjectCurrencyAsync(string? currency, Project? project = null)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? await GetProjectBaseCurrencyCodeAsync(project)
            : currency.Trim().ToUpperInvariant();
    }

    private static string NormalizeCurrencyCode(string? currency, string fallback)
        => string.IsNullOrWhiteSpace(currency)
            ? fallback.Trim().ToUpperInvariant()
            : currency.Trim().ToUpperInvariant();

    private static string BuildExchangeRateKey(string baseCurrencyCode, string targetCurrencyCode)
        => $"{baseCurrencyCode.Trim().ToUpperInvariant()}->{targetCurrencyCode.Trim().ToUpperInvariant()}";

    private async Task<IReadOnlyDictionary<string, decimal>> LoadLatestExchangeRateMapAsync(IEnumerable<string?> currencies, DateTime? effectiveDate = null)
    {
        var normalizedCurrencies = currencies
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (normalizedCurrencies.Count < 2)
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        var asOfDate = (effectiveDate ?? DateTime.UtcNow).Date;
        var rates = (await _unitOfWork.Repository<ExchangeRate>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.IsActive
                && !x.IsDeleted
                && x.EffectiveDate <= asOfDate
                && (!x.EndDate.HasValue || x.EndDate.Value >= asOfDate)
                && normalizedCurrencies.Contains(x.BaseCurrencyCode)
                && normalizedCurrencies.Contains(x.TargetCurrencyCode)))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

        return rates
            .GroupBy(x => BuildExchangeRateKey(x.BaseCurrencyCode, x.TargetCurrencyCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Rate,
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<(string TenantBaseCurrencyCode, IReadOnlyDictionary<string, decimal> ExchangeRates)> BuildCurrencyConversionContextAsync(
        string targetCurrencyCode,
        IEnumerable<string?> currencies,
        DateTime? effectiveDate = null)
    {
        var tenantBaseCurrencyCode = await GetTenantBaseCurrencyCodeAsync();
        var exchangeRates = await LoadLatestExchangeRateMapAsync(
            currencies
                .Concat([targetCurrencyCode, tenantBaseCurrencyCode]),
            effectiveDate);

        return (tenantBaseCurrencyCode, exchangeRates);
    }

    private static decimal SumConvertedAmounts<T>(
        IEnumerable<T> items,
        Func<T, decimal> amountSelector,
        Func<T, string?> currencySelector,
        string targetCurrencyCode,
        string tenantBaseCurrencyCode,
        IReadOnlyDictionary<string, decimal> exchangeRates,
        out int failedConversionCount)
    {
        failedConversionCount = 0;
        decimal total = 0m;

        foreach (var item in items)
        {
            var amount = amountSelector(item);
            var convertedAmount = ConvertAmountToCurrency(
                amount,
                currencySelector(item),
                targetCurrencyCode,
                tenantBaseCurrencyCode,
                exchangeRates,
                out var wasConverted);

            if (!wasConverted && amount != 0m)
            {
                failedConversionCount++;
            }

            total += convertedAmount;
        }

        return decimal.Round(total, 2);
    }

    private async Task<(decimal Total, int FailedConversionCount)> SumConvertedAmountsByEffectiveDateAsync<T>(
        IEnumerable<T> items,
        Func<T, decimal> amountSelector,
        Func<T, string?> currencySelector,
        Func<T, DateTime?> effectiveDateSelector,
        string targetCurrencyCode)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return (0m, 0);
        }

        var tenantBaseCurrencyCode = await GetTenantBaseCurrencyCodeAsync();
        var normalizedTargetCurrencyCode = NormalizeCurrencyCode(targetCurrencyCode, tenantBaseCurrencyCode);
        var normalizedTenantBaseCurrencyCode = NormalizeCurrencyCode(tenantBaseCurrencyCode, normalizedTargetCurrencyCode);
        var currencies = itemList
            .Select(currencySelector)
            .Concat([normalizedTargetCurrencyCode, normalizedTenantBaseCurrencyCode])
            .ToList();
        var exchangeRateMapByDate = new Dictionary<DateTime, IReadOnlyDictionary<string, decimal>>();

        decimal total = 0m;
        var failedConversionCount = 0;

        foreach (var item in itemList)
        {
            var effectiveDate = (effectiveDateSelector(item) ?? DateTime.UtcNow).Date;
            if (!exchangeRateMapByDate.TryGetValue(effectiveDate, out var exchangeRates))
            {
                exchangeRates = await LoadLatestExchangeRateMapAsync(currencies, effectiveDate);
                exchangeRateMapByDate[effectiveDate] = exchangeRates;
            }

            var amount = amountSelector(item);
            var convertedAmount = ConvertAmountToCurrency(
                amount,
                currencySelector(item),
                normalizedTargetCurrencyCode,
                normalizedTenantBaseCurrencyCode,
                exchangeRates,
                out var wasConverted);

            if (!wasConverted && amount != 0m)
            {
                failedConversionCount++;
            }

            total += convertedAmount;
        }

        return (decimal.Round(total, 2), failedConversionCount);
    }

    private static decimal ConvertAmountToCurrency(
        decimal amount,
        string? fromCurrencyCode,
        string targetCurrencyCode,
        string tenantBaseCurrencyCode,
        IReadOnlyDictionary<string, decimal> exchangeRates,
        out bool wasConverted)
    {
        var normalizedTargetCurrency = NormalizeCurrencyCode(targetCurrencyCode, "USD");
        var normalizedSourceCurrency = NormalizeCurrencyCode(fromCurrencyCode, normalizedTargetCurrency);
        var normalizedTenantBaseCurrency = NormalizeCurrencyCode(tenantBaseCurrencyCode, normalizedTargetCurrency);

        if (amount == 0m || normalizedSourceCurrency == normalizedTargetCurrency)
        {
            wasConverted = true;
            return decimal.Round(amount, 2);
        }

        if (TryConvertAmountBetweenCurrencies(amount, normalizedSourceCurrency, normalizedTargetCurrency, normalizedTenantBaseCurrency, exchangeRates, out var converted))
        {
            wasConverted = true;
            return decimal.Round(converted, 2);
        }

        wasConverted = false;
        return decimal.Round(amount, 2);
    }

    private static bool TryConvertAmountBetweenCurrencies(
        decimal amount,
        string fromCurrencyCode,
        string targetCurrencyCode,
        string tenantBaseCurrencyCode,
        IReadOnlyDictionary<string, decimal> exchangeRates,
        out decimal convertedAmount)
    {
        if (fromCurrencyCode == targetCurrencyCode)
        {
            convertedAmount = amount;
            return true;
        }

        if (TryGetDirectConversionRate(exchangeRates, targetCurrencyCode, fromCurrencyCode, out var directToTargetRate))
        {
            convertedAmount = amount * directToTargetRate;
            return true;
        }

        if (TryGetDirectConversionRate(exchangeRates, fromCurrencyCode, targetCurrencyCode, out var inverseToTargetRate))
        {
            convertedAmount = amount / inverseToTargetRate;
            return true;
        }

        if (!TryConvertToAnchorCurrency(amount, fromCurrencyCode, tenantBaseCurrencyCode, exchangeRates, out var anchorAmount))
        {
            convertedAmount = amount;
            return false;
        }

        if (!TryConvertFromAnchorCurrency(anchorAmount, tenantBaseCurrencyCode, targetCurrencyCode, exchangeRates, out convertedAmount))
        {
            convertedAmount = amount;
            return false;
        }

        return true;
    }

    private static bool TryConvertToAnchorCurrency(
        decimal amount,
        string sourceCurrencyCode,
        string anchorCurrencyCode,
        IReadOnlyDictionary<string, decimal> exchangeRates,
        out decimal anchorAmount)
    {
        if (sourceCurrencyCode == anchorCurrencyCode)
        {
            anchorAmount = amount;
            return true;
        }

        if (TryGetDirectConversionRate(exchangeRates, anchorCurrencyCode, sourceCurrencyCode, out var targetToAnchorRate))
        {
            anchorAmount = amount * targetToAnchorRate;
            return true;
        }

        if (TryGetDirectConversionRate(exchangeRates, sourceCurrencyCode, anchorCurrencyCode, out var anchorToTargetRate))
        {
            anchorAmount = amount / anchorToTargetRate;
            return true;
        }

        anchorAmount = amount;
        return false;
    }

    private static bool TryConvertFromAnchorCurrency(
        decimal amount,
        string anchorCurrencyCode,
        string targetCurrencyCode,
        IReadOnlyDictionary<string, decimal> exchangeRates,
        out decimal targetAmount)
    {
        if (anchorCurrencyCode == targetCurrencyCode)
        {
            targetAmount = amount;
            return true;
        }

        if (TryGetDirectConversionRate(exchangeRates, anchorCurrencyCode, targetCurrencyCode, out var anchorToTargetRate))
        {
            targetAmount = amount / anchorToTargetRate;
            return true;
        }

        if (TryGetDirectConversionRate(exchangeRates, targetCurrencyCode, anchorCurrencyCode, out var targetToAnchorRate))
        {
            targetAmount = amount * targetToAnchorRate;
            return true;
        }

        targetAmount = amount;
        return false;
    }

    private static bool TryGetDirectConversionRate(
        IReadOnlyDictionary<string, decimal> exchangeRates,
        string baseCurrencyCode,
        string targetCurrencyCode,
        out decimal rate)
        => exchangeRates.TryGetValue(BuildExchangeRateKey(baseCurrencyCode, targetCurrencyCode), out rate)
           && rate > 0m;
}

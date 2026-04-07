namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    private async Task<string> GetProjectBaseCurrencyCodeAsync()
    {
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        return string.IsNullOrWhiteSpace(baseCurrencyCode)
            ? "USD"
            : baseCurrencyCode.Trim().ToUpperInvariant();
    }

    private async Task<string> ResolveProjectCurrencyAsync(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? await GetProjectBaseCurrencyCodeAsync()
            : currency.Trim().ToUpperInvariant();
    }
}

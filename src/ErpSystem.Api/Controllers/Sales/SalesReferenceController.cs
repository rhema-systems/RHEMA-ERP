using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

/// <summary>
/// Read-only reference data used by internal Sales and CRM transaction screens.
/// This deliberately exposes no Finance balances, rates, configuration, or mutation operations.
/// </summary>
[ApiController]
[Route("api/sales/reference")]
[Authorize(Policy = "InternalOnly")]
public sealed class SalesReferenceController(ICurrencyService currencies) : ControllerBase
{
    [HttpGet("currencies")]
    public async Task<ActionResult<IReadOnlyList<SalesCurrencyReferenceDto>>> GetCurrencies(
        CancellationToken cancellationToken = default)
    {
        var activeCurrencies = await currencies.GetActiveAsync(cancellationToken);
        return Ok(activeCurrencies
            .OrderByDescending(currency => currency.IsBaseCurrency)
            .ThenBy(currency => currency.CurrencyName)
            .Select(currency => new SalesCurrencyReferenceDto(
                currency.CurrencyCode,
                currency.CurrencyName,
                currency.CurrencySymbol,
                currency.DecimalPlaces,
                currency.IsBaseCurrency))
            .ToArray());
    }
}

public sealed record SalesCurrencyReferenceDto(
    string Code,
    string Name,
    string? Symbol,
    int DecimalPlaces,
    bool IsBaseCurrency);

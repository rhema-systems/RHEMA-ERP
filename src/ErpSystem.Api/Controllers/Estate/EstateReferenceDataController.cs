using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Estate;

/// <summary>
/// Exposes controlled, read-only Finance-owned reference data needed by Estate
/// without granting Estate users access to the wider Finance surface.
/// </summary>
[Authorize]
[ApiController]
[Route("api/estate/reference-data")]
public sealed class EstateReferenceDataController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public EstateReferenceDataController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    [HttpGet("currencies")]
    public async Task<ActionResult<IReadOnlyList<CurrencyDto>>> GetActiveCurrencies(
        CancellationToken cancellationToken)
        => Ok(await _currencyService.GetActiveAsync(cancellationToken));
}

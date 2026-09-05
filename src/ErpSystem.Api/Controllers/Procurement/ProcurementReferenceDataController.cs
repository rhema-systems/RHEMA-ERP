using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

/// <summary>
/// Exposes controlled, read-only reference data required by Procurement without
/// granting Procurement users access to the owning module's wider read surface.
/// Finance remains the owner and validator of the currency master.
/// </summary>
[Authorize(Policy = "procurement.records.read")]
[ApiController]
[Route("api/procurement/reference-data")]
public sealed class ProcurementReferenceDataController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public ProcurementReferenceDataController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    [HttpGet("currencies")]
    public async Task<ActionResult<IReadOnlyList<CurrencyDto>>> GetActiveCurrencies(
        CancellationToken cancellationToken)
        => Ok(await _currencyService.GetActiveAsync(cancellationToken));
}

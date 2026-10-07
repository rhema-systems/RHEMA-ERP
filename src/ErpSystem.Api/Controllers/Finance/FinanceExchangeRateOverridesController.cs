using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/exchange-rate-overrides")]
public sealed class FinanceExchangeRateOverridesController : ControllerBase
{
    private readonly IFinanceExchangeRateOverrideService _service;
    public FinanceExchangeRateOverridesController(IFinanceExchangeRateOverrideService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<FinanceExchangeRateOverrideRequestDto>>> Get(
        [FromQuery] string sourceDocumentType, [FromQuery] Guid sourceDocumentId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetForSourceAsync(sourceDocumentType, sourceDocumentId, cancellationToken));

    [HttpPost]
    [Authorize(Policy = FinancePermissions.RequestTransactionExchangeRateOverride)]
    public async Task<ActionResult<FinanceExchangeRateOverrideRequestDto>> Create(
        [FromBody] FinanceExchangeRateOverrideCommandDto command, CancellationToken cancellationToken)
    {
        var created = await _service.RequestAsync(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new
        {
            sourceDocumentType = created.SourceDocumentType,
            sourceDocumentId = created.SourceDocumentId
        }, created);
    }
}

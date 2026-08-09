using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/rate-library")]
public sealed class QuantitySurveyRateLibraryController(IQuantitySurveyRateLibraryService service) : ControllerBase
{
    [HttpGet("lookups")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups(CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] QuantitySurveyRateLibraryListRequest request, CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.GetItemsAsync(request, token)));

    [HttpGet("market-survey-sources")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> MarketSurveySources(CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.GetMarketSurveySourcesAsync(token)));

    [HttpGet("{id:guid}/historical-sources")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> HistoricalSources(
        Guid id,
        [FromQuery] QuantitySurveyHistoricalRateSourceType? sourceType,
        [FromQuery] string? search,
        CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.GetHistoricalRateSourcesAsync(id, sourceType, search, token)));

    [HttpGet("{id:guid}/rate-build-up-context")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> RateBuildUpContext(
        Guid id,
        [FromQuery] DateTime sourceDate,
        [FromQuery] DateTime effectiveAt,
        [FromQuery] Guid currencyId,
        [FromQuery] Guid? projectTypeId,
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? businessPartnerId,
        CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.GetRateBuildUpContextAsync(
            id,
            sourceDate,
            effectiveAt,
            currencyId,
            projectTypeId,
            locationId,
            businessPartnerId,
            token)));

    [HttpGet("{id:guid}/rate-build-ups")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> RateBuildUps(Guid id, CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.GetRateBuildUpsAsync(id, token)));

    [HttpPost("{id:guid}/rate-build-ups/preview")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> PreviewRateBuildUp(
        Guid id,
        [FromBody] PreviewQuantitySurveyRateBuildUpRequest request,
        CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.PreviewRateBuildUpAsync(id, request, token)));

    [HttpPost("{id:guid}/rate-build-ups")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> PrepareRateBuildUp(
        Guid id,
        [FromBody] PrepareQuantitySurveyRateBuildUpRequest request,
        CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.PrepareRateBuildUpAsync(id, request, CorrelationId, token)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.GetItemAsync(id, token)));

    [HttpPost]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> Create([FromBody] CreateQuantitySurveyRateLibraryItemRequest request, CancellationToken token)
        => ExecuteAsync(async () =>
        {
            var value = await service.CreateItemAsync(request, CorrelationId, token);
            return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
        });

    [HttpPut("{id:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateQuantitySurveyRateLibraryItemRequest request, CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.UpdateItemAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/rates")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> CreateRate(Guid id, [FromBody] SaveQuantitySurveyRateRequest request, CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.CreateRateAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/market-survey-updates")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> PrepareMarketSurveyUpdate(
        Guid id,
        [FromBody] PrepareQuantitySurveyMarketSurveyUpdateRequest request,
        CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.PrepareMarketSurveyUpdateAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/historical-rate-promotions")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> PrepareHistoricalRate(
        Guid id,
        [FromBody] PrepareQuantitySurveyHistoricalRateRequest request,
        CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.PrepareHistoricalRateAsync(id, request, CorrelationId, token)));

    [HttpPut("{id:guid}/rates/{rateId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> UpdateRate(Guid id, Guid rateId, [FromBody] UpdateQuantitySurveyRateRequest request, CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.UpdateRateAsync(id, rateId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/rates/{rateId:guid}/publish")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> PublishRate(Guid id, Guid rateId, [FromBody] QuantitySurveyRateLifecycleRequest request, CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.PublishRateAsync(id, rateId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/rates/{rateId:guid}/retire")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> RetireRate(Guid id, Guid rateId, [FromBody] QuantitySurveyRateLifecycleRequest request, CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.RetireRateAsync(id, rateId, request, CorrelationId, token)));

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token)
        => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyRateLibraryNotFoundException exception)
        {
            return NotFound(Problem(404, "QS rate-library record not found", exception.Message));
        }
        catch (QuantitySurveyRateLibraryConflictException exception)
        {
            return Conflict(Problem(409, "QS rate-library conflict", exception.Message));
        }
        catch (QuantitySurveyRateLibraryValidationException exception)
        {
            return BadRequest(Problem(400, "QS rate-library validation failed", exception.Message));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-rate-library-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = $"QS_RATE_LIBRARY_{status}",
            ["correlationId"] = CorrelationId
        }
    };
}

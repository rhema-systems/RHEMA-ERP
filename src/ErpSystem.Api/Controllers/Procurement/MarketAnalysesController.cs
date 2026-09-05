using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class MarketAnalysesController : ControllerBase
{
    private readonly IMarketAnalysisService _analysisService;

    public MarketAnalysesController(IMarketAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<MarketAnalysisDto>>> GetAnalyses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? itemCategory = null,
        [FromQuery] string? status = null)
    {
        var effectiveCategory = itemCategory ?? category;
        return Ok(await _analysisService.GetAnalysesAsync(
            page, pageSize, search, effectiveCategory, status));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MarketAnalysisDetailDto>> GetAnalysis(Guid id)
    {
        var analysis = await _analysisService.GetByIdAsync(id);
        return analysis == null
            ? throw new BusinessRuleException(
                "MARKET_ANALYSIS_NOT_FOUND",
                "The selected market analysis was not found.",
                StatusCodes.Status404NotFound)
            : Ok(analysis);
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<MarketAnalysisDto>>> GetByCategory(string category)
        => Ok(await _analysisService.GetByCategoryAsync(category));

    [HttpGet("item/{itemName}")]
    public async Task<ActionResult<IEnumerable<MarketAnalysisDto>>> GetByItem(string itemName)
        => Ok(await _analysisService.GetByItemAsync(itemName));

    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<MarketAnalysisDto>>> GetRecent([FromQuery] int count = 10)
        => Ok(await _analysisService.GetRecentAnalysesAsync(count));

    [HttpPost]
    public async Task<ActionResult<MarketAnalysisDetailDto>> CreateAnalysis([FromBody] CreateMarketAnalysisDto dto)
    {
        var analysis = await _analysisService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAnalysis), new { id = analysis.Id }, analysis);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MarketAnalysisDetailDto>> UpdateAnalysis(
        Guid id,
        [FromBody] CreateMarketAnalysisDto dto)
        => Ok(await _analysisService.UpdateAsync(id, dto));

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<MarketAnalysisDetailDto>> PublishAnalysis(Guid id)
        => Ok(await _analysisService.PublishAsync(id));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAnalysis(Guid id)
    {
        await _analysisService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{analysisId:guid}/price-history")]
    public async Task<ActionResult<PriceHistoryDto>> AddPriceHistory(
        Guid analysisId,
        [FromBody] CreatePriceHistoryDto dto)
        => Ok(await _analysisService.AddPriceHistoryAsync(analysisId, dto));

    [HttpPost("{analysisId:guid}/survey-quotes")]
    public async Task<ActionResult<PriceHistoryDto>> AddSurveyQuote(
        Guid analysisId,
        [FromBody] CreatePriceHistoryDto dto)
    {
        dto.MarketAnalysisId = analysisId;
        dto.PriceSource = "MarketSurvey";
        return Ok(await _analysisService.AddPriceHistoryAsync(analysisId, dto));
    }

    [HttpPut("{analysisId:guid}/survey-quotes/{priceHistoryId:guid}")]
    public async Task<ActionResult<PriceHistoryDto>> UpdateSurveyQuote(
        Guid analysisId,
        Guid priceHistoryId,
        [FromBody] CreatePriceHistoryDto dto)
    {
        dto.MarketAnalysisId = analysisId;
        dto.PriceSource = "MarketSurvey";
        return Ok(await _analysisService.UpdatePriceHistoryAsync(analysisId, priceHistoryId, dto));
    }

    [HttpGet("{analysisId:guid}/price-history")]
    public async Task<ActionResult<IEnumerable<PriceHistoryDto>>> GetPriceHistory(Guid analysisId)
        => Ok(await _analysisService.GetPriceHistoryAsync(analysisId));

    [HttpGet("{analysisId:guid}/survey-summary")]
    public async Task<ActionResult<MarketSurveySummaryDto>> GetSurveySummary(Guid analysisId)
        => Ok(await _analysisService.GetMarketSurveySummaryAsync(analysisId));

    [HttpGet("{analysisId:guid}/price-trend")]
    public async Task<ActionResult<PriceTrendDto>> GetPriceTrend(
        Guid analysisId,
        [FromQuery] int months = 12)
        => Ok(await _analysisService.GetPriceTrendAsync(analysisId, months));

    [HttpGet("category/{category}/average-price")]
    public async Task<ActionResult<decimal>> GetAveragePrice(string category)
        => Ok(await _analysisService.GetAveragePriceAsync(category));
}

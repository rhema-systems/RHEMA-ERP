using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
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
    private readonly ILogger<MarketAnalysesController> _logger;

    public MarketAnalysesController(
        IMarketAnalysisService analysisService,
        ILogger<MarketAnalysesController> logger)
    {
        _analysisService = analysisService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<MarketAnalysisDto>>> GetAnalyses(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? category = null)
    {
        try
        {
            var result = await _analysisService.GetAnalysesAsync(page, pageSize, search, category);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting market analyses");
            return StatusCode(500, "An error occurred while retrieving market analyses");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MarketAnalysisDetailDto>> GetAnalysis(Guid id)
    {
        try
        {
            var analysis = await _analysisService.GetByIdAsync(id);
            if (analysis == null) return NotFound($"Market analysis with ID {id} not found");
            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting market analysis {AnalysisId}", id);
            return StatusCode(500, "An error occurred while retrieving the market analysis");
        }
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<MarketAnalysisDto>>> GetByCategory(string category)
    {
        try { return Ok(await _analysisService.GetByCategoryAsync(category)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analyses for category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving analyses");
        }
    }

    [HttpGet("item/{itemName}")]
    public async Task<ActionResult<IEnumerable<MarketAnalysisDto>>> GetByItem(string itemName)
    {
        try { return Ok(await _analysisService.GetByItemAsync(itemName)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analyses for item {ItemName}", itemName);
            return StatusCode(500, "An error occurred while retrieving analyses");
        }
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<MarketAnalysisDto>>> GetRecent([FromQuery] int count = 10)
    {
        try { return Ok(await _analysisService.GetRecentAnalysesAsync(count)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent analyses");
            return StatusCode(500, "An error occurred while retrieving recent analyses");
        }
    }

    [HttpPost]
    public async Task<ActionResult<MarketAnalysisDetailDto>> CreateAnalysis([FromBody] CreateMarketAnalysisDto dto)
    {
        try
        {
            var analysis = await _analysisService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAnalysis), new { id = analysis.Id }, analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating market analysis");
            return StatusCode(500, "An error occurred while creating the market analysis");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<MarketAnalysisDetailDto>> UpdateAnalysis(Guid id, [FromBody] CreateMarketAnalysisDto dto)
    {
        try { return Ok(await _analysisService.UpdateAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating market analysis {AnalysisId}", id);
            return StatusCode(500, "An error occurred while updating the market analysis");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAnalysis(Guid id)
    {
        try { await _analysisService.DeleteAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting market analysis {AnalysisId}", id);
            return StatusCode(500, "An error occurred while deleting the market analysis");
        }
    }

    [HttpPost("{analysisId}/price-history")]
    public async Task<ActionResult<PriceHistoryDto>> AddPriceHistory(Guid analysisId, [FromBody] CreatePriceHistoryDto dto)
    {
        try { return Ok(await _analysisService.AddPriceHistoryAsync(analysisId, dto)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding price history to analysis {AnalysisId}", analysisId);
            return StatusCode(500, "An error occurred while adding price history");
        }
    }

    [HttpGet("{analysisId}/price-history")]
    public async Task<ActionResult<IEnumerable<PriceHistoryDto>>> GetPriceHistory(Guid analysisId)
    {
        try { return Ok(await _analysisService.GetPriceHistoryAsync(analysisId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price history for analysis {AnalysisId}", analysisId);
            return StatusCode(500, "An error occurred while retrieving price history");
        }
    }

    [HttpGet("{analysisId}/price-trend")]
    public async Task<ActionResult<PriceTrendDto>> GetPriceTrend(Guid analysisId, [FromQuery] int months = 12)
    {
        try { return Ok(await _analysisService.GetPriceTrendAsync(analysisId, months)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price trend for analysis {AnalysisId}", analysisId);
            return StatusCode(500, "An error occurred while retrieving price trend");
        }
    }

    [HttpGet("category/{category}/average-price")]
    public async Task<ActionResult<decimal>> GetAveragePrice(string category)
    {
        try { return Ok(await _analysisService.GetAveragePriceAsync(category)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting average price for category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving average price");
        }
    }
}
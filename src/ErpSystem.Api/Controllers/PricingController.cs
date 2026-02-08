using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Services.Pricing;

namespace ErpSystem.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PricingController : ControllerBase
{
    private readonly PriceListLookupService _priceListLookupService;
    private readonly ILogger<PricingController> _logger;

    public PricingController(
        PriceListLookupService priceListLookupService,
        ILogger<PricingController> logger)
    {
        _priceListLookupService = priceListLookupService;
        _logger = logger;
    }

    /// <summary>
    /// Get the best price for an item from a supplier's price lists
    /// </summary>
    [HttpGet("supplier/{supplierId}/item/{itemId}/price")]
    public async Task<IActionResult> GetSupplierItemPrice(
        Guid supplierId,
        Guid itemId,
        [FromQuery] decimal quantity = 1)
    {
        try
        {
            var price = await _priceListLookupService.GetSupplierPriceAsync(itemId, supplierId, quantity);
            if (price == null)
            {
                return NotFound(new { message = "No price found for this item from the supplier" });
            }
            return Ok(price);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier item price for item {ItemId} from supplier {SupplierId}", itemId, supplierId);
            return StatusCode(500, new { message = "An error occurred while retrieving the price" });
        }
    }

    /// <summary>
    /// Get all available prices for an item from a supplier (all quantity tiers)
    /// </summary>
    [HttpGet("supplier/{supplierId}/item/{itemId}/prices")]
    public async Task<IActionResult> GetAllSupplierItemPrices(Guid supplierId, Guid itemId)
    {
        try
        {
            var prices = await _priceListLookupService.GetAllSupplierPricesAsync(itemId, supplierId);
            return Ok(prices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all supplier item prices for item {ItemId} from supplier {SupplierId}", itemId, supplierId);
            return StatusCode(500, new { message = "An error occurred while retrieving prices" });
        }
    }

    /// <summary>
    /// Get price history for an item from a supplier
    /// </summary>
    [HttpGet("supplier/{supplierId}/item/{itemId}/history")]
    public async Task<IActionResult> GetPriceHistory(
        Guid supplierId,
        Guid itemId,
        [FromQuery] int months = 12)
    {
        try
        {
            var history = await _priceListLookupService.GetPriceHistoryAsync(itemId, supplierId, months);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price history for item {ItemId} from supplier {SupplierId}", itemId, supplierId);
            return StatusCode(500, new { message = "An error occurred while retrieving price history" });
        }
    }
}

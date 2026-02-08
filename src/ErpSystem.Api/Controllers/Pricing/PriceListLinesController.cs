using ErpSystem.Core.DTOs.Pricing;
using ErpSystem.Core.Interfaces.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Pricing;

[Authorize]
[ApiController]
[Route("api/pricing/price-list-lines")]
public class PriceListLinesController : ControllerBase
{
    private readonly IPriceListLineService _lineService;
    private readonly ILogger<PriceListLinesController> _logger;

    public PriceListLinesController(
        IPriceListLineService lineService,
        ILogger<PriceListLinesController> logger)
    {
        _lineService = lineService;
        _logger = logger;
    }

    /// <summary>
    /// Get all lines for a price list
    /// </summary>
    [HttpGet("by-price-list/{priceListId:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<PriceListLineDto>>> GetByPriceList(Guid priceListId)
    {
        try
        {
            var lines = await _lineService.GetByPriceListAsync(priceListId);
            return Ok(lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lines for price list {PriceListId}", priceListId);
            return StatusCode(500, "An error occurred while retrieving price list lines");
        }
    }

    /// <summary>
    /// Get price list line by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<PriceListLineDto>> GetById(Guid id)
    {
        try
        {
            var line = await _lineService.GetByIdAsync(id);
            if (line == null)
                return NotFound($"Price list line with ID {id} not found");
            return Ok(line);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price list line {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the price list line");
        }
    }

    /// <summary>
    /// Create a new price list line
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PriceListLineDto>> Create([FromBody] CreatePriceListLineDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var line = await _lineService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = line.Id }, line);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating price list line");
            return StatusCode(500, "An error occurred while creating the price list line");
        }
    }

    /// <summary>
    /// Update a price list line
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PriceListLineDto>> Update(Guid id, [FromBody] UpdatePriceListLineDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var line = await _lineService.UpdateAsync(id, dto);
            return Ok(line);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating price list line {Id}", id);
            return StatusCode(500, "An error occurred while updating the price list line");
        }
    }

    /// <summary>
    /// Delete a price list line
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var result = await _lineService.DeleteAsync(id);
            if (!result)
                return NotFound($"Price list line with ID {id} not found");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting price list line {Id}", id);
            return StatusCode(500, "An error occurred while deleting the price list line");
        }
    }

    /// <summary>
    /// Bulk create price list lines
    /// </summary>
    [HttpPost("bulk")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<IEnumerable<PriceListLineDto>>> BulkCreate([FromBody] IEnumerable<CreatePriceListLineDto> dtos)
    {
        try
        {
            var lines = await _lineService.BulkCreateAsync(dtos);
            return Ok(lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk creating price list lines");
            return StatusCode(500, "An error occurred while creating the price list lines");
        }
    }

    /// <summary>
    /// Bulk update prices in a price list by percentage
    /// </summary>
    [HttpPost("bulk-update-prices/{priceListId:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<int>> BulkUpdatePrices(Guid priceListId, [FromQuery] decimal percentageChange)
    {
        try
        {
            var count = await _lineService.BulkUpdatePricesAsync(priceListId, percentageChange);
            return Ok(new { UpdatedCount = count, Message = $"Updated {count} price list lines by {percentageChange}%" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating prices for price list {PriceListId}", priceListId);
            return StatusCode(500, "An error occurred while updating the prices");
        }
    }

    /// <summary>
    /// Export price list lines to CSV
    /// </summary>
    [HttpGet("export/{priceListId:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<IActionResult> ExportToCsv(Guid priceListId)
    {
        try
        {
            var csvBytes = await _lineService.ExportToCsvAsync(priceListId);
            return File(csvBytes, "text/csv", $"pricelist_{priceListId}_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting price list {PriceListId} to CSV", priceListId);
            return StatusCode(500, "An error occurred while exporting the price list");
        }
    }

    /// <summary>
    /// Import price list lines from CSV
    /// </summary>
    [HttpPost("import/{priceListId:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<IEnumerable<PriceListLineDto>>> ImportFromCsv(Guid priceListId, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            using var stream = file.OpenReadStream();
            var lines = await _lineService.ImportFromCsvAsync(priceListId, stream);
            return Ok(new { ImportedCount = lines.Count(), Lines = lines });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing CSV to price list {PriceListId}", priceListId);
            return StatusCode(500, "An error occurred while importing the CSV file");
        }
    }
}


using ErpSystem.Core.DTOs.Pricing;
using ErpSystem.Core.Entities.Pricing;
using ErpSystem.Core.Interfaces.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Pricing;

[Authorize]
[ApiController]
[Route("api/pricing/[controller]")]
public class PriceListsController : ControllerBase
{
    private readonly IPriceListService _priceListService;
    private readonly IPriceListLineService _lineService;
    private readonly ILogger<PriceListsController> _logger;

    public PriceListsController(
        IPriceListService priceListService,
        IPriceListLineService lineService,
        ILogger<PriceListsController> logger)
    {
        _priceListService = priceListService;
        _lineService = lineService;
        _logger = logger;
    }

    #region Price List CRUD

    /// <summary>
    /// Get all price lists
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<PriceListDto>>> GetAll()
    {
        try
        {
            var priceLists = await _priceListService.GetAllAsync();
            return Ok(priceLists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price lists");
            return StatusCode(500, "An error occurred while retrieving price lists");
        }
    }

    /// <summary>
    /// Get price list by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<PriceListDto>> GetById(Guid id)
    {
        try
        {
            var priceList = await _priceListService.GetByIdAsync(id);
            if (priceList == null)
                return NotFound($"Price list with ID {id} not found");
            return Ok(priceList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price list {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the price list");
        }
    }

    /// <summary>
    /// Get price list by code
    /// </summary>
    [HttpGet("by-code/{code}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<PriceListDto>> GetByCode(string code)
    {
        try
        {
            var priceList = await _priceListService.GetByCodeAsync(code);
            if (priceList == null)
                return NotFound($"Price list with code '{code}' not found");
            return Ok(priceList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price list by code {Code}", code);
            return StatusCode(500, "An error occurred while retrieving the price list");
        }
    }

    /// <summary>
    /// Create a new price list
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PriceListDto>> Create([FromBody] CreatePriceListDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var priceList = await _priceListService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = priceList.Id }, priceList);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating price list");
            return StatusCode(500, "An error occurred while creating the price list");
        }
    }

    /// <summary>
    /// Update a price list
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PriceListDto>> Update(Guid id, [FromBody] UpdatePriceListDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var priceList = await _priceListService.UpdateAsync(id, dto);
            return Ok(priceList);
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
            _logger.LogError(ex, "Error updating price list {Id}", id);
            return StatusCode(500, "An error occurred while updating the price list");
        }
    }

    /// <summary>
    /// Delete a price list
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var result = await _priceListService.DeleteAsync(id);
            if (!result)
                return NotFound($"Price list with ID {id} not found");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting price list {Id}", id);
            return StatusCode(500, "An error occurred while deleting the price list");
        }
    }

    #endregion

    #region Filtering

    /// <summary>
    /// Get price lists by type
    /// </summary>
    [HttpGet("by-type/{type}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<PriceListDto>>> GetByType(PriceListType type)
    {
        try
        {
            var priceLists = await _priceListService.GetByTypeAsync(type);
            return Ok(priceLists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price lists by type {Type}", type);
            return StatusCode(500, "An error occurred while retrieving price lists");
        }
    }

    /// <summary>
    /// Get active price lists
    /// </summary>
    [HttpGet("active")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<PriceListDto>>> GetActive()
    {
        try
        {
            var priceLists = await _priceListService.GetActiveAsync();
            return Ok(priceLists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active price lists");
            return StatusCode(500, "An error occurred while retrieving active price lists");
        }
    }

    /// <summary>
    /// Get price lists by status
    /// </summary>
    [HttpGet("by-status/{status}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<PriceListDto>>> GetByStatus(PriceListStatus status)
    {
        try
        {
            var priceLists = await _priceListService.GetByStatusAsync(status);
            return Ok(priceLists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price lists by status {Status}", status);
            return StatusCode(500, "An error occurred while retrieving price lists");
        }
    }

    /// <summary>
    /// Search price lists
    /// </summary>
    [HttpGet("search")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<PriceListDto>>> Search([FromQuery] string searchTerm)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return BadRequest("Search term is required");

            var priceLists = await _priceListService.SearchAsync(searchTerm);
            return Ok(priceLists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching price lists for {SearchTerm}", searchTerm);
            return StatusCode(500, "An error occurred while searching price lists");
        }
    }

    #endregion

    #region Approval Workflow

    /// <summary>
    /// Submit price list for approval
    /// </summary>
    [HttpPost("{id:guid}/submit-for-approval")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PriceListDto>> SubmitForApproval(Guid id)
    {
        try
        {
            var priceList = await _priceListService.SubmitForApprovalAsync(id);
            return Ok(priceList);
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
            _logger.LogError(ex, "Error submitting price list {Id} for approval", id);
            return StatusCode(500, "An error occurred while submitting the price list for approval");
        }
    }

    /// <summary>
    /// Approve a price list
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<PriceListDto>> Approve(Guid id, [FromBody] ApprovalRequest request)
    {
        try
        {
            var priceList = await _priceListService.ApproveAsync(id, request.ApprovedById, request.Comments);
            return Ok(priceList);
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
            _logger.LogError(ex, "Error approving price list {Id}", id);
            return StatusCode(500, "An error occurred while approving the price list");
        }
    }

    /// <summary>
    /// Reject a price list
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<PriceListDto>> Reject(Guid id, [FromBody] RejectionRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest("Rejection reason is required");

            var priceList = await _priceListService.RejectAsync(id, request.RejectedById, request.Reason);
            return Ok(priceList);
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
            _logger.LogError(ex, "Error rejecting price list {Id}", id);
            return StatusCode(500, "An error occurred while rejecting the price list");
        }
    }

    /// <summary>
    /// Get price lists pending approval
    /// </summary>
    [HttpGet("pending-approval")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<IEnumerable<PriceListDto>>> GetPendingApproval()
    {
        try
        {
            var priceLists = await _priceListService.GetPendingApprovalAsync();
            return Ok(priceLists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price lists pending approval");
            return StatusCode(500, "An error occurred while retrieving price lists");
        }
    }

    #endregion

    #region Operations

    /// <summary>
    /// Activate a price list
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PriceListDto>> Activate(Guid id)
    {
        try
        {
            var priceList = await _priceListService.ActivateAsync(id);
            return Ok(priceList);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating price list {Id}", id);
            return StatusCode(500, "An error occurred while activating the price list");
        }
    }

    /// <summary>
    /// Deactivate a price list
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PriceListDto>> Deactivate(Guid id)
    {
        try
        {
            var priceList = await _priceListService.DeactivateAsync(id);
            return Ok(priceList);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating price list {Id}", id);
            return StatusCode(500, "An error occurred while deactivating the price list");
        }
    }

    /// <summary>
    /// Copy a price list
    /// </summary>
    [HttpPost("{id:guid}/copy")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PriceListDto>> Copy(Guid id, [FromBody] CopyPriceListRequest request)
    {
        try
        {
            var priceList = await _priceListService.CopyPriceListAsync(id, request.NewCode, request.NewName);
            return CreatedAtAction(nameof(GetById), new { id = priceList.Id }, priceList);
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
            _logger.LogError(ex, "Error copying price list {Id}", id);
            return StatusCode(500, "An error occurred while copying the price list");
        }
    }

    /// <summary>
    /// Get price for an item from a price list
    /// </summary>
    [HttpGet("{id:guid}/price/{inventoryItemId:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<decimal?>> GetItemPrice(Guid id, Guid inventoryItemId, [FromQuery] decimal quantity = 1)
    {
        try
        {
            var price = await _priceListService.GetPriceForItemAsync(id, inventoryItemId, quantity);
            if (price == null)
                return NotFound("Price not found for the specified item");
            return Ok(price);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price for item {ItemId} from price list {PriceListId}", inventoryItemId, id);
            return StatusCode(500, "An error occurred while retrieving the price");
        }
    }

    #endregion

    #region Price List Lines (Nested Routes)

    /// <summary>
    /// Get all lines for a price list
    /// </summary>
    [HttpGet("{id:guid}/lines")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<PriceListLineDto>>> GetLines(Guid id)
    {
        try
        {
            var lines = await _lineService.GetByPriceListAsync(id);
            return Ok(lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lines for price list {Id}", id);
            return StatusCode(500, "An error occurred while retrieving price list lines");
        }
    }

    /// <summary>
    /// Get a specific line from a price list
    /// </summary>
    [HttpGet("{id:guid}/lines/{lineId:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<PriceListLineDto>> GetLine(Guid id, Guid lineId)
    {
        try
        {
            var line = await _lineService.GetByIdAsync(lineId);
            if (line == null || line.PriceListId != id)
                return NotFound($"Price list line with ID {lineId} not found");
            return Ok(line);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting line {LineId} from price list {Id}", lineId, id);
            return StatusCode(500, "An error occurred while retrieving the price list line");
        }
    }

    /// <summary>
    /// Add a new line to a price list
    /// </summary>
    [HttpPost("{id:guid}/lines")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PriceListLineDto>> CreateLine(Guid id, [FromBody] CreatePriceListLineRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var dto = new CreatePriceListLineDto
            {
                PriceListId = id,
                InventoryItemId = request.InventoryItemId,
                UnitOfMeasure = request.UnitOfMeasure ?? "EA",
                BasePrice = request.BasePrice,
                DiscountPercent = request.DiscountPercent,
                MinQuantity = request.MinQuantity,
                MaxQuantity = request.MaxQuantity,
                SupplierItemCode = request.SupplierItemCode,
                MinimumOrderQuantity = request.MinimumOrderQuantity,
                OrderMultiple = request.OrderMultiple,
                LeadTimeDays = request.LeadTimeDays,
                Notes = request.Notes
            };

            var line = await _lineService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetLine), new { id, lineId = line.Id }, line);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating line for price list {Id}", id);
            return StatusCode(500, "An error occurred while creating the price list line");
        }
    }

    /// <summary>
    /// Update a line in a price list
    /// </summary>
    [HttpPut("{id:guid}/lines/{lineId:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PriceListLineDto>> UpdateLine(Guid id, Guid lineId, [FromBody] UpdatePriceListLineRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existing = await _lineService.GetByIdAsync(lineId);
            if (existing == null || existing.PriceListId != id)
                return NotFound($"Price list line with ID {lineId} not found");

            var dto = new UpdatePriceListLineDto
            {
                BasePrice = request.BasePrice,
                DiscountPercent = request.DiscountPercent,
                MinQuantity = request.MinQuantity,
                MaxQuantity = request.MaxQuantity,
                SupplierItemCode = request.SupplierItemCode,
                MinimumOrderQuantity = request.MinimumOrderQuantity,
                OrderMultiple = request.OrderMultiple,
                LeadTimeDays = request.LeadTimeDays,
                Notes = request.Notes,
                IsActive = request.IsActive
            };

            var line = await _lineService.UpdateAsync(lineId, dto);
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
            _logger.LogError(ex, "Error updating line {LineId} in price list {Id}", lineId, id);
            return StatusCode(500, "An error occurred while updating the price list line");
        }
    }

    /// <summary>
    /// Delete a line from a price list
    /// </summary>
    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult> DeleteLine(Guid id, Guid lineId)
    {
        try
        {
            var existing = await _lineService.GetByIdAsync(lineId);
            if (existing == null || existing.PriceListId != id)
                return NotFound($"Price list line with ID {lineId} not found");

            var result = await _lineService.DeleteAsync(lineId);
            if (!result)
                return NotFound($"Price list line with ID {lineId} not found");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting line {LineId} from price list {Id}", lineId, id);
            return StatusCode(500, "An error occurred while deleting the price list line");
        }
    }

    /// <summary>
    /// Bulk update prices in a price list by percentage
    /// </summary>
    [HttpPost("{id:guid}/bulk-update-prices")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<int>> BulkUpdatePrices(Guid id, [FromBody] BulkUpdatePricesRequest request)
    {
        try
        {
            var count = await _lineService.BulkUpdatePricesAsync(id, request.PercentageChange);
            return Ok(new { UpdatedCount = count, Message = $"Updated {count} price list lines by {request.PercentageChange}%" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating prices for price list {Id}", id);
            return StatusCode(500, "An error occurred while updating the prices");
        }
    }

    /// <summary>
    /// Export price list lines to CSV
    /// </summary>
    [HttpGet("{id:guid}/export")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<IActionResult> ExportToCsv(Guid id)
    {
        try
        {
            var priceList = await _priceListService.GetByIdAsync(id);
            if (priceList == null)
                return NotFound($"Price list with ID {id} not found");

            var csvBytes = await _lineService.ExportToCsvAsync(id);
            return File(csvBytes, "text/csv", $"pricelist_{priceList.PriceListCode}_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting price list {Id} to CSV", id);
            return StatusCode(500, "An error occurred while exporting the price list");
        }
    }

    /// <summary>
    /// Get all price list lines for a specific inventory item
    /// </summary>
    [HttpGet("by-item/{inventoryItemId:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<ItemPriceListLineDto>>> GetLinesByInventoryItem(Guid inventoryItemId)
    {
        try
        {
            var lines = await _lineService.GetByInventoryItemAsync(inventoryItemId);
            return Ok(lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price list lines for inventory item {InventoryItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while retrieving the price list lines");
        }
    }

    #endregion
}

// Request DTOs for approval workflow
public record ApprovalRequest(Guid ApprovedById, string? Comments);
public record RejectionRequest(Guid RejectedById, string Reason);
public record CopyPriceListRequest(string NewCode, string NewName);

// Request DTOs for price list lines
public record CreatePriceListLineRequest(
    Guid InventoryItemId,
    string? UnitOfMeasure,
    decimal BasePrice,
    decimal DiscountPercent = 0,
    decimal MinQuantity = 1,
    decimal? MaxQuantity = null,
    string? SupplierItemCode = null,
    decimal? MinimumOrderQuantity = null,
    decimal? OrderMultiple = null,
    int? LeadTimeDays = null,
    string? Notes = null
);

public record UpdatePriceListLineRequest(
    decimal BasePrice,
    decimal DiscountPercent = 0,
    decimal MinQuantity = 1,
    decimal? MaxQuantity = null,
    string? SupplierItemCode = null,
    decimal? MinimumOrderQuantity = null,
    decimal? OrderMultiple = null,
    int? LeadTimeDays = null,
    string? Notes = null,
    bool IsActive = true
);

public record BulkUpdatePricesRequest(decimal PercentageChange);


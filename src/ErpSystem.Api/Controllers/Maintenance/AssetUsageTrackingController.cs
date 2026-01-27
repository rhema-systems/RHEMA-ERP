using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/usage-tracking")]
[Authorize]
public class AssetUsageTrackingController : ControllerBase
{
    private readonly IAssetUsageTrackingService _usageTrackingService;
    private readonly ILogger<AssetUsageTrackingController> _logger;

    public AssetUsageTrackingController(
        IAssetUsageTrackingService usageTrackingService,
        ILogger<AssetUsageTrackingController> logger)
    {
        _usageTrackingService = usageTrackingService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new usage record
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AssetUsageTrackingDto>> CreateUsageRecord([FromBody] CreateAssetUsageTrackingDto createDto)
    {
        try
        {
            var result = await _usageTrackingService.CreateUsageRecordAsync(createDto);
            return CreatedAtAction(nameof(GetUsageRecordById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating usage record");
            return StatusCode(500, "An error occurred while creating the usage record");
        }
    }

    /// <summary>
    /// Bulk creates usage records from import
    /// </summary>
    [HttpPost("bulk")]
    public async Task<ActionResult<IEnumerable<AssetUsageTrackingDto>>> BulkCreateUsageRecords([FromBody] BulkUsageImportDto bulkDto)
    {
        try
        {
            var result = await _usageTrackingService.BulkCreateUsageRecordsAsync(bulkDto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk creating usage records");
            return StatusCode(500, "An error occurred while importing usage records");
        }
    }

    /// <summary>
    /// Gets a usage record by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetUsageTrackingDto>> GetUsageRecordById(Guid id)
    {
        var record = await _usageTrackingService.GetUsageRecordByIdAsync(id);
        if (record == null)
        {
            return NotFound($"Usage record with ID {id} not found");
        }

        return Ok(record);
    }

    /// <summary>
    /// Gets usage records for an asset
    /// </summary>
    [HttpGet("asset/{assetId:guid}")]
    public async Task<ActionResult<IEnumerable<AssetUsageTrackingDto>>> GetUsageRecordsByAsset(
        Guid assetId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var records = await _usageTrackingService.GetUsageRecordsAsync(assetId, startDate, endDate);
        return Ok(records);
    }

    /// <summary>
    /// Gets paginated usage records for an asset
    /// </summary>
    [HttpGet("asset/{assetId:guid}/paged")]
    public async Task<ActionResult<PagedResult<AssetUsageTrackingDto>>> GetUsageRecordsPagedByAsset(
        Guid assetId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _usageTrackingService.GetUsageRecordsPagedAsync(assetId, page, pageSize, startDate, endDate);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a usage record
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUsageRecord(Guid id)
    {
        try
        {
            await _usageTrackingService.DeleteUsageRecordAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting usage record {Id}", id);
            return StatusCode(500, "An error occurred while deleting the usage record");
        }
    }

    /// <summary>
    /// Gets usage summary for an asset
    /// </summary>
    [HttpGet("asset/{assetId:guid}/summary")]
    public async Task<ActionResult<AssetUsageSummaryDto>> GetAssetUsageSummary(Guid assetId)
    {
        try
        {
            var summary = await _usageTrackingService.GetAssetUsageSummaryAsync(assetId);
            return Ok(summary);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting usage summary for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while getting the usage summary");
        }
    }

    /// <summary>
    /// Gets usage summaries for all assets
    /// </summary>
    [HttpGet("summaries")]
    public async Task<ActionResult<IEnumerable<AssetUsageSummaryDto>>> GetAllAssetUsageSummaries()
    {
        var summaries = await _usageTrackingService.GetAllAssetUsageSummariesAsync();
        return Ok(summaries);
    }

    /// <summary>
    /// Gets current mileage for an asset
    /// </summary>
    [HttpGet("asset/{assetId:guid}/mileage")]
    public async Task<ActionResult<decimal?>> GetCurrentMileage(Guid assetId)
    {
        var mileage = await _usageTrackingService.GetCurrentMileageAsync(assetId);
        return Ok(new { assetId, currentMileage = mileage });
    }

    /// <summary>
    /// Gets current operating hours for an asset
    /// </summary>
    [HttpGet("asset/{assetId:guid}/hours")]
    public async Task<ActionResult<decimal?>> GetCurrentOperatingHours(Guid assetId)
    {
        var hours = await _usageTrackingService.GetCurrentOperatingHoursAsync(assetId);
        return Ok(new { assetId, currentOperatingHours = hours });
    }

    /// <summary>
    /// Gets current cycles for an asset
    /// </summary>
    [HttpGet("asset/{assetId:guid}/cycles")]
    public async Task<ActionResult<int?>> GetCurrentCycles(Guid assetId)
    {
        var cycles = await _usageTrackingService.GetCurrentCyclesAsync(assetId);
        return Ok(new { assetId, currentCycles = cycles });
    }

    /// <summary>
    /// Gets average daily mileage for an asset
    /// </summary>
    [HttpGet("asset/{assetId:guid}/average-mileage")]
    public async Task<ActionResult<decimal?>> GetAverageDailyMileage(Guid assetId, [FromQuery] int days = 30)
    {
        var avgMileage = await _usageTrackingService.GetAverageDailyMileageAsync(assetId, days);
        return Ok(new { assetId, averageDailyMileage = avgMileage, days });
    }

    /// <summary>
    /// Gets average daily hours for an asset
    /// </summary>
    [HttpGet("asset/{assetId:guid}/average-hours")]
    public async Task<ActionResult<decimal?>> GetAverageDailyHours(Guid assetId, [FromQuery] int days = 30)
    {
        var avgHours = await _usageTrackingService.GetAverageDailyHoursAsync(assetId, days);
        return Ok(new { assetId, averageDailyHours = avgHours, days });
    }

    /// <summary>
    /// Imports usage data from external source
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult> ImportUsageData(
        [FromQuery] string dataSource,
        [FromBody] string externalData)
    {
        var success = await _usageTrackingService.ImportUsageDataFromSourceAsync(dataSource, externalData);
        if (success)
        {
            return Ok(new { message = "Usage data imported successfully" });
        }

        return BadRequest(new { message = "Failed to import usage data" });
    }

    /// <summary>
    /// Gets unvalidated usage records
    /// </summary>
    [HttpGet("unvalidated")]
    public async Task<ActionResult<IEnumerable<AssetUsageTrackingDto>>> GetUnvalidatedRecords()
    {
        var records = await _usageTrackingService.GetUnvalidatedRecordsAsync();
        return Ok(records);
    }

    /// <summary>
    /// Validates a usage record
    /// </summary>
    [HttpPost("{id:guid}/validate")]
    public async Task<IActionResult> ValidateUsageRecord(Guid id)
    {
        try
        {
            await _usageTrackingService.ValidateUsageRecordAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating usage record {Id}", id);
            return StatusCode(500, "An error occurred while validating the usage record");
        }
    }
}

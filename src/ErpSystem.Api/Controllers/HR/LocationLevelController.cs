using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Controller for managing location levels
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class LocationLevelController : ControllerBase
{
    private readonly ILocationLevelService _locationLevelService;
    private readonly ILogger<LocationLevelController> _logger;

    public LocationLevelController(
        ILocationLevelService locationLevelService,
        ILogger<LocationLevelController> logger)
    {
        _locationLevelService = locationLevelService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all location levels
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LocationLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _locationLevelService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all location levels");
            return StatusCode(500, "An error occurred while retrieving location levels");
        }
    }

    /// <summary>
    /// Retrieves location levels with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<LocationLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _locationLevelService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged location levels");
            return StatusCode(500, "An error occurred while retrieving location levels");
        }
    }

    /// <summary>
    /// Retrieves all location levels as summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<LocationLevelSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSummary()
    {
        try
        {
            var response = await _locationLevelService.GetAllSummaryAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location level summaries");
            return StatusCode(500, "An error occurred while retrieving location level summaries");
        }
    }

    /// <summary>
    /// Retrieves a location level by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LocationLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _locationLevelService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location level with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the location level");
        }
    }

    /// <summary>
    /// Retrieves detailed location level by ID
    /// </summary>
    [HttpGet("{id:guid}/detail")]
    [ProducesResponseType(typeof(LocationLevelDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailById(Guid id)
    {
        try
        {
            var response = await _locationLevelService.GetDetailByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location level detail with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the location level detail");
        }
    }

    /// <summary>
    /// Retrieves location levels by structure ID
    /// </summary>
    [HttpGet("structure/{structureId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<LocationLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStructureId(Guid structureId)
    {
        try
        {
            var response = await _locationLevelService.GetByStructureIdAsync(structureId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location levels for structure {StructureId}", structureId);
            return StatusCode(500, "An error occurred while retrieving location levels");
        }
    }

    /// <summary>
    /// Creates a new location level
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LocationLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLocationLevelDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _locationLevelService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location level");
            return StatusCode(500, "An error occurred while creating the location level");
        }
    }

    /// <summary>
    /// Updates an existing location level
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LocationLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLocationLevelDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _locationLevelService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location level with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the location level");
        }
    }

    /// <summary>
    /// Deletes a location level
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _locationLevelService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location level with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the location level");
        }
    }
}

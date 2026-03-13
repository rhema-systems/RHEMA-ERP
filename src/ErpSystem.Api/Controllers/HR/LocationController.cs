using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Controller for managing locations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationController : ControllerBase
{
    private readonly ILocationService _locationService;
    private readonly ILogger<LocationController> _logger;

    public LocationController(ILocationService locationService, ILogger<LocationController> logger)
    {
        _locationService = locationService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all locations
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _locationService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all locations");
            return StatusCode(500, "An error occurred while retrieving locations");
        }
    }

    /// <summary>
    /// Retrieves locations with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _locationService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged locations");
            return StatusCode(500, "An error occurred while retrieving locations");
        }
    }

    /// <summary>
    /// Retrieves all locations as summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<LocationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSummary()
    {
        try
        {
            var response = await _locationService.GetAllSummaryAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location summaries");
            return StatusCode(500, "An error occurred while retrieving location summaries");
        }
    }

    /// <summary>
    /// Retrieves a location by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _locationService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the location");
        }
    }

    /// <summary>
    /// Retrieves detailed location by ID
    /// </summary>
    [HttpGet("{id:guid}/detail")]
    [ProducesResponseType(typeof(LocationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailById(Guid id)
    {
        try
        {
            var response = await _locationService.GetDetailByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location detail with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the location detail");
        }
    }

    /// <summary>
    /// Retrieves locations by level ID
    /// </summary>
    [HttpGet("level/{levelId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByLevelId(Guid levelId)
    {
        try
        {
            var response = await _locationService.GetByLevelIdAsync(levelId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locations for level {LevelId}", levelId);
            return StatusCode(500, "An error occurred while retrieving locations");
        }
    }

    /// <summary>
    /// Retrieves locations by structure ID
    /// </summary>
    [HttpGet("structure/{structureId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStructureId(Guid structureId)
    {
        try
        {
            var response = await _locationService.GetByStructureIdAsync(structureId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locations for structure {StructureId}", structureId);
            return StatusCode(500, "An error occurred while retrieving locations");
        }
    }

    /// <summary>
    /// Retrieves child locations of a parent location
    /// </summary>
    [HttpGet("{parentLocationId:guid}/children")]
    [ProducesResponseType(typeof(IEnumerable<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildLocations(Guid parentLocationId)
    {
        try
        {
            var response = await _locationService.GetChildLocationsAsync(parentLocationId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving child locations for parent {ParentLocationId}", parentLocationId);
            return StatusCode(500, "An error occurred while retrieving child locations");
        }
    }

    /// <summary>
    /// Retrieves root locations for a structure
    /// </summary>
    [HttpGet("structure/{structureId:guid}/root")]
    [ProducesResponseType(typeof(IEnumerable<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRootLocations(Guid structureId)
    {
        try
        {
            var response = await _locationService.GetRootLocationsAsync(structureId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving root locations for structure {StructureId}", structureId);
            return StatusCode(500, "An error occurred while retrieving root locations");
        }
    }

    /// <summary>
    /// Retrieves full location hierarchy tree for a structure
    /// </summary>
    [HttpGet("structure/{structureId:guid}/hierarchy/tree")]
    [ProducesResponseType(typeof(IEnumerable<LocationTreeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHierarchyTree(Guid structureId)
    {
        try
        {
            var response = await _locationService.GetHierarchyTreeAsync(structureId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location hierarchy tree for structure {StructureId}", structureId);
            return StatusCode(500, "An error occurred while retrieving the location hierarchy tree");
        }
    }

    /// <summary>
    /// Retrieves hierarchy starting from a specific location
    /// </summary>
    [HttpGet("{locationId:guid}/hierarchy")]
    [ProducesResponseType(typeof(LocationHierarchyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHierarchyFromLocation(Guid locationId)
    {
        try
        {
            var response = await _locationService.GetHierarchyFromLocationAsync(locationId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hierarchy from location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while retrieving the location hierarchy");
        }
    }

    /// <summary>
    /// Retrieves locations by country
    /// </summary>
    [HttpGet("country/{countryId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCountry(Guid countryId)
    {
        try
        {
            var response = await _locationService.GetByCountryAsync(countryId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locations for country {CountryId}", countryId);
            return StatusCode(500, "An error occurred while retrieving locations");
        }
    }

    /// <summary>
    /// Searches locations by name, code, or city
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string searchTerm)
    {
        try
        {
            var response = await _locationService.SearchAsync(searchTerm);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching locations with term {SearchTerm}", searchTerm);
            return StatusCode(500, "An error occurred while searching locations");
        }
    }

    /// <summary>
    /// Creates a new location
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLocationDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _locationService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            return StatusCode(500, "An error occurred while creating the location");
        }
    }

    /// <summary>
    /// Updates an existing location
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLocationDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _locationService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the location");
        }
    }

    /// <summary>
    /// Deletes a location
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _locationService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the location");
        }
    }

    /// <summary>
    /// Moves a location to a new parent
    /// </summary>
    [HttpPost("{locationId:guid}/move")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MoveLocation(Guid locationId, [FromBody] MoveLocationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _locationService.MoveLocationAsync(locationId, request.NewParentId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while moving the location");
        }
    }
}

/// <summary>
/// Request model for moving a location
/// </summary>
public class MoveLocationRequest
{
    public Guid? NewParentId { get; set; }
}

using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Controller for managing location contacts
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationContactController : ControllerBase
{
    private readonly ILocationContactService _locationContactService;
    private readonly ILogger<LocationContactController> _logger;

    public LocationContactController(
        ILocationContactService locationContactService,
        ILogger<LocationContactController> logger)
    {
        _locationContactService = locationContactService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all location contacts
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LocationContactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _locationContactService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all location contacts");
            return StatusCode(500, "An error occurred while retrieving location contacts");
        }
    }

    /// <summary>
    /// Retrieves location contacts with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<LocationContactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _locationContactService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged location contacts");
            return StatusCode(500, "An error occurred while retrieving location contacts");
        }
    }

    /// <summary>
    /// Retrieves a location contact by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LocationContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _locationContactService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location contact with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the location contact");
        }
    }

    /// <summary>
    /// Retrieves detailed location contact by ID
    /// </summary>
    [HttpGet("{id:guid}/detail")]
    [ProducesResponseType(typeof(LocationContactDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailById(Guid id)
    {
        try
        {
            var response = await _locationContactService.GetDetailByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location contact detail with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the location contact detail");
        }
    }

    /// <summary>
    /// Retrieves all contacts for a specific location
    /// </summary>
    [HttpGet("location/{locationId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<LocationContactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByLocationId(Guid locationId)
    {
        try
        {
            var response = await _locationContactService.GetByLocationIdAsync(locationId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contacts for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while retrieving location contacts");
        }
    }

    /// <summary>
    /// Retrieves all contacts for a location as summary
    /// </summary>
    [HttpGet("location/{locationId:guid}/summary")]
    [ProducesResponseType(typeof(IEnumerable<LocationContactSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaryByLocationId(Guid locationId)
    {
        try
        {
            var response = await _locationContactService.GetSummaryByLocationIdAsync(locationId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contact summaries for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while retrieving location contact summaries");
        }
    }

    /// <summary>
    /// Retrieves the primary contact for a location
    /// </summary>
    [HttpGet("location/{locationId:guid}/primary")]
    [ProducesResponseType(typeof(LocationContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPrimaryContact(Guid locationId)
    {
        try
        {
            var response = await _locationContactService.GetPrimaryContactAsync(locationId);
            if (response == null)
                return NotFound(new { message = "No primary contact found for this location" });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving primary contact for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while retrieving the primary contact");
        }
    }

    /// <summary>
    /// Creates a new location contact
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LocationContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLocationContactDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _locationContactService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location contact");
            return StatusCode(500, "An error occurred while creating the location contact");
        }
    }

    /// <summary>
    /// Updates an existing location contact
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LocationContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLocationContactDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _locationContactService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location contact with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the location contact");
        }
    }

    /// <summary>
    /// Deletes a location contact
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _locationContactService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location contact with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the location contact");
        }
    }

    /// <summary>
    /// Sets a contact as primary for its location
    /// </summary>
    [HttpPatch("{id:guid}/set-primary")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAsPrimary(Guid id)
    {
        try
        {
            var response = await _locationContactService.SetAsPrimaryAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting location contact as primary with ID {Id}", id);
            return StatusCode(500, "An error occurred while setting the contact as primary");
        }
    }
}

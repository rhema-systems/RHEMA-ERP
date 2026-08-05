using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/hr/[controller]")]
[Authorize]
public class IdentificationTypesController : ControllerBase
{
    private readonly IIdentificationTypeService _identificationTypeService;
    private readonly ILogger<IdentificationTypesController> _logger;

    public IdentificationTypesController(
        IIdentificationTypeService identificationTypeService,
        ILogger<IdentificationTypesController> logger)
    {
        _identificationTypeService = identificationTypeService;
        _logger = logger;
    }

    /// <summary>
    /// Get all identification types
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<IdentificationTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _identificationTypeService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all identification types");
            return StatusCode(500, "An error occurred while retrieving identification types");
        }
    }

    /// <summary>
    /// Get active identification types only
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<IdentificationTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive()
    {
        try
        {
            var response = await _identificationTypeService.GetActiveAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active identification types");
            return StatusCode(500, "An error occurred while retrieving identification types");
        }
    }

    /// <summary>
    /// Get identification types with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<IdentificationTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _identificationTypeService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged identification types");
            return StatusCode(500, "An error occurred while retrieving identification types");
        }
    }

    /// <summary>
    /// Get identification type by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IdentificationTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _identificationTypeService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving identification type with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the identification type");
        }
    }

    /// <summary>
    /// Create a new identification type
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(IdentificationTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateIdentificationTypeDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _identificationTypeService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating identification type");
            return StatusCode(500, "An error occurred while creating the identification type");
        }
    }

    /// <summary>
    /// Update an existing identification type
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(IdentificationTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIdentificationTypeDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != updateDto.Id)
            {
                return BadRequest(new { message = "ID in URL does not match ID in request body" });
            }

            var response = await _identificationTypeService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating identification type with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the identification type");
        }
    }

    /// <summary>
    /// Delete an identification type
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _identificationTypeService.DeleteAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting identification type with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the identification type");
        }
    }

    /// <summary>
    /// Activate an identification type
    /// </summary>
    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id)
    {
        try
        {
            await _identificationTypeService.ActivateAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating identification type with ID {Id}", id);
            return StatusCode(500, "An error occurred while activating the identification type");
        }
    }

    /// <summary>
    /// Deactivate an identification type
    /// </summary>
    [HttpPut("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        try
        {
            await _identificationTypeService.DeactivateAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating identification type with ID {Id}", id);
            return StatusCode(500, "An error occurred while deactivating the identification type");
        }
    }
}

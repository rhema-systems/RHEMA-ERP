using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PositionCriteriaMappingsController : ControllerBase
{
    private readonly IPositionCriteriaMappingService _criteriaMappingService;
    private ILogger<PositionCriteriaMappingsController> _logger;

    public PositionCriteriaMappingsController(IPositionCriteriaMappingService service, IPositionCriteriaMappingService criteriaMappingService, ILogger<PositionCriteriaMappingsController> logger)
    {
        _criteriaMappingService = criteriaMappingService;
        _logger = logger;
    }

    /// <summary>
    /// Get all position criteria mappings
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PositionCriteriaMappingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _criteriaMappingService.GetAllAsync(cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all position criteria mappings");
            return StatusCode(500, "An error occurred while retrieving position criteria mappings");
        }
    }

    /// <summary>
    /// Get position criteria mappings with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<PositionCriteriaMappingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _criteriaMappingService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving position criteria mappings");
            return StatusCode(500, "An error occurred while retrieving position criteria mappings");
        }
    }

    /// <summary>
    /// Get position criteria mapping by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PositionCriteriaMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _criteriaMappingService.GetByIdAsync(id, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving position criteria mapping with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the position criteria mapping");
        }
    }

    /// <summary>
    /// Get mappings by position ID
    /// </summary>
    [HttpGet("position/{positionId}")]
    [ProducesResponseType(typeof(IEnumerable<PositionCriteriaMappingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPositionId(Guid positionId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _criteriaMappingService.GetByPositionIdAsync(positionId, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving mappings for position {PositionId}", positionId);
            return StatusCode(500, "An error occurred while retrieving position criteria mappings");
        }
    }

    /// <summary>
    /// Get mappings by department ID
    /// </summary>
    [HttpGet("department/{departmentId}")]
    [ProducesResponseType(typeof(IEnumerable<PositionCriteriaMappingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDepartmentId(Guid departmentId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _criteriaMappingService.GetByDepartmentIdAsync(departmentId, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving mappings for department {DepartmentId}", departmentId);
            return StatusCode(500, "An error occurred while retrieving position criteria mappings");
        }
    }

    /// <summary>
    /// Create a new position criteria mapping
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PositionCriteriaMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePositionCriteriaMappingDto createDto, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _criteriaMappingService.CreateAsync(createDto, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch(InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating position criteria mapping");
            return StatusCode(500, "An error occurred while creating the position criteria mapping");
        }
    }

    /// <summary>
    /// Update an existing position criteria mapping
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(PositionCriteriaMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePositionCriteriaMappingDto updateDto, CancellationToken cancellationToken)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _criteriaMappingService.UpdateAsync(updateDto, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch(InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating position criteria mapping with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the position criteria mapping");
        }
    }

    /// <summary>
    /// Delete a position criteria mapping
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _criteriaMappingService.DeleteAsync(id, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting position criteria mapping with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the position criteria mapping");
        }
    }

    #region Grade Range Operations

    /// <summary>
    /// Add a grade range to a mapping
    /// </summary>
    [HttpPost("{mappingId}/grade-ranges")]
    [ProducesResponseType(typeof(MappingGradeRangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddGradeRange(Guid mappingId, [FromBody] CreateMappingGradeRangeDto createDto, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _criteriaMappingService.AddGradeRangeAsync(mappingId, createDto, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch(InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding grade range to mapping {MappingId}", mappingId);
            return StatusCode(500, "An error occurred while adding the grade range");
        }
    }

    /// <summary>
    /// Get all grade ranges for a mapping
    /// </summary>
    [HttpGet("{mappingId}/grade-ranges")]
    [ProducesResponseType(typeof(IEnumerable<MappingGradeRangeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGradeRanges(Guid mappingId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _criteriaMappingService.GetGradeRangesAsync(mappingId, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving grade ranges for mapping {MappingId}", mappingId);
            return StatusCode(500, "An error occurred while retrieving grade ranges");
        }
    }

    /// <summary>
    /// Update a grade range
    /// </summary>
    [HttpPut("{mappingId}/grade-ranges/{rangeId}")]
    [ProducesResponseType(typeof(MappingGradeRangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGradeRange(Guid mappingId, Guid rangeId, [FromBody] UpdateMappingGradeRangeDto updateDto, CancellationToken cancellationToken)
    {
        try
        {
            if (rangeId != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _criteriaMappingService.UpdateGradeRangeAsync(mappingId, updateDto, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch(InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating grade range {RangeId} for mapping {MappingId}", updateDto.Id, mappingId);
            return StatusCode(500, "An error occurred while updating the grade range");
        }
    }

    /// <summary>
    /// Delete a grade range
    /// </summary>
    [HttpDelete("{mappingId}/grade-ranges/{rangeId}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGradeRange(Guid mappingId, Guid rangeId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _criteriaMappingService.DeleteGradeRangeAsync(mappingId, rangeId, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting grade range {RangeId} for mapping {MappingId}", rangeId, mappingId);
            return StatusCode(500, "An error occurred while deleting the grade range");
        }
    }

    #endregion
}

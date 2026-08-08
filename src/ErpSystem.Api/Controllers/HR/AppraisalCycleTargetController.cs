using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppraisalCycleTargetController : ControllerBase
{
    private readonly IAppraisalCycleTargetService _targetService;
    private readonly ILogger<AppraisalCycleTargetController> _logger;

    public AppraisalCycleTargetController(IAppraisalCycleTargetService targetService, ILogger<AppraisalCycleTargetController> logger)
    {
        _targetService = targetService;
        _logger = logger;
    }

    /// <summary>
    /// Get appraisal cycle target by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalCycleTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _targetService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal cycle target with {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the appraisal cycle target");
        }
    }

    /// <summary>
    /// Get appraisal cycle targets by cycle ID
    /// </summary>
    [HttpGet("cycle/{cycleId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleTargetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCycleId(Guid cycleId)
    {
        try
        {
            var response = await _targetService.GetByCycleIdAsync(cycleId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal cycle targets for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving appraisal cycle targets");
        }
    }

    /// <summary>
    /// Get appraisal cycle targets by target type
    /// </summary>
    [HttpGet("type/{targetType}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleTargetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTargetType(AppraisalTargetType targetType)
    {
        try
        {
            var response = await _targetService.GetByTargetTypeAsync(targetType);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal cycle targets by type {TargetType}", targetType);
            return StatusCode(500, "An error occurred while retrieving appraisal cycle targets");
        }
    }

    /// <summary>
    /// Create a new appraisal cycle target
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalCycleTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalCycleTargetDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _targetService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal cycle target");
            return StatusCode(500, "An error occurred while creating the appraisal cycle target");
        }
    }

    /// <summary>
    /// Update an existing appraisal cycle target
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalCycleTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalCycleTargetDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _targetService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal cycle target with Id {TargetId}", id);
            return StatusCode(500, "An error occurred while updating the appraisal cycle target");
        }
    }

    /// <summary>
    /// Validate an appraisal cycle target
    /// </summary>
    [HttpGet("{id:guid}/validate")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateTarget(Guid id)
    {
        try
        {
            var response = await _targetService.ValidateTargetAsync(id);
            return Ok(new { isValid = response, message = response ? "Target is valid" : "Target has invalid configuration" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating appraisal cycle target with {Id}", id);
            return StatusCode(500, "An error occurred while validating the target");
        }
    }

    /// <summary>
    /// Delete an appraisal cycle target
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _targetService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting appraisal cycle target with Id {TargetId}", id);
            return StatusCode(500, "An error occurred while deleting the appraisal cycle target");
        }
    }

    #region Exclusion Operations

    /// <summary>Get all exclusions for a target</summary>
    [HttpGet("{targetId:guid}/exclusions")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleTargetExclusionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExclusions(Guid targetId)
    {
        try
        {
            var response = await _targetService.GetExclusionsAsync(targetId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving exclusions for target {TargetId}", targetId);
            return StatusCode(500, "An error occurred while retrieving exclusions");
        }
    }

    /// <summary>Add an exclusion to a target</summary>
    [HttpPost("{targetId:guid}/exclusions")]
    [ProducesResponseType(typeof(AppraisalCycleTargetExclusionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddExclusion(Guid targetId, [FromBody] CreateAppraisalCycleTargetExclusionDto dto)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.AppraisalCycleTargetId = targetId;
            var response = await _targetService.AddExclusionAsync(targetId, dto);
            return Ok(response);
        }
        catch (ArgumentException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding exclusion to target {TargetId}", targetId);
            return StatusCode(500, "An error occurred while adding the exclusion");
        }
    }

    /// <summary>Update an exclusion</summary>
    [HttpPut("{targetId:guid}/exclusions/{exclusionId:guid}")]
    [ProducesResponseType(typeof(AppraisalCycleTargetExclusionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateExclusion(Guid targetId, Guid exclusionId, [FromBody] UpdateAppraisalCycleTargetExclusionDto dto)
    {
        try
        {
            if (exclusionId != dto.Id) return BadRequest("ID mismatch");
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.AppraisalCycleTargetId = targetId;
            var response = await _targetService.UpdateExclusionAsync(targetId, dto);
            return Ok(response);
        }
        catch (ArgumentException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating exclusion {ExclusionId} on target {TargetId}", exclusionId, targetId);
            return StatusCode(500, "An error occurred while updating the exclusion");
        }
    }

    /// <summary>Remove an exclusion</summary>
    [HttpDelete("{targetId:guid}/exclusions/{exclusionId:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveExclusion(Guid targetId, Guid exclusionId)
    {
        try
        {
            var response = await _targetService.RemoveExclusionAsync(targetId, exclusionId);
            return Ok(response);
        }
        catch (ArgumentException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing exclusion {ExclusionId} from target {TargetId}", exclusionId, targetId);
            return StatusCode(500, "An error occurred while removing the exclusion");
        }
    }

    #endregion
}

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

    public PositionCriteriaMappingsController(IPositionCriteriaMappingService service, IPositionCriteriaMappingService criteriaMappingService)
    {
        _criteriaMappingService = criteriaMappingService;
    }

    /// <summary>
    /// Get all position criteria mappings
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PositionCriteriaMappingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var response = await _criteriaMappingService.GetAllAsync(cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Get position criteria mapping by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PositionCriteriaMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _criteriaMappingService.GetByIdAsync(id, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Get mappings by position ID
    /// </summary>
    [HttpGet("position/{positionId}")]
    [ProducesResponseType(typeof(IEnumerable<PositionCriteriaMappingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPositionId(Guid positionId, CancellationToken cancellationToken)
    {
        var response = await _criteriaMappingService.GetByPositionIdAsync(positionId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Get mappings by department ID
    /// </summary>
    [HttpGet("department/{departmentId}")]
    [ProducesResponseType(typeof(IEnumerable<PositionCriteriaMappingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDepartmentId(Guid departmentId, CancellationToken cancellationToken)
    {
        var response = await _criteriaMappingService.GetByDepartmentIdAsync(departmentId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Create a new position criteria mapping
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PositionCriteriaMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePositionCriteriaMappingDto createDto, CancellationToken cancellationToken)
    {
        var response = await _criteriaMappingService.CreateAsync(createDto, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Update an existing position criteria mapping
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(PositionCriteriaMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePositionCriteriaMappingDto updateDto, CancellationToken cancellationToken)
    {
        if (id != updateDto.Id)
        {
            return BadRequest("ID mismatch");
        }

        var response = await _criteriaMappingService.UpdateAsync(updateDto, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Delete a position criteria mapping
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var response = await _criteriaMappingService.DeleteAsync(id, cancellationToken);
        return Ok(response);
    }

    #region Grade Range Operations

    /// <summary>
    /// Add a grade range to a mapping
    /// </summary>
    [HttpPost("{mappingId}/grade-ranges")]
    [ProducesResponseType(typeof(MappingGradeRangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddGradeRange(Guid mappingId, [FromBody] CreateMappingGradeRangeDto createDto, CancellationToken cancellationToken)
    {
        var response = await _criteriaMappingService.AddGradeRangeAsync(mappingId, createDto, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Get all grade ranges for a mapping
    /// </summary>
    [HttpGet("{mappingId}/grade-ranges")]
    [ProducesResponseType(typeof(IEnumerable<MappingGradeRangeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGradeRanges(Guid mappingId, CancellationToken cancellationToken)
    {
        var response = await _criteriaMappingService.GetGradeRangesAsync(mappingId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Update a grade range
    /// </summary>
    [HttpPut("{mappingId}/grade-ranges/{rangeId}")]
    [ProducesResponseType(typeof(MappingGradeRangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateGradeRange(Guid mappingId, Guid rangeId, [FromBody] UpdateMappingGradeRangeDto updateDto, CancellationToken cancellationToken)
    {
        if (rangeId != updateDto.Id)
        {
            return BadRequest("ID mismatch");
        }

        var response = await _criteriaMappingService.UpdateGradeRangeAsync(mappingId, updateDto, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Delete a grade range
    /// </summary>
    [HttpDelete("{mappingId}/grade-ranges/{rangeId}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteGradeRange(Guid mappingId, Guid rangeId, CancellationToken cancellationToken)
    {
        var response = await _criteriaMappingService.DeleteGradeRangeAsync(mappingId, rangeId, cancellationToken);
        return Ok(response);
    }

    #endregion
}

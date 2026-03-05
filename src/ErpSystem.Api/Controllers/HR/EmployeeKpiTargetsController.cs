using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeeKpiTargetsController : ControllerBase
{
    private readonly IEmployeeKpiTargetService _kpiTargetService;
    private ILogger<EmployeeKpiTargetsController> _logger;

    public EmployeeKpiTargetsController(IEmployeeKpiTargetService kpiTargetService, ILogger<EmployeeKpiTargetsController> logger)
    {
        _kpiTargetService = kpiTargetService;
        _logger = logger;
    }

    /// <summary>
    /// Get KPI target by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EmployeeKpiTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kpiTargetService.GetByIdAsync(id, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee KPI target with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the KPI target");
        }
    }

    /// <summary>
    /// Get KPI targets by employee ID
    /// </summary>
    [HttpGet("employee/{employeeId}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeKpiTargetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployeeId(Guid employeeId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kpiTargetService.GetByEmployeeIdAsync(employeeId, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving KPI targets for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving KPI targets");
        }
    }

    /// <summary>
    /// Get KPI targets by period
    /// </summary>
    [HttpGet("period")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeKpiTargetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPeriod([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kpiTargetService.GetByPeriodAsync(startDate, endDate, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving KPI targets for period {StartDate} to {EndDate}", startDate, endDate);
            return StatusCode(500, "An error occurred while retrieving KPI targets");
        }
    }

    /// <summary>
    /// Create a new KPI target
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeKpiTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeKpiTargetDto createDto, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _kpiTargetService.CreateAsync(createDto, cancellationToken);
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
            _logger.LogError(ex, "Error creating employee KPI target");
            return StatusCode(500, "An error occurred while creating the KPI target");
        }
    }

    /// <summary>
    /// Update an existing KPI target
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(EmployeeKpiTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeKpiTargetDto updateDto, CancellationToken cancellationToken)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _kpiTargetService.UpdateAsync(updateDto, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee KPI target with ID {Id}", updateDto.Id);
            return StatusCode(500, "An error occurred while updating the KPI target");
        }
    }

    /// <summary>
    /// Delete a KPI target
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kpiTargetService.DeleteAsync(id, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee KPI target with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the KPI target");
        }
    }

    #region Evaluation Record Operations

    /// <summary>
    /// Add an evaluation record to a KPI target
    /// </summary>
    [HttpPost("{targetId}/evaluations")]
    [ProducesResponseType(typeof(KpiEvaluationRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEvaluationRecord(Guid targetId, [FromBody] CreateKpiEvaluationRecordDto createDto, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _kpiTargetService.AddEvaluationRecordAsync(targetId, createDto, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding evaluation record to KPI target {TargetId}", targetId);
            return StatusCode(500, "An error occurred while adding the evaluation record");
        }
    }

    /// <summary>
    /// Get all evaluation records for a KPI target
    /// </summary>
    [HttpGet("{targetId}/evaluations")]
    [ProducesResponseType(typeof(IEnumerable<KpiEvaluationRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvaluationRecords(Guid targetId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kpiTargetService.GetEvaluationRecordsAsync(targetId, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluation records for KPI target {TargetId}", targetId);
            return StatusCode(500, "An error occurred while retrieving evaluation records");
        }
    }

    /// <summary>
    /// Get final evaluation for a KPI target
    /// </summary>
    [HttpGet("{targetId}/evaluations/final")]
    [ProducesResponseType(typeof(KpiEvaluationRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFinalEvaluation(Guid targetId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kpiTargetService.GetFinalEvaluationAsync(targetId, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving final evaluation for KPI target {TargetId}", targetId);
            return StatusCode(500, "An error occurred while retrieving the final evaluation");
        }
    }

    /// <summary>
    /// Update an evaluation record
    /// </summary>
    [HttpPut("{targetId}/evaluations/{recordId}")]
    [ProducesResponseType(typeof(KpiEvaluationRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvaluationRecord(Guid targetId, Guid recordId, [FromBody] UpdateKpiEvaluationRecordDto updateDto, CancellationToken cancellationToken)
    {
        try
        {
            if (recordId != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _kpiTargetService.UpdateEvaluationRecordAsync(targetId, updateDto, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating evaluation record {RecordId} for KPI target {TargetId}", updateDto.Id, targetId);
            return StatusCode(500, "An error occurred while updating the evaluation record");
        }
    }

    /// <summary>
    /// Delete an evaluation record
    /// </summary>
    [HttpDelete("{targetId}/evaluations/{recordId}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvaluationRecord(Guid targetId, Guid recordId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kpiTargetService.DeleteEvaluationRecordAsync(targetId, recordId, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting evaluation record {RecordId} for KPI target {TargetId}", recordId, targetId);
            return StatusCode(500, "An error occurred while deleting the evaluation record");
        }
    }

    #endregion
}

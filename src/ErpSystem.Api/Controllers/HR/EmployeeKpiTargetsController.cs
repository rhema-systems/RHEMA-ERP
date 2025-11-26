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

    public EmployeeKpiTargetsController(IEmployeeKpiTargetService kpiTargetService)
    {
        _kpiTargetService = kpiTargetService;
    }

    /// <summary>
    /// Get KPI target by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EmployeeKpiTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _kpiTargetService.GetByIdAsync(id, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Get KPI targets by employee ID
    /// </summary>
    [HttpGet("employee/{employeeId}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeKpiTargetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployeeId(Guid employeeId, CancellationToken cancellationToken)
    {
        var response = await _kpiTargetService.GetByEmployeeIdAsync(employeeId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Get KPI targets by period
    /// </summary>
    [HttpGet("period")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeKpiTargetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPeriod([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
    {
        var response = await _kpiTargetService.GetByPeriodAsync(startDate, endDate, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Create a new KPI target
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeKpiTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeKpiTargetDto createDto, CancellationToken cancellationToken)
    {
        var response = await _kpiTargetService.CreateAsync(createDto, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Update an existing KPI target
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(EmployeeKpiTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeKpiTargetDto updateDto, CancellationToken cancellationToken)
    {
        if (id != updateDto.Id)
        {
            return BadRequest("ID mismatch");
        }

        var response = await _kpiTargetService.UpdateAsync(updateDto, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Delete a KPI target
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var response = await _kpiTargetService.DeleteAsync(id, cancellationToken);
        return Ok(response);
    }

    #region Evaluation Record Operations

    /// <summary>
    /// Add an evaluation record to a KPI target
    /// </summary>
    [HttpPost("{targetId}/evaluations")]
    [ProducesResponseType(typeof(KpiEvaluationRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddEvaluationRecord(Guid targetId, [FromBody] CreateKpiEvaluationRecordDto createDto, CancellationToken cancellationToken)
    {
        var response = await _kpiTargetService.AddEvaluationRecordAsync(targetId, createDto, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Get all evaluation records for a KPI target
    /// </summary>
    [HttpGet("{targetId}/evaluations")]
    [ProducesResponseType(typeof(IEnumerable<KpiEvaluationRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvaluationRecords(Guid targetId, CancellationToken cancellationToken)
    {
        var response = await _kpiTargetService.GetEvaluationRecordsAsync(targetId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Get final evaluation for a KPI target
    /// </summary>
    [HttpGet("{targetId}/evaluations/final")]
    [ProducesResponseType(typeof(KpiEvaluationRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFinalEvaluation(Guid targetId, CancellationToken cancellationToken)
    {
        var response = await _kpiTargetService.GetFinalEvaluationAsync(targetId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Update an evaluation record
    /// </summary>
    [HttpPut("{targetId}/evaluations/{recordId}")]
    [ProducesResponseType(typeof(KpiEvaluationRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateEvaluationRecord(Guid targetId, Guid recordId, [FromBody] UpdateKpiEvaluationRecordDto updateDto, CancellationToken cancellationToken)
    {
        if (recordId != updateDto.Id)
        {
            return BadRequest("ID mismatch");
        }

        var response = await _kpiTargetService.UpdateEvaluationRecordAsync(targetId, updateDto, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Delete an evaluation record
    /// </summary>
    [HttpDelete("{targetId}/evaluations/{recordId}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteEvaluationRecord(Guid targetId, Guid recordId, CancellationToken cancellationToken)
    {
        var response = await _kpiTargetService.DeleteEvaluationRecordAsync(targetId, recordId, cancellationToken);
        return Ok(response);
    }

    #endregion
}

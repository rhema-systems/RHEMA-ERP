using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PerformanceAppraisalsController : ControllerBase
{
    private readonly IPerformanceAppraisalService _appraisalService;
    private readonly ILogger<PerformanceAppraisalsController> _logger;

    public PerformanceAppraisalsController(IPerformanceAppraisalService appraisalService, ILogger<PerformanceAppraisalsController> logger)
    {
        _appraisalService = appraisalService;
        _logger = logger;
    }

    /// <summary>
    /// Get all performance appraisals
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _appraisalService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisals with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _appraisalService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisal by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceAppraisalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _appraisalService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisal with {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the performance appraisal");
        }
    }

    /// <summary>
    /// Get performance appraisals by employee ID
    /// </summary>
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployeeId(Guid employeeId)
    {
        try
        {
            var response = await _appraisalService.GetByEmployeeIdAsync(employeeId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisals by year
    /// </summary>
    [HttpGet("year/{year}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByYear(int year)
    {
        try
        {
            var response = await _appraisalService.GetByYearAsync(year);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisals by status
    /// </summary>
    [HttpGet("status/{status}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStatus(AppraisalStatus status)
    {
        try
        {
            var response = await _appraisalService.GetByStatusAsync(status);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Create a new performance appraisal
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PerformanceAppraisalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePerformanceAppraisalDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating performance appraisal");
            return StatusCode(500, "An error occurred while creating the performance appraisal");
        }
    }

    /// <summary>
    /// Update an existing performance appraisal
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceAppraisalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePerformanceAppraisalDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while updating the performance appraisal");
        }
    }

    /// <summary>
    /// Update appraisal status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAppraisalStatusDto statusDto)
    {
        try
        {
            if (id != statusDto.AppraisalId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.UpdateStatusAsync(statusDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while updating the performance appraisal");
        }
    }

    /// <summary>
    /// Calculate overall score for an appraisal
    /// </summary>
    [HttpPost("{id:guid}/calculate-score")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CalculateOverallScore(Guid id)
    {
        try
        {
            var response = await _appraisalService.CalculateOverallScoreAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating overall score for appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while calculating the overall score for the appraisal");
        }
    }

    /// <summary>
    /// File an appeal for an appraisal
    /// </summary>
    [HttpPost("{id:guid}/appeal")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FileAppeal(Guid id, [FromBody] FileAppraisalAppealDto appealDto)
    {
        try
        {
            if (id != appealDto.AppraisalId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.FileAppealAsync(appealDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filing appraisal appeal for appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while filing appraisal appeal");
        }
    }

    /// <summary>
    /// Resolve an appraisal appeal
    /// </summary>
    [HttpPost("{id:guid}/appeal/resolve")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResolveAppeal(Guid id, [FromBody] ResolveAppraisalAppealDto resolveDto)
    {
        try
        {
            if (id != resolveDto.AppraisalId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.ResolveAppealAsync(resolveDto);
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
            _logger.LogError(ex, "Error resolving appraisal appeal for appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while resolving appraisal appeal");
        }
    }

    /// <summary>
    /// Delete a performance appraisal
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _appraisalService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting performance appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while deleting the performance appraisal");
        }
    }

    #region Evaluator Evaluation Operations

    /// <summary>
    /// Add an evaluator evaluation to an appraisal
    /// </summary>
    [HttpPost("{appraisalId}/evaluations")]
    [ProducesResponseType(typeof(EvaluatorEvaluationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEvaluatorEvaluation(Guid appraisalId, [FromBody] CreateEvaluatorEvaluationDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.AddEvaluatorEvaluationAsync(appraisalId, createDto);
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
            _logger.LogError(ex, "Error adding evaluator evaluation to appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while adding the evaluator evaluation");
        }
    }

    /// <summary>
    /// Get all evaluator evaluations for an appraisal
    /// </summary>
    [HttpGet("{appraisalId}/evaluations")]
    [ProducesResponseType(typeof(IEnumerable<EvaluatorEvaluationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvaluatorEvaluations(Guid appraisalId)
    {
        try
        {
            var response = await _appraisalService.GetEvaluatorEvaluationsAsync(appraisalId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluator evaluations for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving evaluator evaluations");
        }
    }

    /// <summary>
    /// Update an evaluator evaluation
    /// </summary>
    [HttpPut("{appraisalId}/evaluations/{evaluationId}")]
    [ProducesResponseType(typeof(EvaluatorEvaluationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvaluatorEvaluation(Guid appraisalId, Guid evaluationId, [FromBody] UpdateEvaluatorEvaluationDto updateDto)
    {
        try
        {
            if (evaluationId != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.UpdateEvaluatorEvaluationAsync(appraisalId, updateDto);
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
            _logger.LogError(ex, "Error updating evaluator evaluation {EvaluationId} for appraisal {AppraisalId}", updateDto.Id, appraisalId);
            return StatusCode(500, "An error occurred while updating the evaluator evaluation");
        }
    }

    /// <summary>
    /// Delete an evaluator evaluation
    /// </summary>
    [HttpDelete("{appraisalId}/evaluations/{evaluationId}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvaluatorEvaluation(Guid appraisalId, Guid evaluationId)
    {
        try
        {
            var response = await _appraisalService.DeleteEvaluatorEvaluationAsync(appraisalId, evaluationId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting evaluator evaluation {EvaluationId} for appraisal {AppraisalId}", evaluationId, appraisalId);
            return StatusCode(500, "An error occurred while deleting the evaluator evaluation");
        }
    }

    #endregion

    #region Criterion Score Operations

    /// <summary>
    /// Add a criterion score to an evaluator evaluation
    /// </summary>
    [HttpPost("evaluations/{evaluationId}/scores")]
    [ProducesResponseType(typeof(CriterionScoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddCriterionScore(Guid evaluationId, [FromBody] CreateCriterionScoreDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.AddCriterionScoreAsync(evaluationId, createDto);
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
            _logger.LogError(ex, "Error adding criterion score to evaluation {EvaluationId}", evaluationId);
            return StatusCode(500, "An error occurred while adding the criterion score");
        }
    }

    /// <summary>
    /// Get all criterion scores for an evaluator evaluation
    /// </summary>
    [HttpGet("evaluations/{evaluationId}/scores")]
    [ProducesResponseType(typeof(IEnumerable<CriterionScoreDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCriterionScores(Guid evaluationId)
    {
        try
        {
            var response = await _appraisalService.GetCriterionScoresAsync(evaluationId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving criterion scores for evaluation {EvaluationId}", evaluationId);
            return StatusCode(500, "An error occurred while retrieving criterion scores");
        }
    }

    /// <summary>
    /// Update a criterion score
    /// </summary>
    [HttpPut("evaluations/{evaluationId}/scores/{scoreId}")]
    [ProducesResponseType(typeof(CriterionScoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCriterionScore(Guid evaluationId, Guid scoreId, [FromBody] UpdateCriterionScoreDto updateDto)
    {
        try
        {
            if (scoreId != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.UpdateCriterionScoreAsync(evaluationId, updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating criterion score {ScoreId} for evaluation {EvaluationId}", updateDto.Id, evaluationId);
            return StatusCode(500, "An error occurred while updating the criterion score");
        }
    }

    /// <summary>
    /// Delete a criterion score
    /// </summary>
    [HttpDelete("evaluations/{evaluationId}/scores/{scoreId}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCriterionScore(Guid evaluationId, Guid scoreId)
    {
        try
        {
            var response = await _appraisalService.DeleteCriterionScoreAsync(evaluationId, scoreId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting criterion score {ScoreId} for evaluation {EvaluationId}", scoreId, evaluationId);
            return StatusCode(500, "An error occurred while deleting the criterion score");
        }
    }

    #endregion

    #region Employee Response Operations

    /// <summary>
    /// Add an employee response to an appraisal
    /// </summary>
    [HttpPost("{appraisalId}/responses")]
    [ProducesResponseType(typeof(AppraisalEmployeeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEmployeeResponse(Guid appraisalId, [FromBody] CreateAppraisalEmployeeResponseDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.AddEmployeeResponseAsync(appraisalId, createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding employee response to appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while adding the employee response");
        }
    }

    /// <summary>
    /// Get all employee responses for an appraisal
    /// </summary>
    [HttpGet("{appraisalId}/responses")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalEmployeeResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployeeResponses(Guid appraisalId)
    {
        try
        {
            var response = await _appraisalService.GetEmployeeResponsesAsync(appraisalId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee responses for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving employee responses");
        }
    }

    #endregion

    #region Attachment Operations

    /// <summary>
    /// Add an attachment to an appraisal
    /// </summary>
    [HttpPost("{appraisalId}/attachments")]
    [ProducesResponseType(typeof(AppraisalAttachmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AddAttachment(Guid appraisalId, [FromBody] CreateAppraisalAttachmentDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.AddAttachmentAsync(appraisalId, createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding attachment to appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while adding the attachment");
        }
    }

    /// <summary>
    /// Get all attachments for an appraisal
    /// </summary>
    [HttpGet("{appraisalId}/attachments")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalAttachmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttachments(Guid appraisalId)
    {
        try
        {
            var response = await _appraisalService.GetAttachmentsAsync(appraisalId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving attachments");
        }
    }

    #endregion
}

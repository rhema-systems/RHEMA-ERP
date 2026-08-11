using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Manages the recruitment application pipeline.
/// Provides endpoints for moving applications between stages, querying the
/// lightweight overview bar, and the paginated per-stage application list.
/// </summary>
[ApiController]
[Route("api/applications")]
[Authorize(Roles = ApplicationPipelineController.HrRoles)]
public class ApplicationPipelineController : ControllerBase
{
    // HR-only for the same reason as JobApplicationController: every board column, stage list and
    // scoring run on this controller carries candidate names and scores. The endpoints already map
    // their own exceptions, so this controller does not take [RecruitmentBusinessRules].
    internal const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IApplicationPipelineService _pipelineService;
    private readonly IPipelineQueryService _queryService;
    private readonly IAutoScoringService _scoringService;
    private readonly ICurrentUserService _currentUser;

    public ApplicationPipelineController(
        IApplicationPipelineService pipelineService,
        IPipelineQueryService queryService,
        IAutoScoringService scoringService,
        ICurrentUserService currentUser)
    {
        _pipelineService = pipelineService;
        _queryService    = queryService;
        _scoringService  = scoringService;
        _currentUser     = currentUser;
    }

    // =========================================================================
    // GET /api/applications/pipeline/{vacancyId}
    // (Legacy Kanban — kept for backward compatibility)
    // =========================================================================

    /// <summary>
    /// Returns the full Kanban board for a vacancy: ordered pipeline stages, each
    /// containing the applications currently in that stage.
    /// </summary>
    [HttpGet("pipeline/{vacancyId:guid}")]
    [ProducesResponseType(typeof(List<PipelineStageWithApplicationsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<PipelineStageWithApplicationsDto>>> GetPipeline(
        Guid vacancyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var board = await _pipelineService.GetPipelineByVacancyAsync(vacancyId, cancellationToken);
            return Ok(board);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // =========================================================================
    // GET /api/applications/pipeline/{vacancyId}/overview
    // =========================================================================

    /// <summary>
    /// Returns the lightweight pipeline overview for a vacancy: each stage with
    /// only its application count. Includes the synthetic inbox bucket
    /// (StageId = Guid.Empty). Use this to render the pipeline header bar.
    /// </summary>
    [HttpGet("pipeline/{vacancyId:guid}/overview")]
    [ProducesResponseType(typeof(PipelineOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PipelineOverviewDto>> GetPipelineOverview(
        Guid vacancyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var overview = await _queryService.GetPipelineOverviewAsync(vacancyId, cancellationToken);
            return Ok(overview);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // =========================================================================
    // GET /api/applications/pipeline/{vacancyId}/stages/{stageId}/applications
    // =========================================================================

    /// <summary>
    /// Returns a paginated, filterable list of applications in a specific pipeline stage.
    /// Use stageId = 00000000-0000-0000-0000-000000000000 (Guid.Empty) for the inbox
    /// (applications not yet placed in any stage).
    /// </summary>
    [HttpGet("pipeline/{vacancyId:guid}/stages/{stageId:guid}/applications")]
    [ProducesResponseType(typeof(PagedResult<PipelineApplicationListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<PipelineApplicationListItemDto>>> GetStageApplications(
        Guid vacancyId,
        Guid stageId,
        [FromQuery] int page            = 1,
        [FromQuery] int pageSize        = 20,
        [FromQuery] string? nameSearch  = null,
        [FromQuery] int? status         = null,
        [FromQuery] int? source         = null,
        [FromQuery] DateTime? dateFrom  = null,
        [FromQuery] DateTime? dateTo    = null,
        [FromQuery] decimal? minScore   = null,
        [FromQuery] decimal? maxScore   = null,
        [FromQuery] string? sortBy      = null,
        [FromQuery] bool sortDesc       = true,
        CancellationToken cancellationToken = default)
    {
        var query = new StageApplicationsQuery
        {
            Page           = page,
            PageSize       = pageSize,
            NameSearch     = nameSearch,
            Status         = status.HasValue ? (ErpSystem.Core.Enums.ApplicationStatus)status.Value : null,
            Source         = source.HasValue ? (ErpSystem.Core.Enums.ApplicationSource)source.Value : null,
            DateFrom       = dateFrom,
            DateTo         = dateTo,
            MinScore       = minScore,
            MaxScore       = maxScore,
            SortBy         = sortBy,
            SortDescending = sortDesc,
        };

        try
        {
            var result = await _queryService.GetStageApplicationsAsync(vacancyId, stageId, query, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // =========================================================================
    // POST /api/applications/move-stage
    // =========================================================================

    /// <summary>
    /// Moves an application to the specified pipeline stage.
    /// Enforces transition rules: stage order, CanRepeat, and MaxAttempts.
    /// Updates the application status, stage history, and vacancy counters atomically.
    /// </summary>
    [HttpPost("move-stage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MoveStage(
        [FromBody] MoveStageRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // [Required] on Guid does not catch Guid.Empty — check explicitly
        if (request.ApplicationId == Guid.Empty || request.TargetStageId == Guid.Empty)
            return BadRequest(new { message = "ApplicationId and TargetStageId must not be empty GUIDs." });

        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
            return BadRequest(new
            {
                message = "Your user account is not linked to an employee record. " +
                          "Please contact your administrator."
            });

        try
        {
            await _pipelineService.MoveApplicationToStageAsync(
                request.ApplicationId,
                request.TargetStageId,
                employeeId.Value,
                cancellationToken);

            return Ok(new { message = "Application moved to the target stage successfully." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    // =========================================================================
    // POST /api/applications/bulk-move
    // =========================================================================

    /// <summary>
    /// Moves a set of applications to the same pipeline stage.
    /// Each application is processed individually; failures are captured in the result
    /// without aborting the remaining items.
    /// </summary>
    [HttpPost("bulk-move")]
    [ProducesResponseType(typeof(RecruitmentBulkOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecruitmentBulkOperationResultDto>> BulkMove(
        [FromBody] RecruitmentBulkMoveToStageDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.TargetStageId == Guid.Empty)
            return BadRequest(new { message = "TargetStageId must not be an empty GUID." });

        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
            return BadRequest(new
            {
                message = "Your user account is not linked to an employee record. " +
                          "Please contact your administrator."
            });

        var result = await _pipelineService.BulkMoveToStageAsync(dto, employeeId.Value, cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // POST /api/applications/bulk-pipeline-reject
    // =========================================================================

    /// <summary>
    /// Rejects a set of applications from the pipeline in one operation.
    /// Closes each application's current stage history record and sets
    /// <c>Status = Rejected</c>. Individual failures do not abort the batch.
    /// </summary>
    [HttpPost("bulk-pipeline-reject")]
    [ProducesResponseType(typeof(RecruitmentBulkOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecruitmentBulkOperationResultDto>> BulkPipelineReject(
        [FromBody] RecruitmentBulkPipelineRejectDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
            return BadRequest(new
            {
                message = "Your user account is not linked to an employee record. " +
                          "Please contact your administrator."
            });

        var result = await _pipelineService.BulkPipelineRejectAsync(dto, employeeId.Value, cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // POST /api/applications/pipeline/{vacancyId}/run-scoring
    // =========================================================================

    /// <summary>
    /// Scores all active applications for a vacancy in one operation.
    /// Per-application failures are captured in the result without aborting the
    /// remaining items — safe to call even when some applications have incomplete
    /// candidate profiles.
    /// </summary>
    [HttpPost("pipeline/{vacancyId:guid}/run-scoring")]
    [ProducesResponseType(typeof(RecruitmentScoringRunResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecruitmentScoringRunResultDto>> RunScoring(
        Guid vacancyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _scoringService.RunScoringForVacancyAsync(vacancyId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}


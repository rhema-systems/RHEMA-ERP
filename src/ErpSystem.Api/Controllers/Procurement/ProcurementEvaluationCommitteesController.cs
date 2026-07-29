using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/evaluation-committees")]
[Authorize]
public sealed class ProcurementEvaluationCommitteesController : ControllerBase
{
    private readonly IProcurementEvaluationCommitteeControlService _service;

    public ProcurementEvaluationCommitteesController(
        IProcurementEvaluationCommitteeControlService service) =>
        _service = service;

    [HttpGet("readiness")]
    public Task<IActionResult> GetReadiness(
        [FromQuery] ProcurementEvaluationSourceType sourceType,
        [FromQuery] Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.GetReadinessAsync(sourceType, sourceId, cancellationToken)));

    [HttpGet("options")]
    public Task<IActionResult> GetOptions(
        [FromQuery] ProcurementEvaluationSourceType sourceType,
        [FromQuery] Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.GetOptionsAsync(sourceType, sourceId, cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Get(
        [FromQuery] ProcurementEvaluationSourceType sourceType,
        [FromQuery] Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.GetAsync(sourceType, sourceId, cancellationToken)));

    [HttpGet("scorer-eligibility")]
    public Task<IActionResult> GetScorerEligibility(
        [FromQuery] ProcurementEvaluationSourceType sourceType,
        [FromQuery] Guid sourceId,
        [FromQuery] ProcurementEvaluationPhase phase,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.EnsureScorerEligibleAsync(
                sourceType, sourceId, phase, CorrelationId, cancellationToken)));

    [HttpPost("bind")]
    [Authorize]
    public Task<IActionResult> Bind(
        [FromBody] BindProcurementEvaluationCommitteeRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.BindAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get),
                new { sourceType = value.SourceType, sourceId = value.SourceId }, value);
        });

    [HttpPost("{committeeControlId:guid}/activate")]
    [Authorize]
    public Task<IActionResult> Activate(
        Guid committeeControlId,
        [FromBody] ActivateProcurementEvaluationCommitteeRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.ActivateAsync(
                committeeControlId, request, CorrelationId, cancellationToken)));

    [HttpPost("appointments/{appointmentId:guid}/response")]
    public Task<IActionResult> RespondToAppointment(
        Guid appointmentId,
        [FromBody] RespondProcurementEvaluationAppointmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.RespondToAppointmentAsync(
                appointmentId, request, CorrelationId, cancellationToken)));

    [HttpPost("appointments/{appointmentId:guid}/conflict-declarations")]
    public Task<IActionResult> SubmitConflictDeclaration(
        Guid appointmentId,
        [FromBody] SubmitProcurementEvaluationConflictDeclarationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.SubmitConflictDeclarationAsync(
                appointmentId, request, CorrelationId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, value);
        });

    [HttpPost("{committeeControlId:guid}/meetings")]
    [Authorize]
    public Task<IActionResult> CreateMeeting(
        Guid committeeControlId,
        [FromBody] CreateProcurementEvaluationMeetingRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.CreateMeetingAsync(
                committeeControlId, request, CorrelationId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, value);
        });

    [HttpPost("meetings/{meetingId:guid}/attendance")]
    public Task<IActionResult> SignAttendance(
        Guid meetingId,
        [FromBody] SignProcurementEvaluationAttendanceRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.SignAttendanceAsync(
                meetingId, request, CorrelationId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, value);
        });

    [HttpPost("meetings/{meetingId:guid}/quorum")]
    [Authorize]
    public Task<IActionResult> ConfirmQuorum(
        Guid meetingId,
        [FromBody] ConfirmProcurementEvaluationQuorumRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.ConfirmQuorumAsync(
                meetingId, request, CorrelationId, cancellationToken)));

    [HttpPost("score-sheets/{scoreSheetId:guid}/recalls")]
    public Task<IActionResult> RequestScoreRecall(
        Guid scoreSheetId,
        [FromBody] RequestProcurementEvaluationScoreRecallRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.RequestScoreRecallAsync(
                scoreSheetId, request, CorrelationId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, value);
        });

    [HttpPost("score-recalls/{recallId:guid}/decision")]
    [Authorize]
    public Task<IActionResult> DecideScoreRecall(
        Guid recallId,
        [FromBody] DecideProcurementEvaluationScoreRecallRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.DecideScoreRecallAsync(
                recallId, request, CorrelationId, cancellationToken)));

    private string CorrelationId
    {
        get
        {
            var supplied = Request.Headers["X-Correlation-ID"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(supplied)
                ? string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier)
                    ? Guid.NewGuid().ToString("N")
                    : HttpContext.TraceIdentifier
                : supplied;
        }
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementEvaluationCommitteeNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Evaluation committee control not found", exception.Message));
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "EVALUATION_COMMITTEE_ACCESS_FORBIDDEN",
                    "Evaluation committee access forbidden", exception.Message));
        }
        catch (ProcurementEvaluationCommitteeConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Evaluation committee control conflict", exception.Message));
        }
        catch (ProcurementEvaluationCommitteeValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = [exception.Message]
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Evaluation committee control validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
    }

    private ProblemDetails Problem(int status, string code, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = code,
            ["correlationId"] = CorrelationId
        }
    };
}

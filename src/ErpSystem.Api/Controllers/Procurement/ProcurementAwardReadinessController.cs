using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/award-readiness")]
[Authorize]
public sealed class ProcurementAwardReadinessController : ControllerBase
{
    private const int MaximumHistoryTake = 200;
    private readonly IProcurementAwardReadinessService _service;

    public ProcurementAwardReadinessController(
        IProcurementAwardReadinessService service) =>
        _service = service;

    [HttpGet("latest")]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetLatest(
        [FromQuery] ProcurementAwardReadinessSourceType sourceType,
        [FromQuery] Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            EnsureSourceId(sourceId);
            var decision = await _service.GetLatestAsync(
                sourceType, sourceId, cancellationToken);
            return decision is null
                ? NotFound(Problem(
                    StatusCodes.Status404NotFound,
                    "AWARD_READINESS_DECISION_NOT_FOUND",
                    "Award-readiness decision not found",
                    "No award-readiness decision has been retained for this source in the current tenant."))
                : Ok(decision);
        });

    [HttpGet("history")]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetHistory(
        [FromQuery] ProcurementAwardReadinessSourceType sourceType,
        [FromQuery] Guid sourceId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            EnsureSourceId(sourceId);
            if (take is < 1 or > MaximumHistoryTake)
                throw new ProcurementAwardReadinessValidationException(
                    "AWARD_READINESS_HISTORY_TAKE_INVALID",
                    $"History take must be between 1 and {MaximumHistoryTake}.");

            return Ok(await _service.GetHistoryAsync(
                sourceType, sourceId, take, cancellationToken));
        });

    [HttpGet("sod-status")]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetEvaluatorAwardApproverSodStatus(
        [FromQuery] ProcurementAwardReadinessSourceType sourceType,
        [FromQuery] Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            EnsureSourceId(sourceId);
            return Ok(await _service.GetEvaluatorAwardApproverSodStatusAsync(
                sourceType,
                sourceId,
                CorrelationId,
                cancellationToken));
        });

    [HttpPost("evaluate")]
    [Authorize(Policy = "procurement.tender.approve")]
    public Task<IActionResult> Evaluate(
        [FromQuery] ProcurementAwardReadinessSourceType sourceType,
        [FromQuery] Guid sourceId,
        [FromBody] EvaluateProcurementAwardReadinessRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            EnsureSourceId(sourceId);
            return Ok(await _service.EvaluateAsync(
                sourceType, sourceId, request, CorrelationId, cancellationToken));
        });

    private static void EnsureSourceId(Guid sourceId)
    {
        if (sourceId == Guid.Empty)
            throw new ProcurementAwardReadinessValidationException(
                "AWARD_READINESS_SOURCE_ID_REQUIRED",
                "A non-empty award-readiness source identifier is required.");
    }

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
        catch (ProcurementAwardReadinessNotFoundException exception)
        {
            return NotFound(Problem(
                StatusCodes.Status404NotFound,
                exception.Code,
                "Award-readiness source not found",
                exception.Message));
        }
        catch (ProcurementAwardReadinessAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem(
                StatusCodes.Status403Forbidden,
                "AWARD_READINESS_ACCESS_FORBIDDEN",
                "Award-readiness access forbidden",
                exception.Message));
        }
        catch (ProcurementAwardReadinessConflictException exception)
        {
            return Conflict(Problem(
                StatusCodes.Status409Conflict,
                exception.Code,
                "Award-readiness conflict",
                exception.Message));
        }
        catch (ProcurementAwardReadinessBlockedException exception)
        {
            var problem = Problem(
                StatusCodes.Status422UnprocessableEntity,
                exception.Code,
                "Award is not ready",
                exception.Message);
            problem.Extensions["decision"] = exception.Decision;
            return UnprocessableEntity(problem);
        }
        catch (ProcurementAwardReadinessValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = [exception.Message]
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Award-readiness validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
    }

    private ProblemDetails Problem(
        int status,
        string code,
        string title,
        string detail) => new()
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

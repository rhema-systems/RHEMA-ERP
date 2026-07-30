using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Authorize]
[Route("api/procurement/ghaneps-exchanges/{sourceType}/{sourceId:guid}")]
public sealed class ProcurementGhanepsExchangesController : ControllerBase
{
    private readonly IProcurementGhanepsExchangeService _service;

    public ProcurementGhanepsExchangesController(
        IProcurementGhanepsExchangeService service) =>
        _service = service;

    [HttpGet]
    public Task<IActionResult> GetOverview(
        string sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetOverviewAsync(
            ParseSourceType(sourceType), sourceId, cancellationToken)));

    [HttpGet("status")]
    public Task<IActionResult> GetStatus(
        string sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetOverviewAsync(
            ParseSourceType(sourceType), sourceId, cancellationToken)));

    [HttpGet("options")]
    public Task<IActionResult> GetOptions(
        string sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetOptionsAsync(
            ParseSourceType(sourceType), sourceId, cancellationToken)));

    [HttpGet("history")]
    public Task<IActionResult> GetHistory(
        string sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await _service.GetOverviewAsync(
                ParseSourceType(sourceType), sourceId, cancellationToken);
            var history = overview.Events
                .SelectMany(exchangeEvent => exchangeEvent.History)
                .OrderByDescending(item => item.OccurredAtUtc)
                .ThenByDescending(item => item.Sequence)
                .ToArray();
            return Ok(history);
        });

    [HttpGet("events/{exchangeEventId:guid}")]
    public Task<IActionResult> GetEvent(
        string sourceType,
        Guid sourceId,
        Guid exchangeEventId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await GetSourceEventAsync(
            ParseSourceType(sourceType),
            sourceId,
            exchangeEventId,
            cancellationToken)));

    [HttpPost("exports")]
    public Task<IActionResult> PrepareExport(
        string sourceType,
        Guid sourceId,
        [FromBody] PrepareProcurementGhanepsExportRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var parsedSourceType = ParseSourceType(sourceType);
            BindCreateRoute(request, parsedSourceType, sourceId);
            var value = await _service.PrepareExportAsync(
                request, CorrelationId, cancellationToken);
            return CreatedAtAction(
                nameof(GetEvent),
                EventRouteValues(value),
                value);
        });

    [HttpPost("imports")]
    public Task<IActionResult> RecordImport(
        string sourceType,
        Guid sourceId,
        [FromBody] RecordProcurementGhanepsImportRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var parsedSourceType = ParseSourceType(sourceType);
            BindCreateRoute(request, parsedSourceType, sourceId);
            var value = await _service.RecordImportAsync(
                request, CorrelationId, cancellationToken);
            return CreatedAtAction(
                nameof(GetEvent),
                EventRouteValues(value),
                value);
        });

    [HttpPost("events/{exchangeEventId:guid}/attempts")]
    public Task<IActionResult> RecordAttempt(
        string sourceType,
        Guid sourceId,
        Guid exchangeEventId,
        [FromBody] RecordProcurementGhanepsAttemptRequest request,
        CancellationToken cancellationToken) =>
        ExecuteEventActionAsync(
            sourceType,
            sourceId,
            request,
            cancellationToken,
            () => _service.RecordAttemptAsync(
                exchangeEventId,
                request,
                CorrelationId,
                cancellationToken));

    [HttpPost("events/{exchangeEventId:guid}/retry")]
    public Task<IActionResult> Retry(
        string sourceType,
        Guid sourceId,
        Guid exchangeEventId,
        [FromBody] RetryProcurementGhanepsExchangeRequest request,
        CancellationToken cancellationToken) =>
        ExecuteEventActionAsync(
            sourceType,
            sourceId,
            request,
            cancellationToken,
            () => _service.RetryAsync(
                exchangeEventId,
                request,
                CorrelationId,
                cancellationToken));

    [HttpPost("events/{exchangeEventId:guid}/acknowledgements")]
    public Task<IActionResult> RecordAcknowledgement(
        string sourceType,
        Guid sourceId,
        Guid exchangeEventId,
        [FromBody] RecordProcurementGhanepsAcknowledgementRequest request,
        CancellationToken cancellationToken) =>
        ExecuteEventActionAsync(
            sourceType,
            sourceId,
            request,
            cancellationToken,
            () => _service.RecordAcknowledgementAsync(
                exchangeEventId,
                request,
                CorrelationId,
                cancellationToken));

    [HttpPost("events/{exchangeEventId:guid}/reconciliations")]
    public Task<IActionResult> Reconcile(
        string sourceType,
        Guid sourceId,
        Guid exchangeEventId,
        [FromBody] ReconcileProcurementGhanepsExchangeRequest request,
        CancellationToken cancellationToken) =>
        ExecuteEventActionAsync(
            sourceType,
            sourceId,
            request,
            cancellationToken,
            () => _service.ReconcileAsync(
                exchangeEventId,
                request,
                CorrelationId,
                cancellationToken));

    private async Task<IActionResult> ExecuteEventActionAsync(
        string sourceType,
        Guid sourceId,
        ProcurementGhanepsRouteBoundMutationRequest request,
        CancellationToken cancellationToken,
        Func<Task<ProcurementGhanepsExchangeEventDto>> action) =>
        await ExecuteAsync(async () =>
        {
            request.RouteSourceType = ParseSourceType(sourceType);
            request.RouteSourceId = sourceId;
            var value = await action();
            return Ok(value);
        });

    private async Task<ProcurementGhanepsExchangeEventDto> GetSourceEventAsync(
        ProcurementGhanepsSourceType sourceType,
        Guid sourceId,
        Guid exchangeEventId,
        CancellationToken cancellationToken)
    {
        var value = await _service.GetAsync(exchangeEventId, cancellationToken);
        if (value.SourceType != sourceType || value.SourceId != sourceId)
            throw RouteResourceNotFound();
        return value;
    }

    private static void BindCreateRoute(
        ProcurementGhanepsPayloadRequest request,
        ProcurementGhanepsSourceType routeSourceType,
        Guid routeSourceId)
    {
        request.RouteSourceType = routeSourceType;
        request.RouteSourceId = routeSourceId;
        request.SourceType ??= routeSourceType;
        request.SourceId ??= routeSourceId;
    }

    private static ProcurementGhanepsSourceType ParseSourceType(
        string sourceType)
    {
        if (sourceType.Equals(
                nameof(ProcurementGhanepsSourceType.Tender),
                StringComparison.OrdinalIgnoreCase))
            return ProcurementGhanepsSourceType.Tender;
        if (sourceType.Equals(
                nameof(ProcurementGhanepsSourceType.RequestForQuotation),
                StringComparison.OrdinalIgnoreCase))
            return ProcurementGhanepsSourceType.RequestForQuotation;
        if (sourceType.Equals(
                nameof(ProcurementGhanepsSourceType.ExceptionalSourcing),
                StringComparison.OrdinalIgnoreCase))
            return ProcurementGhanepsSourceType.ExceptionalSourcing;

        throw new ProcurementGhanepsExchangeValidationException(
            "GHANEPS_EXCHANGE_SOURCE_TYPE_INVALID",
            "Source type must be Tender, RequestForQuotation, or ExceptionalSourcing.");
    }

    private static object EventRouteValues(
        ProcurementGhanepsExchangeEventDto value) => new
        {
            sourceType = value.SourceType.ToString(),
            sourceId = value.SourceId,
            exchangeEventId = value.Id
        };

    private static ProcurementGhanepsExchangeNotFoundException
        RouteResourceNotFound() => new(
            "GHANEPS_EXCHANGE_ROUTE_RESOURCE_NOT_FOUND",
            "The requested GHANEPS exchange event does not belong to this source.");

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
        catch (ProcurementGhanepsExchangeNotFoundException exception)
        {
            return NotFound(Problem(
                StatusCodes.Status404NotFound,
                exception.Code,
                "GHANEPS exchange record not found",
                exception.Message));
        }
        catch (ProcurementGhanepsExchangeAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem(
                StatusCodes.Status403Forbidden,
                exception.Code,
                "GHANEPS exchange access forbidden",
                exception.Message));
        }
        catch (ProcurementGhanepsExchangeConflictException exception)
        {
            return Conflict(Problem(
                StatusCodes.Status409Conflict,
                exception.Code,
                "GHANEPS exchange conflict",
                exception.Message));
        }
        catch (ProcurementGhanepsExchangeValidationException exception)
        {
            var problem = new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [exception.Code] = [exception.Message]
                })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "GHANEPS exchange validation failed",
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

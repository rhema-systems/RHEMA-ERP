using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Authorize]
[Route("api/procurement/bidder-communications")]
public sealed class ProcurementBidderCommunicationsController : ControllerBase
{
    private readonly IProcurementBidderCommunicationService _service;

    public ProcurementBidderCommunicationsController(
        IProcurementBidderCommunicationService service) =>
        _service = service;

    [HttpGet("{sourceType}/{sourceId:guid}")]
    public Task<IActionResult> GetOverview(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetOverviewAsync(
            sourceType, sourceId, cancellationToken)));

    [HttpGet("{sourceType}/{sourceId:guid}/history")]
    public Task<IActionResult> GetHistory(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetOverviewAsync(
            sourceType, sourceId, cancellationToken)));

    [HttpPost("{sourceType}/{sourceId:guid}/initialize")]
    public Task<IActionResult> Initialize(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        [FromBody] InitializeProcurementBidderCommunicationRegisterRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            EnsureSourceMatchesRoute(sourceType, sourceId, request);
            var value = await _service.InitializeAsync(
                request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(GetOverview),
                new { sourceType = value.SourceType, sourceId = value.SourceId },
                value);
        });

    [HttpPost("{sourceType}/{sourceId:guid}/recipients/{recipientId:guid}/letters/approve")]
    public Task<IActionResult> ApproveLetter(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid recipientId,
        [FromBody] ApproveProcurementBidderCommunicationLetterRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await InternalOverviewAsync(
                sourceType, sourceId, cancellationToken);
            EnsureRecipient(overview, recipientId);
            return StatusCode(StatusCodes.Status201Created,
                await _service.ApproveLetterAsync(
                    recipientId, request, CorrelationId, cancellationToken));
        });

    [HttpPost("{sourceType}/{sourceId:guid}/letters/{letterVersionId:guid}/dispatch")]
    public Task<IActionResult> DispatchLetter(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid letterVersionId,
        [FromBody] DispatchProcurementBidderCommunicationLetterRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await InternalOverviewAsync(
                sourceType, sourceId, cancellationToken);
            EnsureLetterVersion(overview, letterVersionId);
            return StatusCode(StatusCodes.Status201Created,
                await _service.DispatchLetterAsync(
                    letterVersionId, request, CorrelationId, cancellationToken));
        });

    [HttpPost("{sourceType}/{sourceId:guid}/dispatches/{dispatchId:guid}/delivery")]
    public Task<IActionResult> RecordDelivery(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid dispatchId,
        [FromBody] RecordProcurementBidderCommunicationDeliveryRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await InternalOverviewAsync(
                sourceType, sourceId, cancellationToken);
            EnsureDispatch(overview, dispatchId);
            return StatusCode(StatusCodes.Status201Created,
                await _service.RecordDeliveryAsync(
                    dispatchId, request, CorrelationId, cancellationToken));
        });

    [HttpPost("{sourceType}/{sourceId:guid}/dispatches/{dispatchId:guid}/acknowledgement")]
    public Task<IActionResult> RecordAcknowledgement(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid dispatchId,
        [FromBody] RecordProcurementBidderCommunicationAcknowledgementRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await InternalOverviewAsync(
                sourceType, sourceId, cancellationToken);
            EnsureDispatch(overview, dispatchId);
            return StatusCode(StatusCodes.Status201Created,
                await _service.RecordAcknowledgementAsync(
                    dispatchId, request, CorrelationId, cancellationToken));
        });

    [HttpPost("{sourceType}/{sourceId:guid}/recipients/{recipientId:guid}/appeals")]
    public Task<IActionResult> FileAppeal(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid recipientId,
        [FromBody] FileProcurementBidderAppealRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await InternalOverviewAsync(
                sourceType, sourceId, cancellationToken);
            EnsureRecipient(overview, recipientId);
            return StatusCode(StatusCodes.Status201Created,
                await _service.FileAppealAsync(
                    recipientId, request, CorrelationId, cancellationToken));
        });

    [HttpPost("{sourceType}/{sourceId:guid}/appeals/{appealId:guid}/resolve")]
    public Task<IActionResult> ResolveAppeal(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid appealId,
        [FromBody] ResolveProcurementBidderAppealRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await InternalOverviewAsync(
                sourceType, sourceId, cancellationToken);
            EnsureAppeal(overview, appealId);
            return StatusCode(StatusCodes.Status201Created,
                await _service.ResolveAppealAsync(
                    appealId, request, CorrelationId, cancellationToken));
        });

    [HttpPost("{sourceType}/{sourceId:guid}/recipients/{recipientId:guid}/securities")]
    public Task<IActionResult> RegisterSecurity(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid recipientId,
        [FromBody] RegisterProcurementTenderSecurityRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await InternalOverviewAsync(
                sourceType, sourceId, cancellationToken);
            EnsureRecipient(overview, recipientId);
            return StatusCode(StatusCodes.Status201Created,
                await _service.RegisterSecurityAsync(
                    recipientId, request, CorrelationId, cancellationToken));
        });

    [HttpPost("{sourceType}/{sourceId:guid}/securities/{securityId:guid}/actions")]
    public Task<IActionResult> RecordSecurityAction(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid securityId,
        [FromBody] RecordProcurementTenderSecurityActionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await InternalOverviewAsync(
                sourceType, sourceId, cancellationToken);
            EnsureSecurity(overview, securityId);
            return StatusCode(StatusCodes.Status201Created,
                await _service.RecordSecurityActionAsync(
                    securityId, request, CorrelationId, cancellationToken));
        });

    [HttpGet("external/{sourceType}/{sourceId:guid}")]
    public Task<IActionResult> GetExternalOverview(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetExternalOverviewAsync(
            sourceType, sourceId, cancellationToken)));

    [HttpPost("external/{sourceType}/{sourceId:guid}/dispatches/{dispatchId:guid}/acknowledgement")]
    public Task<IActionResult> RecordExternalAcknowledgement(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid dispatchId,
        [FromBody] RecordProcurementBidderCommunicationAcknowledgementRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await ExternalOverviewAsync(
                sourceType, sourceId, cancellationToken);
            EnsureDispatch(overview, dispatchId);
            return StatusCode(StatusCodes.Status201Created,
                await _service.RecordExternalAcknowledgementAsync(
                    dispatchId, request, CorrelationId, cancellationToken));
        });

    [HttpPost("external/{sourceType}/{sourceId:guid}/appeals")]
    public Task<IActionResult> FileExternalAppeal(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        [FromBody] FileProcurementBidderAppealRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var overview = await ExternalOverviewAsync(
                sourceType, sourceId, cancellationToken);
            var recipient = EnsureSingleExternalRecipient(overview);
            return StatusCode(StatusCodes.Status201Created,
                await _service.FileExternalAppealAsync(
                    recipient.Id, request, CorrelationId, cancellationToken));
        });

    private Task<ProcurementBidderCommunicationOverviewDto> ExternalOverviewAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        _service.GetExternalOverviewAsync(sourceType, sourceId, cancellationToken);

    private Task<ProcurementBidderCommunicationOverviewDto> InternalOverviewAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        _service.GetOverviewAsync(sourceType, sourceId, cancellationToken);

    private static void EnsureSourceMatchesRoute(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        InitializeProcurementBidderCommunicationRegisterRequest request)
    {
        if (request.SourceType != sourceType || request.SourceId != sourceId)
            throw new ProcurementBidderCommunicationValidationException(
                "BIDDER_COMMUNICATION_SOURCE_ROUTE_MISMATCH",
                "The request source must exactly match the route source.");
    }

    private static void EnsureDispatch(
        ProcurementBidderCommunicationOverviewDto overview,
        Guid dispatchId)
    {
        if (overview.Recipients.SelectMany(item => item.LetterVersions)
            .SelectMany(item => item.Dispatches)
            .All(item => item.Id != dispatchId))
            throw RouteResourceNotFound();
    }

    private static void EnsureRecipient(
        ProcurementBidderCommunicationOverviewDto overview,
        Guid recipientId)
    {
        if (overview.Recipients.All(item => item.Id != recipientId))
            throw RouteResourceNotFound();
    }

    private static void EnsureLetterVersion(
        ProcurementBidderCommunicationOverviewDto overview,
        Guid letterVersionId)
    {
        if (overview.Recipients.SelectMany(item => item.LetterVersions)
            .All(item => item.Id != letterVersionId))
            throw RouteResourceNotFound();
    }

    private static void EnsureAppeal(
        ProcurementBidderCommunicationOverviewDto overview,
        Guid appealId)
    {
        if (overview.Recipients.SelectMany(item => item.Appeals)
            .All(item => item.Id != appealId))
            throw RouteResourceNotFound();
    }

    private static void EnsureSecurity(
        ProcurementBidderCommunicationOverviewDto overview,
        Guid securityId)
    {
        if (overview.Recipients.SelectMany(item => item.SecurityInstruments)
            .All(item => item.Id != securityId))
            throw RouteResourceNotFound();
    }

    private static ProcurementBidderCommunicationRecipientDto
        EnsureSingleExternalRecipient(
            ProcurementBidderCommunicationOverviewDto overview)
    {
        if (overview.Recipients.Count == 0)
            throw RouteResourceNotFound();
        if (overview.Recipients.Count != 1)
            throw new ProcurementBidderCommunicationValidationException(
                "BIDDER_COMMUNICATION_EXTERNAL_RECIPIENT_AMBIGUOUS",
                "The current supplier identity must resolve to exactly one recipient.");
        return overview.Recipients[0];
    }

    private static ProcurementBidderCommunicationNotFoundException
        RouteResourceNotFound() => new(
            "BIDDER_COMMUNICATION_ROUTE_RESOURCE_NOT_FOUND",
            "The requested communication record does not belong to this source.");

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
        catch (ProcurementBidderCommunicationNotFoundException exception)
        {
            return NotFound(Problem(
                StatusCodes.Status404NotFound,
                exception.Code,
                "Bidder communication record not found",
                exception.Message));
        }
        catch (ProcurementBidderCommunicationAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem(
                StatusCodes.Status403Forbidden,
                "BIDDER_COMMUNICATION_ACCESS_FORBIDDEN",
                "Bidder communication access forbidden",
                exception.Message));
        }
        catch (ProcurementBidderCommunicationConflictException exception)
        {
            return Conflict(Problem(
                StatusCodes.Status409Conflict,
                exception.Code,
                "Bidder communication conflict",
                exception.Message));
        }
        catch (ProcurementBidderCommunicationValidationException exception)
        {
            var problem = new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [exception.Code] = [exception.Message]
                })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Bidder communication validation failed",
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

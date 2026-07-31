using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Authorize]
[Route("api/procurement/purchase-order-amendments")]
public sealed class ProcurementPurchaseOrderAmendmentsController : ControllerBase
{
    private readonly IProcurementPurchaseOrderAmendmentService _service;

    public ProcurementPurchaseOrderAmendmentsController(
        IProcurementPurchaseOrderAmendmentService service) =>
        _service = service;

    [HttpGet("purchase-orders/{purchaseOrderId:guid}")]
    public Task<IActionResult> GetOverview(
        Guid purchaseOrderId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(
            await _service.GetOverviewAsync(
                purchaseOrderId, cancellationToken)));

    [HttpPost("purchase-orders/{purchaseOrderId:guid}")]
    public Task<IActionResult> Create(
        Guid purchaseOrderId,
        [FromBody] CreateProcurementPurchaseOrderAmendmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.CreateAsync(
                purchaseOrderId,
                request,
                CorrelationId,
                cancellationToken);
            return StatusCode(
                StatusCodes.Status201Created, value);
        });

    [HttpPost("{amendmentId:guid}/submit")]
    public Task<IActionResult> Submit(
        Guid amendmentId,
        [FromBody] ProcurementPurchaseOrderAmendmentLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(
            await _service.SubmitAsync(
                amendmentId,
                request,
                CorrelationId,
                cancellationToken)));

    [HttpPost("{amendmentId:guid}/decision")]
    public Task<IActionResult> Decide(
        Guid amendmentId,
        [FromBody] DecideProcurementPurchaseOrderAmendmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(
            await _service.DecideAsync(
                amendmentId,
                request,
                CorrelationId,
                cancellationToken)));

    [HttpPost("{amendmentId:guid}/dispatches")]
    public Task<IActionResult> Dispatch(
        Guid amendmentId,
        [FromBody] DispatchProcurementPurchaseOrderAmendmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => StatusCode(
            StatusCodes.Status201Created,
            await _service.DispatchAsync(
                amendmentId,
                request,
                CorrelationId,
                cancellationToken)));

    [HttpPost("dispatches/{dispatchId:guid}/acknowledgements")]
    public Task<IActionResult> Acknowledge(
        Guid dispatchId,
        [FromBody] AcknowledgeProcurementPurchaseOrderAmendmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => StatusCode(
            StatusCodes.Status201Created,
            await _service.AcknowledgeAsync(
                dispatchId,
                request,
                CorrelationId,
                external: false,
                cancellationToken)));

    [HttpGet("external")]
    public Task<IActionResult> GetExternalOverview(
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(
            await _service.GetExternalOverviewAsync(
                cancellationToken)));

    [HttpPost("external/dispatches/{dispatchId:guid}/acknowledgements")]
    public Task<IActionResult> AcknowledgeExternal(
        Guid dispatchId,
        [FromBody] AcknowledgeProcurementPurchaseOrderAmendmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => StatusCode(
            StatusCodes.Status201Created,
            await _service.AcknowledgeAsync(
                dispatchId,
                request,
                CorrelationId,
                external: true,
                cancellationToken)));

    private string CorrelationId
    {
        get
        {
            var supplied =
                Request.Headers["X-Correlation-ID"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(supplied)
                ? string.IsNullOrWhiteSpace(
                    HttpContext.TraceIdentifier)
                    ? Guid.NewGuid().ToString("N")
                    : HttpContext.TraceIdentifier
                : supplied;
        }
    }

    private async Task<IActionResult> ExecuteAsync(
        Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementPurchaseOrderAmendmentNotFoundException exception)
        {
            return NotFound(Problem(
                StatusCodes.Status404NotFound,
                exception.Code,
                "PO amendment record not found",
                exception.Message));
        }
        catch (ProcurementPurchaseOrderAmendmentAuthorizationException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                Problem(
                    StatusCodes.Status403Forbidden,
                    "PO_AMENDMENT_ACCESS_FORBIDDEN",
                    "PO amendment access forbidden",
                    exception.Message));
        }
        catch (ProcurementPurchaseOrderSourceAuthorizationException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                Problem(
                    StatusCodes.Status403Forbidden,
                    "PO_AMENDMENT_SOURCE_ACCESS_FORBIDDEN",
                    "PO amendment source access forbidden",
                    exception.Message));
        }
        catch (ProcurementPurchaseOrderSodAuthorizationException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                Problem(
                    StatusCodes.Status403Forbidden,
                    "PO_AMENDMENT_SOD_ACCESS_FORBIDDEN",
                    "PO amendment SOD access forbidden",
                    exception.Message));
        }
        catch (ProcurementPurchaseOrderComplianceAuthorizationException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                Problem(
                    StatusCodes.Status403Forbidden,
                    "PO_AMENDMENT_COMPLIANCE_ACCESS_FORBIDDEN",
                    "PO amendment compliance access forbidden",
                    exception.Message));
        }
        catch (ProcurementPurchaseOrderAmendmentConflictException exception)
        {
            return Conflict(Problem(
                StatusCodes.Status409Conflict,
                exception.Code,
                "PO amendment conflict",
                exception.Message));
        }
        catch (ProcurementPurchaseOrderSodBlockedException exception)
        {
            var problem = Problem(
                StatusCodes.Status409Conflict,
                exception.Code,
                "PO amendment SOD conflict",
                exception.Message);
            problem.Extensions["readiness"] = exception.Readiness;
            return Conflict(problem);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Problem(
                StatusCodes.Status409Conflict,
                "PO_AMENDMENT_CONCURRENCY_CONFLICT",
                "PO amendment concurrency conflict",
                "The amendment changed after it was loaded. Refresh and retry."));
        }
        catch (ProcurementPurchaseOrderAmendmentValidationException exception)
        {
            return Unprocessable(exception.Code, exception.Message);
        }
        catch (ProcurementPurchaseOrderSourceValidationException exception)
        {
            return Unprocessable(exception.Code, exception.Message);
        }
        catch (ProcurementPurchaseOrderComplianceBlockedException exception)
        {
            var problem = ValidationProblem(
                exception.Code, exception.Message);
            problem.Extensions["readiness"] = exception.Readiness;
            return UnprocessableEntity(problem);
        }
    }

    private IActionResult Unprocessable(
        string code,
        string message) =>
        UnprocessableEntity(ValidationProblem(code, message));

    private ValidationProblemDetails ValidationProblem(
        string code,
        string message)
    {
        var problem = new ValidationProblemDetails(
            new Dictionary<string, string[]>
            {
                [code] = [message]
            })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "PO amendment validation failed",
            Detail = message,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] =
            CorrelationId;
        return problem;
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

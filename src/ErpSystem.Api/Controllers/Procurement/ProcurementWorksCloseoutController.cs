using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/works-closeout")]
[Authorize(Policy = "InternalOnly")]
public sealed class ProcurementWorksCloseoutController : ControllerBase
{
    private readonly IProcurementWorksCloseoutService _service;

    public ProcurementWorksCloseoutController(
        IProcurementWorksCloseoutService service)
    {
        _service = service;
    }

    [HttpGet("contracts/{contractId:guid}")]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetOverview(
        Guid contractId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetOverviewAsync(
            contractId, cancellationToken)));

    [HttpPost("contracts/{contractId:guid}/actions")]
    [Authorize(Policy = "procurement.contract.manage")]
    public Task<IActionResult> Submit(
        Guid contractId,
        [FromBody] SubmitProcurementWorksCloseoutActionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var action = await _service.SubmitAsync(
                contractId, request, CorrelationId, cancellationToken);
            return Created(
                $"/api/procurement/works-closeout/actions/{action.Id}",
                action);
        });

    [HttpPost("actions/{actionId:guid}/decision")]
    [Authorize(Policy = "procurement.contract.approve")]
    public Task<IActionResult> Decide(
        Guid actionId,
        [FromBody] DecideProcurementWorksCloseoutActionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.DecideAsync(
            actionId, request, CorrelationId, cancellationToken)));

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
        catch (ProcurementWorksCloseoutNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Works closeout record not found", exception.Message));
        }
        catch (ProcurementWorksCloseoutAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "WORKS_CLOSEOUT_ACCESS_FORBIDDEN",
                    "Works closeout access forbidden", exception.Message));
        }
        catch (ProcurementWorksCloseoutConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Works closeout conflict", exception.Message));
        }
        catch (ProcurementWorksCloseoutValidationException exception)
        {
            return UnprocessableEntity(Problem(422, exception.Code,
                "Works closeout validation failed", exception.Message));
        }
    }

    private static ProblemDetails Problem(
        int status, string code, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Extensions = { ["code"] = code }
    };
}

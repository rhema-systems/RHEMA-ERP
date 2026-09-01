using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/sod-controls")]
[Authorize]
public sealed class ProcurementSodControlsController : ControllerBase
{
    private readonly IProcurementSodGuardService _service;

    public ProcurementSodControlsController(IProcurementSodGuardService service) => _service = service;

    [HttpGet("coverage")]
    [Authorize(Policy = "procurement.audit.read")]
    [ProducesResponseType(typeof(ProcurementSodCoverageDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetCoverage([FromQuery] DateTime? atUtc = null, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.GetCoverageAsync(atUtc ?? DateTime.UtcNow, cancellationToken)));

    [HttpPost("policies/{policySetId:guid}/apply-required")]
    [Authorize(Policy = "procurement.access.manage")]
    [ProducesResponseType(typeof(ProcurementSodProvisionResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> ApplyRequired(
        Guid policySetId,
        [FromBody] ApplyRequiredProcurementSodControlsRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.ApplyRequiredControlsAsync(
            policySetId, request, CorrelationId, cancellationToken)));

    [HttpPost("check")]
    [ProducesResponseType(typeof(ProcurementSodGuardDecisionDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Check(
        [FromBody] ProcurementSodGuardRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.CheckAsync(request, CorrelationId, cancellationToken)));

    [HttpPost("enforce")]
    [ProducesResponseType(typeof(ProcurementSodGuardDecisionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProcurementSodGuardDecisionDto), StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Enforce(
        [FromBody] ProcurementSodGuardRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            var decision = await _service.EnforceAsync(request, CorrelationId, cancellationToken);
            return decision.Allowed ? Ok(decision) : StatusCode(StatusCodes.Status403Forbidden, decision);
        });

    [HttpGet("blocked-attempts")]
    [Authorize(Policy = "procurement.audit.read")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcurementSodBypassAuditDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetBlockedAttempts(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.GetBlockedAttemptsAsync(take, cancellationToken)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementSodRequestValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = new[] { exception.Message }
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Procurement SOD request validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
        catch (ProcurementPolicyNotFoundException exception)
        {
            return NotFound(CreateProblem(404, "Procurement policy not found", exception.Message));
        }
        catch (ProcurementPolicyAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                CreateProblem(403, "Procurement SOD administration forbidden", exception.Message));
        }
        catch (ProcurementPolicyConflictException exception)
        {
            return Conflict(CreateProblem(409, "Procurement SOD policy conflict", exception.Message));
        }
        catch (ProcurementPolicyValidationException exception)
        {
            var problem = new ValidationProblemDetails(exception.Validation.Errors
                .GroupBy(item => item.Code)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Message).ToArray()))
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Procurement SOD policy validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
    }

    private ProblemDetails CreateProblem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions = { ["correlationId"] = CorrelationId }
    };
}

using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/compliance-decisions")]
[Authorize(Policy = "procurement.records.read")]
public sealed class ProcurementComplianceDecisionsController : ControllerBase
{
    private readonly IProcurementComplianceDecisionService _service;

    public ProcurementComplianceDecisionsController(IProcurementComplianceDecisionService service) =>
        _service = service;

    [HttpGet("policy-options")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcurementCompliancePolicyOptionDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetPolicyOptions(
        [FromQuery] DateTime? atUtc = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.GetEffectivePolicyOptionsAsync(
            atUtc ?? DateTime.UtcNow,
            cancellationToken)));

    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(ProcurementComplianceDecisionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Evaluate(
        [FromBody] ProcurementComplianceDecisionRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.EvaluateAsync(request, CorrelationId, cancellationToken)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementComplianceRequestValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = new[] { exception.Message }
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Procurement compliance request validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
        catch (ProcurementCompliancePolicyNotFoundException exception)
        {
            return NotFound(CreateProblem(404, "No effective procurement policy", exception.Message));
        }
        catch (ProcurementCompliancePolicyConflictException exception)
        {
            return Conflict(CreateProblem(409, "Procurement policy selection conflict", exception.Message));
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

using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/escalation-calculations")]
public sealed class QuantitySurveyEscalationCalculationsController(
    IQuantitySurveyEscalationCalculationService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups(CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet("impact-targets"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> ImpactTargets([FromQuery] Guid formulaId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetImpactTargetsAsync(formulaId, token)));

    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] QuantitySurveyEscalationCalculationListRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(request, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, token)));

    [HttpPost, Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> Calculate([FromBody] CreateQuantitySurveyEscalationCalculationRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CalculateAsync(request, CorrelationId, token)));

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> Submit(Guid id, [FromBody] QuantitySurveyEscalationCalculationLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/review-adjustment"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> ReviewAdjustment(Guid id, [FromBody] ReviewQuantitySurveyEscalationCalculationRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ReviewAdjustmentAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] QuantitySurveyEscalationCalculationLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ApproveAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveyEscalationCalculationLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RejectAsync(id, request, CorrelationId, token)));

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (QuantitySurveyEscalationCalculationNotFoundException exception)
        {
            return NotFound(Problem(404, "QS escalation calculation not found", exception.Message));
        }
        catch (QuantitySurveyEscalationCalculationConflictException exception)
        {
            return Conflict(Problem(409, "QS escalation calculation conflict", exception.Message));
        }
        catch (QuantitySurveyEscalationCalculationValidationException exception)
        {
            return BadRequest(Problem(400, "QS escalation calculation validation failed", exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "QS escalation calculation access forbidden", exception.Message));
        }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-escalation-calculation-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = $"QS_ESCALATION_CALCULATION_{status}",
            ["correlationId"] = CorrelationId
        }
    };
}

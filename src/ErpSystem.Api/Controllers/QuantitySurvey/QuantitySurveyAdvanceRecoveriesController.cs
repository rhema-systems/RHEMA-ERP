using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/advance-recoveries")]
public sealed class QuantitySurveyAdvanceRecoveriesController(IQuantitySurveyAdvanceRecoveryService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Workspace([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, token)));

    [HttpPost, Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> Create([FromQuery] Guid projectId,
        [FromBody] CreateQuantitySurveyAdvanceRecoveryRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> Submit(Guid id, [FromBody] QuantitySurveyAdvanceRecoveryActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] QuantitySurveyAdvanceRecoveryActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ApproveAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveyAdvanceRecoveryActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RejectAsync(id, request, CorrelationId, token)));

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyAdvanceRecoveryNotFoundException exception)
        { return NotFound(Problem(404, "QS advance recovery not found", exception.Message)); }
        catch (QuantitySurveyAdvanceRecoveryConflictException exception)
        { return Conflict(Problem(409, "QS advance recovery conflict", exception.Message)); }
        catch (QuantitySurveyAdvanceRecoveryValidationException exception)
        { return BadRequest(Problem(400, "QS advance recovery validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS advance recovery access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-advance-recovery-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = $"QS_ADVANCE_RECOVERY_{status}", ["correlationId"] = CorrelationId }
    };
}

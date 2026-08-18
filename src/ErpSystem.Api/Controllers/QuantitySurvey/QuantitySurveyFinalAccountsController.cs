using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/final-accounts")]
public sealed class QuantitySurveyFinalAccountsController(IQuantitySurveyFinalAccountService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Workspace([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, token)));

    [HttpPost, Authorize(Policy = QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    public Task<IActionResult> Prepare([FromQuery] Guid projectId,
        [FromBody] PrepareQuantitySurveyFinalAccountRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.PrepareAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    public Task<IActionResult> Submit(Guid id, [FromBody] QuantitySurveyFinalAccountActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] QuantitySurveyFinalAccountActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ApproveAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveyFinalAccountActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RejectAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/close"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Close(Guid id, [FromBody] QuantitySurveyFinalAccountActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CloseAsync(id, request, CorrelationId, token)));

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetHistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyFinalAccountNotFoundException exception)
        { return NotFound(Problem(404, "QS final account not found", exception.Message)); }
        catch (QuantitySurveyFinalAccountConflictException exception)
        { return Conflict(Problem(409, "QS final account conflict", exception.Message)); }
        catch (QuantitySurveyFinalAccountValidationException exception)
        { return BadRequest(Problem(400, "QS final account validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS final account access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-final-account-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = $"QS_FINAL_ACCOUNT_{status}",
            ["correlationId"] = CorrelationId
        }
    };
}

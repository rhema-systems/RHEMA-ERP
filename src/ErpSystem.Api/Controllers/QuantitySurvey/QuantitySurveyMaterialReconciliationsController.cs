using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/material-reconciliations")]
public sealed class QuantitySurveyMaterialReconciliationsController(
    IQuantitySurveyMaterialReconciliationService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Workspace([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, false, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, false, token)));

    [HttpPut, Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> Save([FromQuery] Guid projectId,
        [FromBody] SaveQuantitySurveyMaterialReconciliationRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SaveAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> Submit(Guid id,
        [FromBody] QuantitySurveyMaterialReconciliationActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id,
        [FromBody] QuantitySurveyMaterialReconciliationActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ApproveAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id,
        [FromBody] QuantitySurveyMaterialReconciliationActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RejectAsync(id, request, CorrelationId, token)));

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyMaterialReconciliationNotFoundException exception)
        { return NotFound(Problem(404, "QS material reconciliation not found", exception.Message)); }
        catch (QuantitySurveyMaterialReconciliationConflictException exception)
        { return Conflict(Problem(409, "QS material reconciliation conflict", exception.Message)); }
        catch (QuantitySurveyMaterialReconciliationValidationException exception)
        { return BadRequest(Problem(400, "QS material reconciliation validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS material reconciliation access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-material-reconciliation-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = $"QS_MATERIAL_RECONCILIATION_{status}", ["correlationId"] = CorrelationId }
    };
}

[ApiController]
[Authorize]
[Route("api/projects/external/my-projects/{projectId:guid}/material-reconciliations")]
public sealed class ExternalProjectMaterialReconciliationsController(
    IQuantitySurveyMaterialReconciliationService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Workspace(Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, true, token)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid projectId, Guid id, CancellationToken token) =>
        ForProjectAsync(projectId, id, () => service.GetAsync(id, true, token));

    [HttpPost("{id:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid projectId, Guid id,
        [FromBody] QuantitySurveyMaterialContractorConfirmationRequest request, CancellationToken token) =>
        ForProjectAsync(projectId, id, () => service.ConfirmAsync(id, request, true, CorrelationId, token));

    private Task<IActionResult> ForProjectAsync(Guid projectId, Guid id,
        Func<Task<QuantitySurveyMaterialReconciliationDto>> action) => ExecuteAsync(async () =>
    {
        var current = await service.GetAsync(id, true);
        if (current.ProjectId != projectId)
            return NotFound(Problem(404, "QS material reconciliation not found", "The material reconciliation was not found in this project."));
        return Ok(await action());
    });

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyMaterialReconciliationNotFoundException exception)
        { return NotFound(Problem(404, "QS material reconciliation not found", exception.Message)); }
        catch (QuantitySurveyMaterialReconciliationConflictException exception)
        { return Conflict(Problem(409, "QS material reconciliation conflict", exception.Message)); }
        catch (QuantitySurveyMaterialReconciliationValidationException exception)
        { return BadRequest(Problem(400, "QS material reconciliation validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS material reconciliation access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-material-reconciliation-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = $"QS_MATERIAL_RECONCILIATION_{status}", ["correlationId"] = CorrelationId }
    };
}

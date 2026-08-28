using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/design-revision-impacts")]
public sealed class QuantitySurveyDesignRevisionImpactsController(IQuantitySurveyDesignRevisionImpactService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, token)));

    [HttpPost, Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> Create([FromBody] CreateQuantitySurveyDesignImpactRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(request, CorrelationId, token)));

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> Submit(Guid id, [FromBody] QuantitySurveyDesignImpactLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] QuantitySurveyDesignImpactLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ApproveAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveyDesignImpactLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RejectAsync(id, request, CorrelationId, token)));

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyDesignImpactNotFoundException exception)
        { return NotFound(Problem(404, "QS design impact not found", exception.Message)); }
        catch (QuantitySurveyDesignImpactConflictException exception)
        { return Conflict(Problem(409, "QS design impact conflict", exception.Message)); }
        catch (QuantitySurveyDesignImpactValidationException exception)
        { return BadRequest(Problem(400, "QS design impact validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS design impact access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-design-impact-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = $"QS_DESIGN_IMPACT_{status}", ["correlationId"] = CorrelationId }
    };
}

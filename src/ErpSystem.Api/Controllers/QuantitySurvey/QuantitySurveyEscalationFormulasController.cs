using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/escalation-formulas")]
public sealed class QuantitySurveyEscalationFormulasController(IQuantitySurveyEscalationFormulaService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups(CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet("index-families"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> IndexFamilies([FromQuery] QuantitySurveyIndexFamilyListRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetIndexFamiliesAsync(request, token)));

    [HttpPost("index-families"), Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> CreateIndexFamily([FromBody] SaveQuantitySurveyIndexFamilyRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.CreateIndexFamilyAsync(request, CorrelationId, token)));

    [HttpPut("index-families/{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> UpdateIndexFamily(Guid id, [FromBody] SaveQuantitySurveyIndexFamilyRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.UpdateIndexFamilyAsync(id, request, CorrelationId, token)));

    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] QuantitySurveyEscalationFormulaListRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetFormulasAsync(request, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetFormulaAsync(id, token)));

    [HttpPost, Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> Create([FromBody] CreateQuantitySurveyEscalationFormulaRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.CreateFormulaAsync(request, CorrelationId, token)));

    [HttpPut("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateQuantitySurveyEscalationFormulaRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.UpdateFormulaAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> Submit(Guid id, [FromBody] QuantitySurveyEscalationLifecycleRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.SubmitFormulaAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] QuantitySurveyEscalationLifecycleRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ApproveFormulaAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveyEscalationLifecycleRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.RejectFormulaAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/retire"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Retire(Guid id, [FromBody] QuantitySurveyEscalationLifecycleRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.RetireFormulaAsync(id, request, CorrelationId, token)));

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyEscalationNotFoundException exception) { return NotFound(Problem(404, "QS escalation record not found", exception.Message)); }
        catch (QuantitySurveyEscalationConflictException exception) { return Conflict(Problem(409, "QS escalation conflict", exception.Message)); }
        catch (QuantitySurveyEscalationValidationException exception) { return BadRequest(Problem(400, "QS escalation validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(StatusCodes.Status403Forbidden, Problem(403, "QS escalation access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-escalation-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = $"QS_ESCALATION_{status}", ["correlationId"] = CorrelationId }
    };
}

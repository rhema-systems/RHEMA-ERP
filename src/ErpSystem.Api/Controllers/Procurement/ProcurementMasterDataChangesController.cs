using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/master-data-changes")]
[Authorize]
public sealed class ProcurementMasterDataChangesController : ControllerBase
{
    private readonly IProcurementMasterDataChangeService _service;

    public ProcurementMasterDataChangesController(IProcurementMasterDataChangeService service) => _service = service;

    [HttpGet("registry")]
    public Task<IActionResult> GetRegistry(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetRegistryAsync(cancellationToken)));

    [HttpGet("summary")]
    public Task<IActionResult> GetSummary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet("policies")]
    public Task<IActionResult> GetPolicies(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetPoliciesAsync(cancellationToken)));

    [HttpPost("policies"), Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> CreatePolicy([FromBody] SaveProcurementMasterDataPolicyRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SavePolicyAsync(null, request, CorrelationId, cancellationToken)));

    [HttpPut("policies/{id:guid}"), Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> UpdatePolicy(Guid id, [FromBody] SaveProcurementMasterDataPolicyRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SavePolicyAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("policies/{id:guid}/activate"), Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> ActivatePolicy(Guid id, [FromBody] ProcurementMasterDataPolicyLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ActivatePolicyAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("policies/{id:guid}/retire"), Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> RetirePolicy(Guid id, [FromBody] ProcurementMasterDataPolicyLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RetirePolicyAsync(id, request, CorrelationId, cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search([FromQuery] ProcurementMasterDataChangeSearchRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] SaveProcurementMasterDataChangeRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.SaveDraftAsync(null, request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] SaveProcurementMasterDataChangeRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SaveDraftAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid id, [FromBody] ProcurementMasterDataChangeLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SubmitAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/revalidate")]
    public Task<IActionResult> Revalidate(Guid id, [FromBody] ProcurementMasterDataChangeLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RevalidateAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, [FromBody] ProcurementMasterDataChangeDecisionRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ApproveAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, [FromBody] ProcurementMasterDataChangeDecisionRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RejectAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/apply")]
    public Task<IActionResult> Apply(Guid id, [FromBody] ProcurementMasterDataChangeLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ApplyAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, [FromBody] ProcurementMasterDataChangeLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.CancelAsync(id, request, CorrelationId, cancellationToken)));

    private string CorrelationId => string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier)
        ? Guid.NewGuid().ToString("N")
        : HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ProcurementMasterDataChangeNotFoundException exception)
        {
            return NotFound(Problem(404, "Controlled master-data record not found", exception.Message));
        }
        catch (ProcurementMasterDataChangeAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem(403, "Controlled master-data access forbidden", exception.Message));
        }
        catch (ProcurementMasterDataChangeConflictException exception)
        {
            return Conflict(Problem(409, "Controlled master-data conflict", exception.Message));
        }
        catch (ProcurementMasterDataChangeValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]> { [exception.Code] = new[] { exception.Message } })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Controlled master-data validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions = { ["correlationId"] = CorrelationId }
    };
}

using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/policy-sets")]
[Authorize]
public sealed class ProcurementPolicySetsController : ControllerBase
{
    private readonly IProcurementPolicyService _service;

    public ProcurementPolicySetsController(IProcurementPolicyService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetPolicySets([FromQuery] ProcurementPolicySetListRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetPolicySetsAsync(request, cancellationToken)));

    [HttpGet("effective")]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetEffective(
        [FromQuery] string code,
        [FromQuery] DateTime? atUtc = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
        {
            var policySet = await _service.GetEffectivePolicySetAsync(code, atUtc ?? DateTime.UtcNow, cancellationToken);
            return policySet is null
                ? NotFound(CreateProblem(404, "No effective policy", "No Published, effective executable procurement policy was found."))
                : Ok(policySet);
        });

    [HttpGet("role-options")]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetRoleOptions(
        [FromQuery] Guid? workflowDefinitionId = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.GetRoleOptionsAsync(workflowDefinitionId, cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetPolicySet(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetPolicySetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> Create([FromBody] CreateProcurementPolicySetRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var created = await _service.CreatePolicySetAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(GetPolicySet), new { id = created.Id }, created);
        });

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateProcurementPolicySetRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.UpdatePolicySetAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/rules")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> CreateRule(Guid id, [FromBody] SaveProcurementPolicyRuleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SaveRuleAsync(id, null, request, CorrelationId, cancellationToken)));

    [HttpPut("{id:guid}/rules/{ruleId:guid}")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> UpdateRule(Guid id, Guid ruleId, [FromBody] SaveProcurementPolicyRuleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SaveRuleAsync(id, ruleId, request, CorrelationId, cancellationToken)));

    [HttpDelete("{id:guid}/rules/{kind}/{ruleId:guid}")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> DeleteRule(
        Guid id,
        ProcurementPolicyRuleKind kind,
        Guid ruleId,
        [FromBody] DeleteProcurementPolicyRuleRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            await _service.DeleteRuleAsync(id, kind, ruleId, request, CorrelationId, cancellationToken);
            return NoContent();
        });

    [HttpPost("{id:guid}/validate")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> Validate(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ValidatePolicySetAsync(id, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> Publish(Guid id, [FromBody] ProcurementPolicyLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.PublishPolicySetAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> Retire(Guid id, [FromBody] ProcurementPolicyLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RetirePolicySetAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/clone-draft")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> CloneDraft(Guid id, [FromBody] CloneProcurementPolicySetRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var clone = await _service.CloneDraftAsync(id, request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(GetPolicySet), new { id = clone.Id }, clone);
        });

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> DeleteDraft(Guid id, [FromBody] ProcurementPolicyLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            await _service.DeleteDraftAsync(id, request, CorrelationId, cancellationToken);
            return NoContent();
        });

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = "procurement.audit.read")]
    public Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetHistoryAsync(id, cancellationToken)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ProcurementPolicyNotFoundException exception)
        { return NotFound(CreateProblem(404, "Procurement policy not found", exception.Message)); }
        catch (ProcurementPolicyAuthorizationException)
        { return Forbid(); }
        catch (ProcurementPolicyConflictException exception)
        { return Conflict(CreateProblem(409, "Procurement policy conflict", exception.Message)); }
        catch (ProcurementPolicyValidationException exception)
        {
            var errors = exception.Validation.Errors
                .GroupBy(item => item.RuleCode ?? item.RuleKind?.ToString() ?? "policy")
                .ToDictionary(group => group.Key, group => group.Select(item => item.Message).Distinct().ToArray());
            var problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Procurement policy validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["issues"] = exception.Validation.Errors;
            problem.Extensions["warnings"] = exception.Validation.Warnings;
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

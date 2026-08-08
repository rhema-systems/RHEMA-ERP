using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/configuration-profiles")]
public sealed class QuantitySurveyConfigurationProfilesController(IQuantitySurveyConfigurationService service) : ControllerBase
{
    [HttpGet("schemas"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Read)]
    public IActionResult Schemas() => Ok(service.GetSchemas());

    [HttpGet("lookups"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Read)]
    public Task<IActionResult> Lookups(CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.Read)]
    public Task<IActionResult> List([FromQuery] QuantitySurveyProfileListRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetProfilesAsync(request, token)));

    [HttpGet("effective"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Read)]
    public Task<IActionResult> Effective([FromQuery] DateTime? atUtc, CancellationToken token) => ExecuteAsync(async () =>
    {
        var result = await service.GetEffectiveProfileAsync(atUtc ?? DateTime.UtcNow, token);
        return result is null ? NotFound(Problem(404, "No effective QS configuration", "No published QS configuration is effective at the selected date.")) : Ok(result);
    });

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Read)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetProfileAsync(id, token)));

    [HttpPost, Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> Create([FromBody] CreateQuantitySurveyProfileRequest request, CancellationToken token) => ExecuteAsync(async () =>
    {
        var result = await service.CreateProfileAsync(request, CorrelationId, token);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    });

    [HttpPut("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateQuantitySurveyProfileRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.UpdateProfileAsync(id, request, CorrelationId, token)));

    [HttpPut("{id:guid}/decisions/{key}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> SaveDecision(Guid id, string key, [FromBody] SaveQuantitySurveyDecisionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.SaveDecisionAsync(id, key, request, CorrelationId, token)));

    [HttpPost("{id:guid}/decisions/{key}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> Submit(Guid id, string key, [FromBody] SubmitQuantitySurveyDecisionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.SubmitDecisionAsync(id, key, request, CorrelationId, token)));

    [HttpPost("{id:guid}/decisions/{key}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Approve)]
    public Task<IActionResult> Approve(Guid id, string key, [FromBody] DecideQuantitySurveyDecisionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ApproveDecisionAsync(id, key, request, CorrelationId, token)));

    [HttpPost("{id:guid}/decisions/{key}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Approve)]
    public Task<IActionResult> Reject(Guid id, string key, [FromBody] DecideQuantitySurveyDecisionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.RejectDecisionAsync(id, key, request, CorrelationId, token)));

    [HttpPost("{id:guid}/decisions/{key}/evidence"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> LinkEvidence(Guid id, string key, [FromBody] LinkQuantitySurveyEvidenceRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.LinkEvidenceAsync(id, key, request, CorrelationId, token)));

    [HttpDelete("{id:guid}/decisions/{key}/evidence/{evidenceId:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> UnlinkEvidence(Guid id, string key, Guid evidenceId, [FromQuery] string rowVersion, [FromQuery] string? reason, CancellationToken token) => ExecuteAsync(async () => { await service.UnlinkEvidenceAsync(id, key, evidenceId, rowVersion, reason, CorrelationId, token); return NoContent(); });

    [HttpPost("{id:guid}/validate"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> Validate(Guid id, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ValidateProfileAsync(id, token)));

    [HttpPost("{id:guid}/publish"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Approve)]
    public Task<IActionResult> Publish(Guid id, [FromBody] QuantitySurveyLifecycleRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.PublishProfileAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/retire"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Approve)]
    public Task<IActionResult> Retire(Guid id, [FromBody] QuantitySurveyLifecycleRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.RetireProfileAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/clone-draft"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> Clone(Guid id, [FromBody] CloneQuantitySurveyProfileRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.CloneDraftAsync(id, request, CorrelationId, token)));

    [HttpDelete("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> Delete(Guid id, [FromBody] QuantitySurveyLifecycleRequest request, CancellationToken token) => ExecuteAsync(async () => { await service.DeleteDraftAsync(id, request, CorrelationId, token); return NoContent(); });

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyConfigurationNotFoundException ex) { return NotFound(Problem(404, "QS configuration not found", ex.Message)); }
        catch (QuantitySurveyConfigurationConflictException ex) { return Conflict(Problem(409, "QS configuration conflict", ex.Message)); }
        catch (QuantitySurveyConfigurationValidationException ex)
        {
            var details = Problem(400, "QS configuration validation failed", ex.Message);
            details.Extensions["errors"] = ex.Validation.Errors.GroupBy(x => x.DecisionKey ?? "profile").ToDictionary(x => x.Key, x => x.Select(v => v.Message).Distinct().ToArray());
            return BadRequest(details);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Type = $"https://tdc.gov.gh/problems/quantity-survey-configuration-{status}", Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = $"QS_CONFIGURATION_{status}", ["correlationId"] = CorrelationId }
    };
}

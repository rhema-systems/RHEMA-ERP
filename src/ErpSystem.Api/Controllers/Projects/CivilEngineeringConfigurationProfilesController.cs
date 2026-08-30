using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/civil-engineering/configuration-profiles")]
public sealed class CivilEngineeringConfigurationProfilesController(ICivilEngineeringConfigurationService service) : ControllerBase
{
    [HttpGet("schemas"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationRead)]
    public IActionResult Schemas() => Ok(service.GetSchemas());

    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationRead)]
    public Task<IActionResult> Lookups(CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationRead)]
    public Task<IActionResult> List([FromQuery] CivilEngineeringProfileListRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetProfilesAsync(request, token)));

    [HttpGet("effective"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationRead)]
    public Task<IActionResult> Effective([FromQuery] DateTime? atUtc, CancellationToken token) => ExecuteAsync(async () =>
    {
        var result = await service.GetEffectiveProfileAsync(atUtc ?? DateTime.UtcNow, token);
        return result is null ? NotFound(Problem(404, "No effective Civil Engineering configuration", "No published Civil Engineering configuration is effective at the selected date.")) : Ok(result);
    });

    [HttpGet("{id:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetProfileAsync(id, token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationManage)]
    public Task<IActionResult> Create([FromBody] CreateCivilEngineeringProfileRequest request, CancellationToken token) => ExecuteAsync(async () =>
    {
        var result = await service.CreateProfileAsync(request, CorrelationId, token);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    });

    [HttpPut("{id:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationManage)]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateCivilEngineeringProfileRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.UpdateProfileAsync(id, request, CorrelationId, token)));

    [HttpPut("{id:guid}/decisions/{key}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationManage)]
    public Task<IActionResult> SaveDecision(Guid id, string key, [FromBody] SaveCivilEngineeringDecisionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.SaveDecisionAsync(id, key, request, CorrelationId, token)));

    [HttpPost("{id:guid}/decisions/{key}/submit"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationManage)]
    public Task<IActionResult> Submit(Guid id, string key, [FromBody] SubmitCivilEngineeringDecisionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.SubmitDecisionAsync(id, key, request, CorrelationId, token)));

    [HttpPost("{id:guid}/decisions/{key}/approve"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationApprove)]
    public Task<IActionResult> Approve(Guid id, string key, [FromBody] DecideCivilEngineeringDecisionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ApproveDecisionAsync(id, key, request, CorrelationId, token)));

    [HttpPost("{id:guid}/decisions/{key}/reject"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationApprove)]
    public Task<IActionResult> Reject(Guid id, string key, [FromBody] DecideCivilEngineeringDecisionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.RejectDecisionAsync(id, key, request, CorrelationId, token)));

    [HttpPost("{id:guid}/decisions/{key}/evidence"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationManage)]
    public Task<IActionResult> LinkEvidence(Guid id, string key, [FromBody] LinkCivilEngineeringEvidenceRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.LinkEvidenceAsync(id, key, request, CorrelationId, token)));

    [HttpDelete("{id:guid}/decisions/{key}/evidence/{evidenceId:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationManage)]
    public Task<IActionResult> UnlinkEvidence(Guid id, string key, Guid evidenceId, [FromQuery] string rowVersion, [FromQuery] string? reason, CancellationToken token) => ExecuteAsync(async () => { await service.UnlinkEvidenceAsync(id, key, evidenceId, rowVersion, reason, CorrelationId, token); return NoContent(); });

    [HttpPost("{id:guid}/validate"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationManage)]
    public Task<IActionResult> Validate(Guid id, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ValidateProfileAsync(id, token)));

    [HttpPost("{id:guid}/publish"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationApprove)]
    public Task<IActionResult> Publish(Guid id, [FromBody] CivilEngineeringLifecycleRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.PublishProfileAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/retire"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationApprove)]
    public Task<IActionResult> Retire(Guid id, [FromBody] CivilEngineeringLifecycleRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.RetireProfileAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/clone-draft"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationManage)]
    public Task<IActionResult> Clone(Guid id, [FromBody] CloneCivilEngineeringProfileRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.CloneDraftAsync(id, request, CorrelationId, token)));

    [HttpDelete("{id:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.ConfigurationManage)]
    public Task<IActionResult> Delete(Guid id, [FromBody] CivilEngineeringLifecycleRequest request, CancellationToken token) => ExecuteAsync(async () => { await service.DeleteDraftAsync(id, request, CorrelationId, token); return NoContent(); });

    [HttpGet("{id:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringConfigurationNotFoundException ex) { return NotFound(Problem(404, "Civil Engineering configuration not found", ex.Message)); }
        catch (CivilEngineeringConfigurationConflictException ex) { return Conflict(Problem(409, "Civil Engineering configuration conflict", ex.Message)); }
        catch (CivilEngineeringConfigurationValidationException ex)
        {
            var details = Problem(400, "Civil Engineering configuration validation failed", ex.Message);
            details.Extensions["errors"] = ex.Validation.Errors.GroupBy(x => x.ConfigurationKey ?? "profile").ToDictionary(x => x.Key, x => x.Select(v => v.Message).Distinct().ToArray());
            return BadRequest(details);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Type = $"https://tdc.gov.gh/problems/civil-engineering-configuration-{status}", Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = $"CIVIL_ENGINEERING_CONFIGURATION_{status}", ["correlationId"] = CorrelationId }
    };
}

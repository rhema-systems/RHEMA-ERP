using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/configuration-profiles")]
[Authorize]
public sealed class ProcurementConfigurationProfilesController : ControllerBase
{
    private readonly IProcurementConfigurationService _service;

    public ProcurementConfigurationProfilesController(IProcurementConfigurationService service)
    {
        _service = service;
    }

    [HttpGet("schemas")]
    [Authorize(Policy = "procurement.records.read")]
    public IActionResult GetSchemas() => Ok(_service.GetDecisionSchemas());

    [HttpGet]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetProfiles(
        [FromQuery] ProcurementConfigurationProfileListRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
            Ok(await _service.GetProfilesAsync(request, cancellationToken)));

    [HttpGet("effective")]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetEffective(
        [FromQuery] string profileCode = "TDC-PROCUREMENT",
        [FromQuery] DateTime? atUtc = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
        {
            var profile = await _service.GetEffectiveProfileAsync(profileCode, atUtc ?? DateTime.UtcNow, cancellationToken);
            return profile is null ? NotFound(CreateProblem(404, "No effective profile", "No published, effective procurement configuration profile was found.")) : Ok(profile);
        });

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procurement.records.read")]
    public Task<IActionResult> GetProfile(Guid id, CancellationToken cancellationToken) => ExecuteAsync(async () =>
        Ok(await _service.GetProfileAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> Create(
        [FromBody] CreateProcurementConfigurationProfileRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            var created = await _service.CreateProfileAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(GetProfile), new { id = created.Id }, created);
        });

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProcurementConfigurationProfileRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
            Ok(await _service.UpdateProfileAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPut("{id:guid}/decisions/{decisionKey}")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> SaveDecision(
        Guid id,
        string decisionKey,
        [FromBody] SaveProcurementConfigurationDecisionRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
            Ok(await _service.SaveDecisionAsync(id, decisionKey, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/validate")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> Validate(Guid id, CancellationToken cancellationToken) => ExecuteAsync(async () =>
        Ok(await _service.ValidateProfileAsync(id, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> Publish(
        Guid id,
        [FromBody] ProcurementConfigurationLifecycleRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
            Ok(await _service.PublishProfileAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> Retire(
        Guid id,
        [FromBody] ProcurementConfigurationLifecycleRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
            Ok(await _service.RetireProfileAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/clone-draft")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> CloneDraft(
        Guid id,
        [FromBody] CloneProcurementConfigurationProfileRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            var clone = await _service.CloneDraftAsync(id, request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(GetProfile), new { id = clone.Id }, clone);
        });

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> DeleteDraft(
        Guid id,
        [FromBody] ProcurementConfigurationLifecycleRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            await _service.DeleteDraftAsync(id, request, CorrelationId, cancellationToken);
            return NoContent();
        });

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = "procurement.audit.read")]
    public Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken) => ExecuteAsync(async () =>
        Ok(await _service.GetHistoryAsync(id, cancellationToken)));

    [HttpPost("{id:guid}/decisions/{decisionKey}/evidence")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> LinkEvidence(
        Guid id,
        string decisionKey,
        [FromBody] LinkProcurementConfigurationEvidenceRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            var evidence = await _service.LinkEvidenceAsync(id, decisionKey, request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(GetProfile), new { id }, evidence);
        });

    [HttpDelete("{id:guid}/decisions/{decisionKey}/evidence/{evidenceId:guid}")]
    [Authorize(Policy = "procurement.access.manage")]
    public Task<IActionResult> UnlinkEvidence(
        Guid id,
        string decisionKey,
        Guid evidenceId,
        [FromQuery] string decisionRowVersion,
        [FromQuery] string? reason,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            await _service.UnlinkEvidenceAsync(id, decisionKey, evidenceId, decisionRowVersion, reason, CorrelationId, cancellationToken);
            return NoContent();
        });

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementConfigurationNotFoundException exception)
        {
            return NotFound(CreateProblem(404, "Procurement configuration not found", exception.Message));
        }
        catch (ProcurementConfigurationAuthorizationException)
        {
            return Forbid();
        }
        catch (ProcurementConfigurationConflictException exception)
        {
            return Conflict(CreateProblem(409, "Procurement configuration conflict", exception.Message));
        }
        catch (ProcurementConfigurationValidationException exception)
        {
            var errors = exception.Validation.Errors
                .GroupBy(item => item.DecisionKey ?? "profile")
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.Message).Distinct().ToArray());
            var problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Procurement configuration validation failed",
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

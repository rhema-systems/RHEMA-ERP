using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/site-instructions")]
public sealed class CivilEngineeringSiteInstructionsController(ICivilEngineeringSiteInstructionService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    [HttpPost("projects/{projectId:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> Issue(Guid projectId, [FromBody] CreateCivilEngineeringSiteInstructionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.IssueAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{routingId:guid}/project-manager-routing"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> ProcessRouting(Guid routingId, [FromBody] ProcessCivilEngineeringSiteInstructionRoutingRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ProcessProjectManagerRoutingAsync(routingId, request, CorrelationId, token)));

    // External contractor access is determined server-side from the controlled project/business-partner relationship.
    [HttpPost("{routingId:guid}/contractor-response")]
    public Task<IActionResult> ContractorResponse(Guid routingId, [FromBody] RespondToCivilEngineeringSiteInstructionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.RecordContractorResponseAsync(routingId, request, CorrelationId, token)));

    [HttpPost("{routingId:guid}/engineering-review"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> ReviewContractorResponse(Guid routingId, [FromBody] ReviewCivilEngineeringSiteInstructionResponseRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ReviewContractorResponseAsync(routingId, request, CorrelationId, token)));

    [HttpPost("{routingId:guid}/engineering-follow-up"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> RecordEngineeringFollowUp(Guid routingId, [FromBody] FollowUpCivilEngineeringSiteInstructionRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.RecordEngineeringFollowUpAsync(routingId, request, CorrelationId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringSupervisionNotFoundException exception) { return NotFound(Problem(404, "Civil Engineering site instruction not found", exception.Message)); }
        catch (CivilEngineeringSupervisionConflictException exception) { return Conflict(Problem(409, "Civil Engineering site instruction conflict", exception.Message)); }
        catch (CivilEngineeringSupervisionValidationException exception) { return BadRequest(Problem(400, "Civil Engineering site instruction validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil Engineering site instruction access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-site-instruction-{status}",
        Extensions = { ["code"] = $"CIVIL_SITE_INSTRUCTION_{status}", ["correlationId"] = CorrelationId }
    };
}

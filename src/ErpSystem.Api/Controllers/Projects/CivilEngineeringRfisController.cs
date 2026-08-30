using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/rfis")]
public sealed class CivilEngineeringRfisController(ICivilEngineeringRfiService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    // The service derives the current contractor/consultant from the authenticated business-partner portal account.
    [HttpPost("projects/{projectId:guid}")]
    public Task<IActionResult> CreateExternal(Guid projectId, [FromBody] CreateCivilEngineeringRfiRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.CreateExternalAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{routingId:guid}/project-engineer-response"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> SubmitProjectEngineerResponse(Guid routingId, [FromBody] SubmitCivilEngineeringRfiResponseRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.SubmitProjectEngineerResponseAsync(routingId, request, CorrelationId, token)));

    [HttpPost("{routingId:guid}/project-manager-response"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> ProcessProjectManagerResponse(Guid routingId, [FromBody] ProcessCivilEngineeringRfiResponseRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ProcessProjectManagerResponseAsync(routingId, request, CorrelationId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringSupervisionNotFoundException exception) { return NotFound(Problem(404, "Civil Engineering RFI not found", exception.Message)); }
        catch (CivilEngineeringSupervisionConflictException exception) { return Conflict(Problem(409, "Civil Engineering RFI conflict", exception.Message)); }
        catch (CivilEngineeringSupervisionValidationException exception) { return BadRequest(Problem(400, "Civil Engineering RFI validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil Engineering RFI access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-rfi-{status}",
        Extensions = { ["code"] = $"CIVIL_RFI_{status}", ["correlationId"] = CorrelationId }
    };
}

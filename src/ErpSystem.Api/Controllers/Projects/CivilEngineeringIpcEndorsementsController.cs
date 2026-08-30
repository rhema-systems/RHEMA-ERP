using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/ipc-endorsements")]
public sealed class CivilEngineeringIpcEndorsementsController(ICivilEngineeringIpcEndorsementService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    [HttpPost("payment-certificates/{certificateId:guid}/submit"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> Submit(Guid certificateId, [FromBody] SubmitCivilEngineeringIpcEndorsementRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(certificateId, request, CorrelationId, token)));

    [HttpPost("{endorsementId:guid}/review"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> Review(Guid endorsementId, [FromBody] ReviewCivilEngineeringIpcEndorsementRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ReviewAsync(endorsementId, request, CorrelationId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringSupervisionNotFoundException exception)
        { return NotFound(Problem(404, "Civil Engineering IPC review not found", exception.Message)); }
        catch (CivilEngineeringSupervisionConflictException exception)
        { return Conflict(Problem(409, "Civil Engineering IPC review conflict", exception.Message)); }
        catch (CivilEngineeringSupervisionValidationException exception)
        { return BadRequest(Problem(400, "Civil Engineering IPC review validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "Civil Engineering IPC review access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-ipc-endorsement-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = $"CIVIL_IPC_ENDORSEMENT_{status}", ["correlationId"] = CorrelationId }
    };
}

using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/supervision")]
public sealed class CivilEngineeringProjectSupervisionController(ICivilEngineeringSupervisionService service) : ControllerBase
{
    [HttpGet("project-engineer-assignments/lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetProjectEngineerAssignmentLookupsAsync(projectId, token)));

    [HttpGet("project-engineer-assignments"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListProjectEngineerAssignmentsAsync(projectId, token)));

    [HttpPost("projects/{projectId:guid}/project-engineer-assignments"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AssignmentsManage)]
    public Task<IActionResult> Assign(Guid projectId, [FromBody] AssignCivilEngineeringProjectEngineerRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.AssignProjectEngineerAsync(projectId, request, CorrelationId, token)));

    [HttpPost("project-engineer-assignments/{assignmentId:guid}/end"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AssignmentsManage)]
    public Task<IActionResult> End(Guid assignmentId, [FromBody] EndCivilEngineeringProjectEngineerAssignmentRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.EndProjectEngineerAssignmentAsync(assignmentId, request, CorrelationId, token)));

    [HttpGet("project-engineer-assignments/{assignmentId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid assignmentId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetProjectEngineerAssignmentHistoryAsync(assignmentId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringSupervisionNotFoundException exception)
        { return NotFound(Problem(404, "Civil Engineering supervision record not found", exception.Message)); }
        catch (CivilEngineeringSupervisionConflictException exception)
        { return Conflict(Problem(409, "Civil Engineering supervision conflict", exception.Message)); }
        catch (CivilEngineeringSupervisionValidationException exception)
        { return BadRequest(Problem(400, "Civil Engineering supervision validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "Civil Engineering supervision access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-supervision-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = $"CIVIL_SUPERVISION_{status}", ["correlationId"] = CorrelationId }
    };
}

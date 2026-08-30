using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/civil-engineering/direct-tasks")]
public sealed class CivilEngineeringDirectTasksController(ICivilEngineeringDirectTaskService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AssignmentsManage)]
    public Task<IActionResult> Lookups(Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.AssignmentsManage)]
    public Task<IActionResult> Create(Guid projectId, [FromBody] CreateCivilEngineeringDirectTaskRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(projectId, request, HttpContext.TraceIdentifier, token)));

    [HttpPost("escalate-urgent"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AssignmentsManage)]
    public Task<IActionResult> EscalateUrgent(Guid projectId, [FromBody] EscalateCivilEngineeringUrgentTasksRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.EscalateUrgentAsync(projectId, request, HttpContext.TraceIdentifier, token)));

    [HttpGet("{taskId:guid}/feedback/lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AssignedWorkManage)]
    public Task<IActionResult> FeedbackLookups(Guid projectId, Guid taskId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetFeedbackLookupsAsync(projectId, RequireProjectTask(projectId, taskId), token)));

    [HttpGet("{taskId:guid}/feedback"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Feedback(Guid projectId, Guid taskId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetFeedbackAsync(projectId, RequireProjectTask(projectId, taskId), token)));

    [HttpPost("{taskId:guid}/feedback/assignee"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AssignedWorkManage)]
    public Task<IActionResult> AssigneeFeedback(Guid projectId, Guid taskId, [FromBody] ProcessCivilEngineeringDirectTaskFeedbackRequest request, CancellationToken token) =>
        ExecuteAsync(async () =>
        {
            if (request.Action is CivilEngineeringDirectTaskFeedbackAction.Accept or CivilEngineeringDirectTaskFeedbackAction.Return)
                return BadRequest(Problem(400, "Civil task feedback validation failed", "Use the independent reviewer endpoint for accept or return actions."));
            return Ok(await service.ProcessFeedbackAsync(projectId, RequireProjectTask(projectId, taskId), request, HttpContext.TraceIdentifier, token));
        });

    [HttpPost("{taskId:guid}/feedback/review"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AssignmentsManage)]
    public Task<IActionResult> ReviewFeedback(Guid projectId, Guid taskId, [FromBody] ProcessCivilEngineeringDirectTaskFeedbackRequest request, CancellationToken token) =>
        ExecuteAsync(async () =>
        {
            if (request.Action is not (CivilEngineeringDirectTaskFeedbackAction.Accept or CivilEngineeringDirectTaskFeedbackAction.Return))
                return BadRequest(Problem(400, "Civil task feedback validation failed", "The independent reviewer endpoint accepts only accept or return actions."));
            return Ok(await service.ProcessFeedbackAsync(projectId, RequireProjectTask(projectId, taskId), request, HttpContext.TraceIdentifier, token));
        });

    private static Guid RequireProjectTask(Guid projectId, Guid taskId)
    {
        if (projectId == Guid.Empty || taskId == Guid.Empty)
            throw new CivilEngineeringDirectTaskValidationException("A project and Civil task identifier are required.");
        return taskId;
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringDirectTaskNotFoundException exception) { return NotFound(Problem(404, "Civil direct task not found", exception.Message)); }
        catch (CivilEngineeringDirectTaskConflictException exception) { return Conflict(Problem(409, "Civil direct task conflict", exception.Message)); }
        catch (CivilEngineeringDirectTaskValidationException exception) { return BadRequest(Problem(400, "Civil direct task validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil direct task access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-direct-task-{status}",
        Extensions = { ["code"] = $"CIVIL_DIRECT_TASK_{status}", ["correlationId"] = HttpContext.TraceIdentifier }
    };
}

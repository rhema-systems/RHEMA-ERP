using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/extension-of-time-controls")]
public sealed class CivilEngineeringExtensionOfTimeControlsController(ICivilEngineeringExtensionOfTimeService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    [HttpPost("projects/{projectId:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.CommercialManage)]
    public Task<IActionResult> Create(Guid projectId, [FromBody] CreateCivilEngineeringExtensionOfTimeRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.CreateAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{controlId:guid}/review"), Authorize(Policy = CivilEngineeringAccessControlRegistry.CommercialManage)]
    public Task<IActionResult> Review(Guid controlId, [FromBody] ReviewCivilEngineeringExtensionOfTimeRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ReviewAsync(controlId, request, CorrelationId, token)));

    [HttpGet("{controlId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid controlId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(controlId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringSupervisionNotFoundException exception) { return NotFound(Problem(404, "Civil extension-of-time control not found", exception.Message)); }
        catch (CivilEngineeringSupervisionConflictException exception) { return Conflict(Problem(409, "Civil extension-of-time control conflict", exception.Message)); }
        catch (CivilEngineeringSupervisionValidationException exception) { return BadRequest(Problem(400, "Civil extension-of-time control validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil extension-of-time control access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-extension-of-time-{status}",
        Extensions = { ["code"] = $"CIVIL_EXTENSION_OF_TIME_{status}", ["correlationId"] = CorrelationId }
    };
}

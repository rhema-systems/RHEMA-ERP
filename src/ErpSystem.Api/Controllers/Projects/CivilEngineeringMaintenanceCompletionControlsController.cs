using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/maintenance-completion-controls")]
public sealed class CivilEngineeringMaintenanceCompletionControlsController(ICivilEngineeringMaintenanceCompletionControlService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups(CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.MaintenanceManage)]
    public Task<IActionResult> Create([FromBody] CreateCivilEngineeringMaintenanceCompletionControlRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(request, CorrelationId, token)));

    [HttpPost("{completionControlId:guid}/process"), Authorize(Policy = CivilEngineeringAccessControlRegistry.MaintenanceManage)]
    public Task<IActionResult> Process(Guid completionControlId, [FromBody] ProcessCivilEngineeringMaintenanceCompletionControlRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ProcessAsync(completionControlId, request, CorrelationId, token)));

    [HttpGet("{completionControlId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid completionControlId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(completionControlId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringMaintenanceCompletionControlNotFoundException exception) { return NotFound(Problem(404, "Civil Maintenance completion control not found", exception.Message)); }
        catch (CivilEngineeringMaintenanceExecutionLinkNotFoundException exception) { return NotFound(Problem(404, "Civil Maintenance execution link not found", exception.Message)); }
        catch (CivilEngineeringMaintenanceCompletionControlConflictException exception) { return Conflict(Problem(409, "Civil Maintenance completion-control conflict", exception.Message)); }
        catch (CivilEngineeringMaintenanceCompletionControlValidationException exception) { return BadRequest(Problem(400, "Civil Maintenance completion-control validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil Maintenance completion-control access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-maintenance-completion-control-{status}",
        Extensions = { ["code"] = $"CIVIL_MAINTENANCE_COMPLETION_CONTROL_{status}", ["correlationId"] = CorrelationId }
    };
}

using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/maintenance-execution-links")]
public sealed class CivilEngineeringMaintenanceExecutionLinksController(ICivilEngineeringMaintenanceExecutionLinkService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups(CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.MaintenanceManage)]
    public Task<IActionResult> Create([FromBody] CreateCivilEngineeringMaintenanceExecutionLinkRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(request, CorrelationId, token)));

    [HttpPost("{executionLinkId:guid}/refresh"), Authorize(Policy = CivilEngineeringAccessControlRegistry.MaintenanceManage)]
    public Task<IActionResult> Refresh(Guid executionLinkId, [FromBody] RefreshCivilEngineeringMaintenanceExecutionLinkRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RefreshAsync(executionLinkId, request, CorrelationId, token)));

    [HttpGet("{executionLinkId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid executionLinkId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(executionLinkId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringMaintenanceExecutionLinkNotFoundException exception) { return NotFound(Problem(404, "Civil Maintenance execution link not found", exception.Message)); }
        catch (CivilEngineeringMaintenanceCostingHandoffNotFoundException exception) { return NotFound(Problem(404, "Civil costing handoff not found", exception.Message)); }
        catch (CivilEngineeringMaintenanceExecutionLinkConflictException exception) { return Conflict(Problem(409, "Civil Maintenance execution-link conflict", exception.Message)); }
        catch (CivilEngineeringMaintenanceExecutionLinkValidationException exception) { return BadRequest(Problem(400, "Civil Maintenance execution-link validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil Maintenance execution-link access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-maintenance-execution-link-{status}",
        Extensions = { ["code"] = $"CIVIL_MAINTENANCE_EXECUTION_LINK_{status}", ["correlationId"] = CorrelationId }
    };
}

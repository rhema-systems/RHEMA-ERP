using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/maintenance-costing-handoffs")]
public sealed class CivilEngineeringMaintenanceCostingHandoffsController(ICivilEngineeringMaintenanceCostingHandoffService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups(CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.MaintenanceManage)]
    public Task<IActionResult> Create([FromBody] CreateCivilEngineeringMaintenanceCostingHandoffRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(request, CorrelationId, token)));

    [HttpPost("{handoffId:guid}/actions"), Authorize(Policy = CivilEngineeringAccessControlRegistry.MaintenanceManage)]
    public Task<IActionResult> Act(Guid handoffId, [FromBody] CivilEngineeringMaintenanceCostingHandoffActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ActAsync(handoffId, request, CorrelationId, token)));

    [HttpGet("{handoffId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid handoffId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(handoffId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringMaintenanceCostingHandoffNotFoundException exception) { return NotFound(Problem(404, "Civil costing handoff not found", exception.Message)); }
        catch (CivilEngineeringMaintenanceAssessmentNotFoundException exception) { return NotFound(Problem(404, "Civil maintenance assessment not found", exception.Message)); }
        catch (CivilEngineeringMaintenanceCostingHandoffConflictException exception) { return Conflict(Problem(409, "Civil costing handoff conflict", exception.Message)); }
        catch (CivilEngineeringMaintenanceCostingHandoffValidationException exception) { return BadRequest(Problem(400, "Civil costing handoff validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil costing handoff access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-maintenance-costing-handoff-{status}",
        Extensions = { ["code"] = $"CIVIL_MAINTENANCE_COSTING_HANDOFF_{status}", ["correlationId"] = CorrelationId }
    };
}

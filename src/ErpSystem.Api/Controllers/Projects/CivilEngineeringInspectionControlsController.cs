using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/inspection-controls")]
public sealed class CivilEngineeringInspectionControlsController(ICivilEngineeringInspectionControlService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    [HttpPost("projects/{projectId:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> Create(Guid projectId, [FromBody] CreateCivilEngineeringInspectionControlRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.CreateAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{inspectionControlId:guid}/process"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> Process(Guid inspectionControlId, [FromBody] ProcessCivilEngineeringInspectionControlRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ProcessAsync(inspectionControlId, request, CorrelationId, token)));

    [HttpGet("{inspectionControlId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid inspectionControlId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(inspectionControlId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringSupervisionNotFoundException exception) { return NotFound(Problem(404, "Civil inspection control not found", exception.Message)); }
        catch (CivilEngineeringSupervisionConflictException exception) { return Conflict(Problem(409, "Civil inspection control conflict", exception.Message)); }
        catch (CivilEngineeringSupervisionValidationException exception) { return BadRequest(Problem(400, "Civil inspection control validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil inspection control access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-inspection-control-{status}",
        Extensions = { ["code"] = $"CIVIL_INSPECTION_CONTROL_{status}", ["correlationId"] = CorrelationId }
    };
}

using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/maintenance-intakes")]
public sealed class CivilEngineeringMaintenanceIntakesController(ICivilEngineeringMaintenanceIntakeService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.MaintenanceManage)]
    public Task<IActionResult> Lookups(CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.MaintenanceManage)]
    public Task<IActionResult> Create([FromBody] CreateCivilEngineeringMaintenanceIntakeRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(request, CorrelationId, token)));

    [HttpGet("{intakeId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> History(Guid intakeId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(intakeId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringMaintenanceIntakeNotFoundException exception) { return NotFound(Problem(404, "Civil Engineering maintenance intake not found", exception.Message)); }
        catch (CivilEngineeringMaintenanceIntakeConflictException exception) { return Conflict(Problem(409, "Civil Engineering maintenance intake conflict", exception.Message)); }
        catch (CivilEngineeringMaintenanceIntakeValidationException exception) { return BadRequest(Problem(400, "Civil Engineering maintenance intake validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil Engineering maintenance intake access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-maintenance-intake-{status}",
        Extensions = { ["code"] = $"CIVIL_MAINTENANCE_INTAKE_{status}", ["correlationId"] = CorrelationId }
    };
}

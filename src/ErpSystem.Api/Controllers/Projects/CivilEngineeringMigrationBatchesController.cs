using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

/// <summary>Pre-posting historical-data validation only; no receiving Civil owner is mutated here.</summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/civil-engineering/migration-batches")]
public sealed class CivilEngineeringMigrationBatchesController(ICivilEngineeringMigrationService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.MigrationManage)]
    public Task<IActionResult> Lookups(Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.MigrationManage)]
    public Task<IActionResult> Stage(Guid projectId, [FromBody] StageCivilEngineeringMigrationBatchRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.StageAsync(projectId, request, HttpContext.TraceIdentifier, token)));

    [HttpPost("{batchId:guid}/reconcile"), Authorize(Policy = CivilEngineeringAccessControlRegistry.MigrationManage)]
    public Task<IActionResult> Reconcile(Guid projectId, Guid batchId, [FromBody] ReconcileCivilEngineeringMigrationBatchRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ReconcileAsync(projectId, batchId, request, HttpContext.TraceIdentifier, token)));

    [HttpPost("{batchId:guid}/sign-off"), Authorize(Policy = CivilEngineeringAccessControlRegistry.MigrationManage)]
    public Task<IActionResult> SignOff(Guid projectId, Guid batchId, [FromBody] SignOffCivilEngineeringMigrationBatchRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SignOffAsync(projectId, batchId, request, HttpContext.TraceIdentifier, token)));

    [HttpGet("{batchId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid projectId, Guid batchId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetHistoryAsync(projectId, batchId, token)));

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringMigrationNotFoundException exception) { return NotFound(Problem(404, "Civil migration batch not found", exception.Message)); }
        catch (CivilEngineeringMigrationConflictException exception) { return Conflict(Problem(409, "Civil migration batch conflict", exception.Message)); }
        catch (CivilEngineeringMigrationValidationException exception) { return BadRequest(Problem(400, "Civil migration batch validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil migration batch access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-migration-{status}",
        Extensions = { ["code"] = $"CIVIL_MIGRATION_{status}", ["correlationId"] = HttpContext.TraceIdentifier }
    };
}

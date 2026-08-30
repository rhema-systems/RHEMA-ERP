using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/quality-tests")]
public sealed class CivilEngineeringQualityTestsController(ICivilEngineeringQualityTestService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    [HttpPost("projects/{projectId:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> Create(Guid projectId, [FromBody] CreateCivilEngineeringQualityTestReportRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.CreateAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{reportId:guid}/review"), Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public Task<IActionResult> Review(Guid reportId, [FromBody] ProcessCivilEngineeringQualityTestReportRequest request, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ProcessAsync(reportId, request, CorrelationId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringSupervisionNotFoundException exception) { return NotFound(Problem(404, "Civil Engineering test report not found", exception.Message)); }
        catch (CivilEngineeringSupervisionConflictException exception) { return Conflict(Problem(409, "Civil Engineering test report conflict", exception.Message)); }
        catch (CivilEngineeringSupervisionValidationException exception) { return BadRequest(Problem(400, "Civil Engineering test report validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil Engineering test report access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-quality-test-{status}",
        Extensions = { ["code"] = $"CIVIL_QUALITY_TEST_{status}", ["correlationId"] = CorrelationId }
    };
}

using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/maintenance-assessments")]
public sealed class CivilEngineeringMaintenanceAssessmentsController(ICivilEngineeringMaintenanceAssessmentService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups(CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(token)));

    [HttpPost("intakes/{intakeId:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.MaintenanceManage)]
    public Task<IActionResult> Start(Guid intakeId, [FromBody] StartCivilEngineeringMaintenanceAssessmentRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.StartAsync(intakeId, request, CorrelationId, token)));

    [HttpPost("{assessmentId:guid}/transition"), Authorize(Policy = CivilEngineeringAccessControlRegistry.MaintenanceManage)]
    public Task<IActionResult> Transition(Guid assessmentId, [FromBody] CivilEngineeringMaintenanceAssessmentTransitionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.TransitionAsync(assessmentId, request, CorrelationId, token)));

    [HttpGet("{assessmentId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid assessmentId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(assessmentId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringMaintenanceIntakeNotFoundException exception) { return NotFound(Problem(404, "Civil maintenance intake not found", exception.Message)); }
        catch (CivilEngineeringMaintenanceAssessmentNotFoundException exception) { return NotFound(Problem(404, "Civil maintenance assessment not found", exception.Message)); }
        catch (CivilEngineeringMaintenanceAssessmentConflictException exception) { return Conflict(Problem(409, "Civil maintenance assessment conflict", exception.Message)); }
        catch (CivilEngineeringMaintenanceAssessmentValidationException exception) { return BadRequest(Problem(400, "Civil maintenance assessment validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Civil maintenance assessment access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-maintenance-assessment-{status}",
        Extensions = { ["code"] = $"CIVIL_MAINTENANCE_ASSESSMENT_{status}", ["correlationId"] = CorrelationId }
    };
}

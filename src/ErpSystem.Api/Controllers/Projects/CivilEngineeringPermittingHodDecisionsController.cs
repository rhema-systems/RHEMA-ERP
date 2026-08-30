using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/permitting-engineering-reviews")]
public sealed class CivilEngineeringPermittingHodDecisionsController(ICivilEngineeringPermittingHodDecisionService service) : ControllerBase
{
    [HttpGet("pending-hod-decisions"), Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> Pending(CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListPendingAsync(token)));

    [HttpPost("{engineeringReviewId:guid}/hod-decision"), Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> Decide(Guid engineeringReviewId, [FromBody] DecideCivilEngineeringPermittingReviewRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.DecideAsync(engineeringReviewId, request, HttpContext.TraceIdentifier, token)));

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringPermittingHodDecisionNotFoundException exception) { return NotFound(Problem(404, "Engineering recommendation not found", exception.Message)); }
        catch (CivilEngineeringPermittingHodDecisionConflictException exception) { return Conflict(Problem(409, "HOD decision conflict", exception.Message)); }
        catch (CivilEngineeringPermittingHodDecisionValidationException exception) { return BadRequest(Problem(400, "HOD decision validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "HOD decision access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-permitting-hod-decision-{status}",
        Extensions = { ["code"] = $"CIVIL_PERMITTING_HOD_DECISION_{status}", ["correlationId"] = HttpContext.TraceIdentifier }
    };
}

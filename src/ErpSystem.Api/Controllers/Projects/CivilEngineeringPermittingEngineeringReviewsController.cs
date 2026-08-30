using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/development-approval-files/{fileId:guid}/engineering-reviews")]
public sealed class CivilEngineeringPermittingEngineeringReviewsController(ICivilEngineeringPermittingEngineeringReviewService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> Lookups(Guid fileId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(fileId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(Guid fileId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(fileId, token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> Submit(Guid fileId, [FromBody] SubmitCivilEngineeringPermittingEngineeringReviewRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(fileId, request, HttpContext.TraceIdentifier, token)));

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringPermittingEngineeringReviewNotFoundException exception) { return NotFound(Problem(404, "Development approval file not found", exception.Message)); }
        catch (CivilEngineeringPermittingEngineeringReviewConflictException exception) { return Conflict(Problem(409, "Engineering review conflict", exception.Message)); }
        catch (CivilEngineeringPermittingEngineeringReviewValidationException exception) { return BadRequest(Problem(400, "Engineering review validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Engineering review access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-permitting-engineering-review-{status}",
        Extensions = { ["code"] = $"CIVIL_PERMITTING_ENGINEERING_REVIEW_{status}", ["correlationId"] = HttpContext.TraceIdentifier }
    };
}

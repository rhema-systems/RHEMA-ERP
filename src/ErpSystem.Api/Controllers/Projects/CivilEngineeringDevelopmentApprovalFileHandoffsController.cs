using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/development-approval-files/{fileId:guid}/handoffs")]
public sealed class CivilEngineeringDevelopmentApprovalFileHandoffsController(ICivilEngineeringDevelopmentApprovalFileHandoffService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> Lookups(Guid fileId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(fileId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(Guid fileId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(fileId, token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> Create(Guid fileId, [FromBody] CreateCivilEngineeringDevelopmentApprovalHandoffRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(fileId, request, CorrelationId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringDevelopmentApprovalFileHandoffNotFoundException exception) { return NotFound(Problem(404, "Development approval file not found", exception.Message)); }
        catch (CivilEngineeringDevelopmentApprovalFileHandoffConflictException exception) { return Conflict(Problem(409, "Development approval file handoff conflict", exception.Message)); }
        catch (CivilEngineeringDevelopmentApprovalFileHandoffValidationException exception) { return BadRequest(Problem(400, "Development approval file handoff validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Development approval file handoff access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-development-approval-file-handoff-{status}",
        Extensions = { ["code"] = $"CIVIL_DEVELOPMENT_APPROVAL_FILE_HANDOFF_{status}", ["correlationId"] = CorrelationId }
    };
}

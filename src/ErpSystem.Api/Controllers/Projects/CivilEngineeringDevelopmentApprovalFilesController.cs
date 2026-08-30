using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/development-approval-files")]
public sealed class CivilEngineeringDevelopmentApprovalFilesController(ICivilEngineeringDevelopmentApprovalFileService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> Lookups(CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetLookupsAsync(token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(CancellationToken token) => ExecuteAsync(async () => Ok(await service.ListAsync(token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> Create([FromBody] CreateCivilEngineeringDevelopmentApprovalFileRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(request, CorrelationId, token)));

    [HttpPost("{fileId:guid}/site-inspection"), Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> RecordSiteInspection(Guid fileId, [FromBody] RecordCivilEngineeringSiteInspectionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RecordSiteInspectionAsync(fileId, request, CorrelationId, token)));

    [HttpGet("{fileId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid fileId, CancellationToken token) => ExecuteAsync(async () => Ok(await service.GetHistoryAsync(fileId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringDevelopmentApprovalFileNotFoundException exception) { return NotFound(Problem(404, "Development approval file not found", exception.Message)); }
        catch (CivilEngineeringDevelopmentApprovalFileConflictException exception) { return Conflict(Problem(409, "Development approval file conflict", exception.Message)); }
        catch (CivilEngineeringDevelopmentApprovalFileValidationException exception) { return BadRequest(Problem(400, "Development approval file validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "Development approval file access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail, Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-development-approval-file-{status}",
        Extensions = { ["code"] = $"CIVIL_DEVELOPMENT_APPROVAL_FILE_{status}", ["correlationId"] = CorrelationId }
    };
}

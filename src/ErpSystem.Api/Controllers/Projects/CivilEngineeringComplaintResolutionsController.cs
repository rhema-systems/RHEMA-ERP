using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/complaint-resolutions")]
public sealed class CivilEngineeringComplaintResolutionsController(ICivilEngineeringComplaintResolutionService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{helpdeskTicketId:guid}/timeline")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public async Task<IActionResult> Timeline(Guid helpdeskTicketId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.GetTimelineAsync(helpdeskTicketId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ProblemDetails(404, "Civil complaint resolution not found", exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ProblemDetails(400, "Civil complaint resolution validation failed", exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(403, ProblemDetails(403, "Civil complaint resolution access forbidden", exception.Message));
        }
    }

    private ProblemDetails ProblemDetails(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-complaint-resolution-{status}",
        Extensions = { ["code"] = $"CIVIL_COMPLAINT_RESOLUTION_{status}", ["correlationId"] = HttpContext.TraceIdentifier }
    };
}

using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/access-controls")]
[Authorize]
public sealed class ProcurementAccessControlsController : ControllerBase
{
    private const string AccessManagementPolicy = "procurement.access.manage";
    private readonly IProcurementAccessControlService _service;

    public ProcurementAccessControlsController(IProcurementAccessControlService service) => _service = service;

    [HttpGet("readiness"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> GetReadiness(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetReadinessAsync(cancellationToken)));

    [HttpGet("roles"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> GetRoles(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetRolesAsync(cancellationToken)));

    [HttpGet("permissions"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> GetPermissions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetPermissionsAsync(cancellationToken)));

    [HttpGet("users"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> GetUsers(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetUsersAsync(cancellationToken)));

    [HttpGet("warehouses"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> GetWarehouses(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetWarehousesAsync(cancellationToken)));

    [HttpGet("locations"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> GetLocations(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetLocationsAsync(cancellationToken)));

    [HttpGet("assignments"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> GetAssignments(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAssignmentsAsync(cancellationToken)));

    [HttpPost("assignments"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> CreateAssignment(
        [FromBody] SaveProcurementResponsibilityAssignmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var assignment = await _service.SaveAssignmentAsync(null, request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(GetAssignments), assignment);
        });

    [HttpPut("assignments/{id:guid}"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> UpdateAssignment(
        Guid id,
        [FromBody] SaveProcurementResponsibilityAssignmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SaveAssignmentAsync(id, request, CorrelationId, cancellationToken)));

    [HttpGet("committees"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> GetCommittees(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetCommitteesAsync(cancellationToken)));

    [HttpPut("committees/{id:guid}"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> UpdateCommittee(
        Guid id,
        [FromBody] UpdateProcurementCommitteeRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.UpdateCommitteeAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("committees/{committeeId:guid}/members"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> AddCommitteeMember(
        Guid committeeId,
        [FromBody] SaveProcurementCommitteeMemberRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.AddCommitteeMemberAsync(committeeId, request, CorrelationId, cancellationToken)));

    [HttpDelete("committees/{committeeId:guid}/members/{memberId:guid}"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> RemoveCommitteeMember(
        Guid committeeId,
        Guid memberId,
        [FromBody] RemoveProcurementCommitteeMemberRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            await _service.RemoveCommitteeMemberAsync(committeeId, memberId, request, CorrelationId, cancellationToken);
            return NoContent();
        });

    [HttpGet("workflows"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> GetWorkflows(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetWorkflowsAsync(cancellationToken)));

    [HttpPost("capabilities/check")]
    public Task<IActionResult> CheckCapability(
        [FromBody] ProcurementAccessCapabilityRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.CheckCapabilityAsync(request, CorrelationId, cancellationToken)));

    [HttpPost("capabilities/enforce")]
    public Task<IActionResult> EnforceCapability(
        [FromBody] ProcurementAccessCapabilityRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var decision = await _service.EnforceCapabilityAsync(request, CorrelationId, cancellationToken);
            return decision.Allowed ? Ok(decision) : StatusCode(StatusCodes.Status403Forbidden, decision);
        });

    [HttpGet("audit"), Authorize(Policy = AccessManagementPolicy)]
    public Task<IActionResult> GetAudit([FromQuery] int take = 100, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.GetAuditAsync(take, cancellationToken)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementAccessNotFoundException exception)
        {
            return NotFound(Problem(404, "Procurement access record not found", exception.Message));
        }
        catch (ProcurementAccessAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "Procurement access forbidden", exception.Message));
        }
        catch (ProcurementAccessConflictException exception)
        {
            return Conflict(Problem(409, "Procurement access conflict", exception.Message));
        }
        catch (ProcurementAccessValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = new[] { exception.Message }
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Procurement access validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions = { ["correlationId"] = CorrelationId }
    };
}

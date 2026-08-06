using System.Security.Claims;
using ErpSystem.Core.DTOs.Identity;
using ErpSystem.Core.Entities.Identity;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Administration;

[ApiController]
[Route("api/administration/hr-identity-reconciliation")]
[Authorize]
public sealed class HrIdentityReconciliationController : ControllerBase
{
    private readonly IHrIdentityReconciliationService _service;
    private readonly ICurrentUserService _currentUser;

    public HrIdentityReconciliationController(
        IHrIdentityReconciliationService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = "HrIdentityReconciliationRead")]
    public async Task<ActionResult<HrIdentityReconciliationDashboardDto>> GetDashboard(
        CancellationToken cancellationToken)
        => Ok(await _service.GetDashboardAsync(RequireTenantId(), cancellationToken));

    [HttpGet("replacement-users")]
    [Authorize(Policy = "HrIdentityReconciliationManage")]
    public async Task<ActionResult<IReadOnlyList<HrIdentityUserOptionDto>>> GetReplacementUsers(
        CancellationToken cancellationToken)
        => Ok(await _service.GetEligibleReplacementUsersAsync(RequireTenantId(), cancellationToken));

    [HttpPost("runs")]
    [Authorize(Policy = "HrIdentityReconciliationManage")]
    public async Task<ActionResult<HrIdentityReconciliationRunDto>> Run(
        [FromBody] HrIdentityReconciliationRunRequest? request,
        CancellationToken cancellationToken)
        => Ok(await _service.RunAsync(
            RequireTenantId(),
            RequireUserId(),
            HrIdentityReconciliationTrigger.Manual,
            request?.IdempotencyKey,
            cancellationToken: cancellationToken));

    [HttpPost("runs/{runId:guid}/retry")]
    [Authorize(Policy = "HrIdentityReconciliationManage")]
    public async Task<ActionResult<HrIdentityReconciliationRunDto>> Retry(
        Guid runId,
        CancellationToken cancellationToken)
        => Ok(await _service.RetryFailedRunAsync(
            RequireTenantId(), runId, RequireUserId(), cancellationToken));

    [HttpPost("issues/{issueId:guid}/resolve")]
    [Authorize(Policy = "HrIdentityReconciliationManage")]
    public async Task<ActionResult<HrIdentityWorkflowIssueDto>> ResolveIssue(
        Guid issueId,
        [FromBody] HrIdentityWorkflowIssueResolutionRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.ResolveWorkflowIssueAsync(
            RequireTenantId(),
            issueId,
            request.ReplacementUserId,
            RequireUserId(),
            request.ResolutionNote,
            cancellationToken));

    [HttpPost("users/{userId:guid}/reactivate")]
    [Authorize(Policy = "HrIdentityReconciliationManage")]
    public async Task<ActionResult<HrIdentityReconciliationStateDto>> Reactivate(
        Guid userId,
        [FromBody] HrIdentityReactivationRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.ApproveReactivationAsync(
            RequireTenantId(), userId, RequireUserId(), request.ReviewNote, cancellationToken));

    private Guid RequireTenantId()
        => _currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new UnauthorizedAccessException("A valid tenant claim is required.");

    private Guid RequireUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty
            ? userId
            : throw new UnauthorizedAccessException("A valid user claim is required.");
}

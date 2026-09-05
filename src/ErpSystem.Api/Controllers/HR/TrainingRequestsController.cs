using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-requests")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class TrainingRequestsController : ControllerBase
{
    private readonly ITrainingRequestService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;

    public TrainingRequestsController(
        ITrainingRequestService service,
        ICurrentUserService currentUser,
        IAuthorizationService authorization)
    {
        _service = service;
        _currentUser = currentUser;
        _authorization = authorization;
    }

    /// <summary>Self-or-permission (W3), as on LeavesController — see the remarks there.</summary>
    private async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (_currentUser.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
            return true;
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<Core.DTOs.Common.PagedResult<TrainingRequestSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingRequestDto>> GetById(Guid id, CancellationToken ct)
    {
        var request = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(request.EmployeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(request);
    }

    [HttpGet("number/{requestNumber}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingRequestDto?>> GetByRequestNumber(string requestNumber, CancellationToken ct)
        => Ok(await _service.GetByRequestNumberAsync(requestNumber, ct));

    /// <summary>The caller's own training requests, taken from the token.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<TrainingRequestSummaryDto>>> GetMine(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId.Value, ct));
    }

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingRequestSummaryDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingRequestSummaryDto>>> GetByStatus(TrainingRequestStatus status, CancellationToken ct)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("pending-approval")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingRequestSummaryDto>>> GetPendingApproval(CancellationToken ct)
        => Ok(await _service.GetPendingApprovalAsync(ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<TrainingRequestDto>> Create([FromBody] CreateTrainingRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        // W3: the request's subject comes from the body — an employee may file their own,
        // raising one for someone else is the desk's act.
        if (!await SelfOrPolicyAsync(dto.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        // W3: deleting one's own request is withdrawal (the attendance-slice shape); deleting
        // someone else's is administration.
        var request = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(request.EmployeeId, HrPermissions.TrainingAdminPolicy))
            return Forbid();

        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TrainingRequestDto>> Update(Guid id, [FromBody] UpdateTrainingRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        // W3: amending is the requester's act or the desk's — ownership is read off the stored
        // record, not the payload.
        var existing = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(existing.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var request = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(request.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        await _service.SubmitAsync(id, employeeId.Value, ct);
        return Ok(new { message = "Training request submitted for approval." });
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveTrainingRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.RequestId = id;
        // Returns the updated record rather than a bare message — the service already builds the
        // full DTO through its includes chain.
        return Ok(await _service.ApproveAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectTrainingRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.RequestId = id;
        await _service.RejectAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Training request rejected." });
    }
}

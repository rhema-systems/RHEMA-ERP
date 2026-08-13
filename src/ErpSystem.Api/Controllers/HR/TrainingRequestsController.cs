using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-requests")]
[Authorize]
[TrainingBusinessRulesAttribute]
public class TrainingRequestsController : ControllerBase
{
    private readonly ITrainingRequestService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingRequestsController(ITrainingRequestService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("paged")]
    public async Task<ActionResult<Core.DTOs.Common.PagedResult<TrainingRequestSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingRequestDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("number/{requestNumber}")]
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
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<TrainingRequestSummaryDto>>> GetByStatus(TrainingRequestStatus status, CancellationToken ct)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("pending-approval")]
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

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TrainingRequestDto>> Update(Guid id, [FromBody] UpdateTrainingRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
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

        await _service.SubmitAsync(id, employeeId.Value, ct);
        return Ok(new { message = "Training request submitted for approval." });
    }

    [HttpPost("{id:guid}/approve")]
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

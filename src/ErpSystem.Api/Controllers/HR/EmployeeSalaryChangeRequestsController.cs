using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Salary change requests (round 3, lane S). Raise, amend, submit; approve / reject / recall through
/// the engine; retry applying. Reads and writes sit on the compensation policies; approve, reject
/// and recall carry no policy of their own because the published definition decides who may — the
/// service refuses anyone the engine has not assigned.
/// </summary>
[ApiController]
[Route("api/hr/salary-change-requests")]
[Authorize(Policy = "InternalOnly")]
public class EmployeeSalaryChangeRequestsController : ControllerBase
{
    private readonly IEmployeeSalaryChangeRequestService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<EmployeeSalaryChangeRequestsController> _logger;

    public EmployeeSalaryChangeRequestsController(
        IEmployeeSalaryChangeRequestService service,
        ICurrentUserService currentUser,
        ILogger<EmployeeSalaryChangeRequestsController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? employeeId, [FromQuery] SalaryChangeRequestStatus? status, CancellationToken ct)
        => Ok(await _service.GetAllAsync(employeeId, status, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    public Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Run(() => _service.GetByIdAsync(id, ct), id, "reading the request");

    [HttpPost]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    public Task<IActionResult> Create([FromBody] CreateSalaryChangeRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Task.FromResult<IActionResult>(BadRequest(ModelState));
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Your user account is not linked to an employee record, so a request cannot be attributed to you." }));
        return Run(() => _service.CreateAsync(dto, employeeId.Value, ct), Guid.Empty, "raising the request", created: true);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateSalaryChangeRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Task.FromResult<IActionResult>(BadRequest(ModelState));
        return Run(() => _service.UpdateAsync(id, dto, ct), id, "amending the request");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _service.DeleteAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return StatusCode(422, new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    public Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => Run(() => _service.SubmitAsync(id, ct), id, "submitting the request");

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => Run(() => _service.ApproveAsync(id, _currentUser.EmployeeId, ct), id, "approving the request");

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, [FromBody] RejectSalaryChangeRequestDto? dto, CancellationToken ct)
        => Run(() => _service.RejectAsync(id, dto?.Reason, _currentUser.EmployeeId, ct), id, "rejecting the request");

    [HttpPost("{id:guid}/recall")]
    public Task<IActionResult> Recall(Guid id, CancellationToken ct)
        => Run(() => _service.RecallAsync(id, ct), id, "recalling the request");

    [HttpPost("{id:guid}/retry-apply")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    public Task<IActionResult> RetryApply(Guid id, CancellationToken ct)
        => Run(() => _service.RetryApplyAsync(id, ct), id, "applying the request");

    private async Task<IActionResult> Run(Func<Task<SalaryChangeRequestDto>> action, Guid id, string description, bool created = false)
    {
        try
        {
            var dto = await action();
            return created ? CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto) : Ok(dto);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return StatusCode(422, new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error {Description} for salary change request {Id}", description, id);
            return StatusCode(500, new { message = $"An error occurred while {description}." });
        }
    }
}

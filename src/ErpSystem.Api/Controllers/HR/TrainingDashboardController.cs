using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-dashboard")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class TrainingDashboardController : ControllerBase
{
    private readonly ITrainingDashboardService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingDashboardController(ITrainingDashboardService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingDashboardDto>> GetDashboard(
        [FromQuery] int? year = null,
        CancellationToken ct = default)
        => Ok(await _service.GetDashboardAsync(year, ct));

    [HttpGet("analytics")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingAnalyticsDto>> GetAnalytics([FromQuery] int? year = null, CancellationToken ct = default)
        => Ok(await _service.GetAnalyticsAsync(year, ct));

    /// <summary>
    /// The caller's own training record — the "My Training" hub's read.
    ///
    /// Token-derived, so no employee id crosses the wire for a self-service page. Without this the
    /// only way to build that page was to pass your own id to the by-employee route, which is the
    /// exact shape that turns into a read-anyone's-record hole.
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<EmployeeTrainingSummaryDto>> GetMySummary(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.GetEmployeeSummaryAsync(employeeId.Value, ct));
    }

    /// <summary>
    /// Someone else's training record. Restricted to the desk read — it carries compliance
    /// standing and certificate history, which is not colleague-readable simply because both
    /// work here. (W3: converted from the HR-role gate to the Training permission family.)
    /// </summary>
    [HttpGet("employee/{employeeId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<EmployeeTrainingSummaryDto>> GetEmployeeSummary(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetEmployeeSummaryAsync(employeeId, ct));
}

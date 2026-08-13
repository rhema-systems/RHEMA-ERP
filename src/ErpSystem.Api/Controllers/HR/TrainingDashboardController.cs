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
[Authorize]
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

    private const string HrRoles =
        Constants.Roles.SuperAdmin + "," + Constants.Roles.TenantAdmin + "," + Constants.Roles.Hr;

    [HttpGet]
    public async Task<ActionResult<TrainingDashboardDto>> GetDashboard(
        [FromQuery] int? year = null,
        CancellationToken ct = default)
        => Ok(await _service.GetDashboardAsync(year, ct));

    [HttpGet("analytics")]
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
    /// Someone else's training record. Restricted to HR — it carries compliance standing and
    /// certificate history, which is not colleague-readable simply because both work here.
    /// </summary>
    [HttpGet("employee/{employeeId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<EmployeeTrainingSummaryDto>> GetEmployeeSummary(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetEmployeeSummaryAsync(employeeId, ct));
}

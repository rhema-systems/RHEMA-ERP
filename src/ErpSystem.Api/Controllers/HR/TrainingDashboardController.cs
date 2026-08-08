using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-dashboard")]
[Authorize]
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
    public async Task<ActionResult<TrainingDashboardDto>> GetDashboard(
        [FromQuery] int? year = null,
        CancellationToken ct = default)
        => Ok(await _service.GetDashboardAsync(year, ct));

    [HttpGet("analytics")]
    public async Task<ActionResult<TrainingAnalyticsDto>> GetAnalytics([FromQuery] int? year = null, CancellationToken ct = default)
        => Ok(await _service.GetAnalyticsAsync(year, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<EmployeeTrainingSummaryDto>> GetEmployeeSummary(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetEmployeeSummaryAsync(employeeId, ct));
}

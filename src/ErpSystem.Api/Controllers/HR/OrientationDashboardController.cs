using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Tenant-wide roll-ups plus named overdue participants and certificate holders — an HR view of
/// everyone, so HR only.
/// </summary>
[ApiController]
[OrientationBusinessRules]
[Route("api/orientation-dashboard")]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
public class OrientationDashboardController : ControllerBase
{
    private readonly IOrientationDashboardService _service;

    public OrientationDashboardController(IOrientationDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<OrientationDashboardDto>> GetDashboard()
        => Ok(await _service.GetDashboardAsync());
}

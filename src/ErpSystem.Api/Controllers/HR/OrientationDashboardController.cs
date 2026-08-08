using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/orientation-dashboard")]
[Authorize]
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

using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/medical/dashboard")]
[Authorize]
public class MedicalDashboardController : ControllerBase
{
    private readonly IMedicalDashboardService _service;

    public MedicalDashboardController(IMedicalDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<MedicalDashboardDto>> Get([FromQuery] int upcomingDays = 30, CancellationToken ct = default)
        => Ok(await _service.GetDashboardAsync(upcomingDays, ct));
}

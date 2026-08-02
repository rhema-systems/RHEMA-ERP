using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/medical/dashboard")]
// Medical records are special-category personal data. This controller previously carried a
// bare [Authorize], so any authenticated employee could read them. Read is the class-level
// floor; write and delete are tightened per action.
[Authorize(Policy = HrPermissions.MedicalReadPolicy)]
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

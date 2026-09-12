using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/dashboard")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheDashboardController : SheApiControllerBase
{
    private readonly ISheDashboardService _service;

    public SheDashboardController(ISheDashboardService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheDashboardDto>> Get()
        => Ok(await _service.GetAsync());
}

using ErpSystem.Api.Services;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Developer Test Data — the HR seed as three buttons (Administration → HR → HR Settings). See
/// <see cref="IHrTestDataSeedService"/> for what each tier seeds and why the Workforce and Logins
/// tiers refuse beside real staff.
/// </summary>
/// <remarks>
/// Both attributes apply (stacked <c>[Authorize]</c> is ANDed): an internal user AND a SuperAdmin or
/// TenantAdmin. The service narrows it further — a TenantAdmin only of the DEFAULT tenant, because
/// DEFAULT is what gets seeded.
/// </remarks>
[ApiController]
[Route("api/administration/hr-test-data")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
public class HrTestDataController : ControllerBase
{
    private readonly IHrTestDataSeedService _service;
    private readonly ICurrentUserService _currentUser;

    public HrTestDataController(IHrTestDataSeedService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>What each tier holds, which demo logins exist, and the current or last run.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HrTestDataStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HrTestDataStatusDto>> GetStatus(CancellationToken ct)
        => Ok(await _service.GetStatusAsync(ct));

    /// <summary>
    /// Starts a seed of <paramref name="tier"/> (and every tier before it) in the background. Poll
    /// <c>GET</c> for its progress.
    /// </summary>
    [HttpPost("{tier}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Run(HrTestDataTier tier, CancellationToken ct)
    {
        if (!Enum.IsDefined(tier))
            return BadRequest(new { message = "Unknown tier. Use Foundation, Workforce or Logins." });

        var result = await _service.StartAsync(
            tier,
            _currentUser.UserName ?? _currentUser.UserId ?? "unknown",
            _currentUser.TenantId,
            _currentUser.IsInRole(Constants.Roles.SuperAdmin),
            ct);

        var body = new { message = result.Message };
        return result.Outcome switch
        {
            HrTestDataStartOutcome.Started => Accepted(body),
            HrTestDataStartOutcome.Disabled or HrTestDataStartOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, body),
            HrTestDataStartOutcome.Busy => Conflict(body),
            _ => UnprocessableEntity(body),
        };
    }
}

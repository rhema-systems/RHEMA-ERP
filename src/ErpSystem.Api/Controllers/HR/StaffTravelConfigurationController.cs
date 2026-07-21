using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/configuration")]
[Authorize]
public class StaffTravelConfigurationController : ControllerBase
{
    private readonly IStaffTravelConfigurationService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffTravelConfigurationController(IStaffTravelConfigurationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private (Guid tenantId, Guid userId)? ResolveContext()
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null || userId is null) return null;
        return (tenantId.Value, userId.Value);
    }

    // =========================================================================
    // CURRENCY EXCHANGE RATES
    // =========================================================================

    [HttpGet("exchange-rates/{id:guid}")]
    public async Task<ActionResult<StaffTravelCurrencyExchangeRateDto>> GetById(Guid id)
        => Ok(await _service.GetRateByIdAsync(id));

    [HttpGet("exchange-rates/latest")]
    public async Task<ActionResult<StaffTravelCurrencyExchangeRateDto?>> GetLatestRate(
        [FromQuery] string fromCurrency, [FromQuery] string toCurrency)
        => Ok(await _service.GetLatestRateAsync(fromCurrency, toCurrency));

    [HttpGet("exchange-rates/on-date")]
    public async Task<ActionResult<StaffTravelCurrencyExchangeRateDto?>> GetRateOnDate(
        [FromQuery] string fromCurrency, [FromQuery] string toCurrency, [FromQuery] DateOnly date)
        => Ok(await _service.GetRateOnDateAsync(fromCurrency, toCurrency, date));

    [HttpGet("exchange-rates/by-date")]
    public async Task<ActionResult<IEnumerable<StaffTravelCurrencyExchangeRateDto>>> GetRatesByDate([FromQuery] DateOnly rateDate)
        => Ok(await _service.GetRatesByDateAsync(rateDate));

    [HttpPost("exchange-rates")]
    public async Task<ActionResult<StaffTravelCurrencyExchangeRateDto>> Create([FromBody] CreateStaffTravelCurrencyExchangeRateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateRateAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("exchange-rates/{id:guid}")]
    public async Task<ActionResult<StaffTravelCurrencyExchangeRateDto>> Update(Guid id, [FromBody] UpdateStaffTravelCurrencyExchangeRateDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateRateAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("exchange-rates/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteRateAsync(id);
        return NoContent();
    }
}

using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Company-wide HR policy settings (retirement ages, probation/notice defaults, alert
/// lead times, org-wide defaults). One record per tenant, GET + PUT.
/// </summary>
/// <remarks>
/// <para><b>Read and write are gated differently, on purpose.</b> HR works inside these numbers
/// every day — the probation length, the notice periods, the retirement ages — and needs to see
/// them. But three of the knobs here are not ordinary configuration:
/// <see cref="CompanyHrPolicySettingsDto.ProceduralAbsenceDays"/> decides when HR may terminate
/// someone <i>without</i> the Managing Director's signature (FR-HR-092), and the two enforcement
/// modes decide whether exceeding an approved manpower budget or an authorised establishment blocks
/// a hire or merely warns (FR-HR-136). Those are the trust boundaries the settings exist to hold,
/// and moving them is administration's, not the function the boundary constrains.</para>
///
/// <para>So (W3 slice 14, preserving the prior role gate's line exactly): <b>read</b> =
/// <c>HR.Company.Read</c>, which HR's Read+Write grant satisfies; <b>write</b> =
/// <c>HR.Company.Admin</c>, which it does not — the write stays with administrators. Slice 0
/// measured a plain <c>Employee</c> doing both. If TDC would rather HR held the write as well,
/// granting the HR role <c>HR.Company.Admin</c> is the whole change.</para>
/// </remarks>
[ApiController]
[Route("api/hr/policy-settings")]
[Authorize(Policy = "InternalOnly")]
public class CompanyHrPolicySettingsController : ControllerBase
{
    private readonly ICompanyHrPolicySettingsService _service;
    private readonly ILogger<CompanyHrPolicySettingsController> _logger;

    public CompanyHrPolicySettingsController(
        ICompanyHrPolicySettingsService service,
        ILogger<CompanyHrPolicySettingsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Get the current tenant's HR policy settings (coded defaults if none saved yet).</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    [ProducesResponseType(typeof(CompanyHrPolicySettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _service.GetAsync(cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company HR policy settings");
            return StatusCode(500, "An error occurred while retrieving HR policy settings");
        }
    }

    /// <summary>Update (upsert) the current tenant's HR policy settings.</summary>
    [HttpPut]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    [ProducesResponseType(typeof(CompanyHrPolicySettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update([FromBody] UpdateCompanyHrPolicySettingsDto dto, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _service.UpdateAsync(dto, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company HR policy settings");
            return StatusCode(500, "An error occurred while updating HR policy settings");
        }
    }
}

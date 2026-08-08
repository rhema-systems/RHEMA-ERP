using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Company-wide HR policy settings (retirement ages, probation/notice defaults, alert
/// lead times, org-wide defaults). One record per tenant, GET + PUT.
/// </summary>
[ApiController]
[Route("api/hr/policy-settings")]
[Authorize]
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
    [ProducesResponseType(typeof(CompanyHrPolicySettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

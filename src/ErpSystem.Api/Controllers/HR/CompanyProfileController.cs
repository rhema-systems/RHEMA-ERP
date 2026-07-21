using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The tenant's company (legal-employer) profile — identity, statutory numbers, registered address,
/// contacts and document-presentation details. One record per tenant, GET + PUT.
/// </summary>
[ApiController]
[Route("api/hr/company-profile")]
[Authorize]
public class CompanyProfileController : ControllerBase
{
    private readonly ICompanyProfileService _service;
    private readonly ILogger<CompanyProfileController> _logger;

    public CompanyProfileController(
        ICompanyProfileService service,
        ILogger<CompanyProfileController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Get the current tenant's company profile (Tenant/config-resolved defaults if none saved yet).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CompanyProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company profile");
            return StatusCode(500, "An error occurred while retrieving the company profile");
        }
    }

    /// <summary>Update (upsert) the current tenant's company profile.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(CompanyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateCompanyProfileDto dto, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            return Ok(await _service.UpdateAsync(dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company profile");
            return StatusCode(500, "An error occurred while updating the company profile");
        }
    }
}

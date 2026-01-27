using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/license-types")]
[Authorize]
public class LicenseTypesController : ControllerBase
{
    private readonly ILicenseTypeService _licenseTypeService;
    private readonly ILogger<LicenseTypesController> _logger;

    public LicenseTypesController(
        ILicenseTypeService licenseTypeService,
        ILogger<LicenseTypesController> logger)
    {
        _licenseTypeService = licenseTypeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenseTypeDto>>> GetAllLicenseTypes()
    {
        try
        {
            var licenseTypes = await _licenseTypeService.GetAllLicenseTypesAsync();
            return Ok(licenseTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving license types");
            return StatusCode(500, "An error occurred while retrieving license types");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<LicenseTypeDto>>> GetActiveLicenseTypes()
    {
        try
        {
            var licenseTypes = await _licenseTypeService.GetActiveLicenseTypesAsync();
            return Ok(licenseTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active license types");
            return StatusCode(500, "An error occurred while retrieving active license types");
        }
    }

    [HttpGet("mandatory")]
    public async Task<ActionResult<IEnumerable<LicenseTypeDto>>> GetMandatoryLicenseTypes([FromQuery] string applicableTo = "Both")
    {
        try
        {
            var licenseTypes = await _licenseTypeService.GetMandatoryLicenseTypesAsync(applicableTo);
            return Ok(licenseTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving mandatory license types");
            return StatusCode(500, "An error occurred while retrieving mandatory license types");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LicenseTypeDto>> GetLicenseType(Guid id)
    {
        try
        {
            var licenseType = await _licenseTypeService.GetByIdAsync(id);
            if (licenseType == null)
            {
                return NotFound();
            }

            return Ok(licenseType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving license type {LicenseTypeId}", id);
            return StatusCode(500, "An error occurred while retrieving the license type");
        }
    }

    [HttpPost]
    public async Task<ActionResult<LicenseTypeDto>> CreateLicenseType([FromBody] CreateLicenseTypeDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var licenseType = await _licenseTypeService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetLicenseType), new { id = licenseType.Id }, licenseType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating license type");
            return StatusCode(500, "An error occurred while creating the license type");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LicenseTypeDto>> UpdateLicenseType(Guid id, [FromBody] UpdateLicenseTypeDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var licenseType = await _licenseTypeService.UpdateAsync(id, updateDto);
            return Ok(licenseType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating license type {LicenseTypeId}", id);
            return StatusCode(500, "An error occurred while updating the license type");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLicenseType(Guid id)
    {
        try
        {
            await _licenseTypeService.DeleteAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting license type {LicenseTypeId}", id);
            return StatusCode(500, "An error occurred while deleting the license type");
        }
    }
}


using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class AppraisalSettingsController : ControllerBase
{
    private readonly IAppraisalSettingsService _settingsService;
    private readonly ILogger<AppraisalSettingsController> _logger;

    public AppraisalSettingsController(IAppraisalSettingsService settingsService, ILogger<AppraisalSettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Get all appraisal settings
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppraisalSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _settingsService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all appraisal settings");
            return StatusCode(500, "An error occurred while retrieving appraisal settings");
        }
    }

    /// <summary>
    /// Get appraisal settings with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<AppraisalSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _settingsService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged appraisal settings");
            return StatusCode(500, "An error occurred while retrieving appraisal settings");
        }
    }

    /// <summary>
    /// Get appraisal settings by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _settingsService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal settings with {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the appraisal settings");
        }
    }

    /// <summary>
    /// Get default appraisal settings
    /// </summary>
    [HttpGet("default")]
    [ProducesResponseType(typeof(AppraisalSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDefault()
    {
        try
        {
            var response = await _settingsService.GetDefaultSettingsAsync();
            if (response == null)
                return NotFound(new { message = "No default appraisal settings found" });
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving default appraisal settings");
            return StatusCode(500, "An error occurred while retrieving default appraisal settings");
        }
    }

    /// <summary>
    /// Create new appraisal settings
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalSettingsDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _settingsService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal settings");
            return StatusCode(500, "An error occurred while creating the appraisal settings");
        }
    }

    /// <summary>
    /// Update existing appraisal settings
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalSettingsDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _settingsService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (AppraisalConfigurationLockedException ex)
        {
            // In use (performance closure E-e, D-67): appraisals read its rules — a conflict with its state, 409.
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            // An unknown profile, or another tenant's: it answered 400.
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal settings with Id {SettingsId}", id);
            return StatusCode(500, "An error occurred while updating the appraisal settings");
        }
    }

    /// <summary>
    /// Validate weights for appraisal settings
    /// </summary>
    [HttpGet("{id:guid}/validate-weights")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateWeights(Guid id)
    {
        try
        {
            var response = await _settingsService.ValidateWeightsAsync(id);
            return Ok(new { isValid = response, message = response ? "Weights are valid" : "Weights do not sum to 1.0" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating weights for appraisal settings with {Id}", id);
            return StatusCode(500, "An error occurred while validating weights");
        }
    }

    /// <summary>
    /// Make this profile the tenant's default, and no other (performance closure B6, P-2). The
    /// default was "the newest profile", which any new or test profile won.
    /// </summary>
    [HttpPost("{id:guid}/make-default")]
    [ProducesResponseType(typeof(AppraisalSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> MakeDefault(Guid id)
    {
        try
        {
            var response = await _settingsService.MakeDefaultAsync(id, HttpContext.RequestAborted);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error making appraisal settings {SettingsId} the default", id);
            return StatusCode(500, "An error occurred while making the appraisal settings the default");
        }
    }

    /// <summary>
    /// Copy a profile under a new name (performance closure E-e, D-46): how the rules of a profile in use change. The
    /// copy is not the default; a cycle takes it when created, or once it is made the default.
    /// </summary>
    [HttpPost("{id:guid}/clone")]
    [ProducesResponseType(typeof(AppraisalSettingsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Clone(Guid id, [FromBody] CloneAppraisalSettingsDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _settingsService.CloneAsync(id, dto, HttpContext.RequestAborted);
            return StatusCode(201, response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying appraisal settings {SettingsId}", id);
            return StatusCode(500, "An error occurred while copying the appraisal settings");
        }
    }

    /// <summary>
    /// Delete appraisal settings
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _settingsService.DeleteAsync(id);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting appraisal settings with Id {SettingsId}", id);
            return StatusCode(500, "An error occurred while deleting the appraisal settings");
        }
    }
}

using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/contractor-specializations")]
[Authorize]
public class ContractorSpecializationsController : ControllerBase
{
    private readonly IContractorSpecializationService _specializationService;
    private readonly ILogger<ContractorSpecializationsController> _logger;

    public ContractorSpecializationsController(
        IContractorSpecializationService specializationService,
        ILogger<ContractorSpecializationsController> _logger)
    {
        _specializationService = specializationService;
        this._logger = _logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContractorSpecializationDto>>> GetAllSpecializations()
    {
        try
        {
            var specializations = await _specializationService.GetAllSpecializationsAsync();
            return Ok(specializations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contractor specializations");
            return StatusCode(500, "An error occurred while retrieving contractor specializations");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ContractorSpecializationDto>>> GetActiveSpecializations()
    {
        try
        {
            var specializations = await _specializationService.GetActiveSpecializationsAsync();
            return Ok(specializations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active contractor specializations");
            return StatusCode(500, "An error occurred while retrieving active contractor specializations");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContractorSpecializationDto>> GetSpecialization(Guid id)
    {
        try
        {
            var specialization = await _specializationService.GetByIdAsync(id);
            if (specialization == null)
            {
                return NotFound();
            }

            return Ok(specialization);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contractor specialization {SpecializationId}", id);
            return StatusCode(500, "An error occurred while retrieving the contractor specialization");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ContractorSpecializationDto>> CreateSpecialization([FromBody] CreateContractorSpecializationDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var specialization = await _specializationService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetSpecialization), new { id = specialization.Id }, specialization);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contractor specialization");
            return StatusCode(500, "An error occurred while creating the contractor specialization");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContractorSpecializationDto>> UpdateSpecialization(Guid id, [FromBody] UpdateContractorSpecializationDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var specialization = await _specializationService.UpdateAsync(id, updateDto);
            return Ok(specialization);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contractor specialization {SpecializationId}", id);
            return StatusCode(500, "An error occurred while updating the contractor specialization");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSpecialization(Guid id)
    {
        try
        {
            await _specializationService.DeleteAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contractor specialization {SpecializationId}", id);
            return StatusCode(500, "An error occurred while deleting the contractor specialization");
        }
    }
}


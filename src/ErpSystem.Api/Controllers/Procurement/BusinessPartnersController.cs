using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/business-partners")]
[Authorize]
public class BusinessPartnersController : ControllerBase
{
    private readonly IBusinessPartnerService _partnerService;
    private readonly ILogger<BusinessPartnersController> _logger;

    public BusinessPartnersController(
        IBusinessPartnerService partnerService,
        ILogger<BusinessPartnersController> logger)
    {
        _partnerService = partnerService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of business partners with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<BusinessPartnerDto>>> GetPartners(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? partnerType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? approvalStatus = null,
        [FromQuery] Guid? categoryId = null)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;

            var categoryIds = categoryId.HasValue ? new List<Guid> { categoryId.Value } : null;
            var result = await _partnerService.GetPartnersAsync(
                page, pageSize, search, partnerType, status, approvalStatus, null, null, categoryIds, null);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partners");
            return StatusCode(500, "An error occurred while retrieving business partners");
        }
    }

    /// <summary>
    /// Gets a business partner by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessPartnerDetailDto>> GetPartner(Guid id)
    {
        try
        {
            var partner = await _partnerService.GetByIdAsync(id);
            if (partner == null)
                return NotFound();

            return Ok(partner);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partner {PartnerId}", id);
            return StatusCode(500, "An error occurred while retrieving the business partner");
        }
    }

    /// <summary>
    /// Gets a business partner by code
    /// </summary>
    [HttpGet("by-code/{partnerCode}")]
    public async Task<ActionResult<BusinessPartnerDto>> GetPartnerByCode(string partnerCode)
    {
        try
        {
            var partner = await _partnerService.GetByCodeAsync(partnerCode);
            if (partner == null)
                return NotFound();

            return Ok(partner);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partner by code {PartnerCode}", partnerCode);
            return StatusCode(500, "An error occurred while retrieving the business partner");
        }
    }

    /// <summary>
    /// Gets active business partners
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerDto>>> GetActivePartners([FromQuery] string? partnerType = null)
    {
        try
        {
            var partners = await _partnerService.GetActivePartnersAsync(partnerType);
            return Ok(partners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active business partners");
            return StatusCode(500, "An error occurred while retrieving active business partners");
        }
    }

    /// <summary>
    /// Gets business partners by category
    /// </summary>
    [HttpGet("by-category/{categoryId:guid}")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerDto>>> GetPartnersByCategory(Guid categoryId)
    {
        try
        {
            var partners = await _partnerService.GetPartnersByCategoryAsync(categoryId);
            return Ok(partners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partners by category {CategoryId}", categoryId);
            return StatusCode(500, "An error occurred while retrieving business partners");
        }
    }

    /// <summary>
    /// Gets contractors by specialization
    /// </summary>
    [HttpGet("contractors/by-specialization/{specializationId:guid}")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerDto>>> GetContractorsBySpecialization(Guid specializationId)
    {
        try
        {
            var partners = await _partnerService.GetPartnersBySpecializationAsync(specializationId);
            return Ok(partners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contractors by specialization {SpecializationId}", specializationId);
            return StatusCode(500, "An error occurred while retrieving contractors");
        }
    }

    /// <summary>
    /// Creates a new business partner
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BusinessPartnerDetailDto>> CreatePartner([FromBody] CreateBusinessPartnerDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var partner = await _partnerService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetPartner), new { id = partner.Id }, partner);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating business partner");
            return StatusCode(500, "An error occurred while creating the business partner");
        }
    }

    /// <summary>
    /// Updates an existing business partner
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BusinessPartnerDetailDto>> UpdatePartner(Guid id, [FromBody] UpdateBusinessPartnerDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var partner = await _partnerService.UpdateAsync(id, updateDto);
            return Ok(partner);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating business partner {PartnerId}", id);
            return StatusCode(500, "An error occurred while updating the business partner");
        }
    }

    /// <summary>
    /// Deletes a business partner
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePartner(Guid id)
    {
        try
        {
            await _partnerService.DeleteAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting business partner {PartnerId}", id);
            return StatusCode(500, "An error occurred while deleting the business partner");
        }
    }

    /// <summary>
    /// Approves a business partner
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApprovePartner(Guid id, [FromBody] Guid approvedById)
    {
        try
        {
            await _partnerService.ApprovePartnerAsync(id, approvedById);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving business partner {PartnerId}", id);
            return StatusCode(500, "An error occurred while approving the business partner");
        }
    }

    /// <summary>
    /// Rejects a business partner
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectPartner(Guid id, [FromBody] RejectPartnerRequest request)
    {
        try
        {
            await _partnerService.RejectPartnerAsync(id, request.RejectedById, request.RejectionReason);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting business partner {PartnerId}", id);
            return StatusCode(500, "An error occurred while rejecting the business partner");
        }
    }

    /// <summary>
    /// Suspends a business partner
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> SuspendPartner(Guid id, [FromBody] SuspendPartnerRequest request)
    {
        try
        {
            await _partnerService.UpdateStatusAsync(id, "Suspended");
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending business partner {PartnerId}", id);
            return StatusCode(500, "An error occurred while suspending the business partner");
        }
    }

    /// <summary>
    /// Activates a business partner
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> ActivatePartner(Guid id)
    {
        try
        {
            await _partnerService.UpdateStatusAsync(id, "Active");
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating business partner {PartnerId}", id);
            return StatusCode(500, "An error occurred while activating the business partner");
        }
    }
}

// Request models
public record RejectPartnerRequest(Guid RejectedById, string? RejectionReason);
public record SuspendPartnerRequest(string? SuspensionReason);

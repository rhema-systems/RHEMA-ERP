using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/business-partner-registrations")]
[Authorize]
public class BusinessPartnerRegistrationsController : ControllerBase
{
    private readonly IBusinessPartnerRegistrationService _registrationService;
    private readonly ILogger<BusinessPartnerRegistrationsController> _logger;

    public BusinessPartnerRegistrationsController(
        IBusinessPartnerRegistrationService registrationService,
        ILogger<BusinessPartnerRegistrationsController> logger)
    {
        _registrationService = registrationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of registrations with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<BusinessPartnerRegistrationDto>>> GetRegistrations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? partnerType = null,
        [FromQuery] string? status = null)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;

            var result = await _registrationService.GetRegistrationsAsync(page, pageSize, search, partnerType, status);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partner registrations");
            return StatusCode(500, "An error occurred while retrieving business partner registrations");
        }
    }

    /// <summary>
    /// Gets registrations for the current user (external portal)
    /// </summary>
    [HttpGet("my-registrations")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerRegistrationDto>>> GetMyRegistrations()
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var registrations = await _registrationService.GetMyRegistrationsAsync(userId);
            return Ok(registrations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user registrations");
            return StatusCode(500, "An error occurred while retrieving your registrations");
        }
    }

    /// <summary>
    /// Gets pending review registrations (internal admin)
    /// </summary>
    [HttpGet("pending-review")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerRegistrationDto>>> GetPendingReviewRegistrations()
    {
        try
        {
            var registrations = await _registrationService.GetPendingReviewRegistrationsAsync();
            return Ok(registrations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending review registrations");
            return StatusCode(500, "An error occurred while retrieving pending review registrations");
        }
    }

    /// <summary>
    /// Gets a registration by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessPartnerRegistrationDetailDto>> GetRegistration(Guid id)
    {
        try
        {
            var registration = await _registrationService.GetByIdAsync(id);
            if (registration == null)
                return NotFound();

            return Ok(registration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while retrieving the business partner registration");
        }
    }

    /// <summary>
    /// Gets a registration by application number
    /// </summary>
    [HttpGet("by-application-number/{applicationNumber}")]
    public async Task<ActionResult<BusinessPartnerRegistrationDto>> GetRegistrationByApplicationNumber(string applicationNumber)
    {
        try
        {
            var registration = await _registrationService.GetByApplicationNumberAsync(applicationNumber);
            if (registration == null)
                return NotFound();

            return Ok(registration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partner registration by application number {ApplicationNumber}", applicationNumber);
            return StatusCode(500, "An error occurred while retrieving the business partner registration");
        }
    }

    /// <summary>
    /// Creates a new registration (external portal - draft)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BusinessPartnerRegistrationDetailDto>> CreateRegistration([FromBody] CreateBusinessPartnerRegistrationDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var registration = await _registrationService.CreateAsync(createDto, userId);
            return CreatedAtAction(nameof(GetRegistration), new { id = registration.Id }, registration);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating business partner registration");
            return StatusCode(500, "An error occurred while creating the business partner registration");
        }
    }

    /// <summary>
    /// Updates an existing registration (external portal - draft only)
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BusinessPartnerRegistrationDetailDto>> UpdateRegistration(Guid id, [FromBody] UpdateBusinessPartnerRegistrationDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var registration = await _registrationService.UpdateAsync(id, updateDto, userId);
            return Ok(registration);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while updating the business partner registration");
        }
    }

    /// <summary>
    /// Deletes a registration (draft only)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRegistration(Guid id)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _registrationService.DeleteAsync(id, userId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while deleting the business partner registration");
        }
    }

    /// <summary>
    /// Submits a registration for review (external portal)
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitRegistration(Guid id)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _registrationService.SubmitForReviewAsync(id, userId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while submitting the business partner registration");
        }
    }

    /// <summary>
    /// Reviews a registration (internal admin)
    /// </summary>
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewRegistration(Guid id, [FromBody] ReviewRegistrationRequest request)
    {
        try
        {
            var reviewDto = new ReviewBusinessPartnerRegistrationDto
            {
                RegistrationId = id,
                Notes = request.ReviewNotes,
                Action = "Review"
            };
            await _registrationService.ReviewRegistrationAsync(reviewDto, request.ReviewedById);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while reviewing the business partner registration");
        }
    }

    /// <summary>
    /// Approves a registration and creates business partner (internal admin)
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<BusinessPartnerDetailDto>> ApproveRegistration(Guid id, [FromBody] ApproveRegistrationRequest request)
    {
        try
        {
            var partner = await _registrationService.ConvertToBusinessPartnerAsync(id, request.ApprovedById);
            return Ok(partner);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while approving the business partner registration");
        }
    }
}

// Request models
public record ReviewRegistrationRequest(Guid ReviewedById, string? ReviewNotes);
public record ApproveRegistrationRequest(Guid ApprovedById);


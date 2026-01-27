using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/business-partner-users")]
[Authorize]
public class BusinessPartnerUsersController : ControllerBase
{
    private readonly IBusinessPartnerUserService _userService;
    private readonly ILogger<BusinessPartnerUsersController> _logger;

    public BusinessPartnerUsersController(
        IBusinessPartnerUserService userService,
        ILogger<BusinessPartnerUsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users for a business partner
    /// </summary>
    [HttpGet("business-partner/{businessPartnerId}")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerUserDto>>> GetByBusinessPartnerId(Guid businessPartnerId)
    {
        try
        {
            var users = await _userService.GetUsersByBusinessPartnerIdAsync(businessPartnerId);
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users for business partner {BusinessPartnerId}", businessPartnerId);
            return StatusCode(500, "An error occurred while retrieving users");
        }
    }

    /// <summary>
    /// Get a specific business partner user by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<BusinessPartnerUserDto>> GetById(Guid id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound($"Business partner user with ID {id} not found");
            }
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting business partner user {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the user");
        }
    }

    /// <summary>
    /// Get business partner user by user ID
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<BusinessPartnerUserDto>> GetByUserId(Guid userId)
    {
        try
        {
            var user = await _userService.GetByUserIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Business partner user with user ID {userId} not found");
            }
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting business partner user by user ID {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving the user");
        }
    }

    /// <summary>
    /// Create a new business partner user
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BusinessPartnerUserDto>> Create([FromBody] CreateBusinessPartnerUserDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while creating business partner user");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating business partner user");
            return StatusCode(500, "An error occurred while creating the user");
        }
    }

    /// <summary>
    /// Update an existing business partner user
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<BusinessPartnerUserDto>> Update(Guid id, [FromBody] UpdateBusinessPartnerUserDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.UpdateAsync(id, dto);
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while updating business partner user {Id}", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating business partner user {Id}", id);
            return StatusCode(500, "An error occurred while updating the user");
        }
    }

    /// <summary>
    /// Activate a business partner user
    /// </summary>
    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        try
        {
            await _userService.ActivateAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while activating business partner user {Id}", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating business partner user {Id}", id);
            return StatusCode(500, "An error occurred while activating the user");
        }
    }

    /// <summary>
    /// Deactivate a business partner user
    /// </summary>
    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        try
        {
            await _userService.DeactivateAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while deactivating business partner user {Id}", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating business partner user {Id}", id);
            return StatusCode(500, "An error occurred while deactivating the user");
        }
    }

    /// <summary>
    /// Delete a business partner user
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _userService.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting business partner user {Id}", id);
            return StatusCode(500, "An error occurred while deleting the user");
        }
    }

    /// <summary>
    /// Check if a user has access to a business partner
    /// </summary>
    [HttpGet("has-access")]
    public async Task<ActionResult<bool>> HasAccess([FromQuery] Guid userId, [FromQuery] Guid businessPartnerId)
    {
        try
        {
            var hasAccess = await _userService.HasAccessAsync(userId, businessPartnerId);
            return Ok(hasAccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking access for user {UserId} to business partner {BusinessPartnerId}", userId, businessPartnerId);
            return StatusCode(500, "An error occurred while checking access");
        }
    }

    /// <summary>
    /// Reset password for a business partner user
    /// </summary>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest("New password is required");
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest("Password must be at least 6 characters long");
            }

            await _userService.ResetPasswordAsync(id, request.NewPassword);
            return Ok(new { message = "Password reset successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while resetting password for user {Id}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for business partner user {Id}", id);
            return StatusCode(500, "An error occurred while resetting the password");
        }
    }
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}


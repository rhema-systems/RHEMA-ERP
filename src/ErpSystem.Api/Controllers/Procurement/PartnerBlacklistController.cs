using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/partner-blacklist")]
[Authorize]
public class PartnerBlacklistController : ControllerBase
{
    private readonly IPartnerBlacklistService _blacklistService;
    private readonly ILogger<PartnerBlacklistController> _logger;

    public PartnerBlacklistController(
        IPartnerBlacklistService blacklistService,
        ILogger<PartnerBlacklistController> logger)
    {
        _blacklistService = blacklistService;
        _logger = logger;
    }

    /// <summary>
    /// Blacklists a partner
    /// </summary>
    [HttpPost("partners/{partnerId:guid}/blacklist")]
    public async Task<IActionResult> BlacklistPartner(Guid partnerId, [FromBody] BlacklistPartnerRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _blacklistService.AddToBlacklistAsync(partnerId, request.Reason, userId, request.BlacklistUntil);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blacklisting partner {PartnerId}", partnerId);
            return StatusCode(500, "An error occurred while blacklisting the partner");
        }
    }

    /// <summary>
    /// Removes a partner from blacklist
    /// </summary>
    [HttpPost("partners/{partnerId:guid}/remove-from-blacklist")]
    public async Task<IActionResult> RemoveFromBlacklist(Guid partnerId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _blacklistService.RemoveFromBlacklistAsync(partnerId, userId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing partner {PartnerId} from blacklist", partnerId);
            return StatusCode(500, "An error occurred while removing the partner from blacklist");
        }
    }

    /// <summary>
    /// Checks if a partner is blacklisted
    /// </summary>
    [HttpGet("partners/{partnerId:guid}/is-blacklisted")]
    public async Task<ActionResult<bool>> IsPartnerBlacklisted(Guid partnerId)
    {
        try
        {
            var isBlacklisted = await _blacklistService.IsBlacklistedAsync(partnerId);
            return Ok(isBlacklisted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking blacklist status for partner {PartnerId}", partnerId);
            return StatusCode(500, "An error occurred while checking blacklist status");
        }
    }

    /// <summary>
    /// Gets all blacklisted partners
    /// </summary>
    [HttpGet("blacklisted-partners")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerDto>>> GetBlacklistedPartners()
    {
        try
        {
            var partners = await _blacklistService.GetBlacklistedPartnersAsync();
            return Ok(partners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blacklisted partners");
            return StatusCode(500, "An error occurred while getting blacklisted partners");
        }
    }

    /// <summary>
    /// Gets partners with expiring blacklists
    /// </summary>
    [HttpGet("expiring-blacklists")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerDto>>> GetExpiringBlacklists([FromQuery] int daysAhead = 30)
    {
        try
        {
            var partners = await _blacklistService.GetPartnersWithExpiringBlacklistAsync(daysAhead);
            return Ok(partners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring blacklists");
            return StatusCode(500, "An error occurred while getting expiring blacklists");
        }
    }
}

// Request models
public record BlacklistPartnerRequest(string Reason, DateTime? BlacklistUntil);


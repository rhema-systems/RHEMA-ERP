using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Public endpoints for candidate offer response — no authentication required.
/// Secured via one-time token embedded in the offer email link.
/// </summary>
[ApiController]
[Route("api/offer-response")]
[AllowAnonymous]
[EnableRateLimiting("PublicPortalPolicy")]
public class OfferResponseController : ControllerBase
{
    private readonly IJobOfferService _offerService;
    private readonly ILogger<OfferResponseController> _logger;

    public OfferResponseController(IJobOfferService offerService, ILogger<OfferResponseController> logger)
    {
        _offerService = offerService;
        _logger       = logger;
    }

    /// <summary>
    /// Validates a candidate token and returns a public-safe offer summary.
    /// Returns 200 even for expired/used tokens — the payload flags indicate the state.
    /// </summary>
    [HttpGet("validate")]
    public async Task<ActionResult<CandidateOfferSummaryDto>> Validate([FromQuery] Guid token, CancellationToken ct)
    {
        if (token == Guid.Empty)
            return BadRequest(new { message = "Invalid token." });

        var summary = await _offerService.ValidateCandidateTokenAsync(token, ct);
        return Ok(summary);
    }

    /// <summary>
    /// Records the candidate's accept / negotiate / decline response via their token.
    /// The token is marked as used on success so it cannot be resubmitted.
    /// </summary>
    [HttpPost("respond")]
    public async Task<IActionResult> Respond([FromBody] CandidateResponseDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _offerService.RecordCandidateResponseAsync(dto, ct);
            return Ok(new { message = "Your response has been recorded. Thank you." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Offer response rejected: {Reason}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Offer response bad argument: {Reason}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }
}

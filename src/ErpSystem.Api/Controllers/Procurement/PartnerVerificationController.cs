using System.Security.Claims;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/partner-verification")]
[Authorize]
public class PartnerVerificationController : ControllerBase
{
    private readonly IPartnerVerificationService _verificationService;
    private readonly IPartnerPerformanceService _performanceService;
    private readonly IPartnerBlacklistService _blacklistService;
    private readonly ILogger<PartnerVerificationController> _logger;

    public PartnerVerificationController(
        IPartnerVerificationService verificationService,
        IPartnerPerformanceService performanceService,
        IPartnerBlacklistService blacklistService,
        ILogger<PartnerVerificationController> logger)
    {
        _verificationService = verificationService;
        _performanceService = performanceService;
        _blacklistService = blacklistService;
        _logger = logger;
    }

    /// <summary>
    /// Verifies a document
    /// </summary>
    [HttpPost("partners/{partnerId:guid}/documents/{documentId:guid}/verify")]
    public async Task<IActionResult> VerifyDocument(Guid partnerId, Guid documentId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _verificationService.VerifyDocumentAsync(partnerId, documentId, userId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while verifying the document");
        }
    }

    /// <summary>
    /// Verifies a license
    /// </summary>
    [HttpPost("partners/{partnerId:guid}/licenses/{licenseId:guid}/verify")]
    public async Task<IActionResult> VerifyLicense(Guid partnerId, Guid licenseId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _verificationService.VerifyLicenseAsync(partnerId, licenseId, userId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying license {LicenseId}", licenseId);
            return StatusCode(500, "An error occurred while verifying the license");
        }
    }

    /// <summary>
    /// Gets unverified documents for a partner
    /// </summary>
    [HttpGet("partners/{partnerId:guid}/unverified-documents")]
    public async Task<ActionResult<IEnumerable<Guid>>> GetUnverifiedDocuments(Guid partnerId)
    {
        try
        {
            var documentIds = await _verificationService.GetUnverifiedDocumentsAsync(partnerId);
            return Ok(documentIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unverified documents for partner {PartnerId}", partnerId);
            return StatusCode(500, "An error occurred while getting unverified documents");
        }
    }

    /// <summary>
    /// Updates partner rating
    /// </summary>
    [HttpPost("partners/{partnerId:guid}/rating")]
    public async Task<IActionResult> UpdatePartnerRating(Guid partnerId, [FromBody] UpdateRatingRequest request)
    {
        try
        {
            await _performanceService.UpdatePerformanceRatingAsync(partnerId, request.Rating);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rating for partner {PartnerId}", partnerId);
            return StatusCode(500, "An error occurred while updating partner rating");
        }
    }

}

// Request models
public record UpdateRatingRequest(decimal Rating);


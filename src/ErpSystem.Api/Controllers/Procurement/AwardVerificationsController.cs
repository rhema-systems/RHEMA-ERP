using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class AwardVerificationsController : ControllerBase
{
    private readonly IAwardVerificationService _service;
    private readonly ILogger<AwardVerificationsController> _logger;

    public AwardVerificationsController(
        IAwardVerificationService service,
        ILogger<AwardVerificationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    #region Checklist Template Endpoints

    /// <summary>
    /// Get all checklist templates
    /// </summary>
    [HttpGet("templates")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<AwardVerificationChecklistTemplateDto>>> GetAllTemplates([FromQuery] bool includeInactive = false)
    {
        try
        {
            var templates = await _service.GetAllTemplatesAsync(includeInactive);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checklist templates");
            return StatusCode(500, "An error occurred while retrieving checklist templates");
        }
    }

    /// <summary>
    /// Get paged checklist templates
    /// </summary>
    [HttpGet("templates/paged")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<PagedResult<AwardVerificationChecklistTemplateDto>>> GetTemplatesPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool includeInactive = false)
    {
        try
        {
            var result = await _service.GetTemplatesPagedAsync(page, pageSize, search, includeInactive);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged checklist templates");
            return StatusCode(500, "An error occurred while retrieving checklist templates");
        }
    }

    /// <summary>
    /// Get a checklist template by ID
    /// </summary>
    [HttpGet("templates/{id}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<AwardVerificationChecklistTemplateDto>> GetTemplateById(Guid id)
    {
        try
        {
            var template = await _service.GetTemplateByIdAsync(id);
            if (template == null)
                return NotFound($"Template with ID {id} not found");
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checklist template {TemplateId}", id);
            return StatusCode(500, "An error occurred while retrieving the checklist template");
        }
    }

    /// <summary>
    /// Get the default checklist template
    /// </summary>
    [HttpGet("templates/default")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<AwardVerificationChecklistTemplateDto>> GetDefaultTemplate()
    {
        try
        {
            var template = await _service.GetDefaultTemplateAsync();
            if (template == null)
                return NotFound("No default template found");
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting default checklist template");
            return StatusCode(500, "An error occurred while retrieving the default template");
        }
    }

    /// <summary>
    /// Get templates applicable for a contract value
    /// </summary>
    [HttpGet("templates/by-value")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<AwardVerificationChecklistTemplateDto>>> GetTemplatesByContractValue([FromQuery] decimal contractValue)
    {
        try
        {
            var templates = await _service.GetTemplatesByContractValueAsync(contractValue);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting templates for contract value {ContractValue}", contractValue);
            return StatusCode(500, "An error occurred while retrieving templates");
        }
    }

    /// <summary>
    /// Create a new checklist template
    /// </summary>
    [HttpPost("templates")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<AwardVerificationChecklistTemplateDto>> CreateTemplate([FromBody] CreateAwardVerificationChecklistTemplateDto dto)
    {
        try
        {
            var template = await _service.CreateTemplateAsync(dto);
            return CreatedAtAction(nameof(GetTemplateById), new { id = template.Id }, template);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checklist template");
            return StatusCode(500, "An error occurred while creating the checklist template");
        }
    }

    /// <summary>
    /// Update a checklist template
    /// </summary>
    [HttpPut("templates/{id}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<AwardVerificationChecklistTemplateDto>> UpdateTemplate(Guid id, [FromBody] UpdateAwardVerificationChecklistTemplateDto dto)
    {
        try
        {
            var template = await _service.UpdateTemplateAsync(id, dto);
            return Ok(template);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating checklist template {TemplateId}", id);
            return StatusCode(500, "An error occurred while updating the checklist template");
        }
    }

    /// <summary>
    /// Delete a checklist template
    /// </summary>
    [HttpDelete("templates/{id}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        try
        {
            await _service.DeleteTemplateAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting checklist template {TemplateId}", id);
            return StatusCode(500, "An error occurred while deleting the checklist template");
        }
    }

    /// <summary>
    /// Add an item to a template
    /// </summary>
    [HttpPost("templates/{templateId}/items")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<AwardVerificationChecklistItemDto>> AddTemplateItem(Guid templateId, [FromBody] CreateAwardVerificationChecklistItemDto dto)
    {
        try
        {
            var item = await _service.AddTemplateItemAsync(templateId, dto);
            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to template {TemplateId}", templateId);
            return StatusCode(500, "An error occurred while adding the item");
        }
    }

    /// <summary>
    /// Update a template item
    /// </summary>
    [HttpPut("templates/items/{itemId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<AwardVerificationChecklistItemDto>> UpdateTemplateItem(Guid itemId, [FromBody] CreateAwardVerificationChecklistItemDto dto)
    {
        try
        {
            var item = await _service.UpdateTemplateItemAsync(itemId, dto);
            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while updating the item");
        }
    }

    /// <summary>
    /// Delete a template item
    /// </summary>
    [HttpDelete("templates/items/{itemId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> DeleteTemplateItem(Guid itemId)
    {
        try
        {
            await _service.DeleteTemplateItemAsync(itemId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while deleting the item");
        }
    }

    #endregion

    #region Verification Process Endpoints

    /// <summary>
    /// Start a verification process for a tender
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardVerificationDto>> StartVerification([FromBody] StartAwardVerificationDto dto)
    {
        try
        {
            var verification = await _service.StartVerificationAsync(dto);
            return CreatedAtAction(nameof(GetVerificationById), new { id = verification.Id }, verification);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting verification for tender {TenderId}", dto.TenderId);
            return StatusCode(500, "An error occurred while starting the verification");
        }
    }

    /// <summary>
    /// Get verification by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<TenderAwardVerificationDto>> GetVerificationById(Guid id)
    {
        try
        {
            var verification = await _service.GetVerificationByIdAsync(id);
            if (verification == null)
                return NotFound($"Verification with ID {id} not found");
            return Ok(verification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting verification {VerificationId}", id);
            return StatusCode(500, "An error occurred while retrieving the verification");
        }
    }

    /// <summary>
    /// Get verification by tender ID
    /// </summary>
    [HttpGet("by-tender/{tenderId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<TenderAwardVerificationDto>> GetVerificationByTenderId(Guid tenderId)
    {
        try
        {
            var verification = await _service.GetVerificationByTenderIdAsync(tenderId);
            if (verification == null)
                return NotFound($"No verification found for tender {tenderId}");
            return Ok(verification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting verification for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while retrieving the verification");
        }
    }

    /// <summary>
    /// Get pending verifications
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<TenderAwardVerificationDto>>> GetPendingVerifications()
    {
        try
        {
            var verifications = await _service.GetPendingVerificationsAsync();
            return Ok(verifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending verifications");
            return StatusCode(500, "An error occurred while retrieving pending verifications");
        }
    }

    /// <summary>
    /// Verify a checklist item for a bidder
    /// </summary>
    [HttpPost("verify-item")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardVerificationItemResultDto>> VerifyChecklistItem([FromBody] VerifyChecklistItemDto dto)
    {
        try
        {
            var result = await _service.VerifyChecklistItemAsync(dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying checklist item");
            return StatusCode(500, "An error occurred while verifying the item");
        }
    }

    /// <summary>
    /// Complete verification for a bidder
    /// </summary>
    [HttpPost("complete-bidder")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardVerificationBidderDto>> CompleteBidderVerification([FromBody] CompleteBidderVerificationDto dto)
    {
        try
        {
            var bidder = await _service.CompleteBidderVerificationAsync(dto);
            return Ok(bidder);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing bidder verification");
            return StatusCode(500, "An error occurred while completing the bidder verification");
        }
    }

    /// <summary>
    /// Complete the entire verification process
    /// </summary>
    [HttpPost("{id}/complete")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardVerificationDto>> CompleteVerification(Guid id, [FromBody] CompleteVerificationDto dto)
    {
        try
        {
            var verification = await _service.CompleteVerificationAsync(id, dto);
            return Ok(verification);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing verification {VerificationId}", id);
            return StatusCode(500, "An error occurred while completing the verification");
        }
    }

    /// <summary>
    /// Cancel a verification process
    /// </summary>
    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> CancelVerification(Guid id, [FromQuery] string? reason = null)
    {
        try
        {
            await _service.CancelVerificationAsync(id, reason);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling verification {VerificationId}", id);
            return StatusCode(500, "An error occurred while cancelling the verification");
        }
    }

    #endregion

    #region Document Endpoints

    /// <summary>
    /// Upload a document for a verification item result
    /// </summary>
    [HttpPost("item-results/{itemResultId}/documents")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardVerificationItemDocumentDto>> UploadDocument(Guid itemResultId, [FromBody] UploadVerificationDocumentDto dto)
    {
        try
        {
            var document = await _service.UploadDocumentAsync(itemResultId, dto);
            return CreatedAtAction(nameof(GetDocumentById), new { documentId = document.Id }, document);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document for item result {ItemResultId}", itemResultId);
            return StatusCode(500, "An error occurred while uploading the document");
        }
    }

    /// <summary>
    /// Get documents for a verification item result
    /// </summary>
    [HttpGet("item-results/{itemResultId}/documents")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<TenderAwardVerificationItemDocumentDto>>> GetDocumentsByItemResultId(Guid itemResultId)
    {
        try
        {
            var documents = await _service.GetDocumentsByItemResultIdAsync(itemResultId);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents for item result {ItemResultId}", itemResultId);
            return StatusCode(500, "An error occurred while retrieving documents");
        }
    }

    /// <summary>
    /// Get a document by ID
    /// </summary>
    [HttpGet("documents/{documentId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<TenderAwardVerificationItemDocumentDto>> GetDocumentById(Guid documentId)
    {
        try
        {
            var document = await _service.GetDocumentByIdAsync(documentId);
            if (document == null)
                return NotFound($"Document with ID {documentId} not found");
            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while retrieving the document");
        }
    }

    /// <summary>
    /// Delete a document
    /// </summary>
    [HttpDelete("documents/{documentId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> DeleteDocument(Guid documentId)
    {
        try
        {
            await _service.DeleteDocumentAsync(documentId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while deleting the document");
        }
    }

    #endregion
}

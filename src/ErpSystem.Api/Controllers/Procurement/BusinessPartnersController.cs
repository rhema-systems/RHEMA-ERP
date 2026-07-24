using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/business-partners")]
[Authorize]
public class BusinessPartnersController : ControllerBase
{
    private readonly IBusinessPartnerService _partnerService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<BusinessPartnersController> _logger;
    private readonly IProcurementMasterDataChangeService? _masterDataChanges;

    public BusinessPartnersController(
        IBusinessPartnerService partnerService,
        ICurrentUserProvider currentUserProvider,
        IWorkflowService workflowService,
        ILogger<BusinessPartnersController> logger,
        IProcurementMasterDataChangeService? masterDataChanges = null)
    {
        _partnerService = partnerService;
        _currentUserProvider = currentUserProvider;
        _workflowService = workflowService;
        _logger = logger;
        _masterDataChanges = masterDataChanges;
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
            {
                pageSize = 100;
            }

            var categoryIds = categoryId.HasValue ? new List<Guid> { categoryId.Value } : null;
            var result = await _partnerService.GetPartnersAsync(
                page, pageSize, search, partnerType, status, approvalStatus, null, null, categoryIds, null);

            // Best-effort: populate current workflow step name for partners pending approval.
            // This keeps list UIs from polling per-row.
            var pending = result.Items
                .Where(p => string.Equals(p.ApprovalStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (pending.Count > 0)
            {
                await Task.WhenAll(pending.Select(async dto =>
                {
                    try
                    {
                        var step = await _workflowService.GetCurrentWorkflowStepAsync("BusinessPartner", dto.Id);
                        dto.CurrentWorkflowStepName = step?.StepName;
                    }
                    catch
                    {
                        // ignore
                    }
                }));
            }

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
            {
                return NotFound();
            }

            if (string.Equals(partner.ApprovalStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var step = await _workflowService.GetCurrentWorkflowStepAsync("BusinessPartner", id);
                    partner.CurrentWorkflowStepName = step?.StepName;
                }
                catch
                {
                    // ignore
                }
            }

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
            {
                return NotFound();
            }

            return Ok(partner);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partner by code {PartnerCode}", partnerCode);
            return StatusCode(500, "An error occurred while retrieving the business partner");
        }
    }

    /// <summary>
    /// Gets a business partner by user ID
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<BusinessPartnerDetailDto>> GetPartnerByUserId(Guid userId)
    {
        try
        {
            var partner = await _partnerService.GetByUserIdAsync(userId);
            if (partner == null)
            {
                return NotFound($"Business partner for user ID {userId} not found");
            }

            return Ok(partner);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partner by user ID {UserId}", userId);
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
            var protection = await GuardDirectMutationAsync(null, "BusinessPartner.Create");
            if (protection is not null) return protection;
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

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
            var protection = await GuardDirectMutationAsync(id, "BusinessPartner.Update");
            if (protection is not null) return protection;
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

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
            var protection = await GuardDirectMutationAsync(id, "BusinessPartner.Delete");
            if (protection is not null) return protection;
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
    /// Submits a business partner for approval (unified workflow)
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitPartner(Guid id)
    {
        try
        {
            await _partnerService.SubmitPartnerForApprovalAsync(id, _currentUserProvider.UserId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting business partner {PartnerId} for approval", id);
            return StatusCode(500, "An error occurred while submitting the business partner for approval");
        }
    }

    /// <summary>
    /// Approves a business partner
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApprovePartner(Guid id, [FromBody] ApprovePartnerRequest? request)
    {
        try
        {
            await _partnerService.ApprovePartnerAsync(id, _currentUserProvider.UserId, request?.Notes);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
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
            if (string.IsNullOrWhiteSpace(request?.RejectionReason))
            {
                return BadRequest("Rejection reason is required");
            }

            await _partnerService.RejectPartnerAsync(id, _currentUserProvider.UserId, request.RejectionReason);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
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

    /// <summary>
    /// Gets contacts for a business partner
    /// </summary>
    [HttpGet("{id:guid}/contacts")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerContactDto>>> GetContacts(Guid id)
    {
        try
        {
            var contacts = await _partnerService.GetContactsAsync(id);
            return Ok(contacts);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contacts for business partner {PartnerId}", id);
            return StatusCode(500, "An error occurred while retrieving business partner contacts");
        }
    }

    /// <summary>
    /// Adds a contact to a business partner
    /// </summary>
    [HttpPost("{id:guid}/contacts")]
    public async Task<ActionResult<BusinessPartnerContactDto>> CreateContact(Guid id, [FromBody] CreateBusinessPartnerContactDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var contact = await _partnerService.AddContactAsync(id, dto);
            return CreatedAtAction(nameof(GetContacts), new { id }, contact);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contact for business partner {PartnerId}", id);
            return StatusCode(500, "An error occurred while creating the business partner contact");
        }
    }

    /// <summary>
    /// Updates a business partner contact
    /// </summary>
    [HttpPut("{id:guid}/contacts/{contactId:guid}")]
    public async Task<ActionResult<BusinessPartnerContactDto>> UpdateContact(Guid id, Guid contactId, [FromBody] CreateBusinessPartnerContactDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var contact = await _partnerService.UpdateContactAsync(id, contactId, dto);
            return Ok(contact);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contact {ContactId} for business partner {PartnerId}", contactId, id);
            return StatusCode(500, "An error occurred while updating the business partner contact");
        }
    }

    /// <summary>
    /// Deletes a business partner contact
    /// </summary>
    [HttpDelete("{id:guid}/contacts/{contactId:guid}")]
    public async Task<IActionResult> DeleteContact(Guid id, Guid contactId)
    {
        try
        {
            await _partnerService.DeleteContactAsync(id, contactId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contact {ContactId} for business partner {PartnerId}", contactId, id);
            return StatusCode(500, "An error occurred while deleting the business partner contact");
        }
    }

    /// <summary>
    /// Sets the primary contact for a business partner
    /// </summary>
    [HttpPost("{id:guid}/contacts/{contactId:guid}/set-primary")]
    public async Task<IActionResult> SetPrimaryContact(Guid id, Guid contactId)
    {
        try
        {
            await _partnerService.SetPrimaryContactAsync(id, contactId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting primary contact {ContactId} for business partner {PartnerId}", contactId, id);
            return StatusCode(500, "An error occurred while updating the primary business partner contact");
        }
    }

    /// <summary>
    /// Downloads a business partner document
    /// </summary>
    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId)
    {
        try
        {
            var documents = await _partnerService.GetDocumentsAsync(id);
            var document = documents.FirstOrDefault(d => d.Id == documentId);

            if (document == null)
            {
                return NotFound("Document not found");
            }

            if (string.IsNullOrEmpty(document.FilePath) || !System.IO.File.Exists(document.FilePath))
            {
                _logger.LogError("Document file not found at path: {FilePath}", document.FilePath);
                return NotFound("Document file not found on server");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(document.FilePath);
            var contentType = document.MimeType ?? "application/octet-stream";

            return File(fileBytes, contentType, document.DocumentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {DocumentId} for business partner {PartnerId}", documentId, id);
            return StatusCode(500, "An error occurred while downloading the document");
        }
    }

    private async Task<ObjectResult?> GuardDirectMutationAsync(Guid? id, string action)
    {
        if (_masterDataChanges is null) return null;
        var decision = await _masterDataChanges.CheckDirectMutationAsync(
            new[] { ProcurementMasterDataResourceType.SupplierProfile, ProcurementMasterDataResourceType.SupplierBankDetails, ProcurementMasterDataResourceType.SupplierTaxDetails },
            id, action, HttpContext.TraceIdentifier, HttpContext.RequestAborted);
        return decision.Allowed ? null : Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Staged supplier change required",
            Detail = decision.Message,
            Instance = HttpContext.Request.Path,
            Extensions = { ["code"] = decision.Code, ["correlationId"] = decision.CorrelationId, ["policyId"] = decision.PolicyId }
        });
    }
}

// Request models
public record RejectPartnerRequest(string? RejectionReason);
public record ApprovePartnerRequest(string? Notes);
public record SuspendPartnerRequest(string? SuspensionReason);

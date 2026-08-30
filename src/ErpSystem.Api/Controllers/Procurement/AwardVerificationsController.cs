using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class AwardVerificationsController : ControllerBase
{
    private static readonly HashSet<string> SupportedDocumentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Certificate",
            "License",
            "Insurance",
            "Financial Statement",
            "Background Check",
            "Reference Letter",
            "Compliance Document",
            "Other"
        };

    private readonly IAwardVerificationService _service;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ILogger<AwardVerificationsController> _logger;

    public AwardVerificationsController(
        IAwardVerificationService service,
        ApplicationDbContext db,
        ICurrentUserProvider currentUser,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        ILogger<AwardVerificationsController> logger)
    {
        _service = service;
        _db = db;
        _currentUser = currentUser;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _logger = logger;
    }

    #region Checklist Template Endpoints

    /// <summary>
    /// Get all checklist templates
    /// </summary>
    [HttpGet("templates")]
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.tender.administer")]
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
    [Authorize(Policy = "procurement.tender.administer")]
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
    [Authorize(Policy = "procurement.tender.administer")]
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
    [Authorize(Policy = "procurement.tender.administer")]
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
    [Authorize(Policy = "procurement.tender.administer")]
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
    [Authorize(Policy = "procurement.tender.administer")]
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
    [Authorize(Policy = "procurement.tender.evaluate")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.tender.evaluate")]
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
    [Authorize(Policy = "procurement.tender.evaluate")]
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
    [Authorize(Policy = "procurement.tender.evaluate")]
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
    [Authorize(Policy = "procurement.tender.evaluate")]
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
    [Authorize(Policy = "procurement.tender.evaluate")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<TenderAwardVerificationItemDocumentDto>> UploadDocument(
        Guid itemResultId,
        [FromForm] IFormFile file,
        [FromForm] UploadVerificationDocumentDto dto)
    {
        try
        {
            var documentType = dto.DocumentType?.Trim();
            if (string.IsNullOrWhiteSpace(documentType) ||
                !SupportedDocumentTypes.Contains(documentType))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Unsupported verification document type",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Select a supported document type from the controlled list.",
                    Extensions = { ["code"] = "AWARD_VERIFICATION_DOCUMENT_TYPE_INVALID" }
                });
            }

            if (file is null || file.Length == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Verification document required",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Select a document file to upload.",
                    Extensions = { ["code"] = "AWARD_VERIFICATION_DOCUMENT_REQUIRED" }
                });
            }

            var source = await _db.TenderAwardVerificationItemResults
                .AsNoTracking()
                .Where(item => item.TenantId == _currentUser.TenantId &&
                               !item.IsDeleted &&
                               item.Id == itemResultId)
                .Select(item => new
                {
                    item.Id,
                    item.Status,
                    ChecklistItem = item.ChecklistItem.ItemText,
                    TenderNumber = item.Bidder.Verification.Tender.TenderNumber,
                    BidNumber = item.Bidder.TenderBid.BidNumber,
                    PartnerName = item.Bidder.BusinessPartner.PartnerName
                })
                .SingleOrDefaultAsync(HttpContext.RequestAborted);
            if (source is null)
            {
                return NotFound($"Verification item result with ID {itemResultId} not found.");
            }

            var safeFileName = Path.GetFileName(file.FileName);
            var actorName = string.IsNullOrWhiteSpace(_currentUser.FullName)
                ? _currentUser.Username
                : _currentUser.FullName;
            var upload = await _controlledFiles.UploadAsync(
                new ControlledFileUploadRequest
                {
                    TenantId = _currentUser.TenantId,
                    ActorUserId = _currentUser.UserId,
                    ActorName = actorName,
                    Category = ControlledFileUploadCategories.DocumentManagement,
                    FileName = safeFileName,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType,
                    FileSize = file.Length,
                    OpenReadStream = file.OpenReadStream
                },
                HttpContext.RequestAborted);

            CentralDocumentRepositoryLink centralDocument;
            try
            {
                centralDocument = await _centralDocuments.RegisterAsync(
                    new CentralDocumentRepositoryRegistration
                    {
                        TenantId = _currentUser.TenantId,
                        ActorUserId = _currentUser.UserId,
                        ActorName = actorName,
                        FileUploadRecordId = upload.Record.Id,
                        SourceModule = "Procurement",
                        SourceLabel = "Procurement / Award verification evidence",
                        SourceEntityType = "TenderAwardVerificationItemResult",
                        SourceRecordId = source.Id,
                        SourceRecordReference = $"{source.TenderNumber}/{source.BidNumber}",
                        Title = safeFileName,
                        DocumentType = "ProcurementApprovalEvidence",
                        MetadataTemplateCode = "TDC-PROC-APPROVAL",
                        AccessProfile = "Procurement approval restricted",
                        VersionStatus = "Submitted",
                        ChangeSummary = $"{documentType} uploaded for {source.ChecklistItem}.",
                        RequirePublishedGovernance = true,
                        MetadataValues =
                        [
                            new("sourceReference", "Source reference", $"{source.TenderNumber}/{source.BidNumber}"),
                            new("documentFamily", "Document family", "Award verification"),
                            new("classification", "Classification", documentType),
                            new("sourceStatus", "Source status", source.Status),
                            new("uploadedBy", "Uploaded by", actorName),
                            new("checksumSha256", "Checksum SHA-256", upload.ChecksumSha256),
                            new("supplier", "Supplier", source.PartnerName),
                            new("checklistItem", "Checklist item", source.ChecklistItem)
                        ]
                    },
                    HttpContext.RequestAborted);
            }
            catch
            {
                await _controlledFiles.DeleteAsync(
                    _currentUser.TenantId,
                    upload.Record.Id,
                    _currentUser.UserId,
                    HttpContext.RequestAborted);
                throw;
            }

            TenderAwardVerificationItemDocumentDto document;
            try
            {
                dto.DocumentType = documentType;
                document = await _service.UploadDocumentAsync(
                    itemResultId,
                    dto,
                    safeFileName,
                    $"dms:{centralDocument.DocumentRecordId:N}:version:{centralDocument.DocumentVersionId:N}",
                    upload.Record.ContentType,
                    upload.Record.FileSize,
                    upload.Record.Id,
                    centralDocument.DocumentRecordId,
                    centralDocument.DocumentVersionId);
            }
            catch
            {
                await _centralDocuments.DeleteAsync(
                    _currentUser.TenantId,
                    centralDocument.DocumentRecordId,
                    _currentUser.UserId,
                    HttpContext.RequestAborted);
                throw;
            }

            return CreatedAtAction(nameof(GetDocumentById), new { documentId = document.Id }, document);
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "Award verification evidence upload failed",
                Status = ex.StatusCode,
                Detail = ex.Message,
                Extensions = { ["code"] = ex.Code }
            });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Award verification evidence governance failed",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = ex.Message,
                Extensions = { ["code"] = "AWARD_VERIFICATION_DMS_GOVERNANCE_FAILED" }
            });
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    /// Download a verification document through the central DMS.
    /// </summary>
    [HttpGet("documents/{documentId}/download")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<IActionResult> DownloadDocument(Guid documentId)
    {
        try
        {
            var document = await _service.GetDocumentByIdAsync(documentId);
            if (document is null)
            {
                return NotFound($"Document with ID {documentId} not found");
            }

            if (document.CentralDocumentRecordId.HasValue &&
                document.CentralDocumentVersionId.HasValue)
            {
                var content = await _centralDocuments.OpenAsync(
                    _currentUser.TenantId,
                    document.CentralDocumentRecordId.Value,
                    document.CentralDocumentVersionId.Value,
                    HttpContext.RequestAborted);
                if (content is null || content.UploadRecord.VirusScanStatus != FileVirusScanStatus.Clean)
                {
                    if (content is not null) await content.DisposeAsync();
                    return NotFound("Document is unavailable in the central repository.");
                }

                return File(content.Content, content.ContentType, content.FileName,
                    enableRangeProcessing: true);
            }

            if (string.IsNullOrWhiteSpace(document.FilePath) ||
                !System.IO.File.Exists(document.FilePath))
            {
                return NotFound("Legacy document file not found on server.");
            }

            return PhysicalFile(
                Path.GetFullPath(document.FilePath),
                document.ContentType ?? "application/octet-stream",
                document.FileName,
                enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading verification document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while downloading the document");
        }
    }

    /// <summary>
    /// Delete a document
    /// </summary>
    [HttpDelete("documents/{documentId}")]
    [Authorize(Policy = "procurement.tender.evaluate")]
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

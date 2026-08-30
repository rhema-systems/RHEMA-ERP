using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class TenderBidsController : ControllerBase
{
    private readonly ITenderBidService _bidService;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IBusinessPartnerUserRepository _businessPartnerUserRepository;
    private readonly ITenderAssignmentRepository _assignmentRepository;
    private readonly ITenderPaymentRepository _paymentRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ILogger<TenderBidsController> _logger;

    public TenderBidsController(
        ITenderBidService bidService,
        IBusinessPartnerRepository businessPartnerRepository,
        IBusinessPartnerUserRepository businessPartnerUserRepository,
        ITenderAssignmentRepository assignmentRepository,
        ITenderPaymentRepository paymentRepository,
        ICurrentUserProvider currentUserProvider,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        ILogger<TenderBidsController> logger)
    {
        _bidService = bidService;
        _businessPartnerRepository = businessPartnerRepository;
        _businessPartnerUserRepository = businessPartnerUserRepository;
        _assignmentRepository = assignmentRepository;
        _paymentRepository = paymentRepository;
        _currentUserProvider = currentUserProvider;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _logger = logger;
    }

    /// <summary>
    /// Get all bids with pagination
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<PagedResult<TenderBidSummaryDto>>> GetBids(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] Guid? tenderId = null)
    {
        try
        {
            // Service interface expects (int page, int pageSize, string? search, string? status)
            // tenderId parameter is Guid? but service expects string? for search
            var result = await _bidService.GetBidsAsync(page, pageSize, null, status);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bids");
            return StatusCode(500, "An error occurred while retrieving bids");
        }
    }

    /// <summary>
    /// Get bid by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TenderBidDetailDto>> GetBid(Guid id)
    {
        try
        {
            var bid = await _bidService.GetBidByIdAsync(id);
            if (bid == null)
            {
                return NotFound($"Bid with ID {id} not found");
            }

            // For external users, verify they have access to this bid
            if (_currentUserProvider.IsExternalUser)
            {
                // Get the business partner for the current user
                // First, try to get as main account owner
                var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);

                // If not found, try to get as sub-user via BusinessPartnerUser table
                if (businessPartner == null)
                {
                    var businessPartnerUser = await _businessPartnerUserRepository.GetByUserIdAsync(_currentUserProvider.UserId);
                    if (businessPartnerUser != null && businessPartnerUser.IsActive)
                    {
                        businessPartner = businessPartnerUser.BusinessPartner;
                    }
                }

                if (businessPartner == null)
                {
                    return NotFound($"Bid with ID {id} not found");
                }

                // A tender assignment grants access to the opportunity, never to another
                // supplier's sealed bid. External users may read only their own bid.
                if (bid.BusinessPartnerId != businessPartner.Id)
                {
                    return NotFound($"Bid with ID {id} not found");
                }
            }

            return Ok(bid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bid {BidId}", id);
            return StatusCode(500, "An error occurred while retrieving the bid");
        }
    }

    /// <summary>
    /// Get bids by tender ID
    /// </summary>
    [HttpGet("by-tender/{tenderId}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<TenderBidSummaryDto>>> GetBidsByTender(Guid tenderId)
    {
        try
        {
            var bids = await _bidService.GetBidsByTenderIdAsync(tenderId);
            return Ok(bids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bids for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while retrieving bids");
        }
    }

    /// <summary>
    /// Get my bids (for business partners)
    /// </summary>
    [HttpGet("my-bids")]
    public async Task<ActionResult<IEnumerable<TenderBidSummaryDto>>> GetMyBids()
    {
        try
        {
            // Get the business partner for the current user
            // First, try to get as main account owner
            var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);

            // If not found, try to get as sub-user via BusinessPartnerUser table
            if (businessPartner == null)
            {
                var businessPartnerUser = await _businessPartnerUserRepository.GetByUserIdAsync(_currentUserProvider.UserId);
                if (businessPartnerUser != null && businessPartnerUser.IsActive)
                {
                    businessPartner = businessPartnerUser.BusinessPartner;
                }
            }

            if (businessPartner == null)
            {
                return Ok(new List<TenderBidSummaryDto>()); // Return empty list if no business partner found
            }

            var bids = await _bidService.GetMyBidsAsync(businessPartner.Id);
            return Ok(bids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting my bids");
            return StatusCode(500, "An error occurred while retrieving your bids");
        }
    }

    /// <summary>
    /// Get my draft bid for a specific tender (for business partners)
    /// </summary>
    [HttpGet("my-draft-bid/{tenderId}")]
    public async Task<ActionResult<TenderBidDetailDto>> GetMyDraftBid(Guid tenderId)
    {
        try
        {
            var bid = await _bidService.GetMyDraftBidByTenderIdAsync(tenderId);
            if (bid == null)
            {
                return NotFound();
            }
            return Ok(bid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting draft bid for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while retrieving your draft bid");
        }
    }

    /// <summary>
    /// Check bid initiation status (assignment and payment) for a tender
    /// </summary>
    [HttpGet("initiation-status/{tenderId}")]
    public async Task<ActionResult<object>> GetInitiationStatus(Guid tenderId)
    {
        try
        {
            // Get the business partner for the current user
            var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);
            if (businessPartner == null)
            {
                return NotFound("Business partner not found");
            }

            // Check if assignment exists
            var assignments = await _assignmentRepository.GetByTenderAndBusinessPartnerAsync(tenderId, businessPartner.Id);
            var hasAssignment = assignments.Any();
            var assignmentType = assignments.FirstOrDefault()?.AssignmentType;

            // Check if payment has been made
            var payments = await _paymentRepository.GetByBusinessPartnerIdAsync(businessPartner.Id);
            var tenderPayments = payments.Where(p => p.TenderFee != null && p.TenderFee.TenderId == tenderId);
            var hasPayment = tenderPayments.Any(p => p.Status == "Completed" || p.Status == "Verified");

            return Ok(new
            {
                hasAssignment,
                assignmentType,
                hasPayment,
                canProceed = hasAssignment && hasPayment
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking initiation status for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while checking initiation status");
        }
    }

    /// <summary>
    /// Create a new bid
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TenderBidDetailDto>> CreateBid([FromBody] CreateTenderBidDto dto)
    {
        try
        {
            var bid = await _bidService.CreateBidAsync(dto);
            return CreatedAtAction(nameof(GetBid), new { id = bid.Id }, bid);
        }
        catch (ProcurementSupplierEvidencePackAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Supplier bid evidence access forbidden",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["code"] = "SUPPLIER_BID_EVIDENCE_ACCESS_FORBIDDEN",
                    ["correlationId"] = HttpContext.TraceIdentifier
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bid");
            return StatusCode(500, "An error occurred while creating the bid");
        }
    }

    /// <summary>
    /// Update a bid
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<TenderBidDetailDto>> UpdateBid(Guid id, [FromBody] UpdateTenderBidDto dto)
    {
        try
        {
            var bid = await _bidService.UpdateBidAsync(id, dto);
            return Ok(bid);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bid {BidId}", id);
            return StatusCode(500, "An error occurred while updating the bid");
        }
    }

    /// <summary>
    /// Submit a bid
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<ActionResult<TenderBidDetailDto>> SubmitBid(Guid id, [FromBody] SubmitTenderBidDto dto)
    {
        try
        {
            var bid = await _bidService.SubmitBidAsync(id, dto);
            return Ok(bid);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting bid {BidId}", id);
            return StatusCode(500, "An error occurred while submitting the bid");
        }
    }

    /// <summary>
    /// Withdraw a bid
    /// </summary>
    [HttpPost("{id}/withdraw")]
    public async Task<ActionResult<TenderBidDetailDto>> WithdrawBid(Guid id, [FromBody] WithdrawTenderBidDto dto)
    {
        try
        {
            // Service method returns Task (void), not Task<TenderBidDetailDto>
            await _bidService.WithdrawBidAsync(id, dto);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing bid {BidId}", id);
            return StatusCode(500, "An error occurred while withdrawing the bid");
        }
    }

    /// <summary>
    /// Mark bid as opened
    /// </summary>
    [HttpPost("{id}/open")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderBidDetailDto>> OpenBid(Guid id)
    {
        try
        {
            var bid = await _bidService.OpenBidAsync(id);
            return Ok(bid);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening bid {BidId}", id);
            return StatusCode(500, "An error occurred while opening the bid");
        }
    }

    /// <summary>
    /// Open all submitted bids for a tender
    /// </summary>
    [HttpPost("tender/{tenderId}/open-all")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<object>> OpenAllBidsByTender(Guid tenderId)
    {
        try
        {
            var count = await _bidService.OpenAllBidsByTenderAsync(tenderId);
            return Ok(new { openedCount = count, message = $"{count} bid(s) opened successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening bids for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while opening the bids");
        }
    }

    /// <summary>
    /// Get supplier bid list for a tender
    /// </summary>
    [HttpGet("tender/{tenderId}/supplier-list")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<List<SupplierBidListItemDto>>> GetSupplierBidList(Guid tenderId)
    {
        try
        {
            var list = await _bidService.GetSupplierBidListByTenderAsync(tenderId);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier bid list for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while getting the supplier bid list");
        }
    }

    /// <summary>
    /// Delete a bid
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteBid(Guid id)
    {
        try
        {
            await _bidService.DeleteBidAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid {BidId}", id);
            return StatusCode(500, "An error occurred while deleting the bid");
        }
    }

    /// <summary>
    /// Add bid item
    /// </summary>
    [HttpPost("{id}/items")]
    public async Task<ActionResult<TenderBidItemDto>> AddBidItem(Guid id, [FromBody] CreateTenderBidItemDto dto)
    {
        try
        {
            var item = await _bidService.AddBidItemAsync(id, dto);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding bid item");
            return StatusCode(500, "An error occurred while adding the bid item");
        }
    }

    /// <summary>
    /// Update bid item
    /// </summary>
    [HttpPut("{bidId}/items/{itemId}")]
    public async Task<ActionResult<TenderBidItemDto>> UpdateBidItem(Guid bidId, Guid itemId, [FromBody] UpdateTenderBidItemDto dto)
    {
        try
        {
            // Service interface expects CreateTenderBidItemDto, not UpdateTenderBidItemDto
            // Map UpdateTenderBidItemDto to CreateTenderBidItemDto
            var createDto = new CreateTenderBidItemDto
            {
                TenderItemId = Guid.Empty, // Would need to be provided
                OfferedQuantity = dto.OfferedQuantity,
                UnitPrice = dto.UnitPrice,
                DeliveryDays = dto.DeliveryDays,
                Specifications = dto.Specifications,
                Brand = dto.Brand,
                Model = dto.Model,
                TechnicalDetails = dto.TechnicalDetails
            };

            var item = await _bidService.UpdateBidItemAsync(itemId, createDto);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bid item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while updating the bid item");
        }
    }

    /// <summary>
    /// Delete bid item
    /// </summary>
    [HttpDelete("{bidId}/items/{itemId}")]
    public async Task<ActionResult> DeleteBidItem(Guid bidId, Guid itemId)
    {
        try
        {
            await _bidService.DeleteBidItemAsync(itemId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while deleting the bid item");
        }
    }

    /// <summary>
    /// Upload bid document
    /// </summary>
    [HttpPost("{id}/documents")]
    [RequestSizeLimit(20_000_000)] // 20MB limit
    public async Task<ActionResult<TenderBidDocumentDto>> UploadDocument(
        Guid id,
        IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? documentName = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            var bid = await _bidService.GetBidByIdAsync(id);
            if (bid is null || !await CanAccessBidAsync(bid))
            {
                return NotFound("Bid not found");
            }

            var normalizedType = documentType?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedType) || normalizedType.Length > 50)
                return BadRequest("A valid document type is required.");
            var safeName = Path.GetFileName(file.FileName);
            var normalizedName = string.IsNullOrWhiteSpace(documentName) ? safeName : documentName.Trim();
            if (normalizedName.Length > 200)
                return BadRequest("Document name cannot exceed 200 characters.");
            var actorName = string.IsNullOrWhiteSpace(_currentUserProvider.FullName)
                ? _currentUserProvider.Username
                : _currentUserProvider.FullName;

            var upload = await _controlledFiles.UploadAsync(new ControlledFileUploadRequest
            {
                TenantId = _currentUserProvider.TenantId,
                ActorUserId = _currentUserProvider.UserId,
                ActorName = actorName,
                Category = ControlledFileUploadCategories.DocumentManagement,
                FileName = safeName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                FileSize = file.Length,
                OpenReadStream = file.OpenReadStream
            }, HttpContext.RequestAborted);

            CentralDocumentRepositoryLink centralDocument;
            try
            {
                centralDocument = await _centralDocuments.RegisterAsync(
                    new CentralDocumentRepositoryRegistration
                    {
                        TenantId = _currentUserProvider.TenantId,
                        ActorUserId = _currentUserProvider.UserId,
                        ActorName = actorName,
                        FileUploadRecordId = upload.Record.Id,
                        SourceModule = "Procurement",
                        SourceLabel = "Procurement / Tender bid documents",
                        SourceEntityType = "TenderBid",
                        SourceRecordId = id,
                        SourceRecordReference = bid.BidNumber,
                        Title = normalizedName,
                        DocumentType = "TenderDocument",
                        MetadataTemplateCode = "TDC-PROC-TENDER",
                        AccessProfile = "Procurement tender restricted",
                        VersionStatus = "Submitted",
                        ChangeSummary = $"{normalizedType} uploaded from the tender bid.",
                        RequirePublishedGovernance = true,
                        MetadataValues =
                        [
                            new("sourceReference", "Source reference", bid.BidNumber),
                            new("documentFamily", "Document family", "Tender bid"),
                            new("classification", "Classification", normalizedType),
                            new("sourceStatus", "Source status", bid.Status),
                            new("uploadedBy", "Uploaded by", actorName),
                            new("checksumSha256", "Checksum SHA-256", upload.ChecksumSha256)
                        ]
                    }, HttpContext.RequestAborted);
            }
            catch
            {
                await _controlledFiles.DeleteAsync(_currentUserProvider.TenantId,
                    upload.Record.Id, _currentUserProvider.UserId, HttpContext.RequestAborted);
                throw;
            }

            // Create DTO
            var dto = new UploadBidDocumentDto
            {
                DocumentName = normalizedName,
                DocumentType = normalizedType
            };

            TenderBidDocumentDto document;
            try
            {
                document = await _bidService.UploadBidDocumentAsync(id, dto,
                    $"dms:{centralDocument.DocumentRecordId:N}:version:{centralDocument.DocumentVersionId:N}",
                    upload.Record.ContentType, upload.Record.FileSize, upload.Record.Id,
                    centralDocument.DocumentRecordId, centralDocument.DocumentVersionId);
            }
            catch
            {
                await _centralDocuments.DeleteAsync(_currentUserProvider.TenantId,
                    centralDocument.DocumentRecordId, _currentUserProvider.UserId,
                    HttpContext.RequestAborted);
                throw;
            }
            return Created($"/api/procurement/TenderBids/{id}/documents/{document.Id}", document);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading bid document");
            return StatusCode(500, "An error occurred while uploading the document");
        }
    }

    /// <summary>
    /// Get bid documents
    /// </summary>
    [HttpGet("{id}/documents")]
    public async Task<ActionResult<IEnumerable<TenderBidDocumentDto>>> GetDocuments(Guid id)
    {
        try
        {
            var bid = await _bidService.GetBidByIdAsync(id);
            if (bid is null || !await CanAccessBidAsync(bid)) return NotFound();
            var documents = await _bidService.GetBidDocumentsAsync(id);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bid documents for bid {BidId}", id);
            return StatusCode(500, "An error occurred while retrieving documents");
        }
    }

    /// <summary>
    /// Download bid document
    /// </summary>
    [HttpGet("{bidId}/documents/{documentId}/download")]
    public async Task<IActionResult> DownloadDocument(Guid bidId, Guid documentId)
    {
        try
        {
            var bid = await _bidService.GetBidByIdAsync(bidId);
            if (bid is null || !await CanAccessBidAsync(bid)) return NotFound();
            var documents = await _bidService.GetBidDocumentsAsync(bidId);
            var document = documents.FirstOrDefault(d => d.Id == documentId);

            if (document == null)
            {
                return NotFound("Document not found");
            }

            if (document.CentralDocumentRecordId.HasValue && document.CentralDocumentVersionId.HasValue)
            {
                var content = await _centralDocuments.OpenAsync(_currentUserProvider.TenantId,
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

            if (string.IsNullOrEmpty(document.FilePath) || !System.IO.File.Exists(document.FilePath))
                return NotFound("Legacy document file not found on server");
            var fileBytes = await System.IO.File.ReadAllBytesAsync(document.FilePath);
            var contentType = document.FileType ?? "application/octet-stream";

            return File(fileBytes, contentType, document.DocumentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while downloading the document");
        }
    }

    /// <summary>
    /// Delete bid document
    /// </summary>
    [HttpDelete("{bidId}/documents/{documentId}")]
    public async Task<ActionResult> DeleteDocument(Guid bidId, Guid documentId)
    {
        try
        {
            var bid = await _bidService.GetBidByIdAsync(bidId);
            if (bid is null || !await CanAccessBidAsync(bid)) return NotFound();
            await _bidService.DeleteBidDocumentAsync(documentId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while deleting the document");
        }
    }

    private async Task<bool> CanAccessBidAsync(TenderBidDetailDto bid)
    {
        if (!_currentUserProvider.IsExternalUser)
        {
            return true;
        }

        var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(
            _currentUserProvider.UserId);
        if (businessPartner is null)
        {
            var link = await _businessPartnerUserRepository.GetByUserIdAsync(
                _currentUserProvider.UserId);
            if (link is { IsActive: true }) businessPartner = link.BusinessPartner;
        }

        if (businessPartner is null)
        {
            return false;
        }

        if (bid.BusinessPartnerId == businessPartner.Id)
        {
            return true;
        }

        var assignments = await _assignmentRepository.GetByBusinessPartnerIdAsync(
            businessPartner.Id);
        return assignments.Any(item => item.TenderId == bid.TenderId &&
            (item.AssignmentType == "AllUsers" ||
             item.AssignedToUserId == _currentUserProvider.UserId));
    }

    /// <summary>
    /// Get bid payments
    /// </summary>
    [HttpGet("{id}/payments")]
    public async Task<ActionResult<IEnumerable<TenderPaymentDto>>> GetBidPayments(Guid id)
    {
        try
        {
            var payments = await _bidService.GetBidPaymentsAsync(id);
            return Ok(payments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payments for bid {BidId}", id);
            return StatusCode(500, "An error occurred while retrieving payments");
        }
    }

    /// <summary>
    /// Record payment
    /// </summary>
    [HttpPost("{id}/payments")]
    public async Task<ActionResult<TenderPaymentDto>> RecordPayment(Guid id, [FromBody] RecordPaymentDto dto)
    {
        try
        {
            // Service interface expects CreateTenderPaymentDto, not (Guid id, RecordPaymentDto dto)
            // Map RecordPaymentDto to CreateTenderPaymentDto
            var createDto = new CreateTenderPaymentDto
            {
                TenderFeeId = dto.TenderFeeId,
                TenderBidId = id, // Use the bid ID from the route
                Amount = dto.Amount,
                Currency = dto.Currency,
                PaymentMethod = dto.PaymentMethod,
                TransactionId = dto.TransactionId,
                Notes = dto.Notes
            };

            var payment = await _bidService.RecordPaymentAsync(createDto);
            return Ok(payment);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording payment");
            return StatusCode(500, "An error occurred while recording the payment");
        }
    }

    /// <summary>
    /// Verify payment
    /// </summary>
    [HttpPost("{bidId}/payments/{paymentId}/verify")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderPaymentDto>> VerifyPayment(Guid bidId, Guid paymentId, [FromBody] VerifyPaymentDto dto)
    {
        try
        {
            var payment = await _bidService.VerifyPaymentAsync(paymentId, dto);
            return Ok(payment);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment {PaymentId}", paymentId);
            return StatusCode(500, "An error occurred while verifying the payment");
        }
    }

    /// <summary>
    /// Schedule interview
    /// </summary>
    [HttpPost("{id}/interviews")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderInterviewDto>> ScheduleInterview(Guid id, [FromBody] ScheduleInterviewDto dto)
    {
        try
        {
            // Service interface expects only ScheduleInterviewDto, not (Guid id, ScheduleInterviewDto dto)
            // The DTO should contain the TenderBidId
            var interview = await _bidService.ScheduleInterviewAsync(dto);
            return Ok(interview);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling interview");
            return StatusCode(500, "An error occurred while scheduling the interview");
        }
    }

    /// <summary>
    /// Update interview
    /// </summary>
    [HttpPut("{bidId}/interviews/{interviewId}")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderInterviewDto>> UpdateInterview(Guid bidId, Guid interviewId, [FromBody] UpdateInterviewDto dto)
    {
        try
        {
            // Service interface expects (Guid interviewId, ScheduleInterviewDto dto), not UpdateInterviewDto
            // Map UpdateInterviewDto to ScheduleInterviewDto
            // Note: ScheduleInterviewDto doesn't have a Notes property, so we can't map dto.Notes
            var scheduleDto = new ScheduleInterviewDto
            {
                TenderBidId = bidId,
                Title = dto.Title,
                Description = dto.Description,
                ScheduledDate = dto.ScheduledDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                InterviewType = dto.InterviewType,
                Location = dto.Location,
                Agenda = dto.Agenda,
                PanelMembers = dto.PanelMembers
            };

            var interview = await _bidService.UpdateInterviewAsync(interviewId, scheduleDto);
            return Ok(interview);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating interview {InterviewId}", interviewId);
            return StatusCode(500, "An error occurred while updating the interview");
        }
    }

    #region Bid LOT Endpoints

    /// <summary>
    /// Get all LOTs for a bid
    /// </summary>
    [HttpGet("{bidId}/lots")]
    public async Task<ActionResult<IEnumerable<TenderBidLotDto>>> GetBidLots(Guid bidId)
    {
        try
        {
            var lots = await _bidService.GetBidLotsAsync(bidId);
            return Ok(lots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lots for bid {BidId}", bidId);
            return StatusCode(500, "An error occurred while retrieving bid lots");
        }
    }

    /// <summary>
    /// Get a specific bid LOT by ID
    /// </summary>
    [HttpGet("lots/{bidLotId}")]
    public async Task<ActionResult<TenderBidLotDto>> GetBidLot(Guid bidLotId)
    {
        try
        {
            var lot = await _bidService.GetBidLotByIdAsync(bidLotId);
            if (lot == null)
            {
                return NotFound($"Bid lot with ID {bidLotId} not found");
            }
            return Ok(lot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bid lot {BidLotId}", bidLotId);
            return StatusCode(500, "An error occurred while retrieving the bid lot");
        }
    }

    /// <summary>
    /// Add a new LOT to a bid
    /// </summary>
    [HttpPost("{bidId}/lots")]
    public async Task<ActionResult<TenderBidLotDto>> AddBidLot(Guid bidId, [FromBody] CreateTenderBidLotDto dto)
    {
        try
        {
            var lot = await _bidService.AddBidLotAsync(bidId, dto);
            return CreatedAtAction(nameof(GetBidLot), new { bidLotId = lot.Id }, lot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding lot to bid {BidId}", bidId);
            return StatusCode(500, "An error occurred while adding the bid lot");
        }
    }

    /// <summary>
    /// Update a bid LOT
    /// </summary>
    [HttpPut("lots/{bidLotId}")]
    public async Task<ActionResult<TenderBidLotDto>> UpdateBidLot(Guid bidLotId, [FromBody] UpdateTenderBidLotDto dto)
    {
        try
        {
            var lot = await _bidService.UpdateBidLotAsync(bidLotId, dto);
            return Ok(lot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bid lot {BidLotId}", bidLotId);
            return StatusCode(500, "An error occurred while updating the bid lot");
        }
    }

    /// <summary>
    /// Delete a bid LOT
    /// </summary>
    [HttpDelete("lots/{bidLotId}")]
    public async Task<ActionResult> DeleteBidLot(Guid bidLotId)
    {
        try
        {
            await _bidService.DeleteBidLotAsync(bidLotId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid lot {BidLotId}", bidLotId);
            return StatusCode(500, "An error occurred while deleting the bid lot");
        }
    }

    /// <summary>
    /// Assign a bid item to a LOT
    /// </summary>
    [HttpPost("items/{bidItemId}/assign-lot/{bidLotId}")]
    public async Task<ActionResult> AssignBidItemToLot(Guid bidItemId, Guid bidLotId)
    {
        try
        {
            await _bidService.AssignBidItemToLotAsync(bidItemId, bidLotId);
            return Ok(new { message = "Bid item assigned to lot successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning bid item {BidItemId} to lot {BidLotId}", bidItemId, bidLotId);
            return StatusCode(500, "An error occurred while assigning the bid item to the lot");
        }
    }

    /// <summary>
    /// Remove a bid item from its LOT
    /// </summary>
    [HttpPost("items/{bidItemId}/remove-from-lot")]
    public async Task<ActionResult> RemoveBidItemFromLot(Guid bidItemId)
    {
        try
        {
            await _bidService.RemoveBidItemFromLotAsync(bidItemId);
            return Ok(new { message = "Bid item removed from lot successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing bid item {BidItemId} from lot", bidItemId);
            return StatusCode(500, "An error occurred while removing the bid item from the lot");
        }
    }

    #endregion
}

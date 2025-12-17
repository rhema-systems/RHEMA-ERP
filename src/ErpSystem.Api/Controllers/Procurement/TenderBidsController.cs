using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
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
    private readonly ILogger<TenderBidsController> _logger;

    public TenderBidsController(
        ITenderBidService bidService,
        IBusinessPartnerRepository businessPartnerRepository,
        IBusinessPartnerUserRepository businessPartnerUserRepository,
        ITenderAssignmentRepository assignmentRepository,
        ITenderPaymentRepository paymentRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<TenderBidsController> logger)
    {
        _bidService = bidService;
        _businessPartnerRepository = businessPartnerRepository;
        _businessPartnerUserRepository = businessPartnerUserRepository;
        _assignmentRepository = assignmentRepository;
        _paymentRepository = paymentRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get all bids with pagination
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
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

                // Verify the bid belongs to this business partner OR the user is assigned to the tender
                if (bid.BusinessPartnerId != businessPartner.Id)
                {
                    // Check if user is assigned to this tender via TenderAssignment
                    var assignments = await _assignmentRepository.GetByBusinessPartnerIdAsync(businessPartner.Id);
                    var isAssigned = assignments.Any(a =>
                        a.TenderId == bid.TenderId &&
                        (a.AssignmentType == "AllUsers" || a.AssignedToUserId == _currentUserProvider.UserId)
                    );

                    if (!isAssigned)
                    {
                        return NotFound($"Bid with ID {id} not found");
                    }
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
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
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
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
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
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
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
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
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

            // Create uploads folder for bid documents
            var uploadsFolder = Path.Combine("uploads", "tender-bids", id.ToString());
            Directory.CreateDirectory(uploadsFolder);

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            // Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Create DTO
            var dto = new UploadBidDocumentDto
            {
                DocumentName = documentName ?? file.FileName,
                DocumentType = documentType
            };

            // Upload document (service will update with file info)
            var document = await _bidService.UploadBidDocumentAsync(id, dto, filePath, file.ContentType, file.Length);
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
            var documents = await _bidService.GetBidDocumentsAsync(bidId);
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
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
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
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
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
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
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
}


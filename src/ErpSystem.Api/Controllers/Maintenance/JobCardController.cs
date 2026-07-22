using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/job-cards")]
[Authorize]
public class JobCardController : ControllerBase
{
    private readonly IJobCardService _jobCardService;
    private readonly IMaintenanceNotificationService _notificationService;
    private readonly ILogger<JobCardController> _logger;

    public JobCardController(
        IJobCardService jobCardService,
        IMaintenanceNotificationService notificationService,
        ILogger<JobCardController> logger)
    {
        _jobCardService = jobCardService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of job cards with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<JobCardListDto>>> GetJobCards(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? status = null,
        [FromQuery] string? approvalStatus = null,
        [FromQuery] Guid? assetId = null,
        [FromQuery] Guid? maintenanceTypeId = null,
        [FromQuery] Guid? priorityLevelId = null,
        [FromQuery] Guid? requestedById = null,
        [FromQuery] Guid? assignedTechnicianId = null,
        [FromQuery] DateTime? requestedFrom = null,
        [FromQuery] DateTime? requestedTo = null,
        [FromQuery] DateTime? requiredFrom = null,
        [FromQuery] DateTime? requiredTo = null,
        [FromQuery] bool? requiresApproval = null,
        [FromQuery] bool? hasWorkOrder = null)
    {
        try
        {
            if (pageSize > 100)
            {
                pageSize = 100;
            }

            var filter = new JobCardFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Status = status,
                ApprovalStatus = approvalStatus,
                AssetId = assetId,
                MaintenanceTypeId = maintenanceTypeId,
                PriorityLevelId = priorityLevelId,
                RequestedById = requestedById,
                AssignedTechnicianId = assignedTechnicianId,
                RequestedFrom = requestedFrom,
                RequestedTo = requestedTo,
                RequiredFrom = requiredFrom,
                RequiredTo = requiredTo,
                RequiresApproval = requiresApproval,
                HasWorkOrder = hasWorkOrder
            };

            var result = await _jobCardService.GetJobCardsPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job cards");
            return StatusCode(500, "An error occurred while retrieving job cards");
        }
    }

    /// <summary>
    /// Gets a specific job card by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobCardDto>> GetJobCard(Guid id)
    {
        try
        {
            var jobCard = await _jobCardService.GetJobCardByIdAsync(id);
            if (jobCard == null)
            {
                return NotFound($"Job card with ID {id} not found");
            }

            return Ok(jobCard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while retrieving the job card");
        }
    }

    /// <summary>
    /// Gets a specific job card by job card number
    /// </summary>
    [HttpGet("by-number/{jobCardNumber}")]
    public async Task<ActionResult<JobCardDto>> GetJobCardByNumber(string jobCardNumber)
    {
        try
        {
            var jobCard = await _jobCardService.GetJobCardByNumberAsync(jobCardNumber);
            if (jobCard == null)
            {
                return NotFound($"Job card with number {jobCardNumber} not found");
            }

            return Ok(jobCard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job card by number {JobCardNumber}", jobCardNumber);
            return StatusCode(500, "An error occurred while retrieving the job card");
        }
    }

    /// <summary>
    /// Creates a new job card
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<JobCardDto>> CreateJobCard([FromBody] CreateJobCardDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var jobCard = await _jobCardService.CreateJobCardAsync(createDto);
            return CreatedAtAction(nameof(GetJobCard), new { id = jobCard.Id }, jobCard);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job card");
            return StatusCode(500, "An error occurred while creating the job card");
        }
    }

    /// <summary>
    /// Updates an existing job card (only allowed in Draft status)
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JobCardDto>> UpdateJobCard(Guid id, [FromBody] UpdateJobCardDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var jobCard = await _jobCardService.UpdateJobCardAsync(id, updateDto);
            return Ok(jobCard);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while updating the job card");
        }
    }

    /// <summary>
    /// Deletes a job card (only allowed if not submitted for approval)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteJobCard(Guid id)
    {
        try
        {
            await _jobCardService.DeleteJobCardAsync(id);
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
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while deleting the job card");
        }
    }

    /// <summary>
    /// Submits a job card for approval workflow
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<JobCardDto>> SubmitJobCard(Guid id, [FromBody] SubmitJobCardDto submitDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var jobCard = await _jobCardService.SubmitJobCardAsync(id, submitDto);

            // Notify approvers when job card is submitted
            try
            {
                await _notificationService.NotifyJobCardSubmittedAsync(id);
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send job card submission notification for {JobCardId}", id);
            }

            return Ok(jobCard);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while submitting the job card");
        }
    }

    /// <summary>
    /// Processes an approval action on a job card (approve, reject, request changes)
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<JobCardDto>> ProcessApproval(Guid id, [FromBody] JobCardApprovalActionDto approvalDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (approvalDto.Action?.Equals("Reject", StringComparison.OrdinalIgnoreCase) == true &&
                string.IsNullOrWhiteSpace(approvalDto.Comments))
            {
                return BadRequest("Rejection comment is required.");
            }

            var jobCard = await _jobCardService.ProcessApprovalAsync(id, approvalDto);

            // Send notification based on approval action
            try
            {
                if (approvalDto.Action?.Equals("Approve", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await _notificationService.NotifyJobCardApprovedAsync(id);
                }
                else if (approvalDto.Action?.Equals("Reject", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await _notificationService.NotifyJobCardRejectedAsync(id, approvalDto.Comments);
                }
                else if (approvalDto.Action?.Equals("RequestChanges", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await _notificationService.NotifyJobCardChangesRequestedAsync(id, approvalDto.Comments);
                }
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send job card approval notification for {JobCardId}", id);
            }

            return Ok(jobCard);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing approval for job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while processing the approval");
        }
    }

    /// <summary>
    /// Cancels a job card
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<JobCardDto>> CancelJobCard(Guid id, [FromBody] CancelJobCardDto cancelDto)
    {
        try
        {
            var jobCard = await _jobCardService.CancelJobCardAsync(id, cancelDto.Reason);
            return Ok(jobCard);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while cancelling the job card");
        }
    }

    /// <summary>
    /// Generates a work order from an approved job card
    /// </summary>
    [HttpPost("{id:guid}/generate-work-order")]
    public async Task<ActionResult<GenerateWorkOrderResponse>> GenerateWorkOrder(Guid id)
    {
        try
        {
            var workOrderId = await _jobCardService.GenerateWorkOrderAsync(id);
            return Ok(new GenerateWorkOrderResponse { WorkOrderId = workOrderId });
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
            _logger.LogError(ex, "Error generating work order from job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while generating the work order");
        }
    }

    /// <summary>
    /// Adds a comment to a job card
    /// </summary>
    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<JobCardCommentDto>> AddComment(Guid id, [FromBody] AddJobCardCommentDto commentDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var comment = await _jobCardService.AddCommentAsync(id, commentDto);
            return Created($"/api/maintenance/job-cards/{id}/comments/{comment.Id}", comment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while adding the comment");
        }
    }

    /// <summary>
    /// Gets all comments for a job card
    /// </summary>
    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<List<JobCardCommentDto>>> GetComments(Guid id)
    {
        try
        {
            var comments = await _jobCardService.GetCommentsAsync(id);
            return Ok(comments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving comments for job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while retrieving comments");
        }
    }

    /// <summary>
    /// Gets approval history for a job card
    /// </summary>
    [HttpGet("{id:guid}/approval-history")]
    public async Task<ActionResult<List<JobCardApprovalStepDto>>> GetApprovalHistory(Guid id)
    {
        try
        {
            var history = await _jobCardService.GetApprovalHistoryAsync(id);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approval history for job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while retrieving approval history");
        }
    }

    /// <summary>
    /// Gets job cards pending approval for the current user
    /// </summary>
    [HttpGet("pending-approvals")]
    public async Task<ActionResult<PagedResult<JobCardListDto>>> GetPendingApprovals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null)
    {
        try
        {
            if (pageSize > 100)
            {
                pageSize = 100;
            }

            var filter = new JobCardFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm
            };

            var result = await _jobCardService.GetPendingApprovalsAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending approvals");
            return StatusCode(500, "An error occurred while retrieving pending approvals");
        }
    }

    /// <summary>
    /// Gets job cards that have been approved and are ready for work order generation
    /// </summary>
    [HttpGet("approved")]
    public async Task<ActionResult<List<JobCardListDto>>> GetApprovedJobCards()
    {
        try
        {
            var jobCards = await _jobCardService.GetApprovedJobCardsAsync();
            return Ok(jobCards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approved job cards");
            return StatusCode(500, "An error occurred while retrieving approved job cards");
        }
    }

    /// <summary>
    /// Gets job cards by asset ID
    /// </summary>
    [HttpGet("by-asset/{assetId:guid}")]
    public async Task<ActionResult<List<JobCardListDto>>> GetJobCardsByAsset(Guid assetId)
    {
        try
        {
            var jobCards = await _jobCardService.GetJobCardsByAssetAsync(assetId);
            return Ok(jobCards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job cards for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while retrieving job cards");
        }
    }

    /// <summary>
    /// Gets job cards requested by a specific user
    /// </summary>
    [HttpGet("by-requester/{requesterId:guid}")]
    public async Task<ActionResult<List<JobCardListDto>>> GetJobCardsByRequester(Guid requesterId)
    {
        try
        {
            var jobCards = await _jobCardService.GetJobCardsByRequesterAsync(requesterId);
            return Ok(jobCards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job cards for requester {RequesterId}", requesterId);
            return StatusCode(500, "An error occurred while retrieving job cards");
        }
    }

    /// <summary>
    /// Gets job cards assigned to a specific technician
    /// </summary>
    [HttpGet("by-technician/{technicianId:guid}")]
    public async Task<ActionResult<List<JobCardListDto>>> GetJobCardsByTechnician(Guid technicianId)
    {
        try
        {
            var jobCards = await _jobCardService.GetJobCardsByTechnicianAsync(technicianId);
            return Ok(jobCards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job cards for technician {TechnicianId}", technicianId);
            return StatusCode(500, "An error occurred while retrieving job cards");
        }
    }

    /// <summary>
    /// Gets dashboard statistics for job cards
    /// </summary>
    [HttpGet("dashboard-stats")]
    public async Task<ActionResult<JobCardDashboardStatsDto>> GetDashboardStats()
    {
        try
        {
            var stats = await _jobCardService.GetDashboardStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job card dashboard statistics");
            return StatusCode(500, "An error occurred while retrieving dashboard statistics");
        }
    }

    /// <summary>
    /// Uploads a document/attachment to a job card
    /// </summary>
    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<JobCardDocumentDto>> UploadDocument(
        Guid id,
        IFormFile file,
        [FromForm] string documentType = "General")
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            using var stream = file.OpenReadStream();
            var document = await _jobCardService.UploadDocumentAsync(id, stream, file.FileName, documentType);
            return Created($"/api/maintenance/job-cards/{id}/documents/{document.Id}", document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while uploading the document");
        }
    }

    /// <summary>
    /// Deletes a document from a job card
    /// </summary>
    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid documentId)
    {
        try
        {
            await _jobCardService.DeleteDocumentAsync(id, documentId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId} from job card {JobCardId}", documentId, id);
            return StatusCode(500, "An error occurred while deleting the document");
        }
    }

    /// <summary>
    /// Gets all documents for a job card
    /// </summary>
    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<List<JobCardDocumentDto>>> GetDocuments(Guid id)
    {
        try
        {
            var documents = await _jobCardService.GetDocumentsAsync(id);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while retrieving documents");
        }
    }

    /// <summary>
    /// Downloads a document from a job card
    /// </summary>
    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId)
    {
        try
        {
            var document = await _jobCardService.DownloadDocumentAsync(id, documentId);
            return File(document.Content, document.ContentType, document.FileName);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document {DocumentId} not found for job card {JobCardId}", documentId, id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {DocumentId} from job card {JobCardId}", documentId, id);
            return StatusCode(500, "An error occurred while downloading the document");
        }
    }

    /// <summary>
    /// Marks a job card as completed with completion details
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<JobCardDto>> CompleteJobCard(Guid id, [FromBody] CompleteJobCardDto completeDto)
    {
        try
        {
            var jobCard = await _jobCardService.CompleteJobCardAsync(id, completeDto);
            return Ok(jobCard);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while completing the job card");
        }
    }

    /// <summary>
    /// Performs quality check on a completed job card
    /// </summary>
    [HttpPost("{id:guid}/quality-check")]
    public async Task<ActionResult<JobCardDto>> PerformQualityCheck(Guid id, [FromBody] JobCardQualityCheckDto qualityCheckDto)
    {
        try
        {
            var jobCard = await _jobCardService.PerformQualityCheckAsync(id, qualityCheckDto);
            return Ok(jobCard);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing quality check on job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while performing quality check");
        }
    }

    /// <summary>
    /// Records customer/user acceptance of completed work
    /// </summary>
    [HttpPost("{id:guid}/acceptance")]
    public async Task<ActionResult<JobCardDto>> RecordAcceptance(Guid id, [FromBody] JobCardAcceptanceDto acceptanceDto)
    {
        try
        {
            var jobCard = await _jobCardService.RecordAcceptanceAsync(id, acceptanceDto);
            return Ok(jobCard);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording acceptance for job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while recording acceptance");
        }
    }

    /// <summary>
    /// Generates a certificate for a completed and accepted job card
    /// </summary>
    [HttpPost("{id:guid}/certificates")]
    public async Task<ActionResult<JobCardCertificateDto>> GenerateCertificate(Guid id, [FromBody] GenerateJobCardCertificateDto certificateDto)
    {
        try
        {
            var certificate = await _jobCardService.GenerateCertificateAsync(id, certificateDto);
            return Created($"/api/maintenance/job-cards/{id}/certificates/{certificate.Id}", certificate);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating certificate for job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while generating the certificate");
        }
    }

    /// <summary>
    /// Gets all certificates for a job card
    /// </summary>
    [HttpGet("{id:guid}/certificates")]
    public async Task<ActionResult<List<JobCardCertificateDto>>> GetCertificates(Guid id)
    {
        try
        {
            var certificates = await _jobCardService.GetCertificatesAsync(id);
            return Ok(certificates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving certificates for job card {JobCardId}", id);
            return StatusCode(500, "An error occurred while retrieving certificates");
        }
    }

    /// <summary>
    /// Gets a specific certificate by ID
    /// </summary>
    [HttpGet("certificates/{certificateId:guid}")]
    public async Task<ActionResult<JobCardCertificateDto>> GetCertificateById(Guid certificateId)
    {
        try
        {
            var certificate = await _jobCardService.GetCertificateByIdAsync(certificateId);
            if (certificate == null)
            {
                return NotFound($"Certificate with ID {certificateId} not found");
            }

            return Ok(certificate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving certificate {CertificateId}", certificateId);
            return StatusCode(500, "An error occurred while retrieving the certificate");
        }
    }
}

/// <summary>
/// DTO for cancelling a job card
/// </summary>
public class CancelJobCardDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for work order generation
/// </summary>
public class GenerateWorkOrderResponse
{
    public Guid WorkOrderId { get; set; }
}

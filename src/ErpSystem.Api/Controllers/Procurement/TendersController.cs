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
public class TendersController : ControllerBase
{
    private readonly ITenderService _tenderService;
    private readonly IWorkflowService _workflowService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ILogger<TendersController> _logger;

    public TendersController(
        ITenderService tenderService,
        IWorkflowService workflowService,
        ICurrentUserProvider currentUserProvider,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        ILogger<TendersController> logger)
    {
        _tenderService = tenderService;
        _workflowService = workflowService;
        _currentUserProvider = currentUserProvider;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _logger = logger;
    }

    /// <summary>
    /// Get all tenders with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TenderDto>>> GetTenders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? tenderType = null,
        [FromQuery] string? searchTerm = null)
    {
        try
        {
            // Fix parameter order: search, status, tenderType
            var result = await _tenderService.GetTendersAsync(page, pageSize, searchTerm, status, tenderType);

            // Populate current workflow step name for submitted tenders (avoid per-row UI polling).
            var submitted = result.Items.Where(t => string.Equals(t.Status, "Submitted", StringComparison.OrdinalIgnoreCase)).ToList();
            if (submitted.Count > 0)
            {
                await Task.WhenAll(submitted.Select(async dto =>
                {
                    try
                    {
                        var step = await _workflowService.GetCurrentWorkflowStepAsync("Tender", dto.Id);
                        dto.CurrentWorkflowStepName = step?.StepName;
                    }
                    catch
                    {
                        // Best-effort only.
                    }
                }));
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tenders");
            return StatusCode(500, "An error occurred while retrieving tenders");
        }
    }

    /// <summary>
    /// Get tender by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TenderDetailDto>> GetTender(Guid id)
    {
        try
        {
            var tender = await _tenderService.GetTenderByIdAsync(id);
            if (tender == null)
            {
                return NotFound($"Tender with ID {id} not found");
            }

            if (string.Equals(tender.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var step = await _workflowService.GetCurrentWorkflowStepAsync("Tender", id);
                    tender.CurrentWorkflowStepName = step?.StepName;
                }
                catch
                {
                    // ignore
                }
            }

            return Ok(tender);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tender {TenderId}", id);
            return StatusCode(500, "An error occurred while retrieving the tender");
        }
    }

    /// <summary>
    /// Get tender by tender number
    /// </summary>
    [HttpGet("by-number/{tenderNumber}")]
    public async Task<ActionResult<TenderDetailDto>> GetTenderByNumber(string tenderNumber)
    {
        try
        {
            var tender = await _tenderService.GetTenderByNumberAsync(tenderNumber);
            if (tender == null)
            {
                return NotFound($"Tender with number {tenderNumber} not found");
            }

            return Ok(tender);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tender by number {TenderNumber}", tenderNumber);
            return StatusCode(500, "An error occurred while retrieving the tender");
        }
    }

    /// <summary>
    /// Get tenders assigned to current user as evaluator
    /// </summary>
    [HttpGet("my-assigned")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<TenderDto>>> GetMyAssignedTenders()
    {
        try
        {
            var tenders = await _tenderService.GetMyAssignedTendersAsync();
            return Ok(tenders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assigned tenders");
            return StatusCode(500, "An error occurred while retrieving assigned tenders");
        }
    }

    /// <summary>
    /// Create a new tender
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderDto>> CreateTender([FromBody] CreateTenderDto dto)
    {
        try
        {
            var tender = await _tenderService.CreateTenderAsync(dto);
            return CreatedAtAction(nameof(GetTender), new { id = tender.Id }, tender);
        }
        catch (ProcurementRequisitionSourcingBlockedException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Readiness.DecisionCode, ex.Message, ex.Readiness));
        }
        catch (ProcurementRequisitionSourcingValidationException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Code, ex.Message));
        }
        catch (ProcurementRequisitionSourcingAuthorizationException ex)
        {
            return StatusCode(403, SourcingProblem("PR_SOURCING_CONTROL_FORBIDDEN", ex.Message, status: 403));
        }
        catch (ProcurementExceptionalSourcingConflictException ex)
        {
            return Conflict(new { code = ex.Code, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tender");
            return StatusCode(500, "An error occurred while creating the tender");
        }
    }

    /// <summary>
    /// Update a tender
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderDto>> UpdateTender(Guid id, [FromBody] UpdateTenderDto dto)
    {
        try
        {
            var tender = await _tenderService.UpdateTenderAsync(id, dto);
            return Ok(tender);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender {TenderId}", id);
            return StatusCode(500, "An error occurred while updating the tender");
        }
    }

    /// <summary>
    /// Submit a tender for approval (unified workflow)
    /// </summary>
    [HttpPost("{id}/submit")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<IActionResult> SubmitTender(Guid id)
    {
        try
        {
            var userId = _currentUserProvider.UserId;
            await _tenderService.SubmitTenderForApprovalAsync(id, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting tender {TenderId} for approval", id);
            return StatusCode(500, "An error occurred while submitting the tender for approval");
        }
    }

    /// <summary>
    /// Approve the current tender workflow step
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveTender(Guid id, [FromBody] ApproveTenderRequest? request)
    {
        try
        {
            var userId = _currentUserProvider.UserId;
            await _tenderService.ApproveTenderAsync(id, userId, request?.Notes);
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
            _logger.LogError(ex, "Error approving tender {TenderId}", id);
            return StatusCode(500, "An error occurred while approving the tender");
        }
    }

    /// <summary>
    /// Reject the current tender workflow step
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectTender(Guid id, [FromBody] RejectTenderRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Reason))
            {
                return BadRequest("Rejection reason is required");
            }

            var userId = _currentUserProvider.UserId;
            await _tenderService.RejectTenderAsync(id, userId, request.Reason);
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
            _logger.LogError(ex, "Error rejecting tender {TenderId}", id);
            return StatusCode(500, "An error occurred while rejecting the tender");
        }
    }

    /// <summary>
    /// Publish a tender
    /// </summary>
    [HttpPost("{id}/publish")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderDto>> PublishTender(Guid id, [FromBody] PublishTenderDto dto)
    {
        try
        {
            var tender = await _tenderService.PublishTenderAsync(id, dto);
            return Ok(tender);
        }
        catch (ProcurementRequisitionSourcingBlockedException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Readiness.DecisionCode, ex.Message, ex.Readiness));
        }
        catch (ProcurementRequisitionSourcingValidationException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Code, ex.Message));
        }
        catch (ProcurementRequisitionSourcingAuthorizationException ex)
        {
            return StatusCode(403, SourcingProblem("PR_SOURCING_CONTROL_FORBIDDEN", ex.Message, status: 403));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing tender {TenderId}", id);
            return StatusCode(500, "An error occurred while publishing the tender");
        }
    }

    /// <summary>
    /// Close a tender
    /// </summary>
    [HttpPost("{id}/close")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderDto>> CloseTender(Guid id)
    {
        try
        {
            // TODO: Service interface doesn't have CloseTenderAsync method
            // Commenting out until the method is implemented
            // var tender = await _tenderService.CloseTenderAsync(id);
            // return Ok(tender);
            await Task.CompletedTask;
            return StatusCode(501, "Close tender functionality not yet implemented");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing tender {TenderId}", id);
            return StatusCode(500, "An error occurred while closing the tender");
        }
    }

    // TODO: Implement CancelTender endpoint when CancelTenderAsync service method is added
    // /// <summary>
    // /// Cancel a tender
    // /// </summary>
    // [HttpPost("{id}/cancel")]
    // [Authorize(Roles = "Admin,ProcurementManager")]
    // public async Task<ActionResult<TenderDto>> CancelTender(Guid id, [FromBody] CancelTenderDto dto)
    // {
    //     try
    //     {
    //         var tender = await _tenderService.CancelTenderAsync(id, dto);
    //         return Ok(tender);
    //     }
    //     catch (InvalidOperationException ex)
    //     {
    //         return BadRequest(ex.Message);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Error cancelling tender {TenderId}", id);
    //         return StatusCode(500, "An error occurred while cancelling the tender");
    //     }
    // }

    /// <summary>
    /// Delete a tender
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> DeleteTender(Guid id)
    {
        try
        {
            await _tenderService.DeleteTenderAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender {TenderId}", id);
            return StatusCode(500, "An error occurred while deleting the tender");
        }
    }

    /// <summary>
    /// Add tender item
    /// </summary>
    [HttpPost("{id}/items")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderItemDto>> AddTenderItem(Guid id, [FromBody] CreateTenderItemDto dto)
    {
        try
        {
            var item = await _tenderService.AddTenderItemAsync(id, dto);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tender item");
            return StatusCode(500, "An error occurred while adding the tender item");
        }
    }

    /// <summary>
    /// Update tender item
    /// </summary>
    [HttpPut("{tenderId}/items/{itemId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderItemDto>> UpdateTenderItem(Guid tenderId, Guid itemId, [FromBody] CreateTenderItemDto dto)
    {
        try
        {
            var item = await _tenderService.UpdateTenderItemAsync(itemId, dto);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while updating the tender item");
        }
    }

    /// <summary>
    /// Delete tender item
    /// </summary>
    [HttpDelete("{tenderId}/items/{itemId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> DeleteTenderItem(Guid tenderId, Guid itemId)
    {
        try
        {
            await _tenderService.DeleteTenderItemAsync(itemId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while deleting the tender item");
        }
    }

    /// <summary>
    /// Upload tender document
    /// </summary>
    [HttpPost("{id}/documents")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    [RequestSizeLimit(20_000_000)] // 20MB limit
    public async Task<ActionResult<TenderDocumentDto>> UploadDocument(
        Guid id,
        IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? documentName = null,
        [FromForm] bool isPublic = false)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            var tender = await _tenderService.GetTenderByIdAsync(id);
            if (tender is null)
            {
                return NotFound("Tender not found");
            }

            var normalizedType = documentType?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedType) || normalizedType.Length > 50)
            {
                return BadRequest("A valid document type is required.");
            }

            var safeName = Path.GetFileName(file.FileName);
            var normalizedName = string.IsNullOrWhiteSpace(documentName) ? safeName : documentName.Trim();
            if (normalizedName.Length > 200)
            {
                return BadRequest("Document name cannot exceed 200 characters.");
            }

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
                        SourceLabel = "Procurement / Tender documents",
                        SourceEntityType = "Tender",
                        SourceRecordId = id,
                        SourceRecordReference = tender.TenderNumber,
                        Title = normalizedName,
                        DocumentType = "TenderDocument",
                        MetadataTemplateCode = "TDC-PROC-TENDER",
                        AccessProfile = "Procurement tender restricted",
                        VersionStatus = "Submitted",
                        ChangeSummary = $"{normalizedType} uploaded from the tender record.",
                        RequirePublishedGovernance = true,
                        MetadataValues =
                        [
                            new("sourceReference", "Source reference", tender.TenderNumber),
                            new("documentFamily", "Document family", "Tender"),
                            new("classification", "Classification", normalizedType),
                            new("sourceStatus", "Source status", tender.Status),
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
            var dto = new UploadTenderDocumentDto
            {
                DocumentName = normalizedName,
                DocumentType = normalizedType,
                IsPublic = isPublic
            };

            TenderDocumentDto document;
            try
            {
                document = await _tenderService.UploadTenderDocumentAsync(id, dto,
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
            return Created($"/api/procurement/Tenders/{id}/documents/{document.Id}", document);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading tender document");
            return StatusCode(500, "An error occurred while uploading the document");
        }
    }

    /// <summary>
    /// Download tender document
    /// </summary>
    [HttpGet("{id}/documents/{documentId}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId)
    {
        try
        {
            var documents = await _tenderService.GetTenderDocumentsAsync(
                id, includeInternal: !_currentUserProvider.IsExternalUser);
            var document = documents.FirstOrDefault(d => d.Id == documentId);

            if (document == null)
            {
                return NotFound("Document not found");
            }

            if (document.CentralDocumentRecordId.HasValue && document.CentralDocumentVersionId.HasValue)
            {
                var content = await _centralDocuments.OpenAsync(
                    _currentUserProvider.TenantId,
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

            // Legacy records remain readable until the separately deferred archive migration is selected.
            if (string.IsNullOrEmpty(document.FilePath) || !System.IO.File.Exists(document.FilePath))
            {
                return NotFound("Legacy document file not found on server");
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
    /// Delete tender document
    /// </summary>
    [HttpDelete("{id}/documents/{documentId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> DeleteDocument(Guid id, Guid documentId)
    {
        try
        {
            await _tenderService.DeleteTenderDocumentAsync(documentId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while deleting the document");
        }
    }

    /// <summary>
    /// Invite tenderers
    /// </summary>
    [HttpPost("{id}/invitations")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> InviteTenderers(Guid id, [FromBody] InviteTenderersDto dto)
    {
        try
        {
            await _tenderService.InviteTenderersAsync(id, dto);
            return Ok(new { message = "Invitations sent successfully" });
        }
        catch (ProcurementExceptionalSourcingConflictException ex)
        {
            return Conflict(new { code = ex.Code, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting tenderers");
            return StatusCode(500, "An error occurred while sending invitations");
        }
    }

    /// <summary>
    /// Add tender fee
    /// </summary>
    [HttpPost("{id}/fees")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderFeeDto>> AddTenderFee(Guid id, [FromBody] CreateTenderFeeDto dto)
    {
        try
        {
            var fee = await _tenderService.AddTenderFeeAsync(id, dto);
            return Ok(fee);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tender fee");
            return StatusCode(500, "An error occurred while adding the tender fee");
        }
    }

    /// <summary>
    /// Update tender fee
    /// </summary>
    [HttpPut("{tenderId}/fees/{feeId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderFeeDto>> UpdateTenderFee(Guid tenderId, Guid feeId, [FromBody] CreateTenderFeeDto dto)
    {
        try
        {
            var fee = await _tenderService.UpdateTenderFeeAsync(feeId, dto);
            return Ok(fee);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender fee {FeeId}", feeId);
            return StatusCode(500, "An error occurred while updating the tender fee");
        }
    }

    /// <summary>
    /// Delete tender fee
    /// </summary>
    [HttpDelete("{tenderId}/fees/{feeId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> DeleteTenderFee(Guid tenderId, Guid feeId)
    {
        try
        {
            await _tenderService.DeleteTenderFeeAsync(feeId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender fee {FeeId}", feeId);
            return StatusCode(500, "An error occurred while deleting the tender fee");
        }
    }

    /// <summary>
    /// Get tender evaluators
    /// </summary>
    [HttpGet("{id}/evaluators")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<IEnumerable<TenderEvaluatorDto>>> GetTenderEvaluators(Guid id)
    {
        try
        {
            var evaluators = await _tenderService.GetTenderEvaluatorsAsync(id);
            return Ok(evaluators);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluators for tender {TenderId}", id);
            return StatusCode(500, "An error occurred while retrieving evaluators");
        }
    }

    /// <summary>
    /// Assign evaluators
    /// </summary>
    [HttpPost("{id}/evaluators")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> AssignEvaluators(Guid id, [FromBody] AssignEvaluatorsDto dto)
    {
        try
        {
            await _tenderService.AssignEvaluatorsAsync(id, dto);
            return Ok(new { message = "Evaluators assigned successfully" });
        }
        catch (ProcurementEvaluationCommitteeConflictException ex)
        {
            return Conflict(EvaluationCommitteeProblem(ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                EvaluationCommitteeProblem("EVALUATION_COMMITTEE_ACCESS_FORBIDDEN", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning evaluators");
            return StatusCode(500, "An error occurred while assigning evaluators");
        }
    }

    /// <summary>
    /// Remove evaluator
    /// </summary>
    [HttpDelete("{tenderId}/evaluators/{evaluatorId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> RemoveEvaluator(Guid tenderId, Guid evaluatorId)
    {
        try
        {
            await _tenderService.RemoveEvaluatorAsync(evaluatorId);
            return NoContent();
        }
        catch (ProcurementEvaluationCommitteeConflictException ex)
        {
            return Conflict(EvaluationCommitteeProblem(ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                EvaluationCommitteeProblem("EVALUATION_COMMITTEE_ACCESS_FORBIDDEN", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing evaluator {EvaluatorId}", evaluatorId);
            return StatusCode(500, "An error occurred while removing the evaluator");
        }
    }

    /// <summary>
    /// Get tender clarifications
    /// </summary>
    [HttpGet("{id}/clarifications")]
    public async Task<ActionResult<IEnumerable<TenderClarificationDto>>> GetTenderClarifications(Guid id, [FromQuery] bool publicOnly = true)
    {
        try
        {
            var clarifications = await _tenderService.GetTenderClarificationsAsync(id, publicOnly);
            return Ok(clarifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting clarifications for tender {TenderId}", id);
            return StatusCode(500, "An error occurred while retrieving clarifications");
        }
    }

    /// <summary>
    /// Create clarification
    /// </summary>
    [HttpPost("{id}/clarifications")]
    public async Task<ActionResult<TenderClarificationDto>> CreateClarification(Guid id, [FromBody] CreateClarificationDto dto)
    {
        try
        {
            var clarification = await _tenderService.CreateClarificationAsync(id, dto);
            return Ok(clarification);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating clarification");
            return StatusCode(500, "An error occurred while creating the clarification");
        }
    }

    /// <summary>
    /// Answer clarification
    /// </summary>
    [HttpPost("{tenderId}/clarifications/{clarificationId}/answer")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderClarificationDto>> AnswerClarification(Guid tenderId, Guid clarificationId, [FromBody] AnswerClarificationDto dto)
    {
        try
        {
            var clarification = await _tenderService.AnswerClarificationAsync(clarificationId, dto);
            return Ok(clarification);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering clarification {ClarificationId}", clarificationId);
            return StatusCode(500, "An error occurred while answering the clarification");
        }
    }

    #region Tender LOT Endpoints

    /// <summary>
    /// Get all LOTs for a tender
    /// </summary>
    [HttpGet("{tenderId}/lots")]
    public async Task<ActionResult<IEnumerable<TenderLotDto>>> GetTenderLots(Guid tenderId)
    {
        try
        {
            var lots = await _tenderService.GetTenderLotsAsync(tenderId);
            return Ok(lots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lots for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while retrieving tender lots");
        }
    }

    /// <summary>
    /// Get a specific LOT by ID
    /// </summary>
    [HttpGet("lots/{lotId}")]
    public async Task<ActionResult<TenderLotDto>> GetTenderLot(Guid lotId)
    {
        try
        {
            var lot = await _tenderService.GetTenderLotByIdAsync(lotId);
            if (lot == null)
            {
                return NotFound($"Tender lot with ID {lotId} not found");
            }
            return Ok(lot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tender lot {LotId}", lotId);
            return StatusCode(500, "An error occurred while retrieving the tender lot");
        }
    }

    /// <summary>
    /// Add a new LOT to a tender
    /// </summary>
    [HttpPost("{tenderId}/lots")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderLotDto>> AddTenderLot(Guid tenderId, [FromBody] CreateTenderLotDto dto)
    {
        try
        {
            var lot = await _tenderService.AddTenderLotAsync(tenderId, dto);
            return CreatedAtAction(nameof(GetTenderLot), new { lotId = lot.Id }, lot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding lot to tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while adding the tender lot");
        }
    }

    /// <summary>
    /// Update a tender LOT
    /// </summary>
    [HttpPut("lots/{lotId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderLotDto>> UpdateTenderLot(Guid lotId, [FromBody] UpdateTenderLotDto dto)
    {
        try
        {
            var lot = await _tenderService.UpdateTenderLotAsync(lotId, dto);
            return Ok(lot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender lot {LotId}", lotId);
            return StatusCode(500, "An error occurred while updating the tender lot");
        }
    }

    /// <summary>
    /// Delete a tender LOT
    /// </summary>
    [HttpDelete("lots/{lotId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> DeleteTenderLot(Guid lotId)
    {
        try
        {
            await _tenderService.DeleteTenderLotAsync(lotId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender lot {LotId}", lotId);
            return StatusCode(500, "An error occurred while deleting the tender lot");
        }
    }

    /// <summary>
    /// Assign an item to a LOT
    /// </summary>
    [HttpPost("items/{itemId}/assign-lot/{lotId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> AssignItemToLot(Guid itemId, Guid lotId)
    {
        try
        {
            await _tenderService.AssignItemToLotAsync(itemId, lotId);
            return Ok(new { message = "Item assigned to lot successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning item {ItemId} to lot {LotId}", itemId, lotId);
            return StatusCode(500, "An error occurred while assigning the item to the lot");
        }
    }

    /// <summary>
    /// Remove an item from its LOT
    /// </summary>
    [HttpPost("items/{itemId}/remove-from-lot")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> RemoveItemFromLot(Guid itemId)
    {
        try
        {
            await _tenderService.RemoveItemFromLotAsync(itemId);
            return Ok(new { message = "Item removed from lot successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item {ItemId} from lot", itemId);
            return StatusCode(500, "An error occurred while removing the item from the lot");
        }
    }

    #endregion

    private ProblemDetails EvaluationCommitteeProblem(string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = code == "EVALUATION_COMMITTEE_ACCESS_FORBIDDEN" ? 403 : 409,
            Title = "Evaluation committee control",
            Detail = detail,
            Instance = HttpContext?.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = string.IsNullOrWhiteSpace(HttpContext?.TraceIdentifier)
            ? Guid.NewGuid().ToString("N")
            : HttpContext.TraceIdentifier;
        return problem;
    }

    private ProblemDetails SourcingProblem(
        string code,
        string detail,
        PurchaseRequisitionSourcingReadinessDto? readiness = null,
        int status = 422)
    {
        var problem = new ProblemDetails { Status = status, Title = code, Detail = detail, Instance = HttpContext?.Request.Path };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = string.IsNullOrWhiteSpace(HttpContext?.TraceIdentifier)
            ? Guid.NewGuid().ToString("N")
            : HttpContext.TraceIdentifier;
        if (readiness is not null) problem.Extensions["readiness"] = readiness;
        return problem;
    }
}

public record ApproveTenderRequest(string? Notes);
public record RejectTenderRequest(string? Reason);

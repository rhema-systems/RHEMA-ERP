using System.Security.Claims;
using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/business-partner-registrations")]
[Authorize]
public class BusinessPartnerRegistrationsController : ControllerBase
{
    private readonly IBusinessPartnerRegistrationService _registrationService;
    private readonly IProcurementSupplierApplicantAccessService _applicantAccessService;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<BusinessPartnerRegistrationsController> _logger;

    public BusinessPartnerRegistrationsController(
        IBusinessPartnerRegistrationService registrationService,
        IProcurementSupplierApplicantAccessService applicantAccessService,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        ILogger<BusinessPartnerRegistrationsController> logger)
    {
        _registrationService = registrationService;
        _applicantAccessService = applicantAccessService;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of registrations with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<BusinessPartnerRegistrationDto>>> GetRegistrations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? partnerType = null,
        [FromQuery] string? status = null)
    {
        try
        {
            if (pageSize > 100)
            {
                pageSize = 100;
            }

            var result = await _registrationService.GetRegistrationsAsync(page, pageSize, search, partnerType, status);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partner registrations");
            return StatusCode(500, "An error occurred while retrieving business partner registrations");
        }
    }

    /// <summary>
    /// Debug endpoint to check raw database data
    /// </summary>
    [HttpGet("debug/check-db")]
    public async Task<ActionResult> CheckDatabaseData()
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            // Direct database query to check what exists
            var allRegistrations = await _registrationService.GetAllRegistrationsForDebugAsync();

            return Ok(new
            {
                CurrentUserId = userId,
                TotalRegistrationsInDb = allRegistrations.Count(),
                AllRegistrations = allRegistrations.Select(r => new
                {
                    r.Id,
                    r.ApplicationNumber,
                    r.CompanyName,
                    r.Status,
                    r.CreatedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in debug endpoint");
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    /// Gets registrations for the current user (external portal)
    /// </summary>
    [HttpGet("my-registrations")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerRegistrationDto>>> GetMyRegistrations()
    {
        try
        {
            // Log ALL claims for debugging
            var allClaims = User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
            _logger.LogInformation("GetMyRegistrations - All claims: {Claims}", string.Join(", ", allClaims));

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tenantIdClaim = User.FindFirstValue("tenant_id");
            var usernameClaim = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation("GetMyRegistrations called. UserIdClaim: {UserIdClaim}, TenantIdClaim: {TenantIdClaim}, Username: {Username}, IsAuthenticated: {IsAuthenticated}",
                userIdClaim, tenantIdClaim, usernameClaim, User.Identity?.IsAuthenticated);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("User ID claim not found in token");
                return Unauthorized("User ID not found in token");
            }

            var userId = Guid.Parse(userIdClaim);
            _logger.LogInformation("Fetching registrations for user {UserId}", userId);

            var registrations = await _registrationService.GetMyRegistrationsAsync(userId);

            _logger.LogInformation("Returning {Count} registrations for user {UserId}", registrations.Count(), userId);

            // Log the actual data being returned
            if (registrations.Any())
            {
                foreach (var reg in registrations)
                {
                    _logger.LogInformation("Returning registration: Id={Id}, ApplicationNumber={AppNum}, Status={Status}, CompanyName={Company}",
                        reg.Id, reg.ApplicationNumber, reg.Status, reg.CompanyName);
                }
            }
            else
            {
                _logger.LogWarning("No registrations found to return for user {UserId}", userId);
            }

            return Ok(registrations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user registrations");
            return StatusCode(500, "An error occurred while retrieving your registrations");
        }
    }

    /// <summary>
    /// Gets pending review registrations (internal admin)
    /// </summary>
    [HttpGet("pending-review")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerRegistrationDto>>> GetPendingReviewRegistrations()
    {
        try
        {
            var registrations = await _registrationService.GetPendingReviewRegistrationsAsync();
            return Ok(registrations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending review registrations");
            return StatusCode(500, "An error occurred while retrieving pending review registrations");
        }
    }

    /// <summary>
    /// Gets a registration by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessPartnerRegistrationDetailDto>> GetRegistration(Guid id)
    {
        try
        {
            var registration = await _registrationService.GetByIdAsync(id);
            if (registration == null)
            {
                return NotFound();
            }

            return Ok(registration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while retrieving the business partner registration");
        }
    }

    /// <summary>
    /// Gets a registration by application number
    /// </summary>
    [HttpGet("by-application-number/{applicationNumber}")]
    public async Task<ActionResult<BusinessPartnerRegistrationDto>> GetRegistrationByApplicationNumber(string applicationNumber)
    {
        try
        {
            var registration = await _registrationService.GetByApplicationNumberAsync(applicationNumber);
            if (registration == null)
            {
                return NotFound();
            }

            return Ok(registration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business partner registration by application number {ApplicationNumber}", applicationNumber);
            return StatusCode(500, "An error occurred while retrieving the business partner registration");
        }
    }

    /// <summary>
    /// Creates a new registration (external portal - draft)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<BusinessPartnerRegistrationDetailDto>> CreateRegistration([FromBody] CreateBusinessPartnerRegistrationDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var registration = await _registrationService.CreateAsync(createDto, userId);
            return CreatedAtAction(nameof(GetRegistration), new { id = registration.Id }, registration);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating business partner registration");
            return StatusCode(500, "An error occurred while creating the business partner registration");
        }
    }

    /// <summary>
    /// Updates an existing registration (external portal - draft only)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<BusinessPartnerRegistrationDetailDto>> UpdateRegistration(Guid id, [FromBody] UpdateBusinessPartnerRegistrationDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var registration = await _registrationService.UpdateAsync(id, updateDto, userId);
            return Ok(registration);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while updating the business partner registration");
        }
    }

    /// <summary>
    /// Deletes a registration (draft only)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> DeleteRegistration(Guid id)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _registrationService.DeleteAsync(id, userId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while deleting the business partner registration");
        }
    }

    /// <summary>
    /// Submits a registration for review (external portal)
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> SubmitRegistration(Guid id)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _registrationService.SubmitForReviewAsync(id, userId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ProcurementSupplierEvidencePackAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Supplier registration evidence access forbidden",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (ProcurementSupplierEvidencePackValidationException ex)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [ex.Code] = [ex.Message]
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Supplier registration evidence is incomplete",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = ex.Code;
            return UnprocessableEntity(problem);
        }
        catch (ProcurementSupplierEvidencePackConflictException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Supplier registration evidence-pack conflict",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path,
                Extensions = { ["code"] = ex.Code }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while submitting the business partner registration");
        }
    }

    /// <summary>
    /// Reviews a registration (internal admin)
    /// </summary>
    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ReviewRegistration(Guid id, [FromBody] ReviewRegistrationRequest request)
    {
        try
        {
            var reviewDto = new ReviewBusinessPartnerRegistrationDto
            {
                RegistrationId = id,
                Notes = request.ReviewNotes,
                Action = "Review"
            };
            await _registrationService.ReviewRegistrationAsync(
                reviewDto, AuthenticatedUserId());
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthorizationProblem(ex);
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
            _logger.LogError(ex, "Error reviewing business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while reviewing the business partner registration");
        }
    }

    /// <summary>
    /// Approves a registration and creates business partner (internal admin)
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<BusinessPartnerDetailDto>> ApproveRegistration(Guid id, [FromBody] ApproveRegistrationRequest request)
    {
        try
        {
            var userId = AuthenticatedUserId();

            // Approve the registration (creates business partner and saves everything)
            await _registrationService.ApproveRegistrationAsync(id, userId, request.Notes);

            // Get the updated registration with business partner details
            var registration = await _registrationService.GetByIdAsync(id);
            if (registration?.BusinessPartnerId is Guid businessPartnerId)
            {
                await _applicantAccessService.ProvisionApprovedSupplierAsync(
                    id,
                    businessPartnerId,
                    userId,
                    $"supplier-applicant-approval-{id:N}",
                    HttpContext.RequestAborted);
                registration = await _registrationService.GetByIdAsync(id);
            }

            return Ok(registration);
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthorizationProblem(ex);
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
            _logger.LogError(ex, "Error approving business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while approving the business partner registration");
        }
    }

    /// <summary>
    /// Rejects a registration (internal admin)
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RejectRegistration(Guid id, [FromBody] RejectRegistrationRequest request)
    {
        try
        {
            var userId = AuthenticatedUserId();
            await _registrationService.RejectRegistrationAsync(id, userId, request.Reason);
            await _applicantAccessService.CloseForTerminalRegistrationAsync(
                id,
                "Rejected",
                userId,
                $"supplier-applicant-rejection-{id:N}",
                HttpContext.RequestAborted);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthorizationProblem(ex);
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
            _logger.LogError(ex, "Error rejecting business partner registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while rejecting the business partner registration");
        }
    }

    /// <summary>
    /// Requests more information from applicant (internal admin)
    /// </summary>
    [HttpPost("{id:guid}/request-more-info")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RequestMoreInfo(Guid id, [FromBody] RequestMoreInfoRequest request)
    {
        try
        {
            var userId = AuthenticatedUserId();
            await _registrationService.RequestMoreInfoAsync(id, userId, request.Notes);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthorizationProblem(ex);
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
            _logger.LogError(ex, "Error requesting more info for registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while requesting more information");
        }
    }

    /// <summary>
    /// Gets all documents for a registration
    /// </summary>
    [HttpGet("{id:guid}/documents")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerRegistrationDocumentDto>>> GetDocuments(Guid id)
    {
        try
        {
            var documents = await _registrationService
                .GetDocumentsForInternalReviewAsync(id, AuthenticatedUserId());
            return Ok(documents);
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthorizationProblem(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while retrieving documents");
        }
    }

    /// <summary>
    /// Uploads a document for a registration (external portal)
    /// </summary>
    [HttpPost("{id:guid}/documents")]
    [Authorize(Policy = "InternalOnly")]
    [RequestSizeLimit(20_000_000)] // 20MB limit
    public async Task<ActionResult<BusinessPartnerRegistrationDocumentDto>> UploadDocument(
        Guid id,
        IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? evidenceRequirementCode = null,
        [FromForm] string? classificationCode = null,
        [FromForm] DateTime? issueDate = null,
        [FromForm] DateTime? expiryDate = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            var userId = AuthenticatedUserId();
            var tenantId = _currentUser.TenantId ??
                throw new UnauthorizedAccessException("Tenant context is required.");
            var upload = await _controlledFiles.UploadAsync(
                new ControlledFileUploadRequest
                {
                    TenantId = tenantId,
                    ActorUserId = userId,
                    ActorName = _currentUser.UserName,
                    Category = ControlledFileUploadCategories.DocumentManagement,
                    FileName = Path.GetFileName(file.FileName),
                    ContentType = file.ContentType,
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
                        TenantId = tenantId,
                        ActorUserId = userId,
                        ActorName = _currentUser.UserName,
                        FileUploadRecordId = upload.Record.Id,
                        SourceModule = "Procurement",
                        SourceLabel = "Supplier registration evidence",
                        SourceEntityType = "BusinessPartnerRegistration",
                        SourceRecordId = id,
                        SourceRecordReference = id.ToString(),
                        Title = upload.Record.OriginalFileName,
                        DocumentType = documentType,
                        MetadataTemplateCode = "PROC-SUP-EVD",
                        AccessProfile = "Procurement restricted",
                        ChangeSummary = "Supplier registration evidence uploaded by an internal reviewer."
                    },
                    HttpContext.RequestAborted);
            }
            catch
            {
                await _controlledFiles.DeleteAsync(
                    tenantId, upload.Record.Id, userId, HttpContext.RequestAborted);
                throw;
            }

            BusinessPartnerRegistrationDocumentDto document;
            try
            {
                document = await _registrationService.UploadDocumentAsync(
                    id,
                    new CreateBusinessPartnerDocumentDto
                    {
                        FileUploadRecordId = upload.Record.Id,
                        CentralDocumentRecordId = centralDocument.DocumentRecordId,
                        CentralDocumentVersionId = centralDocument.DocumentVersionId,
                        DocumentType = documentType,
                        DocumentName = upload.Record.OriginalFileName,
                        DocumentPath = null,
                        FilePath = string.Empty,
                        FileSize = upload.Record.FileSize,
                        MimeType = upload.Record.ContentType,
                        EvidenceRequirementCode = evidenceRequirementCode,
                        ClassificationCode = classificationCode,
                        IssueDate = issueDate,
                        ExpiryDate = expiryDate,
                        ChecksumSha256 = upload.ChecksumSha256
                    },
                    userId);
            }
            catch
            {
                await _centralDocuments.DeleteAsync(
                    tenantId,
                    centralDocument.DocumentRecordId,
                    userId,
                    HttpContext.RequestAborted);
                throw;
            }
            return Created($"/api/procurement/business-partner-registrations/{id}/documents/{document.Id}", document);
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "Registration evidence upload failed",
                Status = ex.StatusCode,
                Detail = ex.Message,
                Extensions = { ["code"] = ex.Code }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthorizationProblem(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to registration {RegistrationId}", id);
            return StatusCode(500, "An error occurred while uploading the document");
        }
    }

    /// <summary>
    /// Tracks a document download
    /// </summary>
    [HttpPost("{id:guid}/documents/{documentId:guid}/track-download")]
    public async Task<IActionResult> TrackDocumentDownload(Guid id, Guid documentId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _registrationService.TrackDocumentDownloadAsync(id, documentId, userId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking document download for registration {RegistrationId}, document {DocumentId}", id, documentId);
            // Don't fail the request if tracking fails - just log it
            return NoContent();
        }
    }

    /// <summary>
    /// Verifies a document (internal admin)
    /// </summary>
    [HttpPost("{id:guid}/documents/{documentId:guid}/verify")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> VerifyDocument(Guid id, Guid documentId)
    {
        try
        {
            var userId = AuthenticatedUserId();
            await _registrationService.VerifyDocumentAsync(id, documentId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthorizationProblem(ex);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying document {DocumentId} for registration {RegistrationId}", documentId, id);
            return StatusCode(500, "An error occurred while verifying the document");
        }
    }

    /// <summary>
    /// Rejects a document (internal admin)
    /// </summary>
    [HttpPost("{id:guid}/documents/{documentId:guid}/reject")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RejectDocument(Guid id, Guid documentId, [FromBody] RejectDocumentRequest request)
    {
        try
        {
            var userId = AuthenticatedUserId();
            await _registrationService.RejectDocumentAsync(id, documentId, userId, request.Reason);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthorizationProblem(ex);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting document {DocumentId} for registration {RegistrationId}", documentId, id);
            return StatusCode(500, "An error occurred while rejecting the document");
        }
    }

    /// <summary>
    /// Reverts document rejection (internal admin)
    /// </summary>
    [HttpPost("{id:guid}/documents/{documentId:guid}/revert-rejection")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RevertDocumentRejection(Guid id, Guid documentId)
    {
        try
        {
            var userId = AuthenticatedUserId();
            await _registrationService.RevertDocumentRejectionAsync(id, documentId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthorizationProblem(ex);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reverting document rejection {DocumentId} for registration {RegistrationId}", documentId, id);
            return StatusCode(500, "An error occurred while reverting the document rejection");
        }
    }

    /// <summary>
    /// Downloads a document
    /// </summary>
    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId)
    {
        try
        {
            var document = await _registrationService
                .GetDocumentForInternalDownloadAsync(
                    id, documentId, AuthenticatedUserId());
            if (document == null)
            {
                return NotFound("Document not found");
            }

            ApplySensitiveDownloadHeaders();
            if (document.CentralDocumentRecordId.HasValue &&
                document.CentralDocumentVersionId.HasValue)
            {
                var centralContent = await _centralDocuments.OpenAsync(
                    _currentUser.TenantId ??
                        throw new UnauthorizedAccessException(
                            "Tenant context is required."),
                    document.CentralDocumentRecordId.Value,
                    document.CentralDocumentVersionId.Value,
                    HttpContext.RequestAborted);
                if (centralContent is null)
                {
                    return NotFound("Document content was not found in the central DMS.");
                }

                return File(
                    centralContent.Content,
                    centralContent.ContentType,
                    centralContent.FileName);
            }

            if (string.IsNullOrWhiteSpace(document.InternalStoragePath))
            {
                return NotFound("Document content was not found.");
            }

            var legacyContent = await _fileStorage.DownloadFileAsync(
                document.InternalStoragePath,
                document.FileUploadRecordId ?? document.Id);
            return File(
                legacyContent,
                document.MimeType ?? "application/octet-stream",
                document.DocumentName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthorizationProblem(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {DocumentId} for registration {RegistrationId}", documentId, id);
            return StatusCode(500, "An error occurred while downloading the document");
        }
    }

    private Guid AuthenticatedUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId) || userId == Guid.Empty)
            throw new UnauthorizedAccessException(
                "A valid authenticated user is required.");
        return userId;
    }

    private void ApplySensitiveDownloadHeaders()
    {
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    private ObjectResult AuthorizationProblem(UnauthorizedAccessException exception) =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Type = "https://tdc.gov.gh/problems/procurement-supplier-forbidden",
            Title = "Supplier registration action is forbidden",
            Status = StatusCodes.Status403Forbidden,
            Detail = exception.Message,
            Extensions = { ["code"] = "PROCUREMENT_SUPPLIER_FORBIDDEN" }
        });
}

public class RejectDocumentRequest
{
    public string Reason { get; set; } = string.Empty;
}

// Request models
public record ReviewRegistrationRequest(string? ReviewNotes);
public record ApproveRegistrationRequest(string? Notes);
public record RejectRegistrationRequest(string Reason);
public record RequestMoreInfoRequest(string Notes);


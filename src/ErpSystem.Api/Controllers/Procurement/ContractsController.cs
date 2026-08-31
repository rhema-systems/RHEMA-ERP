using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class ContractsController : ControllerBase
{
    private readonly IContractService _contractService;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<ContractsController> _logger;

    public ContractsController(
        IContractService contractService,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        ICurrentUserProvider currentUser,
        ILogger<ContractsController> logger)
    {
        _contractService = contractService;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _currentUser = currentUser;
        _logger = logger;
    }

    #region Contract CRUD

    /// <summary>
    /// Get all contracts with pagination
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult> GetContracts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? contractType = null)
    {
        try
        {
            var contracts = await _contractService.GetContractsAsync(page, pageSize, search, status, contractType);
            return Ok(contracts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contracts");
            return StatusCode(500, "An error occurred while retrieving contracts");
        }
    }

    /// <summary>
    /// Get contract by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<ContractDto>> GetContract(Guid id)
    {
        try
        {
            var contract = await _contractService.GetByIdAsync(id);
            if (contract == null)
            {
                return NotFound($"Contract with ID {id} not found");
            }

            return Ok(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contract {ContractId}", id);
            return StatusCode(500, "An error occurred while retrieving the contract");
        }
    }

    /// <summary>
    /// Get contract by award ID
    /// </summary>
    [HttpGet("by-award/{awardId}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<ContractDto>> GetContractByAward(Guid awardId)
    {
        try
        {
            var contract = await _contractService.GetByAwardIdAsync(awardId);
            if (contract == null)
            {
                return NotFound($"Contract for award {awardId} not found");
            }

            return Ok(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contract for award {AwardId}", awardId);
            return StatusCode(500, "An error occurred while retrieving the contract");
        }
    }

    /// <summary>
    /// Get active contracts
    /// </summary>
    [HttpGet("active")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<ContractListDto>>> GetActiveContracts()
    {
        try
        {
            var contracts = await _contractService.GetActiveContractsAsync();
            return Ok(contracts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active contracts");
            return StatusCode(500, "An error occurred while retrieving active contracts");
        }
    }

    /// <summary>
    /// Get expiring contracts
    /// </summary>
    [HttpGet("expiring")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<ContractListDto>>> GetExpiringContracts([FromQuery] int daysThreshold = 30)
    {
        try
        {
            var contracts = await _contractService.GetExpiringContractsAsync(daysThreshold);
            return Ok(contracts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring contracts");
            return StatusCode(500, "An error occurred while retrieving expiring contracts");
        }
    }

    /// <summary>
    /// Create contract from award
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "procurement.contract.manage")]
    public async Task<ActionResult<ContractDto>> CreateContract([FromBody] CreateContractDto dto)
    {
        try
        {
            var contract = await _contractService.CreateFromAwardAsync(dto);
            return CreatedAtAction(nameof(GetContract), new { id = contract.Id }, contract);
        }
        catch (SupplierEligibilityException ex)
        {
            return UnprocessableEntity(new
            {
                code = ex.Code,
                message = ex.Message,
                eligibility = ex.Result
            });
        }
        catch (ProcurementRequisitionSourcingValidationException ex)
        {
            return UnprocessableEntity(new
            {
                title = ex.Code,
                status = 422,
                detail = ex.Message,
                instance = Request.Path.Value,
                code = ex.Code,
                correlationId = HttpContext.TraceIdentifier
            });
        }
        catch (ProcurementSourcingCaseAuthorizationException ex)
        {
            return StatusCode(403, new
            {
                title = "Tender source recovery forbidden",
                status = 403,
                detail = ex.Message,
                instance = Request.Path.Value,
                code = "TENDER_SOURCE_RECOVERY_FORBIDDEN",
                correlationId = HttpContext.TraceIdentifier
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contract");
            return StatusCode(500, "An error occurred while creating the contract");
        }
    }

    /// <summary>
    /// Update contract
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "procurement.contract.manage")]
    public async Task<ActionResult<ContractDto>> UpdateContract(Guid id, [FromBody] UpdateContractDto dto)
    {
        try
        {
            var contract = await _contractService.UpdateAsync(id, dto);
            return Ok(contract);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contract {ContractId}", id);
            return StatusCode(500, "An error occurred while updating the contract");
        }
    }

    /// <summary>
    /// Delete contract
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "procurement.contract.manage")]
    public async Task<ActionResult> DeleteContract(Guid id)
    {
        try
        {
            await _contractService.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contract {ContractId}", id);
            return StatusCode(500, "An error occurred while deleting the contract");
        }
    }

    #endregion

    #region Status Management

    /// <summary>
    /// Update contract status
    /// </summary>
    [HttpPut("{id}/status")]
    [Authorize(Policy = "procurement.contract.approve")]
    public async Task<ActionResult<ContractDto>> UpdateStatus(Guid id, [FromBody] UpdateContractStatusDto dto)
    {
        try
        {
            var contract = await _contractService.UpdateStatusAsync(id, dto);
            return Ok(contract);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contract status {ContractId}", id);
            return StatusCode(500, "An error occurred while updating the contract status");
        }
    }

    /// <summary>
    /// Activate contract
    /// </summary>
    [HttpPost("{id}/activate")]
    [Authorize(Policy = "procurement.contract.approve")]
    public async Task<ActionResult<ContractDto>> ActivateContract(Guid id, [FromBody] UpdateContractStatusDto dto)
    {
        try
        {
            var contract = await _contractService.ActivateContractAsync(id, dto);
            return Ok(contract);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating contract {ContractId}", id);
            return StatusCode(500, "An error occurred while activating the contract");
        }
    }

    /// <summary>
    /// Complete contract
    /// </summary>
    [HttpPost("{id}/complete")]
    [Authorize(Policy = "procurement.contract.approve")]
    public async Task<ActionResult<ContractDto>> CompleteContract(Guid id)
    {
        try
        {
            var contract = await _contractService.CompleteContractAsync(id);
            return Ok(contract);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing contract {ContractId}", id);
            return StatusCode(500, "An error occurred while completing the contract");
        }
    }

    /// <summary>
    /// Terminate contract
    /// </summary>
    [HttpPost("{id}/terminate")]
    [Authorize(Policy = "procurement.contract.approve")]
    public async Task<ActionResult<ContractDto>> TerminateContract(Guid id, [FromBody] TerminateContractRequest request)
    {
        try
        {
            var contract = await _contractService.TerminateContractAsync(id, request.Reason);
            return Ok(contract);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating contract {ContractId}", id);
            return StatusCode(500, "An error occurred while terminating the contract");
        }
    }

    #endregion

    #region Milestones

    /// <summary>
    /// Get milestones for a contract
    /// </summary>
    [HttpGet("{contractId}/milestones")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<ContractMilestoneDto>>> GetMilestones(Guid contractId)
    {
        try
        {
            var milestones = await _contractService.GetMilestonesByContractIdAsync(contractId);
            return Ok(milestones);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting milestones for contract {ContractId}", contractId);
            return StatusCode(500, "An error occurred while retrieving milestones");
        }
    }

    /// <summary>
    /// Add milestone to contract
    /// </summary>
    [HttpPost("{contractId}/milestones")]
    [Authorize(Policy = "procurement.contract.manage")]
    public async Task<ActionResult<ContractMilestoneDto>> AddMilestone(Guid contractId, [FromBody] CreateContractMilestoneDto dto)
    {
        try
        {
            var milestone = await _contractService.AddMilestoneAsync(contractId, dto);
            return Ok(milestone);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding milestone to contract {ContractId}", contractId);
            return StatusCode(500, "An error occurred while adding the milestone");
        }
    }

    /// <summary>
    /// Update milestone
    /// </summary>
    [HttpPut("milestones/{milestoneId}")]
    [Authorize(Policy = "procurement.contract.manage")]
    public async Task<ActionResult<ContractMilestoneDto>> UpdateMilestone(Guid milestoneId, [FromBody] UpdateContractMilestoneDto dto)
    {
        try
        {
            var milestone = await _contractService.UpdateMilestoneAsync(milestoneId, dto);
            return Ok(milestone);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating milestone {MilestoneId}", milestoneId);
            return StatusCode(500, "An error occurred while updating the milestone");
        }
    }

    /// <summary>
    /// Update milestone status
    /// </summary>
    [HttpPut("milestones/{milestoneId}/status")]
    [Authorize(Policy = "procurement.contract.manage")]
    public async Task<ActionResult<ContractMilestoneDto>> UpdateMilestoneStatus(Guid milestoneId, [FromBody] UpdateMilestoneStatusDto dto)
    {
        try
        {
            var milestone = await _contractService.UpdateMilestoneStatusAsync(milestoneId, dto);
            return Ok(milestone);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating milestone status {MilestoneId}", milestoneId);
            return StatusCode(500, "An error occurred while updating the milestone status");
        }
    }

    /// <summary>
    /// Delete milestone
    /// </summary>
    [HttpDelete("milestones/{milestoneId}")]
    [Authorize(Policy = "procurement.contract.manage")]
    public async Task<ActionResult> DeleteMilestone(Guid milestoneId)
    {
        try
        {
            await _contractService.DeleteMilestoneAsync(milestoneId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting milestone {MilestoneId}", milestoneId);
            return StatusCode(500, "An error occurred while deleting the milestone");
        }
    }

    #endregion

    #region Amendments

    /// <summary>
    /// Get amendments for a contract
    /// </summary>
    [HttpGet("{contractId}/amendments")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<ContractAmendmentDto>>> GetAmendments(Guid contractId)
    {
        try
        {
            var amendments = await _contractService.GetAmendmentsByContractIdAsync(contractId);
            return Ok(amendments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting amendments for contract {ContractId}", contractId);
            return StatusCode(500, "An error occurred while retrieving amendments");
        }
    }

    /// <summary>
    /// Get pending amendments
    /// </summary>
    [HttpGet("amendments/pending")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<ContractAmendmentDto>>> GetPendingAmendments()
    {
        try
        {
            var amendments = await _contractService.GetPendingAmendmentsAsync();
            return Ok(amendments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending amendments");
            return StatusCode(500, "An error occurred while retrieving pending amendments");
        }
    }

    /// <summary>
    /// Create amendment
    /// </summary>
    [HttpPost("{contractId}/amendments")]
    [Authorize(Policy = "procurement.contract.manage")]
    public async Task<ActionResult<ContractAmendmentDto>> CreateAmendment(Guid contractId, [FromBody] CreateContractAmendmentDto dto)
    {
        try
        {
            var amendment = await _contractService.CreateAmendmentAsync(contractId, dto);
            return Ok(amendment);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating amendment for contract {ContractId}", contractId);
            return StatusCode(500, "An error occurred while creating the amendment");
        }
    }

    /// <summary>
    /// Process (approve/reject) amendment
    /// </summary>
    [HttpPost("amendments/{amendmentId}/process")]
    [Authorize(Policy = "procurement.contract.approve")]
    public async Task<ActionResult<ContractAmendmentDto>> ProcessAmendment(Guid amendmentId, [FromBody] ProcessAmendmentDto dto)
    {
        try
        {
            var amendment = await _contractService.ProcessAmendmentAsync(amendmentId, dto);
            return Ok(amendment);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Contract amendment approval forbidden",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing amendment {AmendmentId}", amendmentId);
            return StatusCode(500, "An error occurred while processing the amendment");
        }
    }

    /// <summary>
    /// Delete amendment
    /// </summary>
    [HttpDelete("amendments/{amendmentId}")]
    [Authorize(Policy = "procurement.contract.manage")]
    public async Task<ActionResult> DeleteAmendment(Guid amendmentId)
    {
        try
        {
            await _contractService.DeleteAmendmentAsync(amendmentId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting amendment {AmendmentId}", amendmentId);
            return StatusCode(500, "An error occurred while deleting the amendment");
        }
    }

    #endregion

    #region Documents

    /// <summary>
    /// Get documents for a contract
    /// </summary>
    [HttpGet("{contractId}/documents")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<ContractDocumentDto>>> GetDocuments(Guid contractId)
    {
        try
        {
            var documents = await _contractService.GetDocumentsByContractIdAsync(contractId);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents for contract {ContractId}", contractId);
            return StatusCode(500, "An error occurred while retrieving documents");
        }
    }

    /// <summary>
    /// Upload document to contract
    /// </summary>
    [HttpPost("{contractId}/documents")]
    [Authorize(Policy = "procurement.contract.manage")]
    [RequestSizeLimit(20_000_000)] // 20MB limit
    public async Task<ActionResult<ContractDocumentDto>> UploadDocument(
        Guid contractId,
        IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? description = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            var contract = await _contractService.GetByIdAsync(contractId);
            if (contract is null)
            {
                return NotFound("Contract not found.");
            }
            var normalizedDocumentType = documentType?.Trim();
            var contractFamily = ProcurementDocumentManagementCatalog.Find(
                ProcurementDocumentFamily.Contract)!;
            if (string.IsNullOrWhiteSpace(normalizedDocumentType) ||
                !contractFamily.Classifications.Contains(
                    normalizedDocumentType, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest("Select a supported contract document type from the controlled list.");
            }

            var upload = await _controlledFiles.UploadAsync(
                new ControlledFileUploadRequest
                {
                    TenantId = _currentUser.TenantId,
                    ActorUserId = _currentUser.UserId,
                    ActorName = ActorName,
                    Category = ControlledFileUploadCategories.DocumentManagement,
                    FileName = Path.GetFileName(file.FileName),
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
                        ActorName = ActorName,
                        FileUploadRecordId = upload.Record.Id,
                        SourceModule = "Procurement",
                        SourceLabel = "Procurement contract evidence",
                        SourceEntityType = "Contract",
                        SourceRecordId = contractId,
                        SourceRecordReference = contract.ContractNumber,
                        Title = upload.Record.OriginalFileName,
                        DocumentType = "ContractEvidence",
                        MetadataTemplateCode = "PROC-CON-EVD",
                        AccessProfile = "Procurement contract restricted",
                        ChangeSummary = description,
                        RequirePublishedGovernance = true,
                        MetadataValues =
                        [
                            new("sourceReference", "Source reference", contract.ContractNumber),
                            new("documentFamily", "Document family", "Contract"),
                            new("classification", "Classification", normalizedDocumentType),
                            new("sourceStatus", "Source status", contract.Status),
                            new("uploadedBy", "Uploaded by", ActorName),
                            new("checksumSha256", "Checksum SHA-256", upload.ChecksumSha256)
                        ]
                    },
                    HttpContext.RequestAborted);
            }
            catch
            {
                await _controlledFiles.DeleteAsync(
                    _currentUser.TenantId, upload.Record.Id,
                    _currentUser.UserId, HttpContext.RequestAborted);
                throw;
            }

            ContractDocumentDto document;
            try
            {
                document = await _contractService.UploadDocumentAsync(
                    contractId,
                    normalizedDocumentType,
                    upload.Record.OriginalFileName,
                    centralDocument.DocumentReference,
                    upload.Record.ContentType,
                    upload.Record.FileSize,
                    description,
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

            return Created($"/api/procurement/Contracts/{contractId}/documents/{document.Id}", document);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to contract {ContractId}", contractId);
            return StatusCode(500, "An error occurred while uploading the document");
        }
    }

    /// <summary>
    /// Delete document
    /// </summary>
    [HttpDelete("documents/{documentId}")]
    [Authorize(Policy = "procurement.contract.manage")]
    public async Task<ActionResult> DeleteDocument(Guid documentId)
    {
        try
        {
            await _contractService.DeleteDocumentAsync(documentId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while deleting the document");
        }
    }

    #endregion

    private string ActorName =>
        string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName;
}

/// <summary>
/// Request body for terminating a contract
/// </summary>
public class TerminateContractRequest
{
    public string Reason { get; set; } = string.Empty;
}

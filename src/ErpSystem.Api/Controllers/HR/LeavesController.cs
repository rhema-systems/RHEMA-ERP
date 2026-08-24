using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Models;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR
{
    /// <summary>
    /// Leave management endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "InternalOnly")]
    public class LeavesController : ControllerBase
    {
        private readonly ILeaveService _leaveService;
        private readonly ILeaveBalanceRecalculationService _recalculationService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IHrControlledDocumentService _hrDocuments;
        private readonly ICentralDocumentRepositoryFileService _centralDocuments;
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<LeavesController> _logger;

        public LeavesController(
            ILeaveService leaveService,
            ILeaveBalanceRecalculationService recalculationService,
            IFileStorageService fileStorageService,
            IHrControlledDocumentService hrDocuments,
            ICentralDocumentRepositoryFileService centralDocuments,
            ApplicationDbContext db,
            ICurrentUserService currentUserService,
            ILogger<LeavesController> logger)
        {
            _leaveService = leaveService;
            _recalculationService = recalculationService;
            _fileStorageService = fileStorageService;
            _hrDocuments = hrDocuments;
            _centralDocuments = centralDocuments;
            _db = db;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new leave request
        /// </summary>
        /// <param name="dto">Leave request data</param>
        /// <returns>Created leave request</returns>
        /// <response code="201">Leave request created successfully</response>
        /// <response code="400">Invalid request data or business rule violation</response>
        [HttpPost]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<LeaveRequestDto>> CreateLeaveRequest([FromBody] CreateLeaveRequestDto dto)
        {
            try
            {
                var application = await _leaveService.CreateLeaveRequestAsync(dto);
                return CreatedAtAction(nameof(GetLeaveApplicationById), new { id = application.Id }, application);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating leave request");
                return StatusCode(500, "An error occurred while creating leave request");
            }
        }

        /// <summary>
        /// Update an existing draft leave request
        /// </summary>
        [HttpPut("{id:guid}/draft")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> UpdateDraft(Guid id, [FromBody] CreateLeaveRequestDto dto)
        {
            try
            {
                var result = await _leaveService.UpdateDraftAsync(id, dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating draft leave request {Id}", id);
                return StatusCode(500, "An error occurred while updating the draft leave request");
            }
        }

        /// <summary>
        /// Get leave application by ID
        /// </summary>
        /// <param name="id">Leave application ID</param>
        /// <returns>Leave application details</returns>
        /// <response code="200">Leave application found</response>
        /// <response code="404">Leave application not found</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> GetLeaveApplicationById(Guid id)
        {
            try
            {
                var application = await _leaveService.GetLeaveRequestByIdAsync(id);
                return Ok(application);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting leave request");
                return StatusCode(500, "An error occurred while getting leave request");
            }
        }

        /// <summary>
        /// Get leave application by application number (tracking)
        /// </summary>
        /// <param name="requestNumber">Application number</param>
        /// <returns>Leave request details</returns>
        /// <response code="200">Leave request found</response>
        /// <response code="404">Leave request not found</response>
        [HttpGet("by-number/{requestNumber}")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> GetLeaveApplicationByNumber(string requestNumber)
        {
            var application = await _leaveService.GetLeaveRequestByNumberAsync(requestNumber);

            if (application == null)
            {
                return NotFound(new { message = $"Leave application '{requestNumber}' not found." });
            }

            return Ok(application);
        }

        /// <summary>
        /// Get employee leave history for a specific year
        /// </summary>
        /// <param name="employeeId">Employee ID</param>
        /// <param name="year">Year</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20)</param>
        /// <returns>Paged list of leave applications</returns>
        /// <response code="200">Leave history retrieved</response>
        [HttpGet("employee/{employeeId}/history")]
        [ProducesResponseType(typeof(PagedResult<LeaveRequestDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<LeaveRequestDto>>> GetEmployeeLeaveHistory(
            Guid employeeId,
            [FromQuery] int year = 0,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            if (year == 0)
            {
                year = DateTime.Today.Year;
            }

            var result = await _leaveService.GetEmployeeLeaveHistoryAsync(employeeId, year, pageNumber, pageSize);

            return Ok(result);
        }

        /// <summary>
        /// Get employee leave balances for a specific year
        /// </summary>
        /// <param name="employeeId">Employee ID</param>
        /// <param name="year">Year (default: current year)</param>
        /// <returns>List of leave balances by leave type</returns>
        /// <response code="200">Leave balances retrieved</response>
        [HttpGet("employee/{employeeId}/balances")]
        [ProducesResponseType(typeof(IEnumerable<LeaveBalanceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<LeaveBalanceDto>>> GetEmployeeLeaveBalances(
            Guid employeeId,
            [FromQuery] int year = 0)
        {
            if (year == 0) year = DateTime.Today.Year;
            var balances = await _leaveService.GetEmployeeLeaveBalancesAsync(employeeId, year);
            return Ok(balances);
        }

        /// <summary>
        /// Get leave balances across all employees with optional filters
        /// </summary>
        [HttpGet("balances")]
        [ProducesResponseType(typeof(IEnumerable<LeaveBalanceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<LeaveBalanceDto>>> GetAllLeaveBalances(
            [FromQuery] int year = 0,
            [FromQuery] Guid? employeeId = null,
            [FromQuery] Guid? leaveTypeId = null)
        {
            if (year == 0) year = DateTime.Today.Year;
            var balances = await _leaveService.GetAllLeaveBalancesAsync(year, employeeId, leaveTypeId);
            return Ok(balances);
        }

        /// <summary>
        /// Mandatory-leave compliance: for leave types flagged as mandatory-to-take, shows each
        /// employee's taken / scheduled / outstanding days for the year.
        /// </summary>
        [HttpGet("mandatory-compliance")]
        [ProducesResponseType(typeof(IEnumerable<MandatoryLeaveComplianceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<MandatoryLeaveComplianceDto>>> GetMandatoryCompliance(
            [FromQuery] int year = 0)
        {
            if (year == 0) year = DateTime.Today.Year;
            var rows = await _leaveService.GetMandatoryLeaveComplianceAsync(year);
            return Ok(rows);
        }

        /// <summary>
        /// Get full audit-trail detail for a single leave balance
        /// </summary>
        [HttpGet("balances/{id}")]
        [ProducesResponseType(typeof(LeaveBalanceDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveBalanceDetailDto>> GetLeaveBalanceDetail(Guid id)
        {
            var detail = await _leaveService.GetLeaveBalanceDetailAsync(id);
            if (detail == null)
                return NotFound(new { message = "Leave balance not found." });
            return Ok(detail);
        }

        /// <summary>
        /// Get all adjustments for a leave balance
        /// </summary>
        [HttpGet("balances/{balanceId}/adjustments")]
        [ProducesResponseType(typeof(IEnumerable<LeaveAdjustmentDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<LeaveAdjustmentDto>>> GetAdjustments(Guid balanceId)
        {
            var adjustments = await _leaveService.GetAdjustmentsAsync(balanceId);
            return Ok(adjustments);
        }

        /// <summary>
        /// Add a manual adjustment to a leave balance
        /// </summary>
        [HttpPost("balances/{balanceId}/adjustments")]
        [ProducesResponseType(typeof(LeaveAdjustmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveAdjustmentDto>> AddAdjustment(Guid balanceId, [FromBody] CreateLeaveAdjustmentDto dto)
        {
            try
            {
                dto.LeaveBalanceId = balanceId;
                var result = await _leaveService.AddAdjustmentAsync(dto);
                return CreatedAtAction(nameof(GetAdjustments), new { balanceId }, result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete a leave adjustment by ID (also recalculates the cached AdjustmentDays on the balance)
        /// </summary>
        [HttpDelete("adjustments/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAdjustment(Guid id)
        {
            try
            {
                await _leaveService.DeleteAdjustmentAsync(id);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all leave adjustments across all employees with optional filters
        /// </summary>
        [HttpGet("adjustments")]
        [ProducesResponseType(typeof(IEnumerable<LeaveAdjustmentDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<LeaveAdjustmentDto>>> GetAllAdjustments(
            [FromQuery] int year = 0,
            [FromQuery] Guid? employeeId = null,
            [FromQuery] Guid? leaveTypeId = null,
            [FromQuery] string? search = null)
        {
            if (year == 0) year = DateTime.Today.Year;
            var adjustments = await _leaveService.GetAllAdjustmentsAsync(year, employeeId, leaveTypeId, search);
            return Ok(adjustments);
        }

        /// <summary>
        /// Get a single leave adjustment by ID
        /// </summary>
        [HttpGet("adjustments/{id}")]
        [ProducesResponseType(typeof(LeaveAdjustmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveAdjustmentDto>> GetAdjustmentById(Guid id)
        {
            var adjustment = await _leaveService.GetAdjustmentByIdAsync(id);
            if (adjustment == null)
                return NotFound(new { message = "Leave adjustment not found." });
            return Ok(adjustment);
        }

        /// <summary>
        /// Create a standalone leave adjustment (balance looked-up or auto-created; no balance ID required)
        /// </summary>
        [HttpPost("adjustments")]
        [ProducesResponseType(typeof(LeaveAdjustmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveAdjustmentDto>> CreateStandaloneAdjustment([FromBody] CreateLeaveAdjustmentStandaloneDto dto)
        {
            try
            {
                var result = await _leaveService.CreateStandaloneAdjustmentAsync(dto);
                return CreatedAtAction(nameof(GetAdjustmentById), new { id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating standalone leave adjustment");
                return StatusCode(500, "An error occurred while creating the adjustment");
            }
        }

        /// <summary>
        /// Update the days and reason of an existing leave adjustment
        /// </summary>
        [HttpPut("adjustments/{id}")]
        [ProducesResponseType(typeof(LeaveAdjustmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveAdjustmentDto>> UpdateAdjustment(Guid id, [FromBody] UpdateLeaveAdjustmentDto dto)
        {
            try
            {
                var result = await _leaveService.UpdateAdjustmentAsync(id, dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating leave adjustment {AdjustmentId}", id);
                return StatusCode(500, "An error occurred while updating the adjustment");
            }
        }

        /// <summary>
        /// Get pending leave approvals for a manager
        /// </summary>
        /// <param name="managerId">Manager's employee ID</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20)</param>
        /// <returns>Paged list of pending leave applications</returns>
        /// <response code="200">Pending approvals retrieved</response>
        [HttpGet("pending-approvals/{managerId}")]
        [ProducesResponseType(typeof(PagedResult<LeaveRequestDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<LeaveRequestDto>>> GetPendingApprovals(
            Guid managerId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _leaveService.GetPendingApprovalsAsync(managerId, pageNumber, pageSize);

            return Ok(result);
        }

        /// <summary>
        /// Submit a leave request for approval
        /// </summary>
        /// <param name="id">Leave request ID</param>
        /// <returns>Success indicator</returns>
        /// <response code="200">Leave request submitted for approval</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave request not found</response>
        [HttpPost("{id}/submit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SubmitForApproval(Guid id)
        {
            try
            {
                await _leaveService.SubmitForApprovalAsync(id);
                return Ok(new { message = "Leave request submitted for approval." });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting leave request {id} for approval", id);
                return StatusCode(500, new { message = "An error occurred while submitting the leave request." });
            }
        }

        /// <summary>
        /// Approve a leave application
        /// </summary>
        /// <param name="id">Leave application ID</param>
        /// <param name="dto">Approval details</param>
        /// <returns>Updated leave application</returns>
        /// <response code="200">Leave approved successfully</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave application not found</response>
        [HttpPut("{id}/approve")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> ApproveLeave(Guid id, [FromBody] ApproveLeaveDto dto)
        {
            try
            {
                var application = await _leaveService.ApproveLeaveAsync(id, dto);
                return Ok(application);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while approving the leave request");
            }
        }

        /// <summary>
        /// Reject a leave application
        /// </summary>
        /// <param name="id">Leave application ID</param>
        /// <param name="dto">Rejection details</param>
        /// <returns>Updated leave application</returns>
        /// <response code="200">Leave rejected successfully</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave application not found</response>
        [HttpPut("{id}/reject")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> RejectLeave(Guid id, [FromBody] RejectLeaveDto dto)
        {
            try
            {
                var application = await _leaveService.RejectLeaveAsync(id, dto);
                return Ok(application);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while rejecting the leave request");
            }
        }

        /// <summary>
        /// Cancel a leave application
        /// </summary>
        /// <param name="id">Leave application ID</param>
        /// <param name="cancellationReason">Reason for cancellation</param>
        /// <returns>Success status</returns>
        /// <response code="200">Leave cancelled successfully</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave application not found</response>
        [HttpPut("{id}/cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelLeave(Guid id, [FromBody] string cancellationReason)
        {
            try
            {
                await _leaveService.CancelLeaveRequestAsync(id, cancellationReason);
                return Ok(new { message = "Leave application cancelled successfully" });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while cancelling the leave request");
            }
        }

        /// <summary>
        /// Close a completed leave application
        /// </summary>
        /// <param name="id">Leave application ID</param>
        /// <param name="dto">Closure details</param>
        /// <returns>Updated leave application</returns>
        /// <response code="200">Leave closed successfully</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave application not found</response>
        [HttpPut("{id}/close")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> CloseLeave(Guid id, [FromBody] CloseLeaveDto dto)
        {
            try
            {
                var application = await _leaveService.CloseLeaveRequestAsync(id, dto);
                return Ok(application);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while closing the leave request");
            }
        }
        /// <summary>
        /// Recalculate leave balance(s) from source data (admin operation).
        /// If LeaveTypeId is provided only that type is recalculated; otherwise all leave types for the employee are recalculated.
        /// </summary>
        [HttpPost("balances/recalculate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RecalculateBalance([FromBody] RecalculateLeaveBalanceRequest request)
        {
            if (request.EmployeeId == Guid.Empty)
                return BadRequest(new { message = "EmployeeId is required." });

            try
            {
                if (request.LeaveTypeId.HasValue)
                    await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId.Value, request.Year);
                else
                    await _recalculationService.RecalculateAllAsync(request.EmployeeId, request.Year);

                return Ok(new { message = "Leave balance recalculation completed." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating leave balance for employee {EmployeeId}", request.EmployeeId);
                return StatusCode(500, "An error occurred during leave balance recalculation");
            }
        }

        // ─── Attachments ──────────────────────────────────────────────────────

        [HttpGet("{id:guid}/attachments")]
        [ProducesResponseType(typeof(IEnumerable<LeaveRequestAttachmentDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<LeaveRequestAttachmentDto>>> GetAttachments(Guid id)
        {
            try
            {
                var attachments = await _leaveService.GetAttachmentsAsync(id);
                return Ok(attachments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching attachments for leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while fetching attachments");
            }
        }

        [HttpPost("{id:guid}/attachments")]
        [ProducesResponseType(typeof(LeaveRequestAttachmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided" });

            var uploadedById = _currentUserService.EmployeeId ?? Guid.Empty;
            if (uploadedById == Guid.Empty)
                return Unauthorized(new { message = "User employee context not found" });

            if (_currentUserService.TenantId is not Guid tenantId ||
                !Guid.TryParse(_currentUserService.UserId, out var actorUserId))
                return Unauthorized(new { message = "User context could not be resolved" });

            // Confirms the leave request exists and belongs to this tenant before anything is
            // stored, and gives the DMS registration a source record that definitely exists.
            try
            {
                await _leaveService.GetAttachmentsAsync(id);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }

            HrControlledDocument document;
            try
            {
                document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
                {
                    TenantId = tenantId,
                    ActorUserId = actorUserId,
                    ActorName = _currentUserService.UserName,
                    Category = ControlledFileUploadCategories.HrLeaveAttachments,
                    File = file,
                    Registration = new HrDocumentDmsRegistration
                    {
                        SourceLabel = "Leave request attachment",
                        SourceEntityType = "LeaveRequest",
                        SourceRecordId = id,
                        Title = Path.GetFileName(file.FileName),
                        DocumentType = "LeaveAttachment",
                        ChangeSummary = "Uploaded through the leave request screen."
                    }
                }, ct);
            }
            catch (ControlledFileUploadException ex)
            {
                return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
            }

            try
            {
                var dto = await _leaveService.UploadAttachmentAsync(
                    id, uploadedById, document.OriginalFileName, string.Empty,
                    document.ContentType, document.FileSize,
                    document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId);

                return Ok(dto);
            }
            catch (Exception ex)
            {
                // The file is stored and catalogued but nothing references it — take it back out.
                await _hrDocuments.RollbackAsync(document, tenantId, actorUserId, ct);

                if (ex is ArgumentException)
                    return NotFound(new { message = ex.Message });

                _logger.LogError(ex, "Error uploading attachment for leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while uploading the attachment");
            }
        }

        /// <summary>
        /// Streams a leave attachment to a caller entitled to see it.
        /// </summary>
        /// <remarks>
        /// Attachments are frequently medical certificates, so they are stored privately and
        /// are only reachable here. Neither this endpoint's helper nor the DMS performs the
        /// entitlement check — that is the ownership test below.
        /// </remarks>
        [HttpGet("attachments/{attachmentId:guid}/download")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken ct = default)
        {
            if (_currentUserService.TenantId is not Guid tenantId)
                return Unauthorized(new { message = "Tenant context could not be resolved" });

            var attachment = await _db.Set<Core.Entities.HR.StaffLeave.LeaveRequestAttachment>()
                .AsNoTracking()
                .Include(item => item.LeaveRequest)
                .SingleOrDefaultAsync(
                    item => item.Id == attachmentId && item.TenantId == tenantId && !item.IsDeleted,
                    ct);
            if (attachment is null)
                return NotFound(new { message = "Attachment not found" });

            var isOwner = _currentUserService.EmployeeId is Guid employeeId &&
                          attachment.LeaveRequest.EmployeeId == employeeId;
            var isHr = _currentUserService.IsInRole("HR") ||
                       _currentUserService.IsInRole("Admin") ||
                       _currentUserService.IsInRole("SuperAdmin");
            if (!isOwner && !isHr)
                return Forbid();

            return await HrDocumentDownload.ServeAsync(
                this, _centralDocuments, _fileStorageService, _db, tenantId,
                attachment.DocumentRecordId, attachment.DocumentVersionId,
                attachment.FileUploadRecordId, attachment.FilePath,
                attachment.FileName, attachment.ContentType,
                inline: false, ct);
        }

        [HttpDelete("attachments/{attachmentId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAttachment(Guid attachmentId, CancellationToken ct = default)
        {
            try
            {
                var attachment = await _leaveService.GetAttachmentByIdAsync(attachmentId);
                if (attachment == null)
                    return NotFound(new { message = "Attachment not found" });

                // Controlled uploads and their DMS records are removed through the shared
                // boundary, which soft-deletes and schedules the physical delete. Only
                // pre-migration rows still carry a raw storage path to remove directly.
                var stored = await _db.Set<Core.Entities.HR.StaffLeave.LeaveRequestAttachment>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == attachmentId, ct);

                if (stored is not null &&
                    _currentUserService.TenantId is Guid tenantId &&
                    Guid.TryParse(_currentUserService.UserId, out var actorUserId) &&
                    stored.FileUploadRecordId is Guid uploadId)
                {
                    await _hrDocuments.RollbackAsync(
                        new HrControlledDocument
                        {
                            FileUploadRecordId = uploadId,
                            FilePath = stored.FilePath,
                            OriginalFileName = stored.FileName,
                            ContentType = stored.ContentType ?? "application/octet-stream",
                            FileSize = stored.FileSizeBytes ?? 0,
                            DocumentRecordId = stored.DocumentRecordId,
                            DocumentVersionId = stored.DocumentVersionId
                        },
                        tenantId, actorUserId, ct);
                }
                else if (!string.IsNullOrWhiteSpace(attachment.FilePath))
                {
                    try { await _fileStorageService.DeleteFileAsync(attachment.FilePath); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Could not delete file {FilePath}", attachment.FilePath); }
                }

                await _leaveService.DeleteAttachmentAsync(attachmentId);
                return Ok(new { message = "Attachment deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attachment {AttachmentId}", attachmentId);
                return StatusCode(500, "An error occurred while deleting the attachment");
            }
        }
    }
}
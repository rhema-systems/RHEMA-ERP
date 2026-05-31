using ErpSystem.Core.Interfaces; // For IGeneralLedgerService
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages journal entries for posting transactions to the General Ledger.
    /// </summary>
    /// <remarks>
    /// This controller is the primary integration endpoint for all ERP modules to post financial transactions.
    /// All modules (Sales, Purchasing, Inventory, Payroll, Fixed Assets) use this controller to create
    /// journal entries that update account balances in the General Ledger.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/finance/journal-entries")]
    public class JournalEntryController : ControllerBase
    {
        private readonly IJournalEntryService _journalEntryService;
        private readonly IGeneralLedgerService _generalLedgerService;
        private readonly IWorkflowService _workflowService;
        private readonly ICurrentUserService _currentUserService;

        public JournalEntryController(
            IJournalEntryService journalEntryService,
            IGeneralLedgerService generalLedgerService,
            IWorkflowService workflowService,
            ICurrentUserService currentUserService)
        {
            _journalEntryService = journalEntryService;
            _generalLedgerService = generalLedgerService;
            _workflowService = workflowService;
            _currentUserService = currentUserService;
        }

        // ====================================================================
        // CORE JOURNAL ENTRY ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Generates the next sequential journal entry number.
        /// </summary>
        [HttpGet("next-number")]
        public async Task<ActionResult<string>> GetNextJournalNumber()
        {
            try
            {
                var number = await _generalLedgerService.GenerateJournalEntryNumberAsync();
                return Ok(new { number });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves journal entries with optional filtering by status, date range, and fiscal period.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<JournalEntryDto>>> GetJournalEntries(
            [FromQuery] string? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? periodId = null)
        {
            try
            {
                var entries = await _journalEntryService.GetJournalEntriesAsync();
                return Ok(entries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific journal entry by ID including all transaction lines.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<JournalEntryDto>> GetJournalEntryById(Guid id)
        {
            try
            {
                var entry = await _journalEntryService.GetJournalEntryByIdAsync(id);
                if (entry == null)
                    return NotFound($"Journal entry with ID {id} not found");

                return Ok(entry);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new journal entry to post transactions to the General Ledger.
        /// This is the primary integration endpoint for all ERP modules.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<JournalEntryDto>> CreateJournalEntry([FromBody] CreateJournalEntryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var entry = await _journalEntryService.CreateJournalEntryAsync(dto);
                return CreatedAtAction(nameof(GetJournalEntryById), new { id = entry.Id }, entry);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates a draft journal entry before it is posted.
        /// Can only update entries with status="Draft".
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<JournalEntryDto>> UpdateJournalEntry(Guid id, [FromBody] UpdateJournalEntryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var entry = await _journalEntryService.UpdateJournalEntryAsync(id, dto);
                return Ok(entry);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a draft journal entry. Can only delete entries with status="Draft".
        /// For posted entries, use the reverse endpoint instead.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteJournalEntry(Guid id)
        {
            try
            {
                await _journalEntryService.DeleteJournalEntryAsync(id);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Posts a draft or approved journal entry to the General Ledger.
        /// Entry must be balanced and fiscal period must be open.
        /// </summary>
        [HttpPost("{id}/post")]
        public async Task<ActionResult> PostJournalEntry(Guid id)
        {
            try
            {
                await _journalEntryService.PostJournalEntryAsync(id);
                return Ok(new { message = "Journal entry posted successfully" });
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reverses a posted journal entry by creating an offsetting entry with opposite debits/credits.
        /// Can only reverse posted entries that haven't already been reversed.
        /// </summary>
        [HttpPost("{id}/reverse")]
        public async Task<ActionResult<JournalEntryDto>> ReverseJournalEntry(Guid id)
        {
            try
            {
                var reversedEntry = await _journalEntryService.ReverseJournalEntryAsync(id, "Manual reversal");
                return Ok(reversedEntry);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // ====================================================================
        // APPROVAL WORKFLOW ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Submits a draft journal entry for approval. Transitions from "Draft" to "Pending Approval".
        /// </summary>
        [HttpPost("{id}/request-approval")]
        public async Task<ActionResult<JournalEntryDto>> RequestApproval(Guid id)
        {
            try
            {
                var entry = await _journalEntryService.GetJournalEntryByIdAsync(id);
                if (entry == null)
                    return NotFound($"Journal entry with ID {id} not found");

                if (entry.PostingStatus != "Draft")
                    return BadRequest($"Only draft journal entries can be submitted for approval. Current status: {entry.PostingStatus}");

                // Trigger workflow stub
                await _workflowService.StartApprovalWorkflowAsync("JournalEntry", id);

                // Update the entry status
                await _journalEntryService.UpdateApprovalStatusAsync(id, "Pending Approval", "Pending");

                var updated = await _journalEntryService.GetJournalEntryByIdAsync(id);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Approves a journal entry that is pending approval. Transitions to "Approved".
        /// Entry can then be posted via the POST endpoint.
        /// </summary>
        [HttpPost("{id}/approve")]
        public async Task<ActionResult<JournalEntryDto>> ApproveJournalEntry(Guid id, [FromBody] ApprovalActionDto? request = null)
        {
            try
            {
                var entry = await _journalEntryService.GetJournalEntryByIdAsync(id);
                if (entry == null)
                    return NotFound($"Journal entry with ID {id} not found");

                if (entry.PostingStatus != "Pending Approval")
                    return BadRequest($"Only entries pending approval can be approved. Current status: {entry.PostingStatus}");

                Guid.TryParse(_currentUserService.UserId, out var userId);

                // Process through workflow stub
                await _workflowService.ProcessApprovalStepAsync("JournalEntry", id, userId, "Approve", request?.Comments);

                // Update the entry
                await _journalEntryService.UpdateApprovalStatusAsync(id, "Approved", "Approved", userId);

                var updated = await _journalEntryService.GetJournalEntryByIdAsync(id);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Rejects a journal entry that is pending approval. A rejection reason is required.
        /// Entry can be edited and resubmitted after correction.
        /// </summary>
        [HttpPost("{id}/reject")]
        public async Task<ActionResult<JournalEntryDto>> RejectJournalEntry(Guid id, [FromBody] ApprovalActionDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Reason))
                    return BadRequest("A rejection reason is required.");

                var entry = await _journalEntryService.GetJournalEntryByIdAsync(id);
                if (entry == null)
                    return NotFound($"Journal entry with ID {id} not found");

                if (entry.PostingStatus != "Pending Approval")
                    return BadRequest($"Only entries pending approval can be rejected. Current status: {entry.PostingStatus}");

                Guid.TryParse(_currentUserService.UserId, out var userId);

                // Process through workflow stub
                await _workflowService.ProcessApprovalStepAsync("JournalEntry", id, userId, "Reject", request.Reason);

                // Update the entry
                await _journalEntryService.UpdateApprovalStatusAsync(id, "Rejected", "Rejected", rejectionReason: request.Reason);

                var updated = await _journalEntryService.GetJournalEntryByIdAsync(id);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Links an uploaded file to a journal entry.
        /// </summary>
        [HttpPost("{id}/attachments/{fileUploadRecordId}")]
        public async Task<IActionResult> LinkAttachment(Guid id, Guid fileUploadRecordId)
        {
            try
            {
                await _journalEntryService.LinkAttachmentAsync(id, fileUploadRecordId);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Unlinks an attachment from a journal entry.
        /// </summary>
        [HttpDelete("{id}/attachments/{fileUploadRecordId}")]
        public async Task<IActionResult> UnlinkAttachment(Guid id, Guid fileUploadRecordId)
        {
            try
            {
                await _journalEntryService.UnlinkAttachmentAsync(id, fileUploadRecordId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets attachment metadata linked to a journal entry.
        /// </summary>
        [HttpGet("{id}/attachments")]
        public async Task<ActionResult<IReadOnlyList<JournalEntryAttachmentDto>>> GetAttachments(Guid id)
        {
            try
            {
                var attachments = await _journalEntryService.GetAttachmentsAsync(id);
                return Ok(attachments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}

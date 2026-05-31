using ErpSystem.Core.Interfaces; // For IGeneralLedgerService
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using System.Security.Claims;

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
        private static readonly string[] PrivilegedRoles = ["admin", "superadmin", "tenantadmin"];
        private static readonly string[] PermissionClaimTypes = ["permission", "permissions", ClaimTypes.Role];
        private static readonly Dictionary<string, string[]> ActionPermissions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Edit"] = ["Finance.JournalEntries.Edit", "Finance.JournalEntries.Write"],
            ["Delete"] = ["Finance.JournalEntries.Delete", "Finance.JournalEntries.Write"],
            ["Post"] = ["Finance.JournalEntries.Post"],
            ["Reverse"] = ["Finance.JournalEntries.Reverse"],
            ["SubmitForApproval"] = ["Finance.JournalEntries.SubmitForApproval", "Finance.JournalEntries.Approve"],
            ["Approve"] = ["Finance.JournalEntries.Approve"],
            ["Reject"] = ["Finance.JournalEntries.Approve"],
            ["Attach"] = ["Finance.JournalEntries.Edit", "Finance.JournalEntries.Write"]
        };

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

        private bool CurrentUserHasAnyPermission(params string[] permissions)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var roles = User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value?.Trim().ToLowerInvariant())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet();

            if (roles.Overlaps(PrivilegedRoles))
            {
                return true;
            }

            var granted = User.Claims
                .Where(c => PermissionClaimTypes.Contains(c.Type, StringComparer.OrdinalIgnoreCase))
                .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(v => v.Trim().ToLowerInvariant())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet();

            if (granted.Contains("*"))
            {
                return true;
            }

            return permissions
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim().ToLowerInvariant())
                .Any(granted.Contains);
        }

        private ActionResult? EnsureActionPermission(string actionName)
        {
            if (!ActionPermissions.TryGetValue(actionName, out var permissions))
            {
                return null;
            }

            if (CurrentUserHasAnyPermission(permissions))
            {
                return null;
            }

            return StatusCode(StatusCodes.Status403Forbidden, $"You do not have permission to {actionName.ToLowerInvariant()} journal entries.");
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
            var permissionCheck = EnsureActionPermission("Edit");
            if (permissionCheck != null) return permissionCheck;

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
            var permissionCheck = EnsureActionPermission("Delete");
            if (permissionCheck != null) return permissionCheck;

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
            var permissionCheck = EnsureActionPermission("Post");
            if (permissionCheck != null) return permissionCheck;

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
            var permissionCheck = EnsureActionPermission("Reverse");
            if (permissionCheck != null) return permissionCheck;

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
        // ATTACHMENT ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Links an uploaded file to a journal entry.
        /// </summary>
        [HttpPost("{id}/attachments/{fileId}")]
        public async Task<ActionResult> LinkAttachment(Guid id, Guid fileId)
        {
            var permissionCheck = EnsureActionPermission("Attach");
            if (permissionCheck != null) return permissionCheck;

            try
            {
                await _journalEntryService.LinkAttachmentAsync(id, fileId);
                return Ok(new { message = "Attachment linked successfully" });
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
        [HttpDelete("{id}/attachments/{fileId}")]
        public async Task<ActionResult> UnlinkAttachment(Guid id, Guid fileId)
        {
            var permissionCheck = EnsureActionPermission("Attach");
            if (permissionCheck != null) return permissionCheck;

            try
            {
                await _journalEntryService.UnlinkAttachmentAsync(id, fileId);
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
        /// Gets full attachment metadata linked to a journal entry.
        /// </summary>
        [HttpGet("{id}/attachments")]
        public async Task<ActionResult<IReadOnlyList<JournalEntryAttachmentDto>>> GetAttachments(Guid id)
        {
            try
            {
                var attachments = await _journalEntryService.GetAttachmentsAsync(id);
                return Ok(attachments);
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
            var permissionCheck = EnsureActionPermission("SubmitForApproval");
            if (permissionCheck != null) return permissionCheck;

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
            var permissionCheck = EnsureActionPermission("Approve");
            if (permissionCheck != null) return permissionCheck;

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
            var permissionCheck = EnsureActionPermission("Reject");
            if (permissionCheck != null) return permissionCheck;

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
    }
}

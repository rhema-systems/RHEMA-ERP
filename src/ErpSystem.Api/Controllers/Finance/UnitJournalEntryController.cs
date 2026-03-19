using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Controller for managing Unit Journal Entries.
    /// </summary>
    /// <remarks>
    /// Handles creation, workflow (submit, approve, reject), posting, and reversal.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/finance/unit-journal-entries")]
    public class UnitJournalEntryController : ControllerBase
    {
        private readonly IUnitJournalEntryService _unitJournalEntryService;

        public UnitJournalEntryController(IUnitJournalEntryService unitJournalEntryService)
        {
            _unitJournalEntryService = unitJournalEntryService;
        }

        /// <summary>
        /// Retrieves the next available unit journal entry number.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Pre-populating the entry number field when creating a new unit journal entry
        /// - Displaying the next sequential number in the UI before the user saves
        ///
        /// **Integration Pattern:**
        /// - Call this endpoint before presenting the "Create Entry" form to the user
        /// - The generated number is reserved but not persisted until the entry is created
        ///
        /// **Business Rules:**
        /// - Entry numbers follow a sequential pattern defined by system configuration
        /// - Numbers are unique across all unit journal entries
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>An object containing the next available entry number string</returns>
        /// <response code="200">Returns the next available entry number</response>
        /// <response code="500">Internal server error occurred while generating the number</response>
        [HttpGet("next-number")]
        public async Task<ActionResult<string>> GetNextEntryNumber()
        {
            try
            {
                var number = await _unitJournalEntryService.GenerateEntryNumberAsync();
                return Ok(new { number });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves all unit journal entries.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating the main journal entries list/grid view
        /// - Exporting all entries for reporting purposes
        ///
        /// **Integration Pattern:**
        /// - Use this endpoint for an unfiltered listing of all entries
        /// - For filtered results, use the `GET /filter` endpoint instead
        ///
        /// **Business Rules:**
        /// - Returns entries in all statuses (Draft, Submitted, Approved, Posted, Rejected, Reversed)
        /// - Results include summary-level data; use the `GET /{id}` endpoint for full detail with lines
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>A list of all unit journal entry summary DTOs</returns>
        /// <response code="200">Returns the list of all unit journal entries</response>
        /// <response code="500">Internal server error occurred while retrieving entries</response>
        [HttpGet]
        public async Task<ActionResult<List<UnitJournalEntryDto>>> GetUnitJournalEntries()
        {
            try
            {
                var entries = await _unitJournalEntryService.GetAllAsync();
                return Ok(entries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves unit journal entries with filters.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Searching entries by status (e.g., only Draft or Posted entries)
        /// - Filtering entries within a specific date range for period-end review
        /// - Retrieving entries tied to a particular fiscal period
        ///
        /// **Integration Pattern:**
        /// - All filter parameters are optional and can be combined
        /// - Omitting all filters returns the same result as `GET /api/finance/unit-journal-entries`
        ///
        /// **Business Rules:**
        /// - Valid status values include: Draft, Submitted, Approved, Posted, Rejected, Reversed
        /// - Date filters apply to the entry date, not the created or modified date
        /// - Fiscal period filter restricts results to entries associated with that period
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="status">Optional. Filter by entry status (e.g., Draft, Submitted, Approved, Posted, Rejected, Reversed)</param>
        /// <param name="startDate">Optional. Filter entries with an entry date on or after this date</param>
        /// <param name="endDate">Optional. Filter entries with an entry date on or before this date</param>
        /// <param name="fiscalPeriodId">Optional. Filter entries by the associated fiscal period ID</param>
        /// <returns>A filtered list of unit journal entry summary DTOs</returns>
        /// <response code="200">Returns the filtered list of unit journal entries</response>
        /// <response code="500">Internal server error occurred while retrieving filtered entries</response>
        [HttpGet("filter")]
        public async Task<ActionResult<List<UnitJournalEntryDto>>> GetFilteredEntries(
            [FromQuery] string? status,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] Guid? fiscalPeriodId)
        {
            try
            {
                var filters = new UnitJournalEntryFilters
                {
                    Status = status,
                    StartDate = startDate,
                    EndDate = endDate,
                    FiscalPeriodId = fiscalPeriodId
                };
                var entries = await _unitJournalEntryService.GetFilteredAsync(filters);
                return Ok(entries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves entries pending approval.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating an approval queue or dashboard for approvers
        /// - Displaying a count or list of items awaiting action
        ///
        /// **Integration Pattern:**
        /// - Typically called on the approver's dashboard to show actionable items
        /// - Each returned entry can then be approved or rejected via the workflow endpoints
        ///
        /// **Business Rules:**
        /// - Only returns entries with a "Submitted" status that are awaiting approval
        /// - Entries in Draft, Approved, Posted, Rejected, or Reversed status are excluded
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>A list of unit journal entries that are pending approval</returns>
        /// <response code="200">Returns the list of entries pending approval</response>
        /// <response code="500">Internal server error occurred while retrieving pending entries</response>
        [HttpGet("pending-approval")]
        public async Task<ActionResult<List<UnitJournalEntryDto>>> GetPendingApproval()
        {
            try
            {
                var entries = await _unitJournalEntryService.GetPendingApprovalAsync();
                return Ok(entries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific unit journal entry with lines.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Viewing the full detail of a journal entry including all debit/credit lines
        /// - Loading entry data for editing or review before approval
        ///
        /// **Integration Pattern:**
        /// - Use this endpoint when navigating to a detail/edit view for a specific entry
        /// - Returns the complete entry with all journal lines, unlike the list endpoints
        ///
        /// **Business Rules:**
        /// - Returns the entry regardless of its current status
        /// - Includes all associated journal lines with account, debit, and credit details
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the unit journal entry to retrieve</param>
        /// <returns>The full unit journal entry detail including all journal lines</returns>
        /// <response code="200">Returns the unit journal entry with its lines</response>
        /// <response code="404">No unit journal entry found with the specified ID</response>
        /// <response code="500">Internal server error occurred while retrieving the entry</response>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UnitJournalEntryDetailDto>> GetUnitJournalEntryById(Guid id)
        {
            try
            {
                var entry = await _unitJournalEntryService.GetByIdAsync(id);
                if (entry == null)
                    return NotFound($"Unit journal entry with ID {id} not found");

                return Ok(entry);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a unit journal entry by entry number.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Looking up a journal entry by its human-readable entry number rather than GUID
        /// - Quick search functionality where users type an entry number
        ///
        /// **Integration Pattern:**
        /// - Use this endpoint when the user searches by entry number string
        /// - For GUID-based lookups, use the `GET /{id}` endpoint instead
        ///
        /// **Business Rules:**
        /// - Entry numbers are unique identifiers assigned sequentially
        /// - The search is exact-match on the entry number string
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="entryNumber">The entry number string to search for (e.g., "UJE-000001")</param>
        /// <returns>The matching unit journal entry summary DTO</returns>
        /// <response code="200">Returns the unit journal entry matching the entry number</response>
        /// <response code="404">No unit journal entry found with the specified entry number</response>
        /// <response code="500">Internal server error occurred while retrieving the entry</response>
        [HttpGet("number/{entryNumber}")]
        public async Task<ActionResult<UnitJournalEntryDto>> GetByEntryNumber(string entryNumber)
        {
            try
            {
                var entry = await _unitJournalEntryService.GetByEntryNumberAsync(entryNumber);
                if (entry == null)
                    return NotFound($"Unit journal entry with number {entryNumber} not found");

                return Ok(entry);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new unit journal entry in Draft status.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Creating a new journal entry with header information and journal lines
        /// - Saving a draft entry that can be edited before submission for approval
        ///
        /// **Integration Pattern:**
        /// - Call `GET /next-number` first to obtain an entry number, then call this endpoint
        /// - The created entry is returned with its generated ID for subsequent operations
        /// - Use the `PUT /{id}` endpoint to modify the draft before submitting
        ///
        /// **Business Rules:**
        /// - New entries are always created in Draft status
        /// - Total debits must equal total credits for all journal lines
        /// - At least one journal line is required
        /// - The fiscal period must be open to accept new entries
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="dto">The unit journal entry creation payload containing header and line details</param>
        /// <returns>The newly created unit journal entry with its assigned ID and entry number</returns>
        /// <response code="201">Entry created successfully; returns the new entry</response>
        /// <response code="400">Validation failed (e.g., unbalanced debits/credits, invalid data, or closed fiscal period)</response>
        /// <response code="500">Internal server error occurred while creating the entry</response>
        [HttpPost]
        public async Task<ActionResult<UnitJournalEntryDto>> CreateUnitJournalEntry([FromBody] CreateUnitJournalEntryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var entry = await _unitJournalEntryService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetUnitJournalEntryById), new { id = entry.Id }, entry);
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
        /// Updates a draft unit journal entry.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Modifying header details or journal lines on an existing draft entry
        /// - Correcting errors before submitting the entry for approval
        ///
        /// **Integration Pattern:**
        /// - Fetch the current entry with `GET /{id}`, modify the needed fields, and send the update
        /// - Only entries in Draft status can be updated
        ///
        /// **Business Rules:**
        /// - Only entries in Draft status can be updated; other statuses will be rejected
        /// - Total debits must still equal total credits after the update
        /// - The fiscal period must remain open
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the draft unit journal entry to update</param>
        /// <param name="dto">The updated unit journal entry payload with modified header and/or line details</param>
        /// <returns>The updated unit journal entry</returns>
        /// <response code="200">Entry updated successfully; returns the updated entry</response>
        /// <response code="400">Validation failed or entry is not in Draft status</response>
        /// <response code="404">No unit journal entry found with the specified ID</response>
        /// <response code="500">Internal server error occurred while updating the entry</response>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<UnitJournalEntryDto>> UpdateUnitJournalEntry(Guid id, [FromBody] UpdateUnitJournalEntryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var entry = await _unitJournalEntryService.UpdateAsync(id, dto);
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
        /// Deletes a draft unit journal entry.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing an unwanted or duplicate draft entry that is no longer needed
        /// - Cleaning up test or erroneous entries before they enter the approval workflow
        ///
        /// **Integration Pattern:**
        /// - Call this endpoint to permanently remove a draft entry
        /// - Returns 204 No Content on success with no response body
        ///
        /// **Business Rules:**
        /// - Only entries in Draft status can be deleted
        /// - Submitted, Approved, Posted, Rejected, and Reversed entries cannot be deleted
        /// - Deletion is permanent and cannot be undone
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the draft unit journal entry to delete</param>
        /// <returns>No content on successful deletion</returns>
        /// <response code="204">Entry deleted successfully</response>
        /// <response code="400">Entry is not in Draft status and cannot be deleted</response>
        /// <response code="404">No unit journal entry found with the specified ID</response>
        /// <response code="500">Internal server error occurred while deleting the entry</response>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteUnitJournalEntry(Guid id)
        {
            try
            {
                await _unitJournalEntryService.DeleteAsync(id);
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

        // ===== WORKFLOW ENDPOINTS =====

        /// <summary>
        /// Submits a draft entry for approval.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Moving a completed draft entry into the approval workflow
        /// - Triggering notifications to approvers that a new entry is awaiting review
        ///
        /// **Integration Pattern:**
        /// - Call this after creating and finalizing a draft entry
        /// - The entry status transitions from Draft to Submitted
        /// - After submission, the entry appears in the `GET /pending-approval` results
        ///
        /// **Business Rules:**
        /// - Only entries in Draft status can be submitted
        /// - The entry must pass validation (balanced debits/credits) before submission
        /// - Once submitted, the entry can no longer be edited; it must be rejected back to Draft first
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the draft entry to submit for approval</param>
        /// <returns>The updated unit journal entry with Submitted status</returns>
        /// <response code="200">Entry submitted successfully; returns the updated entry</response>
        /// <response code="400">Entry is not in Draft status or failed validation</response>
        /// <response code="404">No unit journal entry found with the specified ID</response>
        /// <response code="500">Internal server error occurred while submitting the entry</response>
        [HttpPost("{id:guid}/submit")]
        public async Task<ActionResult<UnitJournalEntryDto>> SubmitForApproval(Guid id)
        {
            try
            {
                var entry = await _unitJournalEntryService.SubmitForApprovalAsync(id);
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
        /// Approves a pending entry.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Approving a submitted journal entry as part of the review workflow
        /// - Advancing an entry so it can be posted to the general ledger
        ///
        /// **Integration Pattern:**
        /// - Retrieve pending entries via `GET /pending-approval`, then approve individually
        /// - The entry status transitions from Submitted to Approved
        /// - After approval, the entry can be posted using the `POST /{id}/post` endpoint
        ///
        /// **Business Rules:**
        /// - Only entries in Submitted status can be approved
        /// - The approver should typically be a different user from the creator
        /// - Approved entries cannot be edited; they must be reversed after posting if corrections are needed
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the submitted entry to approve</param>
        /// <returns>The updated unit journal entry with Approved status</returns>
        /// <response code="200">Entry approved successfully; returns the updated entry</response>
        /// <response code="400">Entry is not in Submitted status</response>
        /// <response code="404">No unit journal entry found with the specified ID</response>
        /// <response code="500">Internal server error occurred while approving the entry</response>
        [HttpPost("{id:guid}/approve")]
        public async Task<ActionResult<UnitJournalEntryDto>> Approve(Guid id)
        {
            try
            {
                var entry = await _unitJournalEntryService.ApproveAsync(id);
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
        /// Rejects a pending entry.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Sending a submitted entry back to the creator with a rejection reason
        /// - Declining entries that have errors, missing information, or policy violations
        ///
        /// **Integration Pattern:**
        /// - Retrieve pending entries via `GET /pending-approval`, then reject with a reason
        /// - The entry status transitions from Submitted back to Rejected
        /// - The creator can review the rejection reason and make corrections
        ///
        /// **Business Rules:**
        /// - Only entries in Submitted status can be rejected
        /// - A rejection reason is required to inform the creator why the entry was declined
        /// - Rejected entries may need to be revised and resubmitted
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the submitted entry to reject</param>
        /// <param name="dto">The rejection payload containing the reason for rejection</param>
        /// <returns>The updated unit journal entry with Rejected status</returns>
        /// <response code="200">Entry rejected successfully; returns the updated entry</response>
        /// <response code="400">Entry is not in Submitted status</response>
        /// <response code="404">No unit journal entry found with the specified ID</response>
        /// <response code="500">Internal server error occurred while rejecting the entry</response>
        [HttpPost("{id:guid}/reject")]
        public async Task<ActionResult<UnitJournalEntryDto>> Reject(Guid id, [FromBody] RejectEntryDto dto)
        {
            try
            {
                var entry = await _unitJournalEntryService.RejectAsync(id, dto.Reason);
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
        /// Posts an approved entry, updating account balances.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Finalizing an approved journal entry by posting it to the general ledger
        /// - Updating account balances with the debit and credit amounts from the entry
        ///
        /// **Integration Pattern:**
        /// - Call this endpoint after an entry has been approved via `POST /{id}/approve`
        /// - The entry status transitions from Approved to Posted
        /// - After posting, the entry is reflected in account balances and financial reports
        ///
        /// **Business Rules:**
        /// - Only entries in Approved status can be posted
        /// - Posting updates the balances of all accounts referenced in the journal lines
        /// - Posted entries cannot be edited; use the `POST /{id}/reverse` endpoint to correct errors
        /// - The fiscal period must be open at the time of posting
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the approved entry to post</param>
        /// <returns>The updated unit journal entry with Posted status</returns>
        /// <response code="200">Entry posted successfully; returns the updated entry</response>
        /// <response code="400">Entry is not in Approved status or the fiscal period is closed</response>
        /// <response code="404">No unit journal entry found with the specified ID</response>
        /// <response code="500">Internal server error occurred while posting the entry</response>
        [HttpPost("{id:guid}/post")]
        public async Task<ActionResult<UnitJournalEntryDto>> Post(Guid id)
        {
            try
            {
                var entry = await _unitJournalEntryService.PostAsync(id);
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
        /// Reverses a posted entry.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Correcting errors discovered after a journal entry has been posted
        /// - Undoing the financial impact of a posted entry by creating an offsetting reversal entry
        ///
        /// **Integration Pattern:**
        /// - Call this endpoint on a posted entry to create a reversal
        /// - A new reversal entry is automatically created with opposite debit/credit amounts
        /// - The original entry status transitions to Reversed
        ///
        /// **Business Rules:**
        /// - Only entries in Posted status can be reversed
        /// - A reversal reason is required for audit trail purposes
        /// - Reversal creates a new journal entry that offsets the original amounts
        /// - Both the original and reversal entries are linked for traceability
        /// - The fiscal period must be open for the reversal to be accepted
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the posted entry to reverse</param>
        /// <param name="dto">The reversal payload containing the reason for reversal</param>
        /// <returns>The newly created reversal unit journal entry</returns>
        /// <response code="200">Entry reversed successfully; returns the new reversal entry</response>
        /// <response code="400">Entry is not in Posted status or the fiscal period is closed</response>
        /// <response code="404">No unit journal entry found with the specified ID</response>
        /// <response code="500">Internal server error occurred while reversing the entry</response>
        [HttpPost("{id:guid}/reverse")]
        public async Task<ActionResult<UnitJournalEntryDto>> Reverse(Guid id, [FromBody] ReverseEntryDto dto)
        {
            try
            {
                var reversalEntry = await _unitJournalEntryService.ReverseAsync(id, dto.Reason);
                return Ok(reversalEntry);
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
        /// Validates an entry before submission.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Pre-checking an entry for errors before submitting it for approval
        /// - Providing real-time validation feedback in the UI as the user finalizes the entry
        ///
        /// **Integration Pattern:**
        /// - Call this endpoint before calling `POST /{id}/submit` to check for issues
        /// - Returns a boolean indicating whether the entry passes all validation rules
        /// - Can be used to enable/disable the "Submit" button in the UI
        ///
        /// **Business Rules:**
        /// - Validates that total debits equal total credits
        /// - Checks that all referenced accounts exist and are active
        /// - Verifies the fiscal period is open and valid
        /// - Does not change the entry status; this is a read-only validation check
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the unit journal entry to validate</param>
        /// <returns>A boolean indicating whether the entry is valid for submission</returns>
        /// <response code="200">Validation completed; returns true if valid, false otherwise</response>
        /// <response code="500">Internal server error occurred while validating the entry</response>
        [HttpGet("{id:guid}/validate")]
        public async Task<ActionResult<bool>> Validate(Guid id)
        {
            try
            {
                var isValid = await _unitJournalEntryService.ValidateEntryAsync(id);
                return Ok(isValid);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

    // DTOs for workflow actions
    public class RejectEntryDto
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class ReverseEntryDto
    {
        public string Reason { get; set; } = string.Empty;
    }
}

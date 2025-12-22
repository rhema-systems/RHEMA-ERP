using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Controller for managing Unit Journal Entries.
    /// Handles creation, workflow (submit, approve, reject), posting, and reversal.
    /// </summary>
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
        /// Retrieves all unit journal entries.
        /// </summary>
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

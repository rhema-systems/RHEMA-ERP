using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Production recurring-journal workspace for FR-GL-006. Existing journal
/// permissions are deliberately reused so Finance administrators do not need a
/// second, parallel authorization model for a specialized journal lifecycle.
/// </summary>
[Authorize]
[ApiController]
[Route("api/finance/recurring-journals")]
public sealed class RecurringJournalController : ControllerBase
{
    private readonly IRecurringJournalService _service;

    public RecurringJournalController(IRecurringJournalService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<RecurringJournalTemplateDto>>> GetAll(
        CancellationToken cancellationToken) => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<RecurringJournalTemplateDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = FinancePermissions.CreateJournalEntries)]
    public async Task<ActionResult<RecurringJournalTemplateDto>> Create(
        [FromBody] CreateRecurringJournalTemplateDto request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = FinancePermissions.EditJournalEntries)]
    public async Task<ActionResult<RecurringJournalTemplateDto>> Update(
        Guid id, [FromBody] UpdateRecurringJournalTemplateDto request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = FinancePermissions.SubmitJournalEntries)]
    public async Task<ActionResult<RecurringJournalTemplateDto>> Submit(
        Guid id, [FromBody] RecurringJournalDecisionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.SubmitAsync(id, request.Comment, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = FinancePermissions.ApproveJournalEntries)]
    public async Task<ActionResult<RecurringJournalTemplateDto>> Approve(
        Guid id, [FromBody] RecurringJournalDecisionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.ApproveAsync(id, request.Comment, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = FinancePermissions.ApproveJournalEntries)]
    public async Task<ActionResult<RecurringJournalTemplateDto>> Reject(
        Guid id, [FromBody] RecurringJournalDecisionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.RejectAsync(id, request.Comment, cancellationToken));

    [HttpPost("{id:guid}/pause")]
    [Authorize(Policy = FinancePermissions.EditJournalEntries)]
    public async Task<ActionResult<RecurringJournalTemplateDto>> Pause(
        Guid id, [FromBody] RecurringJournalDecisionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.PauseAsync(id, request.Comment, cancellationToken));

    [HttpPost("{id:guid}/resume")]
    [Authorize(Policy = FinancePermissions.EditJournalEntries)]
    public async Task<ActionResult<RecurringJournalTemplateDto>> Resume(
        Guid id, [FromBody] RecurringJournalDecisionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.ResumeAsync(id, request.Comment, cancellationToken));

    [HttpPost("process-due")]
    [Authorize(Policy = FinancePermissions.CreateJournalEntries)]
    public async Task<ActionResult<RecurringJournalGenerationResultDto>> ProcessDue(
        [FromQuery] DateOnly? asOfDate, CancellationToken cancellationToken)
    {
        // The explicit date is useful for controlled Finance/UAT catch-up. Normal
        // operation omits it and uses the server's UTC accounting date.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (asOfDate > today)
            return BadRequest(new { error = "Future recurring-journal occurrences cannot be generated early." });
        var result = await _service.GenerateDueAsync(asOfDate ?? today, cancellationToken);
        return Ok(result);
    }

    [HttpPost("process-due-reversals")]
    [Authorize(Policy = FinancePermissions.PostJournalEntries)]
    public async Task<ActionResult<RecurringJournalReversalProcessingResultDto>> ProcessDueReversals(
        [FromQuery] DateOnly? asOfDate, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (asOfDate > today)
            return BadRequest(new { error = "Future recurring-journal reversals cannot be processed early." });
        return Ok(await _service.ProcessDueReversalsAsync(asOfDate ?? today, null, cancellationToken));
    }

    [HttpPost("occurrences/{occurrenceId:guid}/retry-reversal")]
    [Authorize(Policy = FinancePermissions.PostJournalEntries)]
    public async Task<ActionResult<RecurringJournalReversalProcessingResultDto>> RetryReversal(
        Guid occurrenceId, CancellationToken cancellationToken) =>
        Ok(await _service.ProcessDueReversalsAsync(DateOnly.FromDateTime(DateTime.UtcNow), occurrenceId, cancellationToken));

    [HttpPost("occurrences/{occurrenceId:guid}/approve")]
    [Authorize(Policy = FinancePermissions.ApproveJournalEntries)]
    public async Task<ActionResult<RecurringJournalOccurrenceDto>> ApproveOccurrence(
        Guid occurrenceId, [FromBody] RecurringJournalDecisionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.ApproveOccurrenceAsync(occurrenceId, request.Comment, cancellationToken));

    [HttpPost("occurrences/{occurrenceId:guid}/reject")]
    [Authorize(Policy = FinancePermissions.ApproveJournalEntries)]
    public async Task<ActionResult<RecurringJournalOccurrenceDto>> RejectOccurrence(
        Guid occurrenceId, [FromBody] RecurringJournalDecisionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.RejectOccurrenceAsync(occurrenceId, request.Comment, cancellationToken));

    [HttpPost("occurrences/{occurrenceId:guid}/post")]
    [Authorize(Policy = FinancePermissions.PostJournalEntries)]
    public async Task<ActionResult<RecurringJournalOccurrenceDto>> PostOccurrence(
        Guid occurrenceId, CancellationToken cancellationToken) =>
        Ok(await _service.PostOccurrenceAsync(occurrenceId, cancellationToken));
}

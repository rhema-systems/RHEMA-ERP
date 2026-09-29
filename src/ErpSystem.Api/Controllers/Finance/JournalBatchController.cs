using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/journal-batches")]
public sealed class JournalBatchController : ControllerBase
{
    private const long MaximumWorkbookBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedWorkbookContentTypes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/octet-stream"
    };
    private readonly IJournalBatchService _batches;
    private readonly IJournalBatchSpreadsheetService _spreadsheets;

    public JournalBatchController(
        IJournalBatchService batches,
        IJournalBatchSpreadsheetService spreadsheets)
    {
        _batches = batches;
        _spreadsheets = spreadsheets;
    }

    [HttpGet]
    public Task<JournalBatchListResultDto> GetBatches(
        [FromQuery] JournalBatchQueryDto query,
        CancellationToken cancellationToken)
        => _batches.GetAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JournalBatchDetailDto>> GetBatch(Guid id, CancellationToken cancellationToken)
    {
        var batch = await _batches.GetByIdAsync(id, cancellationToken);
        return batch == null ? NotFound() : Ok(batch);
    }

    [HttpGet("eligible-books")]
    public Task<IReadOnlyList<EligibleJournalBatchBookDto>> GetEligibleBooks(
        [FromQuery] Guid fiscalPeriodId,
        CancellationToken cancellationToken)
        => _batches.GetEligibleBooksAsync(fiscalPeriodId, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<JournalBatchDetailDto>> CreateBatch(
        [FromBody] CreateJournalBatchDto dto,
        CancellationToken cancellationToken)
    {
        var created = await _batches.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetBatch), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<JournalBatchDetailDto> UpdateBatch(
        Guid id,
        [FromBody] UpdateJournalBatchDto dto,
        CancellationToken cancellationToken)
        => _batches.UpdateAsync(id, dto, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBatch(Guid id, CancellationToken cancellationToken)
    {
        await _batches.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/entries")]
    public Task<JournalBatchDetailDto> CreateJournal(
        Guid id,
        [FromBody] CreateJournalBatchEntryDto dto,
        CancellationToken cancellationToken)
        => _batches.CreateJournalAsync(id, dto.JournalEntry, cancellationToken);

    [HttpGet("{id:guid}/eligible-draft-journals")]
    public Task<IReadOnlyList<EligibleJournalBatchDraftDto>> GetEligibleDraftJournals(
        Guid id,
        [FromQuery] string? search,
        [FromQuery] int take,
        CancellationToken cancellationToken)
        => _batches.GetEligibleDraftJournalsAsync(id, search, take <= 0 ? 50 : Math.Min(take, 100), cancellationToken);

    [HttpPost("{id:guid}/entries/attach")]
    public Task<JournalBatchDetailDto> AddExistingJournal(
        Guid id,
        [FromBody] AddExistingJournalToBatchDto dto,
        CancellationToken cancellationToken)
        => _batches.AddExistingJournalAsync(id, dto.JournalEntryId, cancellationToken);

    [HttpPut("{id:guid}/entries/{journalEntryId:guid}")]
    public Task<JournalBatchDetailDto> UpdateJournal(
        Guid id,
        Guid journalEntryId,
        [FromBody] UpdateJournalEntryDto dto,
        CancellationToken cancellationToken)
        => _batches.UpdateJournalAsync(id, journalEntryId, dto, cancellationToken);

    [HttpDelete("{id:guid}/entries/{journalEntryId:guid}")]
    public Task<JournalBatchDetailDto> RemoveJournal(
        Guid id,
        Guid journalEntryId,
        CancellationToken cancellationToken)
        => _batches.RemoveJournalAsync(id, journalEntryId, cancellationToken);

    [HttpPost("{id:guid}/validate")]
    public Task<JournalBatchValidationResultDto> ValidateBatch(Guid id, CancellationToken cancellationToken)
        => _batches.ValidateAsync(id, cancellationToken);

    [HttpPost("{id:guid}/submit")]
    public Task<JournalBatchDetailDto> Submit(Guid id, CancellationToken cancellationToken)
        => _batches.SubmitAsync(id, cancellationToken);

    [HttpPost("{id:guid}/withdraw")]
    public Task<JournalBatchDetailDto> Withdraw(
        Guid id,
        [FromBody] JournalBatchActionDto? dto,
        CancellationToken cancellationToken)
        => _batches.WithdrawAsync(id, dto?.Comment, cancellationToken);

    [HttpPost("{id:guid}/review-stage")]
    public Task<JournalBatchDetailDto> ReviewStage(
        Guid id,
        [FromBody] JournalBatchReviewStageDto dto,
        CancellationToken cancellationToken)
        => _batches.ReviewStageAsync(id, dto, cancellationToken);

    [HttpPost("{id:guid}/posting-runs")]
    public Task<JournalBatchPostingRunDto> CreatePostingRun(
        Guid id,
        [FromBody] CreateJournalBatchPostingRunDto dto,
        CancellationToken cancellationToken)
        => _batches.PostAsync(id, dto, cancellationToken);

    [HttpPost("{id:guid}/copy")]
    public Task<JournalBatchDetailDto> CopyBatch(
        Guid id,
        [FromBody] CopyJournalBatchDto dto,
        CancellationToken cancellationToken)
        => _batches.CopyAsync(id, dto, rejectedOnly: false, cancellationToken);

    [HttpPost("{id:guid}/copy-rejected")]
    public Task<JournalBatchDetailDto> CopyRejected(
        Guid id,
        [FromBody] CopyJournalBatchDto dto,
        CancellationToken cancellationToken)
        => _batches.CopyAsync(id, dto, rejectedOnly: true, cancellationToken);

    [HttpPost("{id:guid}/reversal-batch")]
    public Task<JournalBatchDetailDto> CreateReversalBatch(
        Guid id,
        [FromBody] CreateJournalBatchReversalDto dto,
        CancellationToken cancellationToken)
        => _batches.CreateReversalBatchAsync(id, dto, cancellationToken);

    [HttpPost("{id:guid}/attachments/{fileUploadRecordId:guid}")]
    public async Task<IActionResult> LinkAttachment(
        Guid id,
        Guid fileUploadRecordId,
        CancellationToken cancellationToken)
    {
        await _batches.LinkAttachmentAsync(id, fileUploadRecordId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/attachments/{fileUploadRecordId:guid}")]
    public async Task<IActionResult> UnlinkAttachment(
        Guid id,
        Guid fileUploadRecordId,
        CancellationToken cancellationToken)
    {
        await _batches.UnlinkAttachmentAsync(id, fileUploadRecordId, cancellationToken);
        return NoContent();
    }

    [HttpGet("import-template")]
    public async Task<IActionResult> DownloadImportTemplate(CancellationToken cancellationToken)
    {
        var file = await _spreadsheets.CreateTemplateAsync(cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("imports/preview")]
    [RequestSizeLimit(MaximumWorkbookBytes)]
    public async Task<JournalBatchImportPreviewDto> PreviewImport(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Select a non-empty .xlsx workbook.");
        if (!AllowedWorkbookContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException("The upload must use the .xlsx workbook content type.");
        await using var stream = file.OpenReadStream();
        return await _spreadsheets.PreviewAsync(stream, file.FileName, cancellationToken);
    }

    [HttpPost("imports/{sessionId:guid}/commit")]
    public Task<JournalBatchDetailDto> CommitImport(
        Guid sessionId,
        [FromBody] CommitJournalBatchImportDto dto,
        CancellationToken cancellationToken)
        => _spreadsheets.CommitAsync(sessionId, dto, cancellationToken);

    [HttpGet("imports/{sessionId:guid}/errors")]
    public async Task<IActionResult> DownloadImportErrors(Guid sessionId, CancellationToken cancellationToken)
    {
        var file = await _spreadsheets.CreateErrorWorkbookAsync(sessionId, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> ExportBatch(Guid id, CancellationToken cancellationToken)
    {
        var file = await _spreadsheets.ExportAsync(id, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}

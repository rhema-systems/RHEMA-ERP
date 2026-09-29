using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IJournalBatchService
{
    Task<JournalBatchListResultDto> GetAsync(JournalBatchQueryDto query, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EligibleJournalBatchBookDto>> GetEligibleBooksAsync(Guid fiscalPeriodId, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> CreateAsync(CreateJournalBatchDto dto, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> UpdateAsync(Guid id, UpdateJournalBatchDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EligibleJournalBatchDraftDto>> GetEligibleDraftJournalsAsync(Guid id, string? search, int take = 50, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> AddExistingJournalAsync(Guid id, Guid journalEntryId, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> CreateJournalAsync(Guid id, CreateJournalEntryDto dto, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> UpdateJournalAsync(Guid id, Guid journalEntryId, UpdateJournalEntryDto dto, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> RemoveJournalAsync(Guid id, Guid journalEntryId, CancellationToken cancellationToken = default);
    Task<JournalBatchValidationResultDto> ValidateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> WithdrawAsync(Guid id, string? reason, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> ReviewStageAsync(Guid id, JournalBatchReviewStageDto dto, CancellationToken cancellationToken = default);
    Task<JournalBatchPostingRunDto> PostAsync(Guid id, CreateJournalBatchPostingRunDto dto, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> CopyAsync(Guid id, CopyJournalBatchDto dto, bool rejectedOnly, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> CreateReversalBatchAsync(Guid id, CreateJournalBatchReversalDto dto, CancellationToken cancellationToken = default);
    Task LinkAttachmentAsync(Guid id, Guid fileUploadRecordId, CancellationToken cancellationToken = default);
    Task UnlinkAttachmentAsync(Guid id, Guid fileUploadRecordId, CancellationToken cancellationToken = default);
}

public interface IJournalBatchSpreadsheetService
{
    Task<JournalBatchFileDto> CreateTemplateAsync(CancellationToken cancellationToken = default);
    Task<JournalBatchImportPreviewDto> PreviewAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
    Task<JournalBatchDetailDto> CommitAsync(Guid sessionId, CommitJournalBatchImportDto dto, CancellationToken cancellationToken = default);
    Task<JournalBatchFileDto> CreateErrorWorkbookAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<JournalBatchFileDto> ExportAsync(Guid journalBatchId, CancellationToken cancellationToken = default);
}

public sealed class JournalBatchFileDto
{
    public byte[] Content { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

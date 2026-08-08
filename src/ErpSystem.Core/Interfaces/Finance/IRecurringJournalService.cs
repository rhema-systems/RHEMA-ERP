using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IRecurringJournalService
{
    Task<IReadOnlyList<RecurringJournalTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RecurringJournalTemplateDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RecurringJournalTemplateDto> CreateAsync(CreateRecurringJournalTemplateDto request, CancellationToken cancellationToken = default);
    Task<RecurringJournalTemplateDto> UpdateAsync(Guid id, UpdateRecurringJournalTemplateDto request, CancellationToken cancellationToken = default);
    Task<RecurringJournalTemplateDto> SubmitAsync(Guid id, string comment, CancellationToken cancellationToken = default);
    Task<RecurringJournalTemplateDto> ApproveAsync(Guid id, string comment, CancellationToken cancellationToken = default);
    Task<RecurringJournalTemplateDto> RejectAsync(Guid id, string comment, CancellationToken cancellationToken = default);
    Task<RecurringJournalTemplateDto> PauseAsync(Guid id, string comment, CancellationToken cancellationToken = default);
    Task<RecurringJournalTemplateDto> ResumeAsync(Guid id, string comment, CancellationToken cancellationToken = default);
    Task<RecurringJournalOccurrenceDto> ApproveOccurrenceAsync(Guid occurrenceId, string comment, CancellationToken cancellationToken = default);
    Task<RecurringJournalOccurrenceDto> RejectOccurrenceAsync(Guid occurrenceId, string comment, CancellationToken cancellationToken = default);
    Task<RecurringJournalOccurrenceDto> PostOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
    Task<RecurringJournalGenerationResultDto> GenerateDueAsync(DateOnly asOfDate, CancellationToken cancellationToken = default);
}

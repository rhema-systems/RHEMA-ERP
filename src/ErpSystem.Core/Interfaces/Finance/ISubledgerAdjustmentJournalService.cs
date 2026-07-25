using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface ISubledgerAdjustmentJournalService
{
    Task<IReadOnlyList<SubledgerAdjustmentJournalDto>> GetAllAsync(string? module = null, CancellationToken cancellationToken = default);
    Task<SubledgerAdjustmentJournalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SubledgerAdjustmentJournalDto> CreateAndPostAsync(CreateSubledgerAdjustmentJournalDto dto, CancellationToken cancellationToken = default);
    Task<SubledgerAdjustmentJournalDto> ReverseAsync(Guid id, ReverseSubledgerAdjustmentJournalDto dto, CancellationToken cancellationToken = default);
}

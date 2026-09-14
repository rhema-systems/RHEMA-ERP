using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Owns the buyer-side AP supplier-debit-note lifecycle. The supplier's source document remains
/// a credit note, but posting it debits AP control and therefore reduces the buyer's payable.
/// </summary>
public interface ISupplierDebitNoteService
{
    Task<IReadOnlyList<InventoryReturnCreditCandidateDto>> GetInventoryReturnCreditCandidatesAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryReturnCreditSourceDto>> GetInventoryReturnCreditSourcesAsync(Guid returnId, CancellationToken cancellationToken = default);
    Task<SupplierDebitNoteDto> CreateInventoryReturnCreditAsync(Guid returnId, CreateInventoryReturnCreditDto dto,
        FinancePostingProducerContext producer, CancellationToken cancellationToken = default);
    Task<SupplierDebitNoteDto> UpdateInventoryReturnCreditHeaderAsync(Guid noteId, UpdateInventoryReturnCreditHeaderDto dto,
        FinancePostingProducerContext producer, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierDebitNoteDto>> GetAllAsync(
        SupplierDebitNoteQueryDto query,
        CancellationToken cancellationToken = default);

    Task<SupplierDebitNoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierDebitNoteDto?> GetByIdAsync(Guid id, FinancePostingProducerContext producer, CancellationToken cancellationToken = default);

    Task<SupplierDebitNoteDto> CreateAsync(
        CreateSupplierDebitNoteDto dto,
        CancellationToken cancellationToken = default);
    Task<SupplierDebitNoteDto> CreateAsync(
        CreateSupplierDebitNoteDto dto,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default);

    Task<SupplierDebitNoteDto> UpdateDraftAsync(
        Guid id,
        UpdateSupplierDebitNoteDto dto,
        CancellationToken cancellationToken = default);
    Task<SupplierDebitNoteDto> UpdateDraftAsync(
        Guid id,
        UpdateSupplierDebitNoteDto dto,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default);

    Task<SupplierDebitNoteDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierDebitNoteDto> SubmitAsync(Guid id, FinancePostingProducerContext producer, CancellationToken cancellationToken = default);

    Task<SupplierDebitNoteDto> ProcessApprovalAsync(
        Guid id,
        SupplierDebitNoteApprovalDto dto,
        CancellationToken cancellationToken = default);
    Task<SupplierDebitNoteDto> ProcessApprovalAsync(
        Guid id,
        SupplierDebitNoteApprovalDto dto,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default);

    Task<SupplierDebitNoteDto> PostAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierDebitNoteDto> PostAsync(Guid id, FinancePostingProducerContext producer, CancellationToken cancellationToken = default);

    Task<SupplierDebitNoteDto> CancelAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default);

    Task<SupplierDebitNoteDto> ReverseAsync(
        Guid id,
        ReverseSupplierDebitNoteDto dto,
        CancellationToken cancellationToken = default);
}
